using System;
using System.Collections.Generic;
using ChooGuard.App.Fps.Twin;
using ChooGuard.Circulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Connects the two already authored 2F–3F lanes. No geometry or installation positions are generated.</summary>
    public static class ConcourseDriveSetup
    {
        public static string Apply()
        {
            string[] names = { "FPSWorld/맞이방 · 에스컬레이터/에스컬레이터 맞이방 2F-3F 상행", "FPSWorld/맞이방 · 에스컬레이터/에스컬레이터 맞이방 2F-3F 하행" };
            string[] ids = { "esc-2f3f-up", "esc-2f3f-down" };
            var roots = new GameObject[2];
            var allSurfaces = new List<EscalatorMotion.StepSurface>[2];
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i] = GameObject.Find(names[i]);
                if (roots[i] == null) throw new InvalidOperationException("Existing lane geometry does not match: " + names[i]);
                bool coherentUpper = UpperCoherentHeightReplay.TryGetEscalatorUpper(roots[i].transform, out float acceptedUpper);
                int expectedRideVolumes = coherentUpper ? 3 : 2;
                if (roots[i].GetComponentsInChildren<EscalatorRide>(true).Length != expectedRideVolumes ||
                    coherentUpper && (roots[i].transform.Find("RideVolume_UpperLanding") == null ||
                    Mathf.Abs(roots[i].transform.Find("RideVolume_UpperLanding").position.y - acceptedUpper - 1f) > .002f))
                    throw new InvalidOperationException("Existing lane ride volumes do not match the accepted installation: " + names[i]);
                allSurfaces[i] = new List<EscalatorMotion.StepSurface>();
                foreach (var renderer in roots[i].GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++)
                        if (materials[index] != null && materials[index].shader != null && materials[index].shader.name == "CHOOGuard/Escalator/Step")
                            allSurfaces[i].Add(new EscalatorMotion.StepSurface { Renderer = renderer, MaterialIndex = index });
                }
                if (allSurfaces[i].Count == 0) throw new InvalidOperationException("No authored step surfaces: " + names[i]);
            }
            int surfaces = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                var binding = roots[i].GetComponent<EscalatorDriveBinding>() ?? Undo.AddComponent<EscalatorDriveBinding>(roots[i]);
                var motion = roots[i].GetComponent<EscalatorMotion>() ?? Undo.AddComponent<EscalatorMotion>(roots[i]);
                Undo.RecordObjects(new UnityEngine.Object[] { binding, motion }, "Bind existing concourse drive");
                binding.lane_id = ids[i];
                motion.StepSurfaces = allSurfaces[i].ToArray();
                motion.RebuildBindings();
                surfaces += allSurfaces[i].Count;
                EditorUtility.SetDirty(binding); EditorUtility.SetDirty(motion);
                EditorSceneManager.MarkSceneDirty(roots[i].scene);
            }
            EditorSceneManager.SaveScene(roots[0].scene);
            return "CG_CONCOURSE_DRIVE_BINDING lanes=2 ride_volumes=" + (roots[0].GetComponentsInChildren<EscalatorRide>(true).Length + roots[1].GetComponentsInChildren<EscalatorRide>(true).Length) + " step_surfaces=" + surfaces + " field_accuracy_verified=false";
        }
    }
}
