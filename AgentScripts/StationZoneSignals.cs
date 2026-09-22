// 부산역 2층 대합실 구역 신호 추출 — 원본이 스스로 말하게 한다.
//
// Jev 판정(jev-design-method-002): zone_method = derive_from_source_signals (0.94).
// 임의 저작 0.00, MvpStationZones 좌표 재사용 0.00. 그 좌표는 코드 주석이 스스로
// "visual design assumptions, not measured station rooms" 라고 자인했으므로 실격이다.
//
// 그래서 방 경계를 손으로 긋지 않고 원본에서 읽어낸다. 신호 둘을 쓴다.
//   1) 바닥 머티리얼 — RaycastHit.triangleIndex 를 서브메시 범위와 대조해 이름을 얻는다.
//   2) 천장 높이 — 대합실(높은 천장)과 역무실(낮은 천장)은 높이로도 갈린다.
//
// 대상은 StationRooms 가 확정한 성분 id=192 구역: y 6.1~10.9, X -24~74, Z -91~-28.
// 안전하게 여유를 둬 훑되, 결과는 바닥이 MainShell 소유인 셀로만 집계한다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class StationZoneSignals
{
    const float Cell = 1f, HStep = 0.5f, FloorReach = 1.2f, CeilReach = 8f, FlatDot = 0.7072f;
    const float CapsuleRadius = 0.28f, CapsuleFoot = 0.28f, CapsuleHead = 1.44f;
    const float SameFloorEps = 0.3f;
    // StationRooms 성분 192 의 범위에 여유 6m.
    const float XMin = -30f, XMax = 80f, ZMin = -97f, ZMax = -22f, YMin = 5.0f, YMax = 12.0f;

    sealed class Cellinfo
    {
        public int Cx, Cz; public float Y, CeilY;
        public string FloorMat = "?", CeilMat = "?";
    }

    public static void Main(string[] args)
    {
        string outDir = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build";
        Directory.CreateDirectory(Path.GetFullPath(outDir));

        var root = GameObject.Find("공식 자료 부산역 역사");
        var shell = root != null ? root.transform.Find("MainShell") : null;
        if (shell == null) throw new InvalidOperationException("MainShell 을 찾지 못했다");

        Physics.SyncTransforms();
        bool prior = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        // 콜라이더별 서브메시 삼각형 범위를 미리 만든다. triangleIndex -> 서브메시 -> 머티리얼.
        var ranges = new Dictionary<int, int[]>();      // colliderId -> 각 서브메시의 끝 삼각형(누적)
        var mats = new Dictionary<int, string[]>();     // colliderId -> 서브메시별 머티리얼 이름
        foreach (var mc in shell.GetComponentsInChildren<MeshCollider>(true))
        {
            var mesh = mc.sharedMesh; if (mesh == null) continue;
            int sub = mesh.subMeshCount;
            var ends = new int[sub]; int acc = 0;
            for (int i = 0; i < sub; i++) { acc += (int)(mesh.GetIndexCount(i) / 3); ends[i] = acc; }
            ranges[mc.GetInstanceID()] = ends;
            var r = mc.GetComponent<MeshRenderer>();
            var sm = r != null ? r.sharedMaterials : null;
            var names = new string[sub];
            for (int i = 0; i < sub; i++)
                names[i] = sm != null && i < sm.Length && sm[i] != null ? sm[i].name : "(none)";
            mats[mc.GetInstanceID()] = names;
        }

        var cells = new List<Cellinfo>();
        for (float x = XMin; x <= XMax; x += Cell)
        for (float z = ZMin; z <= ZMax; z += Cell)
        {
            float last = float.NaN;
            for (float h = YMin; h <= YMax; h += HStep)
            {
                var p = new Vector3(x, h, z);
                if (!Physics.Raycast(p, Vector3.down, out var fh, FloorReach, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(fh.normal.y) < FlatDot) continue;
                if (fh.collider == null || !fh.collider.transform.IsChildOf(shell)) continue;
                float fy = fh.point.y;
                if (!float.IsNaN(last) && Mathf.Abs(fy - last) < SameFloorEps) continue;
                var feet = new Vector3(x, fy + 0.045f, z);
                if (Physics.CheckCapsule(feet + Vector3.up * CapsuleFoot, feet + Vector3.up * CapsuleHead,
                        CapsuleRadius, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(p, Vector3.up, out var ch, CeilReach, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(ch.normal.y) < FlatDot) continue;
                float head = ch.point.y - fy;
                if (head < 2.2f || head > 8f) continue;

                last = fy;
                cells.Add(new Cellinfo
                {
                    Cx = Mathf.RoundToInt(x), Cz = Mathf.RoundToInt(z), Y = fy, CeilY = ch.point.y,
                    FloorMat = MatOf(fh, ranges, mats), CeilMat = MatOf(ch, ranges, mats),
                });
            }
        }
        Physics.queriesHitBackfaces = prior;

        // 바닥 머티리얼 분포
        var byMat = new Dictionary<string, List<Cellinfo>>();
        foreach (var c in cells)
        {
            if (!byMat.TryGetValue(c.FloorMat, out var l)) byMat[c.FloorMat] = l = new List<Cellinfo>();
            l.Add(c);
        }
        var matOrder = new List<string>(byMat.Keys);
        matOrder.Sort((a, b) => byMat[b].Count.CompareTo(byMat[a].Count));

        // 천장 높이 분포 (0.5m 묶음)
        var byHead = new Dictionary<float, int>();
        foreach (var c in cells)
        {
            float k = Mathf.Round((c.CeilY - c.Y) * 2f) / 2f;
            byHead.TryGetValue(k, out int n); byHead[k] = n + 1;
        }
        var headOrder = new List<float>(byHead.Keys);
        headOrder.Sort((a, b) => byHead[b].CompareTo(byHead[a]));

        // 바닥 머티리얼 지도 PNG — 상위 머티리얼마다 다른 색
        int mnX = int.MaxValue, mxX = int.MinValue, mnZ = int.MaxValue, mxZ = int.MinValue;
        foreach (var c in cells)
        {
            if (c.Cx < mnX) mnX = c.Cx; if (c.Cx > mxX) mxX = c.Cx;
            if (c.Cz < mnZ) mnZ = c.Cz; if (c.Cz > mxZ) mxZ = c.Cz;
        }
        string png = "";
        if (cells.Count > 0)
        {
            var palette = new Color32[]
            {
                new Color32(230,80,70,255), new Color32(70,190,230,255), new Color32(250,200,60,255),
                new Color32(120,220,120,255), new Color32(200,120,235,255), new Color32(250,150,70,255),
                new Color32(140,160,255,255), new Color32(90,230,200,255), new Color32(235,120,170,255),
                new Color32(180,180,180,255),
            };
            var colorOf = new Dictionary<string, Color32>();
            for (int i = 0; i < matOrder.Count; i++)
                colorOf[matOrder[i]] = i < palette.Length ? palette[i] : new Color32(70, 70, 80, 255);

            int w = mxX - mnX + 1, h2 = mxZ - mnZ + 1;
            var tex = new Texture2D(w, h2, TextureFormat.RGBA32, false);
            var px = new Color32[w * h2];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(16, 16, 22, 255);
            foreach (var c in cells) px[(c.Cz - mnZ) * w + (c.Cx - mnX)] = colorOf[c.FloorMat];
            tex.SetPixels32(px); tex.Apply();
            png = Path.Combine(outDir, "zone-floor-materials.png");
            File.WriteAllBytes(Path.GetFullPath(png), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.station-zone-signals.v1\",\n  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"jevBasis\": \"jev-design-method-002 zone_method=derive_from_source_signals 0.94; MvpStationZones coordinates disqualified 1.00\",\n");
        sb.Append("  \"method\": \"per 1m cell, floor ray gives triangleIndex mapped through submesh ranges to the material name; ceiling ray gives headroom. No hand placement.\",\n");
        sb.AppendFormat(inv, "  \"targetComponent\": \"StationRooms id=192\", \"searchBox\": {{\"x\": [{0}, {1}], \"z\": [{2}, {3}], \"y\": [{4}, {5}]}},\n",
            XMin, XMax, ZMin, ZMax, YMin, YMax);
        sb.AppendFormat(inv, "  \"cells\": {0}, \"distinctFloorMaterials\": {1}, \"floorMaterialMapPng\": \"{2}\",\n",
            cells.Count, matOrder.Count, Esc(png));
        sb.AppendFormat(inv, "  \"extentMetres\": {{\"x\": [{0}, {1}], \"z\": [{2}, {3}]}},\n", mnX, mxX, mnZ, mxZ);
        sb.Append("  \"floorMaterials\": [\n");
        for (int i = 0; i < matOrder.Count && i < 16; i++)
        {
            var l = byMat[matOrder[i]];
            float yMin = float.MaxValue, yMax = float.MinValue, headSum = 0;
            int ix0 = int.MaxValue, ix1 = int.MinValue, iz0 = int.MaxValue, iz1 = int.MinValue;
            foreach (var c in l)
            {
                if (c.Y < yMin) yMin = c.Y; if (c.Y > yMax) yMax = c.Y;
                headSum += c.CeilY - c.Y;
                if (c.Cx < ix0) ix0 = c.Cx; if (c.Cx > ix1) ix1 = c.Cx;
                if (c.Cz < iz0) iz0 = c.Cz; if (c.Cz > iz1) iz1 = c.Cz;
            }
            if (i > 0) sb.Append(",\n");
            sb.Append("    {");
            sb.AppendFormat(inv, "\"material\": \"{0}\", \"cells\": {1}, \"yRange\": [{2:F1}, {3:F1}], \"meanHeadroom\": {4:F2}, ",
                Esc(matOrder[i]), l.Count, yMin, yMax, headSum / l.Count);
            sb.AppendFormat(inv, "\"x\": [{0}, {1}], \"z\": [{2}, {3}]", ix0, ix1, iz0, iz1);
            sb.Append("}");
        }
        sb.Append("\n  ],\n  \"headroomHistogram\": [");
        for (int i = 0; i < headOrder.Count && i < 12; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.AppendFormat(inv, "{{\"headroom\": {0:F1}, \"cells\": {1}}}", headOrder[i], byHead[headOrder[i]]);
        }
        sb.Append("]\n}\n");
        var jsonPath = Path.Combine(outDir, "station-zone-signals.json");
        File.WriteAllText(Path.GetFullPath(jsonPath), sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"ZONE_SIGNALS cells={cells.Count} materials={matOrder.Count} -> {jsonPath}");
    }

    static string MatOf(RaycastHit h, Dictionary<int, int[]> ranges, Dictionary<int, string[]> mats)
    {
        if (h.collider == null) return "?";
        int id = h.collider.GetInstanceID();
        if (!ranges.TryGetValue(id, out var ends) || !mats.TryGetValue(id, out var names)) return "?";
        int t = h.triangleIndex;
        if (t < 0) return "(no-tri)";
        for (int i = 0; i < ends.Length; i++) if (t < ends[i]) return names[i];
        return "(out-of-range)";
    }

    static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
