using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.Seeded.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    // Read-only census of new saved KTX render descendants. Historical records,
    // semantic bindings and source evidence are retained, never relabelled as new evidence.
    public static class CurrentStationInventorySupplement
    {
        const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
        const string Resources="Assets/ChooGuard/Seeded/Resources/";
        const string Inventory="WorldSnapshotInventory.json", Manifest="WorldSnapshotPrefabManifest.json";

        public static void ExportBatch()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-inventoryOutput");
            if(index<0||index+1>=args.Length)throw new ArgumentException("Explicit -inventoryOutput directory required");
            Export(args[index+1]);
        }

        public static void Export(string output)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Saved edit-mode source required");
            output=Path.GetFullPath(output);
            if(output.StartsWith(Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Export outside Assets; review the packet before applying it");
            if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new IOException("Output must be empty");
            string sceneHash=Hash(ScenePath), inventoryHash=Hash(Resources+Inventory), manifestHash=Hash(Resources+Manifest);
            string inventoryText=File.ReadAllText(Resources+Inventory),manifestText=File.ReadAllText(Resources+Manifest);
            var inventory=JObject.Parse(inventoryText);
            var manifest=JObject.Parse(manifestText);
            if((string)manifest["boundSceneSha256"]!=sceneHash||(string)manifest["inventorySha256"]!=inventoryHash)
                throw new InvalidOperationException("Pinned scene/inventory changed; reconcile the source first");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(scene.isLoaded&&scene.isDirty)throw new InvalidOperationException("Unsaved scene edits must be preserved");
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var adapters=scene.GetRootGameObjects().SelectMany(root=>root.GetComponents<StationWorldAdapter>()).ToArray();
            if(adapters.Length!=1)throw new InvalidOperationException("Exactly one saved provider adapter required");
            var adapter=adapters[0];
            if(adapter.LiveObjects.Length!=adapter.LiveObjectIds.Length)throw new InvalidOperationException("Anchor arrays differ");
            var anchors=adapter.LiveObjectIds.Select((id,i)=>(id,go:adapter.LiveObjects[i])).ToDictionary(x=>x.id,x=>x.go);
            if(!new HashSet<string>(manifest["liveBindingRoots"].Values<string>()).SetEquals(anchors.Keys))throw new InvalidOperationException("Anchor coverage differs");
            var rows=(JArray)inventory["objects"];
            var byObject=new Dictionary<GameObject,JObject>();
            string sceneGuid=(string)inventory["sourceSnapshot"]["sceneGuid"];
            foreach(JObject row in rows)
            {
                var node=anchors[(string)row["liveBinding"]["rootObjectId"]].transform;
                foreach(int child in row["liveBinding"]["childIndices"])node=node.GetChild(child);
                var go=node.gameObject;
                if(go.name!=(string)row["name"]||go.activeSelf!=(bool)row["activeSelf"]||go.activeInHierarchy!=(bool)row["activeInHierarchy"]||
                   go.layer!=(int)row["layer"]||go.tag!=(string)row["tag"]||
                   GlobalObjectId.GetGlobalObjectIdSlow(go).ToString()!=(string)row["globalObjectId"]||
                   !StationWorldAdapter.MatrixNear(node.localToWorldMatrix,StationWorldAdapter.Matrix(row["worldTransform"]["localToWorldMatrix4x4"].Values<float>().ToArray()),.002f))
                    throw new InvalidOperationException("Existing native correspondence changed: "+(string)row["id"]);
                byObject.Add(go,row);
            }
            foreach(var pair in byObject)
                if(pair.Value["parentId"].Type!=JTokenType.Null&&(!byObject.TryGetValue(pair.Key.transform.parent.gameObject,out var parent)||(string)parent["id"]!=(string)pair.Value["parentId"]))
                    throw new InvalidOperationException("Existing native parent changed");
            int originalCount=rows.Count;
            var added=new JArray();var depthIds=new JArray();
            foreach(var anchor in anchors)
                AppendChildren(anchor.Value.transform,anchor.Key,Array.Empty<int>(),byObject,rows,added,depthIds,sceneGuid,sceneHash);
            int actual=anchors.Values.Sum(root=>root.GetComponentsInChildren<Transform>(true).Length);
            if(rows.Count!=actual||byObject.Count!=actual)throw new InvalidOperationException("Incomplete or duplicate native census");
            if(added.Count>0)
            {
                var supplement=new JObject{["schema"]="chooguard.saved-render-supplement.v1",["sceneSha256"]=sceneHash,
                    ["baseInventorySha256"]=inventoryHash,["baseObjectCount"]=originalCount,["objectCount"]=actual,
                    ["addedObjectIds"]=added,["claim"]="New saved mesh-only descendants; original records and semantic bindings preserved. No capabilities inferred."};
                inventory["currentNativeCensus"]=supplement;
                var counts=(JObject)inventory["counts"];counts["objects"]=actual;
                foreach(string key in new[]{"activeSelf","activeInHierarchy","physicalEligible"})counts[key]=rows.Count(row=>(bool)row[key]);
                counts["componentTypes"]=JObject.FromObject(rows.SelectMany(row=>row["components"]).GroupBy(c=>(string)c["type"]).OrderBy(g=>g.Key,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count()));
                manifest["currentNativeCensus"]=supplement.DeepClone();
                manifest["renderOnlyDerivedObjectIds"]=new JArray((manifest["renderOnlyDerivedObjectIds"]??new JArray()).Values<string>().Concat(depthIds.Values<string>()).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal));
                manifest["claim"]="Every native descendant of the pinned live anchors is registered, including inactive objects. Other scene roots are outside provider scope. Supplemental render objects grant no capabilities.";
            }
            if(scene.isDirty||Hash(ScenePath)!=sceneHash||Hash(Resources+Inventory)!=inventoryHash||Hash(Resources+Manifest)!=manifestHash)
                throw new InvalidOperationException("Source changed during census; no output accepted");
            Directory.CreateDirectory(output);
            if(added.Count==0)
            {
                File.Copy(Resources+Inventory,Path.Combine(output,Inventory));File.Copy(Resources+Manifest,Path.Combine(output,Manifest));
            }
            else
            {
                // Rewriting historical doubles/dates through Unity's JSON reader can
                // change their exact representation. Preserve all base tokens verbatim.
                using(var writer=new StreamWriter(Path.Combine(output,Inventory),false,new UTF8Encoding(false)))
                    WritePreservingBase(writer,inventoryText,new JObject{["counts"]=inventory["counts"].DeepClone(),["currentNativeCensus"]=inventory["currentNativeCensus"].DeepClone()},new JArray(rows.Skip(originalCount)));
                manifest["inventorySha256"]=Hash(Path.Combine(output,Inventory));
                using(var writer=new StreamWriter(Path.Combine(output,Manifest),false,new UTF8Encoding(false)))
                    WritePreservingBase(writer,manifestText,new JObject{["inventorySha256"]=manifest["inventorySha256"].DeepClone(),["currentNativeCensus"]=manifest["currentNativeCensus"].DeepClone(),["renderOnlyDerivedObjectIds"]=manifest["renderOnlyDerivedObjectIds"].DeepClone(),["claim"]=manifest["claim"].DeepClone()},null);
            }
            Write(Path.Combine(output,"receipt.json"),new JObject{["sceneSha256"]=sceneHash,["baseInventorySha256"]=inventoryHash,
                ["inventorySha256"]=Hash(Path.Combine(output,Inventory)),["manifestSha256"]=Hash(Path.Combine(output,Manifest)),
                ["originalObjects"]=originalCount,["objects"]=actual,["addedObjects"]=added.Count,["addedDepthRenderers"]=depthIds.Count,
                ["sourceUnchanged"]=true,["semanticBindingsUnchanged"]=true},Formatting.Indented);
            Debug.Log("Current native supplement exported: "+added.Count+" new objects; total "+actual);
        }

        static void AppendChildren(Transform node,string anchor,int[] indices,Dictionary<GameObject,JObject> known,JArray rows,JArray added,JArray depthIds,string guid,string sceneHash)
        {
            if(!known.ContainsKey(node.gameObject))
            {
                if(node.parent==null||!known.TryGetValue(node.parent.gameObject,out var parent))throw new InvalidOperationException("New root is outside additive scope");
                var go=node.gameObject;var filter=go.GetComponent<MeshFilter>();var renderer=go.GetComponent<MeshRenderer>();
                if(go.GetComponents<Component>().Length!=3||filter==null||renderer==null||filter.sharedMesh==null||renderer.sharedMaterials.Any(m=>m==null))
                    throw new InvalidOperationException("Only fully resolved mesh-only descendants may be appended: "+go.name);
                bool depth=IsDepthOnly(go);
                if(!((string)parent["path"]).StartsWith("FPSWorld/KTXSource/",StringComparison.Ordinal)||
                   (!depth&&!go.name.StartsWith("KTXP_",StringComparison.Ordinal)))
                    throw new InvalidOperationException("Unqualified new render descendant: "+go.name);
                var identity=GlobalObjectId.GetGlobalObjectIdSlow(go);
                if((int)identity.identifierType!=2||identity.assetGUID.ToString()!=guid||identity.targetObjectId==0||identity.targetPrefabId!=0)
                    throw new InvalidOperationException("Explicit saved scene identity required for a new descendant");
                string id="unity:"+guid+":"+identity.targetObjectId;
                var local=Local(node);var world=World(local,(JObject)parent["worldTransform"]);
                if(!StationWorldAdapter.MatrixNear(node.localToWorldMatrix,StationWorldAdapter.Matrix(world["localToWorldMatrix4x4"].Values<float>().ToArray()),.002f))
                    throw new InvalidOperationException("Supplemental world transform differs");
                var comps=new JArray(Component(node,4,"Transform"),Component(filter,33,"MeshFilter"),Component(renderer,23,"MeshRenderer"));
                comps[1]["mesh"]=Asset(filter.sharedMesh);comps[2]["materials"]=new JArray(renderer.sharedMaterials.Select(Asset));
                comps[2]["castShadows"]=(int)renderer.shadowCastingMode;comps[2]["receiveShadows"]=renderer.receiveShadows;
                comps[2]["worldBounds"]=new JObject{["min"]=Vec(renderer.bounds.min),["max"]=Vec(renderer.bounds.max)};
                var provenance=new JObject{["mode"]="typed-only",["boundSceneSha256"]=sceneHash,["nativeCensusGlobalObjectId"]=identity.ToString()};
                if(depth){provenance["renderOnlyDerivedGeometry"]="opaque-texel-depth";provenance["sourceObjectId"]=parent["id"].DeepClone();depthIds.Add(id);}
                var row=new JObject{["id"]=id,["sceneFileId"]=identity.targetObjectId.ToString(CultureInfo.InvariantCulture),["prefabInstanceId"]="0",["globalObjectId"]=identity.ToString(),
                    ["transformFileId"]=GlobalObjectId.GetGlobalObjectIdSlow(node).targetObjectId.ToString(CultureInfo.InvariantCulture),["parentId"]=parent["id"].DeepClone(),["name"]=go.name,["path"]=(string)parent["path"]+"/"+go.name,
                    ["activeSelf"]=go.activeSelf,["activeInHierarchy"]=go.activeInHierarchy,["physicalEligible"]=false,["layer"]=go.layer,["tag"]=go.tag,
                    ["localTransform"]=local,["worldTransform"]=world,["components"]=comps,["geometryProvenance"]=provenance,
                    ["liveBinding"]=new JObject{["rootObjectId"]=anchor,["childIndices"]=new JArray(indices)},["sourceBindings"]=new JArray()};
                known.Add(go,row);rows.Add(row);added.Add(id);
            }
            for(int i=0;i<node.childCount;i++)AppendChildren(node.GetChild(i),anchor,indices.Concat(new[]{i}).ToArray(),known,rows,added,depthIds,guid,sceneHash);
        }

        public static bool IsDepthOnly(GameObject go)
        {
            return StationWorldAdapter.IsOpaqueDepthOnlySource(go);
        }
        static JObject Component(Component component,int classId,string type)
        {
            var id=GlobalObjectId.GetGlobalObjectIdSlow(component);
            return new JObject{["globalObjectId"]=id.ToString(),["fileId"]=id.targetObjectId.ToString(CultureInfo.InvariantCulture),["prefabInstanceId"]=id.targetPrefabId.ToString(CultureInfo.InvariantCulture),
                ["classId"]=classId,["type"]=type,["managedType"]=component.GetType().FullName,["enabled"]=component is Renderer renderer?(JToken)renderer.enabled:JValue.CreateNull()};
        }
        static JObject Asset(UnityEngine.Object asset)
        {
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out string guid,out long fileId)||string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))throw new InvalidOperationException("Persistent source asset required");
            var row=new JObject{["guid"]=guid,["fileId"]=fileId.ToString(CultureInfo.InvariantCulture),["path"]=AssetDatabase.GetAssetPath(asset),["globalObjectId"]=GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString(),["guidLocalIdResolved"]=true};
            if(asset is Mesh mesh){row["vertexCount"]=mesh.vertexCount;row["subMeshCount"]=mesh.subMeshCount;row["isReadable"]=mesh.isReadable;row["localBounds"]=new JObject{["center"]=Vec(mesh.bounds.center),["size"]=Vec(mesh.bounds.size)};}
            return row;
        }
        static JArray Vec(Vector3 v)=>new JArray((double)v.x,(double)v.y,(double)v.z);
        static JObject Local(Transform t)=>new JObject{["position"]=Vec(t.localPosition),["rotation"]=new JArray(Normalize(new[]{(double)t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w})),["scale"]=Vec(t.localScale)};
        static double[] Normalize(double[] q){double length=Math.Sqrt(q.Sum(x=>x*x));return q.Select(x=>x/length).ToArray();}
        static JObject World(JObject local,JObject parent)
        {
            var p=local["position"].Values<double>().ToArray();var q=local["rotation"].Values<double>().ToArray();var s=local["scale"].Values<double>().ToArray();
            double x=q[0],y=q[1],z=q[2],w=q[3];
            var a=parent["localToWorldMatrix4x4"].Values<double>().ToArray();
            var b=new[]{(1-2*(y*y+z*z))*s[0],2*(x*y-z*w)*s[1],2*(x*z+y*w)*s[2],p[0],2*(x*y+z*w)*s[0],(1-2*(x*x+z*z))*s[1],2*(y*z-x*w)*s[2],p[1],2*(x*z-y*w)*s[0],2*(y*z+x*w)*s[1],(1-2*(x*x+y*y))*s[2],p[2],0,0,0,1};
            var m=new double[16];for(int r=0;r<4;r++)for(int c=0;c<4;c++)for(int k=0;k<4;k++)m[4*r+c]+=a[4*r+k]*b[4*k+c];
            var pq=parent["rotation"].Values<double>().ToArray();double ax=pq[0],ay=pq[1],az=pq[2],aw=pq[3];
            var rotation=Normalize(new[]{aw*x+ax*w+ay*z-az*y,aw*y-ax*z+ay*w+az*x,aw*z+ax*y-ay*x+az*w,aw*w-ax*x-ay*y-az*z});
            return new JObject{["position"]=new JArray(m[3],m[7],m[11]),["rotation"]=new JArray(rotation),["lossyScale"]=new JArray(Enumerable.Range(0,3).Select(c=>Math.Sqrt(m[c]*m[c]+m[c+4]*m[c+4]+m[c+8]*m[c+8]))),["localToWorldMatrix4x4"]=new JArray(m)};
        }
        // The caller has parsed/validated the base JSON. This scanner only locates
        // top-level value spans, respecting quoted brackets and escaped quotes.
        public static void WritePreservingBase(TextWriter writer,string original,JObject updates,JArray additions)
        {
            int cursor=0;
            void White(){while(cursor<original.Length&&char.IsWhiteSpace(original[cursor]))cursor++;}
            void Quoted(){cursor++;while(cursor<original.Length){char c=original[cursor++];if(c=='\\')cursor++;else if(c=='"')return;}throw new InvalidDataException("Unclosed JSON string");}
            void Value()
            {
                if(original[cursor]=='"'){Quoted();return;}
                if(original[cursor]=='{'||original[cursor]=='[')
                {
                    int depth=0;
                    do{char c=original[cursor];if(c=='"'){Quoted();continue;}cursor++;if(c=='{'||c=='[')depth++;else if(c=='}'||c==']')depth--;}while(depth>0&&cursor<original.Length);
                    if(depth!=0)throw new InvalidDataException("Unclosed JSON value");
                }
                else while(cursor<original.Length&&!char.IsWhiteSpace(original[cursor])&&original[cursor]!=','&&original[cursor]!='}')cursor++;
            }
            var spans=new Dictionary<string,(int start,int end)>();
            White();if(cursor>=original.Length||original[cursor++]!='{')throw new InvalidDataException("JSON object required");White();
            while(original[cursor]!='}')
            {
                if(original[cursor]!='"')throw new InvalidDataException("JSON property required");
                int keyStart=cursor;Quoted();string key=JsonConvert.DeserializeObject<string>(original.Substring(keyStart,cursor-keyStart));White();
                if(original[cursor++]!=':')throw new InvalidDataException("JSON colon required");White();int start=cursor;Value();spans.Add(key,(start,cursor));White();
                if(original[cursor]==','){cursor++;White();}else if(original[cursor]!='}')throw new InvalidDataException("JSON delimiter required");
            }
            int close=cursor;var edits=new List<(int start,int end,string value)>();var extra=new List<string>();
            foreach(var property in updates.Properties())
                if(spans.TryGetValue(property.Name,out var span))edits.Add((span.start,span.end,property.Value.ToString(Formatting.None)));
                else extra.Add(JsonConvert.SerializeObject(property.Name)+":"+property.Value.ToString(Formatting.None));
            if(additions!=null&&additions.Count>0)
            {
                var span=spans["objects"];if(original[span.start]!='['||original[span.end-1]!=']'||updates["objects"]!=null)throw new InvalidDataException("Separate original object array required");
                int first=span.start+1;while(first<span.end-1&&char.IsWhiteSpace(original[first]))first++;
                string separator=first==span.end-1?"":",";
                edits.Add((span.end-1,span.end-1,separator+string.Join(",",additions.Select(row=>row.ToString(Formatting.None)))));
            }
            if(extra.Count>0)edits.Add((close,close,(spans.Count>0?",":"")+string.Join(",",extra)));
            int copied=0;
            foreach(var edit in edits.OrderBy(e=>e.start))
            {
                if(edit.start<copied)throw new InvalidDataException("Overlapping JSON edits");
                Copy(edit.start);writer.Write(edit.value);copied=edit.end;
            }
            Copy(original.Length);
            void Copy(int end){while(copied<end){int count=Math.Min(65536,end-copied);writer.Write(original.Substring(copied,count));copied+=count;}}
        }
        static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        static void Write(string path,JToken value,Formatting formatting){using(var text=new StreamWriter(path,false,new UTF8Encoding(false)))using(var writer=new JsonTextWriter(text){Formatting=formatting})value.WriteTo(writer);}
    }
}
