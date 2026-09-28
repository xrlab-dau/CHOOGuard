// 포스트 프로세싱에 의존하지 않고 장면 자체 측광을 실사 기준선에 맞춘다.
// 캡처 경로가 포스트 패스를 우회해도 결과가 동일하도록 조명 세기와 알베도를 직접 조정한다.
//
// args: [ambient, pointIntensity, dirIntensity, albedoScale, warmth]

using System.Globalization;
using UnityEngine;
using UnityEditor;

public static class SceneTone
{
    const string GenDir = "Assets/ChooGuard/Art/StationInterior/Generated";

    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    // 기준 알베도 (albedoScale=1.0 일 때의 값)
    static readonly (string mat, float r, float g, float b)[] Base =
    {
        ("Hall_Floor_PolishedGranite", 0.93f, 0.92f, 0.89f),
        ("Hall_Wall_GranitePanel",     0.80f, 0.79f, 0.77f),
        ("Hall_RoofDeck_White",        0.90f, 0.91f, 0.92f),
        ("Hall_SpaceFrame_Steel",      0.93f, 0.93f, 0.94f),
        ("Hall_Column_White",          0.92f, 0.92f, 0.91f),
        ("Tenant_SidePanel",           0.90f, 0.90f, 0.89f),
        ("Fitout_Island",              0.86f, 0.85f, 0.83f),
    };

    public static void Main(string[] args)
    {
        float amb = P(args, 0, 0.30f);
        float pt = P(args, 1, 0.60f);
        float dir = P(args, 2, 0.40f);
        float alb = P(args, 3, 0.78f);
        float warm = P(args, 4, 0.06f);   // 적-청 차이로 채도를 올린다

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.36f, 0.39f, 0.44f);
        RenderSettings.ambientEquatorColor = new Color(0.34f, 0.35f, 0.36f);
        RenderSettings.ambientGroundColor = new Color(0.15f, 0.15f, 0.16f);
        RenderSettings.ambientIntensity = amb;

        int np = 0, nd = 0;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Point) { l.intensity = pt; np++; }
            else if (l.type == LightType.Directional) { l.intensity = dir; nd++; }
            EditorUtility.SetDirty(l);
        }

        int nm = 0;
        foreach (var (name, r, g, b) in Base)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(GenDir + "/" + name + ".mat");
            if (m == null) continue;
            m.SetColor("_BaseColor", new Color(
                Mathf.Clamp01(r * alb * (1f + warm)),
                Mathf.Clamp01(g * alb),
                Mathf.Clamp01(b * alb * (1f - warm))));
            EditorUtility.SetDirty(m);
            nm++;
        }

        // 고측창·지붕데크는 실사에서 하늘빛으로 날아간다. 자발광으로 하이라이트를 되살린다.
        float glow = P(args, 5, 1.9f);
        var sk = AssetDatabase.LoadAssetAtPath<Material>(GenDir + "/Hall_Skylight.mat");
        if (sk != null)
        {
            sk.EnableKeyword("_EMISSION");
            sk.SetColor("_EmissionColor", new Color(1.00f, 0.99f, 0.96f) * (glow * 0.85f));
            sk.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(sk);
        }

        var gl = AssetDatabase.LoadAssetAtPath<Material>(GenDir + "/Hall_Clerestory_Glass.mat");
        if (gl != null)
        {
            gl.SetColor("_BaseColor", new Color(0.86f, 0.90f, 0.96f, 0.42f));
            gl.EnableKeyword("_EMISSION");
            gl.SetColor("_EmissionColor", new Color(1.00f, 0.99f, 0.96f) * glow);
            gl.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(gl);
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("SCENE_TONE amb=" + amb + " point=" + pt + "x" + np + " dir=" + dir + "x" + nd + " mats=" + nm);
    }
}
