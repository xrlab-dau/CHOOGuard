// VolumeProfile 의 오버라이드는 서브에셋으로 등록해야 도메인 리로드 뒤에도 살아남는다.
// 기존 프로파일을 지우고 Tonemapping/ColorAdjustments/Bloom 을 서브에셋으로 다시 만든다.
//
// args: [postExposure, contrast, saturation]

using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FixPostVolume
{
    const string GenDir = "Assets/ChooGuard/Art/StationInterior/Generated";
    const string ProfPath = GenDir + "/HallPostProfile.asset";

    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    public static void Main(string[] args)
    {
        float exp = P(args, 0, -1.05f);
        float con = P(args, 1, 20f);
        float sat = P(args, 2, 24f);

        Directory.CreateDirectory(GenDir);
        AssetDatabase.DeleteAsset(ProfPath);

        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        prof.name = "HallPostProfile";
        AssetDatabase.CreateAsset(prof, ProfPath);

        var tm = ScriptableObject.CreateInstance<Tonemapping>();
        tm.name = "Tonemapping";
        tm.active = true;
        tm.mode.overrideState = true;
        tm.mode.value = TonemappingMode.ACES;
        prof.components.Add(tm);
        AssetDatabase.AddObjectToAsset(tm, prof);

        var ca = ScriptableObject.CreateInstance<ColorAdjustments>();
        ca.name = "ColorAdjustments";
        ca.active = true;
        ca.postExposure.overrideState = true; ca.postExposure.value = exp;
        ca.contrast.overrideState = true; ca.contrast.value = con;
        ca.saturation.overrideState = true; ca.saturation.value = sat;
        prof.components.Add(ca);
        AssetDatabase.AddObjectToAsset(ca, prof);

        var bl = ScriptableObject.CreateInstance<Bloom>();
        bl.name = "Bloom";
        bl.active = true;
        bl.threshold.overrideState = true; bl.threshold.value = 1.1f;
        bl.intensity.overrideState = true; bl.intensity.value = 0.40f;
        prof.components.Add(bl);
        AssetDatabase.AddObjectToAsset(bl, prof);

        EditorUtility.SetDirty(prof);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ProfPath);

        var go = GameObject.Find("맞이방 포스트 볼륨");
        if (go == null) go = new GameObject("맞이방 포스트 볼륨");
        var vol = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100f;
        vol.weight = 1f;
        vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfPath);
        EditorUtility.SetDirty(go);

        int cams = 0;
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var d = c.GetComponent<UniversalAdditionalCameraData>() ?? c.gameObject.AddComponent<UniversalAdditionalCameraData>();
            d.renderPostProcessing = true;
            d.volumeLayerMask = ~0;
            EditorUtility.SetDirty(c);
            cams++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("FIX_POSTVOLUME components=" + prof.components.Count + " cams=" + cams
                  + " exp=" + exp + " con=" + con + " sat=" + sat);
    }
}
