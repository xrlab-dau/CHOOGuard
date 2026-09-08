using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ChooGuard.Foundation.Demo.Editor
{
    // Colliders follow authored major components, not the former universal .8m console box.
    // This remains a bounded simulation proxy; fine screws, glass optics and structural engineering are separate.
    public static class FoundationAssetPhysics
    {
        [Serializable] private sealed class Source {public Descriptor[] assets;}
        [Serializable] private sealed class Descriptor {public string id;public Box[] collisionBoxes;}
        [Serializable] private sealed class Box {public string part;public string parent;public Extent boundsUnity;}
        [Serializable] private sealed class Extent {public float[] min;public float[] max;}
        private static string previousText;
        private static Source cached;
        public static void Apply(Transform owner,Transform model,string id)
        {
            var text=File.ReadAllText(Path.Combine(Application.dataPath,"..","foundation","art","asset-manifest.json"));
            if(cached==null||text!=previousText){cached=JsonUtility.FromJson<Source>(text);previousText=text;}
            var source=cached.assets.Single(a=>a.id==id);
            if(source.collisionBoxes==null||source.collisionBoxes.Length==0)throw new InvalidOperationException("Missing authored collision components: "+id);
            foreach(var collider in owner.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            var previous=owner.Find("AuthoredPhysics");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var physical=new GameObject("AuthoredPhysics").transform;physical.SetParent(owner,false);
            physical.position=model.position;physical.rotation=model.rotation;
            physical.localScale=new Vector3(model.lossyScale.x/owner.lossyScale.x,model.lossyScale.y/owner.lossyScale.y,model.lossyScale.z/owner.lossyScale.z);
            foreach(var box in source.collisionBoxes)
            {
                var minimum=Vector(box.boundsUnity.min);var maximum=Vector(box.boundsUnity.max);var size=maximum-minimum;
                if(size.x<=0||size.y<=0||size.z<=0)throw new InvalidOperationException("Invalid collider extent: "+id+"/"+box.part);
                var parent=physical;var offset=Vector3.zero;
                if(box.parent=="moving")
                {
                    parent=owner.Find("MovingPart");if(parent==null)throw new InvalidOperationException("Missing gate hinge transform.");
                    offset=-parent.localPosition;
                }
                var proxy=new GameObject("Collision_"+box.part).transform;proxy.SetParent(parent,false);proxy.localPosition=offset;
                var collider=proxy.gameObject.AddComponent<BoxCollider>();collider.center=(minimum+maximum)/2;
                collider.size=new Vector3(Mathf.Max(size.x,.015f),Mathf.Max(size.y,.015f),Mathf.Max(size.z,.015f));
            }
        }
        public static Bounds ReviewFocus(string id,Bounds fallback)
        {
            var text=File.ReadAllText(Path.Combine(Application.dataPath,"..","foundation","art","asset-manifest.json"));
            var source=JsonUtility.FromJson<Source>(text).assets.Single(a=>a.id==id);
            if(source.collisionBoxes==null||source.collisionBoxes.Length==0)return fallback;
            var terminals=new[]{"SituationPanel","AlarmSimulator","RadioConsole","RouteConsole","AssemblyRegister","DirectionSign","RallyPoint","HazardIndicator"};
            if(!terminals.Contains(id))return fallback;
            var boxes=source.collisionBoxes.Where(b=>!new[]{"Synthetic","Pedestal","Foot","Base","Post","Cabinet"}.Any(word=>b.part.Contains(word))).ToArray();
            if(boxes.Length==0)return fallback;
            var bounds=new Bounds(Vector(boxes[0].boundsUnity.min),Vector3.zero);
            foreach(var b in boxes){bounds.Encapsulate(Vector(b.boundsUnity.min));bounds.Encapsulate(Vector(b.boundsUnity.max));}
            bounds.Expand(Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)*.35f);return bounds;
        }

        private static Vector3 Vector(float[] values)
        {
            if(values==null||values.Length!=3||values.Any(x=>float.IsNaN(x)||float.IsInfinity(x)))throw new InvalidOperationException("Collider coordinates must be three finite metre values.");
            return new Vector3(values[0],values[1],values[2]);
        }
    }
}
