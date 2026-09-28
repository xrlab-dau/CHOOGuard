// 현재 씬의 부산역 실내 상태 실측. 판단 없이 사실만 낸다. (eval_file: 메서드 본문, using 금지)
var sb = new System.Text.StringBuilder();
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
sb.AppendLine("SCENE " + scene.path + " dirty=" + scene.isDirty);

foreach (var root in scene.GetRootGameObjects())
{
    if (root.name.StartsWith("소화기")) continue;
    sb.AppendLine("ROOT " + root.name + " children=" + root.transform.childCount);
    foreach (UnityEngine.Transform c in root.transform)
    {
        var rends = c.GetComponentsInChildren<UnityEngine.MeshRenderer>(true);
        var b = new UnityEngine.Bounds();
        bool has = false;
        foreach (var r in rends) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        long tris = 0;
        foreach (var mf in c.GetComponentsInChildren<UnityEngine.MeshFilter>(true))
            if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
        sb.AppendLine("  " + c.name + " kids=" + c.childCount + " rends=" + rends.Length + " tris=" + tris +
            (has ? (" min=" + b.min.ToString("F1") + " max=" + b.max.ToString("F1")) : " (no renderer)"));
    }
}

UnityEngine.GameObject shell = null;
foreach (var go in UnityEngine.Object.FindObjectsByType<UnityEngine.GameObject>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    if (go.name.Contains("부산역") && go.name.IndexOf("지붕") < 0 && go.name.IndexOf("선로") < 0 && go.GetComponentsInChildren<UnityEngine.MeshFilter>(true).Length >= 8)
    { shell = go; break; }
}
sb.AppendLine("SHELL " + (shell == null ? "NOT_FOUND" : shell.name));
if (shell != null)
{
    sb.AppendLine("SHELL_PATH " + UnityEditor.AnimationUtility.CalculateTransformPath(shell.transform, null));
    foreach (var mf in shell.GetComponentsInChildren<UnityEngine.MeshFilter>(true))
    {
        var m = mf.sharedMesh;
        if (m == null) continue;
        var rr = mf.GetComponent<UnityEngine.Renderer>();
        var wb = rr != null ? rr.bounds : new UnityEngine.Bounds();
        sb.AppendLine("  MESH " + mf.name + " tris=" + (m.triangles.Length / 3) + " subs=" + m.subMeshCount +
            " min=" + wb.min.ToString("F1") + " max=" + wb.max.ToString("F1") +
            " collider=" + (mf.GetComponent<UnityEngine.MeshCollider>() != null));
    }
}
return sb.ToString();
