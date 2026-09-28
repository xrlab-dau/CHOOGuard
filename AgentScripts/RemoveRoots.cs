using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Main-only: delete superseded kit roots from the active scene (no save). Run KitBuild with an empty spec of the same zone
// first so the root's kit meshes are deleted through its receipt; this removes the then-empty GameObject.
// args = scene paths "Parent/Child" (active objects). Missing paths fail before anything is deleted.
public static class RemoveRoots
{
    public static void Main(string[] args)
    {
        if (args == null || args.Length == 0) throw new ArgumentException("RemoveRoots <scene path> [...]");
        GameObject[] found = args.Select(p => GameObject.Find("/" + p.Trim('/'))).ToArray();
        string[] missing = args.Where((p, i) => found[i] == null).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException("Not found (must be active): " + string.Join(", ", missing));
        Scene scene = found[0].scene;
        foreach (GameObject go in found)
        {
            int children = go.transform.childCount;
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("ROOT_REMOVED children=" + children);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("REMOVE_ROOTS " + string.Join(" | ", args) + " scene=" + scene.path + " dirty=" + scene.isDirty);
    }

    // args = project-relative asset folders (e.g. Assets/.../Kit/east_extension). Every folder must contain no files other
    // than .meta files; if any still holds assets, nothing is deleted.
    public static void DeleteEmptyFolders(string[] args)
    {
        if (args == null || args.Length == 0) throw new ArgumentException("RemoveRoots.DeleteEmptyFolders <Assets/...> [...]");
        foreach (string folder in args)
        {
            if (!AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("Not an asset folder: " + folder);
            string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta", StringComparison.Ordinal)).ToArray();
            if (files.Length > 0) throw new InvalidOperationException(folder + " still holds " + files.Length + " files, e.g. " + files[0]);
        }
        foreach (string folder in args)
        {
            if (!AssetDatabase.DeleteAsset(folder)) throw new InvalidOperationException("DeleteAsset failed: " + folder);
            Debug.Log("FOLDER_DELETED " + folder);
        }
    }
}
