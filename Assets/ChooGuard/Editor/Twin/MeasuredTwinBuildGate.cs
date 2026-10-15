using System.IO;
using ChooGuard.App.Fps.Equipment;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Rechecks new equipment placements against retained survey evidence before a player build.</summary>
    public sealed class MeasuredTwinBuildGate : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!Directory.Exists(EquipmentBuilder.Root)) return;
            foreach (string path in Directory.GetFiles(EquipmentBuilder.Root, "*.json", SearchOption.TopDirectoryOnly))
            {
                var file = JsonUtility.FromJson<EquipmentPlacementFile>(File.ReadAllText(path));
                if (file == null || file.items == null) throw new BuildFailedException("Invalid equipment placements: " + path);
                foreach (var item in file.items)
                {
                    if (!MeasuredTwinImport.VerifyAgainstManifest(item, out var reason)) throw new BuildFailedException(path + ": " + reason);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentBuilder.PrefabRoot + "/" + item.prefab + ".prefab");
                    if (!item.verification.ValidateModelDimensions(model, out reason)) throw new BuildFailedException(item.id + ": " + reason);
                }
            }
        }

        public void OnProcessScene(Scene scene, BuildReport report) => ValidateScene(scene);

        public static void ValidateScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var equipment in root.GetComponentsInChildren<StationEquipment>(true))
                {
                    var item = new EquipmentPlacement { id = equipment.Id, position = equipment.transform.position,
                        rotation = equipment.transform.eulerAngles, verification = equipment.Verification };
                    if (!MeasuredTwinImport.VerifyAgainstManifest(item, out var reason) ||
                        !equipment.Verification.ValidateModelDimensions(equipment.gameObject, out reason))
                        throw new BuildFailedException(scene.path + ": " + equipment.Id + ": " + reason);
                }
        }
    }
}
