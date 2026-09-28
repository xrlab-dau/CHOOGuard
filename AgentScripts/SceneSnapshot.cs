using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Main-only: save a copy of the active scene (the open scene stays unsaved and keeps its path/dirty flag).
// args[0] = destination .unity path (project-relative or absolute inside the project).
public static class SceneSnapshot
{
    public static void Copy(string[] args)
    {
        if (args == null || args.Length < 1) throw new ArgumentException("SceneSnapshot.Copy <dest.unity>");
        Scene scene = SceneManager.GetActiveScene();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
        if (!EditorSceneManager.SaveScene(scene, args[0], true)) throw new InvalidOperationException("SaveScene copy failed: " + args[0]);
        Debug.Log("SCENE_SNAPSHOT " + args[0] + " from " + scene.path + " dirty=" + scene.isDirty + " roots=" + scene.rootCount);
    }
    // Main-only, user-approved: save the active scene to its own path (args[0], optional: a backup path for the file on
    // disk before overwriting it).
    public static void Save(string[] args)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Active scene has no path.");
        if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]) && File.Exists(scene.path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
            File.Copy(scene.path, args[0], true);
        }
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("SaveScene failed: " + scene.path);
        Debug.Log("SCENE_SAVED " + scene.path + " roots=" + scene.rootCount + " dirty=" + scene.isDirty);
    }
}