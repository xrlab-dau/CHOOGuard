// B1 지하도상가와 지상을 잇는 OffMeshLink 를 건다.
// 계단 지오메트리는 이미 서 있으나 지상 포장면이 덮고 있어 NavMesh 가 자동으로 잇지 못한다.
// 실제 지하도상가도 계단실 출입구로만 지상과 통하므로 링크가 옳은 표현이다.
//
// args: [verticalCirculationJson]

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class LinkUnderground
{
    const string RootName = "부산역 역사 내부 · 지하 연결";

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/vertical-circulation.json";
        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));

        var old = GameObject.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject(RootName);

        int made = 0, failed = 0;
        var report = new List<string>();

        foreach (JObject u in (JArray)spec["units"])
        {
            string name = (string)u["name"];
            if (name.IndexOf("지하연결", StringComparison.Ordinal) < 0) continue;

            float by = (float)u["bottomY"], ty = (float)u["topY"], yaw = (float)u["yaw"];
            var p = new Vector3((float)u["pos"][0], by, (float)u["pos"][2]);
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            // 하부: 계단 앞 착지점 / 상부: 계단 상단 너머 지상
            float rise = ty - by;
            float run = rise / Mathf.Tan(30f * Mathf.Deg2Rad);
            var lo = p - fwd * 2.0f;
            var hi = p + fwd * (run + 3.0f); hi.y = ty;

            NavMeshHit a, b;
            bool okA = NavMesh.SamplePosition(lo, out a, 8f, NavMesh.AllAreas);
            bool okB = NavMesh.SamplePosition(hi, out b, 12f, NavMesh.AllAreas);
            if (!okA || !okB || Mathf.Abs(a.position.y - by) > 2.5f || Mathf.Abs(b.position.y - ty) > 2.5f)
            {
                failed++;
                report.Add(name + " 표본실패 lo=" + okA + "(" + (okA ? a.position.y.ToString("F1") : "-") + ") hi=" + okB + "(" + (okB ? b.position.y.ToString("F1") : "-") + ")");
                continue;
            }

            var go = new GameObject("지하 출입구 링크 · " + name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = a.position;

            var sTr = new GameObject("start").transform; sTr.SetParent(go.transform, false); sTr.position = a.position;
            var eTr = new GameObject("end").transform; eTr.SetParent(go.transform, false); eTr.position = b.position;

            var link = go.AddComponent<OffMeshLink>();
            link.startTransform = sTr;
            link.endTransform = eTr;
            link.biDirectional = true;
            link.autoUpdatePositions = true;
            link.costOverride = -1f;
            link.activated = true;
            link.UpdatePositions();
            made++;
            report.Add(name + " 링크 " + a.position.ToString("F1") + " ↔ " + b.position.ToString("F1"));
        }

        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/underground-links.txt"),
            "made=" + made + " failed=" + failed + "\n" + string.Join("\n", report.ToArray()));
        Debug.Log("UNDERGROUND_LINKS made=" + made + " failed=" + failed);
    }
}
