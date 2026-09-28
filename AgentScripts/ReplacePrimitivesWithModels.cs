// 원시 큐브 집기를 실제 3D 모델로 교체한다.
//
// 하드룰: 바퀴부터 만들지 않는다. 3D Warehouse / Poly Pizza(구 Google Poly) 에서 받은
// CC0·CC-BY 모델을 Blender 로 FBX 변환해 투입한다. 큐브는 치수 자리표시였을 뿐이다.
//
// 치수는 모델 원본 바운즈를 실제 설비 치수로 재스케일한다(비균일 허용 — 카운터·보관함은
// 박스형이라 왜곡이 의미 없다). 에스컬레이터·계단처럼 단 비율이 의미를 갖는 물건은
// 여기서 다루지 않는다(VerticalCirculationGenerator 가 파라메트릭으로 만든다).
//
// args: [dummy]

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public static class ReplacePrimitivesWithModels
{
    const string Kit = "Assets/ChooGuard/Art/StationInterior/Kits/Web/";

    class Map
    {
        public string Key;        // 오브젝트 이름에 포함되는 문자열
        public string Fbx;        // null 이면 삭제
        public Vector3 Target;    // 목표 실치수 (Unity XYZ, m)
        public float Yaw;         // 모델 자체 회전 보정
        public string Why;
    }

    public static void Main(string[] args)
    {
        var maps = new List<Map>
        {
            new Map { Key = "개찰기", Fbx = null, Why = "KTX 역사에는 자동개집표기가 없다. 층별안내도의 GATE 는 승강장 탑승구 번호이지 개찰구가 아니다." },
            new Map { Key = "자동발매기", Fbx = "TicketVendingMachine", Target = new Vector3(0.60f, 1.85f, 1.00f), Yaw = 0f },
            new Map { Key = "대기 벤치",  Fbx = "WaitingBench",        Target = new Vector3(1.80f, 0.45f, 0.55f), Yaw = 0f },
            new Map { Key = "매표창구",   Fbx = "TicketCounter",       Target = new Vector3(1.80f, 1.15f, 0.90f), Yaw = 0f },
            new Map { Key = "종합관광안내소 카운터", Fbx = "TicketCounter", Target = new Vector3(7.00f, 1.15f, 1.20f), Yaw = 0f },
            new Map { Key = "물품보관함", Fbx = "LuggageLocker",       Target = new Vector3(6.00f, 2.00f, 0.50f), Yaw = 0f },
            new Map { Key = "ATM",        Fbx = "TicketVendingMachine", Target = new Vector3(0.80f, 1.70f, 0.80f), Yaw = 0f },
            new Map { Key = "전동휠체어", Fbx = "TrashBin",            Target = new Vector3(0.50f, 1.20f, 0.50f), Yaw = 0f },
        };

        var cache = new Dictionary<string, GameObject>();
        var dims = new Dictionary<string, Vector3>();
        foreach (var m in maps)
        {
            if (m.Fbx == null || cache.ContainsKey(m.Fbx)) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + m.Fbx + ".fbx");
            if (go == null) { Debug.LogWarning("모델 없음: " + m.Fbx); continue; }
            cache[m.Fbx] = go;
            dims[m.Fbx] = MeasurePrefab(go);
        }

        var root = GameObject.Find("부산역 역사 내부 · 마감");
        if (root == null) throw new InvalidOperationException("마감 루트 없음");

        int replaced = 0, deleted = 0;
        var victims = new List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) victims.Add(t);

        foreach (var t in victims)
        {
            if (t == null) continue;
            Map hit = null;
            foreach (var m in maps)
                if (t.name.IndexOf(m.Key, StringComparison.Ordinal) >= 0) { hit = m; break; }
            if (hit == null) continue;

            if (hit.Fbx == null)
            {
                UnityEngine.Object.DestroyImmediate(t.gameObject);
                deleted++;
                continue;
            }
            if (!cache.TryGetValue(hit.Fbx, out var src)) continue;
            var prev = t.Find("__model"); if (prev != null) UnityEngine.Object.DestroyImmediate(prev.gameObject);

            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null) UnityEngine.Object.DestroyImmediate(mr);
            if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
            var bc = t.GetComponent<BoxCollider>();
            if (bc == null) bc = t.gameObject.AddComponent<BoxCollider>();

            // 부모 큐브는 스케일이 곧 치수였다. 스케일을 1 로 되돌리고 실치수를 모델에 준다.
            var world = t.lossyScale;
            t.localScale = Vector3.one;
            bc.size = new Vector3(hit.Target.x, hit.Target.y, hit.Target.z);
            bc.center = new Vector3(0f, hit.Target.y * 0.5f, 0f);
            // 층 바닥 레벨을 조상 그룹 이름에서 읽어 스냅한다 (반복 실행해도 누적되지 않는다)
            float levelY = LevelFloor(t);
            t.position = new Vector3(t.position.x, levelY, t.position.z);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, t);
            inst.name = "__model";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, hit.Yaw, 0f);
            // 실치수 정합. 역사 집기는 박스형이라 축별 정합이 왜곡보다 중요하다.
            // FBX 임포터 스케일이 제각각이므로 배치 후 실측해 축별로 보정한다.
            inst.transform.localScale = Vector3.one;
            var cur = WorldSize(inst);
            inst.transform.localScale = new Vector3(
                cur.x > 1e-5f ? hit.Target.x / cur.x : 1f,
                cur.y > 1e-5f ? hit.Target.y / cur.y : 1f,
                cur.z > 1e-5f ? hit.Target.z / cur.z : 1f);
            var after = WorldBounds(inst);
            inst.transform.position -= new Vector3(0f, after.min.y - levelY, 0f);
            replaced++;
        }

        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        var diag = "replaced=" + replaced + " deleted=" + deleted + " models=" + cache.Count + " victims=" + victims.Count + " maps=" + maps.Count;
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/replace-diag.txt"), diag);
        Debug.Log("REPLACE " + diag);
    }

    static float LevelFloor(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
        {
            if (p.name.StartsWith("1F")) return 0.00f;
            if (p.name.StartsWith("2F")) return 7.00f;
            if (p.name.StartsWith("3F")) return 12.00f;
        }
        return 7.00f;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        var b = new Bounds(go.transform.position, Vector3.zero);
        bool has = false;
        foreach (var r in rends) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        return b;
    }

    static Vector3 WorldSize(GameObject go) => WorldBounds(go).size;

    static Vector3 MeasurePrefab(GameObject prefab)
    {
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        tmp.transform.position = Vector3.zero;
        tmp.transform.rotation = Quaternion.identity;
        tmp.transform.localScale = Vector3.one;
        var rends = tmp.GetComponentsInChildren<Renderer>(true);
        var b = new Bounds(Vector3.zero, Vector3.zero);
        bool has = false;
        foreach (var r in rends) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        var size = has ? b.size : Vector3.one;
        UnityEngine.Object.DestroyImmediate(tmp);
        return size;
    }
}
