using System;
using UnityEngine;
using UnityEditor;

public static class RemoveLixoCorridorObstacles
{
    public static void Main(string[] args)
    {
        int count = 0;
        foreach (var g in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (g == null) continue;
            if (g.name.IndexOf("lixo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                UnityEngine.Object.DestroyImmediate(g);
                count++;
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Removed " + count + " lixo objects");
    }
}
