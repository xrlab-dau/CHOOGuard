// 에스컬레이터를 실제로 구동시킨다.
//
// 1) 디딤판 서브메시(인덱스 2, 타일#025(계단))에 URP 스크롤 셰이더 재질을 물린다 → 계단면이 흐른다.
// 2) 경사 구간과 상·하부 착지판에 트리거 볼륨 + EscalatorRide 를 붙인다 → 탑승자가 실제로 실려 간다.
//
// 방향: vertical-circulation.json 의 name 에 '상행'/'하행' 이 들어 있다. yaw 는 진행 방위다.
// 속도 0.5 m/s 는 교통약자법 시행규칙 별표1 (30m/min).
//
// args: [verticalCirculationJson, rootName?] — rootName defaults to the original station circulation root.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class WireEscalatorMotion
{
    const string MatPath = "Assets/ChooGuard/Art/StationInterior/Materials/에스컬레이터_디딤판_구동.mat";
    const string StepTex = "Assets/ChooGuard/Art/OfficialBusanStation/Materials/source-abf1f31198576978dc15.mat";

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/vertical-circulation.json";
        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));

        var sh = Shader.Find("CHOOGuard/Escalator/Step");
        if (sh == null) throw new InvalidOperationException("셰이더 미컴파일: CHOOGuard/Escalator/Step");
        Type rideType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            rideType = asm.GetType("ChooGuard.Circulation.EscalatorRide");
            if (rideType != null) break;
        }
        if (rideType == null)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in asm.GetTypes())
                    if (t.Name == "EscalatorRide") { rideType = t; break; }
                if (rideType != null) break;
            }
        if (rideType == null) throw new InvalidOperationException("EscalatorRide 타입 미컴파일");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, MatPath); }
        mat.shader = sh;
        var src = AssetDatabase.LoadAssetAtPath<Material>(StepTex);
        if (src != null && src.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
        mat.SetFloat("_ScrollSpeed", 0.5f);
        mat.SetFloat("_TexMetres", 0.40f);
        mat.SetFloat("_Direction", 1f);
        EditorUtility.SetDirty(mat);

        string rootName = args != null && args.Length > 1 && !string.IsNullOrEmpty(args[1]) ? args[1] : "부산역 역사 내부 · 수직동선";
        var root = GameObject.Find(rootName);
        if (root == null) throw new InvalidOperationException("수직동선 루트 없음: " + rootName);

        var byName = new Dictionary<string, JObject>();
        foreach (JObject u in (JArray)spec["units"]) byName[(string)u["name"]] = u;

        int matDone = 0, rideDone = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = mf.gameObject.name;
            if (!n.StartsWith("에스컬레이터", StringComparison.Ordinal)) continue;
            if (!byName.TryGetValue(n, out var u)) continue;

            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterials.Length > 2)
            {
                var ms = mr.sharedMaterials;
                ms[2] = mat;                       // 디딤판 서브메시
                mr.sharedMaterials = ms;
                matDone++;
            }

            float y0 = (float)u["bottomY"], y1 = (float)u["topY"];
            float yaw = (float)u["yaw"];
            float w = (float)u["clearWidth"];
            bool up = n.EndsWith("상행", StringComparison.Ordinal);
            float rise = y1 - y0;
            float run = rise / Mathf.Tan(30f * Mathf.Deg2Rad);
            float len = Mathf.Sqrt(run * run + rise * rise);

            var old = mf.transform.Find("RideVolume_Incline");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var old2 = mf.transform.Find("RideVolume_Landing");
            if (old2 != null) UnityEngine.Object.DestroyImmediate(old2.gameObject);

            // 경사 구간 볼륨 — 부모 로컬 기준. 부모는 하부 착지판 앞끝 바닥, +Z 가 진행 방향.
            var vol = new GameObject("RideVolume_Incline");
            vol.transform.SetParent(mf.transform, false);
            vol.transform.localPosition = new Vector3(0f, rise * 0.5f + 1.0f, 1.20f + run * 0.5f);
            vol.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0f, 0f);
            var bc = vol.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(w, 2.0f, len);
            var ride = vol.AddComponent(rideType);
            Apply(ride, up, false);

            // 착지판 볼륨 (상·하부 합쳐 수평 이송)
            var pad = new GameObject("RideVolume_Landing");
            pad.transform.SetParent(mf.transform, false);
            pad.transform.localPosition = new Vector3(0f, 1.0f, 0.60f);
            var pbc = pad.AddComponent<BoxCollider>();
            pbc.isTrigger = true;
            pbc.size = new Vector3(w, 2.0f, 1.20f);
            var ride2 = pad.AddComponent(rideType);
            Apply(ride2, up, true);
            rideDone++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        string record = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFileNameWithoutExtension(path) + "-motion.txt");
        File.WriteAllText(record,
            "materialAssigned=" + matDone + " rideVolumes=" + rideDone);
        Debug.Log("ESCALATOR_MOTION mat=" + matDone + " ride=" + rideDone);
    }

    static void Apply(Component ride, bool up, bool horizontalOnly)
    {
        var so = new SerializedObject(ride);
        var sp = so.FindProperty("speed"); if (sp != null) sp.floatValue = 0.5f;
        // enumNames follow enum VALUE order ([Down(-1), Up(1)]), not declaration order; select by name.
        var dp = so.FindProperty("direction"); if (dp != null) dp.enumValueIndex = Array.IndexOf(dp.enumNames, up ? "Up" : "Down");
        var ap = so.FindProperty("rideAxis"); if (ap != null) ap.vector3Value = Vector3.forward;
        var hp = so.FindProperty("horizontalOnly"); if (hp != null) hp.boolValue = horizontalOnly;
        var cc = so.FindProperty("affectCharacterController"); if (cc != null) cc.boolValue = true;
        var rb = so.FindProperty("affectRigidbody"); if (rb != null) rb.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
