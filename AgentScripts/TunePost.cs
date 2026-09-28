// 맞이방 포스트 볼륨·앰비언트를 실사 측광 기준선에 맞춰 조정한다.
// args: [postExposure, contrast, saturation, ambientIntensity, pointLightScale]

using System.Globalization;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class TunePost
{
    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    public static void Main(string[] args)
    {
        float exp = P(args, 0, -1.0f);
        float con = P(args, 1, 18f);
        float sat = P(args, 2, 22f);
        float amb = P(args, 3, 0.50f);
        float pls = P(args, 4, 0.85f);

        var vol = GameObject.Find("맞이방 포스트 볼륨")?.GetComponent<Volume>();
        if (vol == null || vol.sharedProfile == null) { Debug.LogError("포스트 볼륨 없음"); return; }

        if (vol.sharedProfile.TryGet<ColorAdjustments>(out var ca))
        {
            ca.postExposure.overrideState = true; ca.postExposure.value = exp;
            ca.contrast.overrideState = true; ca.contrast.value = con;
            ca.saturation.overrideState = true; ca.saturation.value = sat;
            EditorUtility.SetDirty(vol.sharedProfile);
        }

        RenderSettings.ambientIntensity = amb;

        int n = 0;
        var root = GameObject.Find("부산역 2층 맞이방 · 신축");
        if (root != null)
        {
            var lightRoot = root.transform.Find("집기·조명/조명");
            if (lightRoot != null)
                foreach (var l in lightRoot.GetComponentsInChildren<Light>())
                {
                    if (l.type != LightType.Point) continue;
                    l.intensity = 1.15f * pls; n++;
                }
        }

        int cams = 0;
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var d = c.GetComponent<UniversalAdditionalCameraData>();
            if (d == null) d = c.gameObject.AddComponent<UniversalAdditionalCameraData>();
            d.renderPostProcessing = true;
            d.volumeLayerMask = ~0;
            d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            EditorUtility.SetDirty(c);
            cams++;
        }
        Debug.Log("TUNE_POST cams=" + cams);

        AssetDatabase.SaveAssets();
        Debug.Log("TUNE_POST exp=" + exp + " con=" + con + " sat=" + sat + " amb=" + amb + " points=" + n);
    }
}
