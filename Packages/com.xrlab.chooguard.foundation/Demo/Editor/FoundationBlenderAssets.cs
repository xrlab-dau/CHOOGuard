using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ChooGuard.Foundation.Demo.Editor
{
    public sealed class FoundationBlenderImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if(!assetPath.StartsWith("Assets/CHOOguardArt/Blender/",StringComparison.Ordinal))return;
            var model=(ModelImporter)assetImporter;
            model.globalScale=1;model.useFileScale=true;model.importAnimation=false;model.importCameras=false;model.importLights=false;
            model.addCollider=false;model.isReadable=true;model.importNormals=ModelImporterNormals.Import;
            model.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
    }
    public static class FoundationBlenderAssets
    {
        public const string Path="Assets/CHOOguardArt/Blender/";
        public static Transform Instantiate(string id,Transform parent,Material[] materials)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Path+id+".fbx");
            if(source==null)throw new InvalidOperationException("Blender asset missing: "+id+". Run scripts/art/build_station_assets.py in Blender first.");
            var instance=UnityEngine.Object.Instantiate(source,parent,false).transform;instance.name="Blender_"+id;
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(material=>
                {
                    var found=materials.FirstOrDefault(m=>material!=null&&m.name==material.name);
                    if(found==null)throw new InvalidOperationException("Unmapped Blender material in "+id+": "+(material==null?"null":material.name));
                    return found;
                }).ToArray();
                renderer.shadowCastingMode=id=="Glove"||id=="NavigationArrow"||id=="AssemblyRing"?
                    UnityEngine.Rendering.ShadowCastingMode.Off:UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows=id!="Glove";
                if(renderer.sharedMaterials.All(m=>m.name=="ClearGlass"))renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return instance;
        }
        private static void RemoveOldMeshes(Transform parent)
        {
            foreach(var mesh in parent.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=mesh.GetComponent<MeshRenderer>();if(renderer!=null)UnityEngine.Object.DestroyImmediate(renderer);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        public static void Apply(Transform root,Material[] materials)
        {
            foreach(var target in root.GetComponentInChildren<DemoGameController>().Targets)
            {
                RemoveOldMeshes(target.transform);
                var id=target.name.Substring(0,target.name.Length-3);
                var model=Instantiate(id,target.transform.Find("Details"),materials);
                foreach(var part in model.GetComponentsInChildren<Transform>().Where(x=>x.name.StartsWith("MovingPart_")).ToArray())
                {
                    var moving=target.transform.Find("MovingPart");
                    part.SetParent(moving,false);part.localPosition=-moving.localPosition;
                }
                target.ConfigureFeedback(model.GetComponentsInChildren<Renderer>().Where(x=>x.name.StartsWith("Status_")).ToArray());
                foreach(var label in target.transform.Find("Details").GetComponentsInChildren<TextMesh>(true))UnityEngine.Object.DestroyImmediate(label.gameObject);
                FoundationAssetPhysics.Apply(target.transform,model,id);
            }
            var props=root.Find("Props");
            foreach(Transform prop in props)
            {
                var id=prop.name.StartsWith("Bench_")?"Bench":prop.name.StartsWith("Pillar_")?"Pillar":
                    prop.name=="SyntheticHazardIndicator"?"HazardIndicator":prop.name=="AssemblyDirectionSign"?"AssemblySign":"InformationKiosk";
                RemoveOldMeshes(prop);
                var model=Instantiate(id,prop,materials);
                model.localScale=new Vector3(1/prop.localScale.x,1/prop.localScale.y,1/prop.localScale.z);
                if(id=="InformationKiosk")model.localRotation=Quaternion.Euler(0,180,0);
                if(id=="Bench"||id=="InformationKiosk"||id=="HazardIndicator")FoundationAssetPhysics.Apply(prop,model,id);
                if(id=="InformationKiosk")foreach(var label in prop.GetComponentsInChildren<TextMesh>(true))UnityEngine.Object.DestroyImmediate(label.gameObject);
                if(id=="HazardIndicator")
                {
                    var old=prop.GetComponentInChildren<DemoIncidentVisual>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
                    var lens=model.GetComponentsInChildren<Renderer>().First(x=>x.name.StartsWith("Beacon_"));
                    var ownLight=prop.GetComponentInChildren<Light>();ownLight.transform.position=lens.bounds.center;
                    lens.gameObject.AddComponent<DemoIncidentVisual>().Configure(ownLight,lens);
                }
            }
            var glove=root.GetComponentInChildren<DemoHands>().transform.Find("Visual");RemoveOldMeshes(glove);Instantiate("Glove",glove,materials);
            // All architecture modules use Blender meshes; the separately authored physics boxes remain unchanged.
            foreach(var mesh in root.Find("Environment").GetComponentsInChildren<MeshFilter>(true))
            {
                var module=AssetDatabase.LoadAssetAtPath<GameObject>(Path+(mesh.name.Contains("Floor")?"FloorModule":"WallModule")+".fbx");
                mesh.sharedMesh=module.GetComponentInChildren<MeshFilter>().sharedMesh;
            }
            foreach(var fixture in root.Find("Environment/InteriorDetails").Cast<Transform>().Where(x=>x.name.StartsWith("CeilingLight_")).ToArray())
            {
                foreach(Transform part in fixture)RemoveOldMeshes(part);
                Instantiate(fixture.localPosition.z<8?"CeilingLight":"RecessedLight",fixture,materials);
            }
            var guide=root.Find("Markers/AssemblyGuide");
            var ring=AssetDatabase.LoadAssetAtPath<GameObject>(Path+"AssemblyRing.fbx");
            guide.Find("AssemblyZone").GetComponent<MeshFilter>().sharedMesh=ring.GetComponentInChildren<MeshFilter>().sharedMesh;
            foreach(Transform arrow in guide)if(arrow.name.StartsWith("RouteArrow_")){RemoveOldMeshes(arrow);Instantiate("NavigationArrow",arrow,materials);}
        }
        public static Transform Group(string name,Transform parent,Vector3 position)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        public static GameObject Box(string name,Transform parent,Vector3 at,Vector3 scale,Material material,bool collision=true)
        {
            var t=Group(name,parent,at);t.localScale=scale;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Path+(name.Contains("Floor")?"FloorModule":"WallModule")+".fbx");
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=source.GetComponentInChildren<MeshFilter>().sharedMesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            if(collision)t.gameObject.AddComponent<BoxCollider>();return t.gameObject;
        }
    }
}
