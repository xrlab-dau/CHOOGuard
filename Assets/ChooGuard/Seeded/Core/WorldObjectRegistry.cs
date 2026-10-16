using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    /// <summary>A source AABB used for conservative candidate queries, never a mesh/visibility proof.</summary>
    public sealed class ObjectBounds
    {
        public readonly double MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
        public ObjectBounds(double minX,double minY,double minZ,double maxX,double maxY,double maxZ)
        {
            if(new[]{minX,minY,minZ,maxX,maxY,maxZ}.Any(x=>double.IsNaN(x)||double.IsInfinity(x)) || minX>maxX || minY>maxY || minZ>maxZ)
                throw new ArgumentException("Registry: finite ordered bounds required");
            MinX=minX;MinY=minY;MinZ=minZ;MaxX=maxX;MaxY=maxY;MaxZ=maxZ;
        }
        public bool Intersects(ObjectBounds b) => MinX<=b.MaxX&&MaxX>=b.MinX&&MinY<=b.MaxY&&MaxY>=b.MinY&&MinZ<=b.MaxZ&&MaxZ>=b.MinZ;
        public ObjectBounds Expand(double distance)
        {
            if(double.IsNaN(distance)||double.IsInfinity(distance)||distance<0)throw new ArgumentException("Registry: nonnegative finite distance required");
            return new ObjectBounds(MinX-distance,MinY-distance,MinZ-distance,MaxX+distance,MaxY+distance,MaxZ+distance);
        }
        internal static ObjectBounds Union(ObjectBounds a,ObjectBounds b) => a==null?b:new ObjectBounds(Math.Min(a.MinX,b.MinX),Math.Min(a.MinY,b.MinY),Math.Min(a.MinZ,b.MinZ),Math.Max(a.MaxX,b.MaxX),Math.Max(a.MaxY,b.MaxY),Math.Max(a.MaxZ,b.MaxZ));
        internal double Center(int axis) => axis==0?(MinX+MaxX)*.5:axis==1?(MinY+MaxY)*.5:(MinZ+MaxZ)*.5;
        internal double Width(int axis) => axis==0?MaxX-MinX:axis==1?MaxY-MinY:MaxZ-MinZ;
        internal bool IntersectsSegment(double[] a,double[] b)
        {
            double lo=0,hi=1;
            for(int i=0;i<3;i++)
            {
                double min=i==0?MinX:i==1?MinY:MinZ,max=i==0?MaxX:i==1?MaxY:MaxZ,d=b[i]-a[i];
                if(Math.Abs(d)<1e-15){if(a[i]<min||a[i]>max)return false;continue;}
                double t0=(min-a[i])/d,t1=(max-a[i])/d;if(t0>t1){double t=t0;t0=t1;t1=t;}
                lo=Math.Max(lo,t0);hi=Math.Min(hi,t1);if(lo>hi)return false;
            }
            return true;
        }
    }

    /// <summary>Explicit source association. This association grants no simulation capabilities.</summary>
    public sealed class WorldObjectBinding
    {
        readonly JObject data;
        public string ObjectId {get;private set;} public string EntityId {get;private set;}
        public string Relation {get;private set;} public string EvidencePath {get;private set;}
        public string EvidenceSha256 {get;private set;} public string EvidenceKind {get;private set;}
        public WorldObjectBinding(string objectId,string entityId,string relation,string evidencePath,string evidenceSha256,string evidenceKind)
        {ObjectId=objectId;EntityId=entityId;Relation=relation;EvidencePath=evidencePath;EvidenceSha256=evidenceSha256;EvidenceKind=evidenceKind;
            data=new JObject{{"objectId",ObjectId},{"entityId",EntityId},{"relation",Relation},{"capabilities",new JArray()},{"evidence",new JObject{{"path",EvidencePath},{"sha256",EvidenceSha256},{"kind",EvidenceKind}}}};}
        internal WorldObjectBinding(JObject row):this((string)row["objectId"],(string)row["entityId"],(string)row["relation"],(string)row["evidence"]["path"],(string)row["evidence"]["sha256"],(string)row["evidence"]["kind"]){data=(JObject)row.DeepClone();}
        public JObject Data=>(JObject)data.DeepClone();
        internal JObject Json()=>(JObject)data.DeepClone();
    }
    public sealed class WorldObjectComponent
    {
        readonly JObject data;
        public string FileId {get;private set;} public int ClassId {get;private set;} public string Type {get;private set;} public bool? Enabled {get;private set;}
        public JObject Data => (JObject)data.DeepClone();
        internal WorldObjectComponent(JObject row){data=(JObject)row.DeepClone();FileId=(string)row["fileId"];ClassId=(int)row["classId"];Type=(string)row["type"];Enabled=(bool?)row["enabled"];}
    }
    public sealed class WorldObjectRecord
    {
        readonly JObject data;
        readonly double[] lp,lr,ls,wp,wr,ws,wm;
        public string Id {get;private set;} public string SceneFileId {get;private set;} public string ParentId {get;private set;}
        public string Name {get;private set;} public string Path {get;private set;} public int Layer {get;private set;} public string Tag {get;private set;}
        public bool ActiveSelf {get;private set;} public bool ActiveInHierarchy {get;private set;} public bool PhysicalEligible {get;private set;}
        public double[] LocalPosition=>(double[])lp.Clone();public double[] LocalRotation=>(double[])lr.Clone();public double[] LocalScale=>(double[])ls.Clone();
        public double[] WorldPosition=>(double[])wp.Clone();public double[] WorldRotation=>(double[])wr.Clone();public double[] WorldLossyScale=>(double[])ws.Clone();public double[] WorldMatrix4x4=>(double[])wm.Clone();
        public IReadOnlyList<WorldObjectComponent> Components {get;private set;}
        public IReadOnlyList<WorldObjectBinding> SourceBindings {get;internal set;}
        public JObject GeometryProvenance=>(JObject)data["geometryProvenance"].DeepClone();
        public JObject Data=>(JObject)data.DeepClone();
        public ObjectBounds WorldBounds {get;internal set;}
        public bool BoundsSupported => WorldBounds!=null;
        // Serialized material references do not describe combustion, conductivity or failure behavior.
        public bool MaterialCapabilitiesSupported => false;
        internal WorldObjectRecord(JObject row)
        {
            data=(JObject)row.DeepClone();Id=(string)row["id"];SceneFileId=(string)row["sceneFileId"];ParentId=(string)row["parentId"];
            Name=(string)row["name"];Path=(string)row["path"];Layer=(int)row["layer"];Tag=(string)row["tag"];
            ActiveSelf=(bool)row["activeSelf"];ActiveInHierarchy=(bool)row["activeInHierarchy"];PhysicalEligible=(bool)row["physicalEligible"];
            lp=Values(row["localTransform"]["position"]);lr=Values(row["localTransform"]["rotation"]);ls=Values(row["localTransform"]["scale"]);
            wp=Values(row["worldTransform"]["position"]);wr=Values(row["worldTransform"]["rotation"]);ws=Values(row["worldTransform"]["lossyScale"]);wm=Values(row["worldTransform"]["localToWorldMatrix4x4"]);
            Components=Array.AsReadOnly(((JArray)row["components"]).Cast<JObject>().OrderBy(c=>(string)c["fileId"],StringComparer.Ordinal).Select(c=>new WorldObjectComponent(c)).ToArray());
            SourceBindings=Array.AsReadOnly(new WorldObjectBinding[0]);
        }
        static double[] Values(JToken token)=>token.Select(t=>(double)t).ToArray();
        internal double[] Matrix=>wm;
    }
    public sealed class WorldObjectRegistryReport
    {
        public int ObjectCount {get;internal set;} public int ActiveCount {get;internal set;} public int PhysicalEligibleCount {get;internal set;}
        public int IndexedColliderObjectCount {get;internal set;} public int UnsupportedBoundsCount {get;internal set;} public int BoundObjectCount {get;internal set;}
    }
    public sealed class WorldObjectApplyResult
    {
        public int AddedObjects {get;internal set;} public int ExistingObjects {get;internal set;} public int BoundObjects {get;internal set;}
        public int PhysicalGroups {get;internal set;} public string BindingFingerprint {get;internal set;}
    }
    public sealed class WorldObjectRelation
    {
        public string ObjectId {get;internal set;} public string OtherObjectId {get;internal set;} public string Kind {get;internal set;}
        public string Claim => "source hierarchy or conservative collider-AABB candidate; no contact/occlusion/operability proof";
    }

    /// <summary>
    /// Immutable, engine-independent saved-scene inventory. Parse validates the complete graph before publication.
    /// Apply retains every row as an inert render-only source input; typed entities retain their state/capabilities.
    /// The BVH indexes only active enabled nontrigger colliders with source-qualified bounds, with O(N) storage.
    /// </summary>
    public sealed class WorldObjectRegistry
    {
        public const string SchemaVersion="chooguard.world-objects.v1";
        public const string CacheVersion="world-object-registry-bvh-v1";
        const double Tolerance=0.00002;
        readonly SortedDictionary<string,WorldObjectRecord> objects;
        readonly Dictionary<string,string[]> children;
        readonly WorldObjectBinding[] bindings;
        readonly Dictionary<string,string> physicalIdentities;
        readonly BvhNode spatialRoot;
        public string SnapshotSha256 {get;private set;} public string SceneSha256 {get;private set;} public string SceneGuid {get;private set;}
        public string ScenePath {get;private set;} public string SnapshotId {get;private set;} public string Fingerprint {get;private set;}
        public string CacheKey=>Stable.Hash(CacheVersion+"|"+SnapshotSha256+"|"+Fingerprint);
        public IReadOnlyDictionary<string,WorldObjectRecord> Objects {get;private set;}
        public IReadOnlyList<WorldObjectBinding> SourceBindings {get;private set;}
        public WorldObjectRegistryReport Report {get;private set;}
        public IReadOnlyList<string> UnsupportedCapabilities {get;private set;}
        WorldObjectRegistry(SortedDictionary<string,WorldObjectRecord> records,JObject root,string snapshotHash,WorldObjectBinding[] associations)
        {
            objects=records;Objects=new ReadOnlyDictionary<string,WorldObjectRecord>(objects);bindings=associations;SourceBindings=Array.AsReadOnly(bindings);
            physicalIdentities=PhysicalIdentities(associations);
            var source=root["sourceSnapshot"];SnapshotSha256=snapshotHash;SceneSha256=(string)source["sceneSha256"];SceneGuid=(string)source["sceneGuid"];ScenePath=(string)source["scenePath"];SnapshotId=(string)source["snapshotId"];
            children=objects.Values.Where(o=>o.ParentId!=null).GroupBy(o=>o.ParentId,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Select(o=>o.Id).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),StringComparer.Ordinal);
            foreach(var group in bindings.GroupBy(b=>b.ObjectId,StringComparer.Ordinal))objects[group.Key].SourceBindings=Array.AsReadOnly(group.OrderBy(b=>b.EntityId,StringComparer.Ordinal).ToArray());
            var canonical=new JObject{{"schemaVersion",SchemaVersion},{"sceneSha256",SceneSha256},{"sceneGuid",SceneGuid},{"frame",root["frame"].DeepClone()},
                {"objects",new JArray(records.Values.Select(o=>CanonicalObject(o.Data)))},{"sourceBindings",new JArray(bindings.Select(b=>b.Json()))}};
            Fingerprint=Stable.Hash(Canonical(canonical).ToString(Formatting.None));
            var indexed=objects.Values.Where(o=>o.PhysicalEligible&&o.BoundsSupported).ToArray();spatialRoot=Build(indexed,0,indexed.Length);
            Report=new WorldObjectRegistryReport{ObjectCount=objects.Count,ActiveCount=objects.Values.Count(o=>o.ActiveInHierarchy),PhysicalEligibleCount=objects.Values.Count(o=>o.PhysicalEligible),IndexedColliderObjectCount=indexed.Length,UnsupportedBoundsCount=objects.Values.Count(o=>o.PhysicalEligible&&!o.BoundsSupported),BoundObjectCount=bindings.Select(b=>b.ObjectId).Distinct(StringComparer.Ordinal).Count()};
            UnsupportedCapabilities=Array.AsReadOnly(new[]{"material-properties","combustion-and-fire","electrical-failure","structural-failure","thermal-failure","operability","exact-mesh-contact","visibility-occlusion"});
        }
        static void Require(bool ok,string reason){if(!ok)throw new ArgumentException("Registry: "+reason);}
        static bool Hash(string text)=>text!=null&&text.Length==64&&text.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f'));
        static bool Guid(string text)=>text!=null&&text.Length==32&&text.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f'));
        static bool FileId(string text)=>!string.IsNullOrEmpty(text)&&text!="0"&&text.All(c=>c>='0'&&c<='9');
        static bool RelativePath(string path)=>!string.IsNullOrWhiteSpace(path)&&!System.IO.Path.IsPathRooted(path)&&!path.Contains('\\')&&!path.Split('/').Any(p=>p==".."||p==".")&&!path.Contains(":");
        static void String(JObject row,string key,bool empty=false){Require(row[key]?.Type==JTokenType.String&&(empty||!string.IsNullOrWhiteSpace((string)row[key])),key+" string required");}
        static void Bool(JObject row,string key){Require(row[key]?.Type==JTokenType.Boolean,key+" Boolean required");}
        static double[] Numbers(JToken token,int count,string key)
        {
            Require(token is JArray&&token.Count()==count,key+" array length "+count+" required");
            Require(token.All(t=>(t.Type==JTokenType.Float||t.Type==JTokenType.Integer)&&!double.IsNaN((double)t)&&!double.IsInfinity((double)t)),"nonfinite or nonnumeric "+key);
            return token.Select(t=>(double)t).ToArray();
        }
        static void FiniteTree(JToken token)
        {
            if(token.Type==JTokenType.Float||token.Type==JTokenType.Integer)Require(!double.IsNaN((double)token)&&!double.IsInfinity((double)token),"nonfinite numeric input");
            foreach(var child in token.Children())FiniteTree(child);
        }
        static void Near(double[] a,double[] b,string reason)
        {Require(a.Length==b.Length&&a.Zip(b,(x,y)=>Math.Abs(x-y)<=Tolerance+1e-9*Math.Max(Math.Abs(x),Math.Abs(y))).All(x=>x),reason);}
        static void Quaternion(double[] q,string label){Require(Math.Abs(q.Sum(x=>x*x)-1)<Tolerance,label+" normalized quaternion required");}

        /// <summary>Expected hashes must be supplied from the separately pinned import manifest, not read from this payload.</summary>
        public static WorldObjectRegistry Parse(string json,string expectedSnapshotSha256,string expectedSceneSha256)
        {
            Require(!string.IsNullOrWhiteSpace(json)&&Hash(expectedSnapshotSha256)&&Hash(expectedSceneSha256),"snapshot and externally pinned hashes required");
            Require(Stable.Hash(json)==expectedSnapshotSha256,"snapshot SHA256 mismatch");
            JObject root;
            try{using(var sr=new StringReader(json))using(var reader=new JsonTextReader(sr){DateParseHandling=DateParseHandling.None,MaxDepth=128}){root=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});Require(!reader.Read(),"trailing JSON input");}}
            catch(JsonException e){throw new ArgumentException("Registry: invalid JSON",e);}
            FiniteTree(root);Require((string)root["schemaVersion"]==SchemaVersion,"unsupported schema version");
            var source=root["sourceSnapshot"] as JObject;Require(source!=null,"sourceSnapshot required");
            foreach(var key in new[]{"scenePath","sceneGuid","sceneSha256","snapshotId","capturedAtUtc"})String(source,key);
            Require(RelativePath((string)source["scenePath"])&&Guid((string)source["sceneGuid"])&&Hash((string)source["sceneSha256"])&&(string)source["sceneSha256"]==expectedSceneSha256,"source identity/hash mismatch");
            var frame=root["frame"] as JObject;Require(frame!=null&&(string)frame["coordinateSystem"]=="Unity left-handed Y-up"&&(string)frame["registration"]=="preserved-saved-scene","preserved Unity source frame required");
            Require(Numbers(frame["sourceToSimulationMatrix4x4"],16,"source frame").SequenceEqual(Identity()),"source registration changed");
            var rows=root["objects"] as JArray;Require(rows!=null&&rows.Count>0,"full objects inventory required");
            var records=new SortedDictionary<string,WorldObjectRecord>(StringComparer.Ordinal);var allComponentIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var token in rows)
            {
                var row=token as JObject;Require(row!=null,"object row required");
                foreach(var key in new[]{"id","sceneFileId","name","path","tag"})String(row,key,key=="name"||key=="tag");
                string id=(string)row["id"],fid=(string)row["sceneFileId"];
                Require(FileId(fid)&&id=="unity:"+(string)source["sceneGuid"]+":"+fid&&!records.ContainsKey(id),"duplicate or mismatched stable object ID");
                Require(row["parentId"]!=null&&(row["parentId"].Type==JTokenType.Null||row["parentId"].Type==JTokenType.String),"parentId required");
                foreach(var key in new[]{"activeSelf","activeInHierarchy","physicalEligible"})Bool(row,key);
                Require(row["layer"]?.Type==JTokenType.Integer&&(int)row["layer"]>=0&&(int)row["layer"]<=31,"Unity layer 0..31 required");
                var local=row["localTransform"] as JObject;var world=row["worldTransform"] as JObject;Require(local!=null&&world!=null,"local/world transforms required");
                Numbers(local["position"],3,"local position");Quaternion(Numbers(local["rotation"],4,"local rotation"),"local");Numbers(local["scale"],3,"local scale");
                Numbers(world["position"],3,"world position");Quaternion(Numbers(world["rotation"],4,"world rotation"),"world");var scale=Numbers(world["lossyScale"],3,"world scale");Require(scale.All(x=>x>=0),"world lossyScale magnitude required");
                var matrix=Numbers(world["localToWorldMatrix4x4"],16,"world matrix");Near(new[]{matrix[12],matrix[13],matrix[14],matrix[15]},new[]{0.0,0,0,1},"affine world matrix required");Near(Numbers(world["position"],3,"world position"),new[]{matrix[3],matrix[7],matrix[11]},"world position/matrix mismatch");
                Near(scale,new[]{ColumnLength(matrix,0),ColumnLength(matrix,1),ColumnLength(matrix,2)},"world scale/matrix mismatch");
                Require(row["geometryProvenance"] is JObject&&new[]{"allowlisted","typed-only"}.Contains((string)row["geometryProvenance"]["mode"]),"explicit geometry provenance required");
                if(row["geometryProvenance"]["sourceSceneSha256"]!=null)Require((string)row["geometryProvenance"]["sourceSceneSha256"]==expectedSceneSha256,"provenance source hash mismatch");
                var comps=row["components"] as JArray;Require(comps!=null,"components inventory required");
                foreach(var cToken in comps)
                {
                    var c=cToken as JObject;Require(c!=null,"component row required");String(c,"fileId");String(c,"type");
                    Require(FileId((string)c["fileId"])&&allComponentIds.Add((string)c["fileId"]),"duplicate component fileID");
                    Require(c["classId"]?.Type==JTokenType.Integer&&c["enabled"]!=null&&(c["enabled"].Type==JTokenType.Null||c["enabled"].Type==JTokenType.Boolean),"component class/enabled required");
                    if(IsCollider(c)){Bool(c,"isTrigger");Require(c["enabled"].Type==JTokenType.Boolean,"collider enabled required");}
                    if(c["localBounds"]!=null&&c["localBounds"].Type!=JTokenType.Null){Numbers(c["localBounds"]["center"],3,"bounds center");Require(Numbers(c["localBounds"]["size"],3,"bounds size").All(x=>x>=0),"negative bounds size");}
                    if(c["worldBounds"]!=null&&c["worldBounds"].Type!=JTokenType.Null)Bounds(c["worldBounds"]);
                }
                bool eligible=(bool)row["activeInHierarchy"]&&comps.Cast<JObject>().Any(c=>IsCollider(c)&&(bool)c["enabled"]&&!(bool)c["isTrigger"]);
                Require(eligible==(bool)row["physicalEligible"],"physical eligibility inconsistent with actual collider flags");
                records.Add(id,new WorldObjectRecord(row));
            }
            // Iterative graph traversal also works for deeply nested source hierarchies without recursion overflow.
            var done=new HashSet<string>(StringComparer.Ordinal);
            foreach(var record in records.Values)
            {
                var chain=new List<WorldObjectRecord>();var visiting=new HashSet<string>(StringComparer.Ordinal);var cursor=record;
                while(!done.Contains(cursor.Id))
                {
                    Require(visiting.Add(cursor.Id),"parent cycle");chain.Add(cursor);
                    if(cursor.ParentId==null)break;
                    WorldObjectRecord parent;Require(records.TryGetValue(cursor.ParentId,out parent),"missing parent "+cursor.ParentId);cursor=parent;
                }
                for(int i=chain.Count-1;i>=0;i--)
                {
                    var o=chain[i];var parent=o.ParentId==null?null:records[o.ParentId];
                    Require(o.Path==(parent==null?o.Name:parent.Path+"/"+o.Name),"parent/name/path mismatch "+o.Id);
                    Require(o.ActiveInHierarchy==(o.ActiveSelf&&(parent==null||parent.ActiveInHierarchy)),"active hierarchy mismatch "+o.Id);
                    var expected=Multiply(parent==null?Identity():parent.Matrix,Trs(o.LocalPosition,o.LocalRotation,o.LocalScale));Near(o.Matrix,expected,"parent/local/world matrix mismatch "+o.Id);
                    var expectedRotation=parent==null?o.LocalRotation:QMultiply(parent.WorldRotation,o.LocalRotation);var actual=o.WorldRotation;
                    if(expectedRotation.Zip(actual,(x,y)=>x*y).Sum()<0)actual=actual.Select(x=>-x).ToArray();Near(actual,expectedRotation,"parent/local/world rotation mismatch "+o.Id);
                    o.WorldBounds=ColliderBounds(o);done.Add(o.Id);
                }
            }
            var rawBindings=new Dictionary<string,JObject>(StringComparer.Ordinal);
            var topBindings=root["sourceBindings"] as JArray;if(topBindings!=null)foreach(var b in topBindings){Require(b is JObject&&b["objectId"]?.Type==JTokenType.String,"binding object required");string objectId=(string)b["objectId"];Require(!rawBindings.ContainsKey(objectId),"duplicate top-level source binding");rawBindings.Add(objectId,(JObject)b);}
            foreach(var row in rows.Cast<JObject>())
            {
                var embedded=row["sourceBindings"] as JArray;Require(embedded!=null,"object sourceBindings required");
                Require(embedded.Count<=1,"duplicate embedded source binding");
                foreach(var token in embedded)
                {
                    Require(token is JObject,"binding object required");var b=(JObject)token.DeepClone();if(b["objectId"]==null)b["objectId"]=row["id"].DeepClone();string objectId=(string)b["objectId"];Require(objectId==(string)row["id"],"embedded binding object mismatch");
                    JObject previous;if(rawBindings.TryGetValue(objectId,out previous))Require(JToken.DeepEquals(Canonical(previous),Canonical(b)),"conflicting embedded/top-level source binding");else rawBindings.Add(objectId,b);
                }
            }
            var associations=rawBindings.Values.Select(ParseBinding).ToArray();ValidateBindings(records,associations);
            foreach(var b in associations)
            {
                var evidence=b.Data["evidence"];
                if(evidence["sourceSceneSha256"]!=null)Require((string)evidence["sourceSceneSha256"]==expectedSceneSha256,"binding scene hash mismatch");
                if(evidence["sceneFileId"]!=null)Require((string)evidence["sceneFileId"]==records[b.ObjectId].SceneFileId,"binding scene fileID mismatch");
                if(evidence["coveragePath"]!=null||evidence["coverageSha256"]!=null)Require(RelativePath((string)evidence["coveragePath"])&&Hash((string)evidence["coverageSha256"]),"binding coverage evidence malformed");
            }
            return new WorldObjectRegistry(records,root,expectedSnapshotSha256,associations.OrderBy(b=>b.ObjectId,StringComparer.Ordinal).ThenBy(b=>b.EntityId,StringComparer.Ordinal).ToArray());
        }

        static WorldObjectBinding ParseBinding(JObject b)
        {
            foreach(var key in new[]{"objectId","entityId","relation"})String(b,key);
            Require(b["capabilities"] is JArray&&b["capabilities"].Count()==0,"source associations cannot grant capabilities");
            var ev=b["evidence"] as JObject;Require(ev!=null,"source binding evidence required");foreach(var key in new[]{"path","sha256","kind"})String(ev,key);
            return new WorldObjectBinding(b);
        }
        static void ValidateBindings(SortedDictionary<string,WorldObjectRecord> records,IEnumerable<WorldObjectBinding> values)
        {
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var b in values)
                Require(b!=null&&records.ContainsKey(b.ObjectId)&&!string.IsNullOrWhiteSpace(b.EntityId)&&!records.ContainsKey(b.EntityId)&&seen.Add(b.ObjectId)&&new[]{"alias","part-of"}.Contains(b.Relation)&&RelativePath(b.EvidencePath)&&Hash(b.EvidenceSha256)&&!string.IsNullOrWhiteSpace(b.EvidenceKind),"invalid/duplicate/conflicting source binding");
        }
        static bool IsCollider(JObject c)=>new[]{"BoxCollider","MeshCollider","SphereCollider","CapsuleCollider","TerrainCollider"}.Contains((string)c["type"]);
        static ObjectBounds Bounds(JToken token)
        {var min=Numbers(token["min"],3,"bounds min");var max=Numbers(token["max"],3,"bounds max");return new ObjectBounds(min[0],min[1],min[2],max[0],max[1],max[2]);}
        static ObjectBounds ColliderBounds(WorldObjectRecord o)
        {
            if(!o.PhysicalEligible)return null;ObjectBounds result=null;
            foreach(var component in o.Components)
            {
                var c=component.Data;if(!IsCollider(c)||component.Enabled!=true||(bool)c["isTrigger"])continue;
                if(c["worldBounds"]==null||c["worldBounds"].Type==JTokenType.Null)continue;
                Require(new[]{"serialized-mesh-aabb","serialized-box","serialized-sphere","serialized-capsule","native-collider-aabb"}.Contains((string)c["boundsEvidence"]),"qualified collider bounds evidence required");
                var bounds=Bounds(c["worldBounds"]);
                if(c["localBounds"]!=null&&c["localBounds"].Type!=JTokenType.Null)
                {
                    var center=Numbers(c["localBounds"]["center"],3,"bounds center");var size=Numbers(c["localBounds"]["size"],3,"bounds size");ObjectBounds derived=null;
                    for(int corner=0;corner<8;corner++)
                    {
                        var p=new[]{center[0]+((corner&1)==0?-.5:.5)*size[0],center[1]+((corner&2)==0?-.5:.5)*size[1],center[2]+((corner&4)==0?-.5:.5)*size[2]};var m=o.Matrix;
                        double x=m[0]*p[0]+m[1]*p[1]+m[2]*p[2]+m[3],y=m[4]*p[0]+m[5]*p[1]+m[6]*p[2]+m[7],z=m[8]*p[0]+m[9]*p[1]+m[10]*p[2]+m[11];derived=ObjectBounds.Union(derived,new ObjectBounds(x,y,z,x,y,z));
                    }
                    Near(new[]{bounds.MinX,bounds.MinY,bounds.MinZ,bounds.MaxX,bounds.MaxY,bounds.MaxZ},new[]{derived.MinX,derived.MinY,derived.MinZ,derived.MaxX,derived.MaxY,derived.MaxZ},"local/world collider bounds mismatch "+o.Id);
                }
                result=ObjectBounds.Union(result,bounds);
            }
            return result;
        }
        public IReadOnlyList<string> ChildrenOf(string id)
        {Require(objects.ContainsKey(id),"unknown object");string[] ids;return Array.AsReadOnly(children.TryGetValue(id,out ids)?ids:new string[0]);}
        /// <summary>Stable per-object streams use source IDs, so unrelated inventory changes do not perturb existing objects.</summary>
        public double Unit(ulong seed,string stream,long tick,string objectId,int draw=0)
        {Require(objects.ContainsKey(objectId)&&!string.IsNullOrEmpty(stream),"registered object/stream required");return Stable.Unit(seed,CacheVersion+"|"+stream,tick,objectId,draw);}
        public void VerifySources(Func<string,byte[]> readBytes)
        {
            Require(readBytes!=null,"source resolver required");Require(HashBytes(readBytes(ScenePath))==SceneSha256,"scene bytes mismatch");
            foreach(var group in bindings.GroupBy(b=>b.EvidencePath,StringComparer.Ordinal))
            {var hashes=group.Select(b=>b.EvidenceSha256).Distinct(StringComparer.Ordinal).ToArray();Require(hashes.Length==1&&HashBytes(readBytes(group.Key))==hashes[0],"binding evidence bytes mismatch");}
            foreach(var group in bindings.Select(b=>b.Data["evidence"]).Where(e=>e["coveragePath"]!=null).GroupBy(e=>(string)e["coveragePath"],StringComparer.Ordinal))
            {var hashes=group.Select(e=>(string)e["coverageSha256"]).Distinct(StringComparer.Ordinal).ToArray();Require(hashes.Length==1&&HashBytes(readBytes(group.Key))==hashes[0],"binding coverage bytes mismatch");}
        }
        static string HashBytes(byte[] bytes)
        {Require(bytes!=null,"missing source bytes");using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        /// <summary>Atomic insertion with idempotent immutable-source markers. Existing typed entities are untouched.</summary>
        public WorldObjectApplyResult Apply(World world,IEnumerable<WorldObjectBinding> explicitBindings=null)
        {
            Require(world!=null,"world required");var associations=bindings.Concat(explicitBindings??Enumerable.Empty<WorldObjectBinding>()).ToArray();ValidateBindings(objects,associations);
            foreach(var b in associations)Require(world.Entities.ContainsKey(b.EntityId)&&!world.Entities[b.EntityId].Has("render-only"),"binding target must be an existing typed entity: "+b.EntityId);
            var byObject=associations.ToDictionary(b=>b.ObjectId,b=>b,StringComparer.Ordinal);
            string bindingFingerprint=Stable.Hash(string.Join("\n",associations.OrderBy(b=>b.ObjectId,StringComparer.Ordinal).Select(b=>Canonical(b.Json()).ToString(Formatting.None))));
            string marker="source-fingerprint:"+Fingerprint+":"+bindingFingerprint;var pending=new List<Entity>();int existing=0;
            var metadata=new SortedDictionary<string,double>(StringComparer.Ordinal);
            foreach(var pair in new[]{new[]{"source.snapshot.sha256",SnapshotSha256},new[]{"source.scene.sha256",SceneSha256},new[]{"source.registry.fingerprint",Fingerprint},new[]{"source.registry.cache-version",Stable.Hash(CacheVersion)}})
                for(int word=0;word<8;word++)metadata[pair[0]+".word"+word]=uint.Parse(pair[1].Substring(word*8,8),NumberStyles.HexNumber,CultureInfo.InvariantCulture);
            int present=metadata.Keys.Count(k=>world.Fields.ContainsKey(k));Require(present==0||present==metadata.Count,"partial source digest metadata");
            foreach(var pair in metadata)Require(!world.Fields.ContainsKey(pair.Key)||world.Fields[pair.Key]==pair.Value,"source digest metadata mismatch");
            foreach(var o in objects.Values)
            {
                Entity found;WorldObjectBinding binding;byObject.TryGetValue(o.Id,out binding);
                string parent=binding!=null?binding.EntityId:o.ParentId;
                if(world.Entities.TryGetValue(o.Id,out found))
                {
                    Require(found.Kind=="source-object"&&found.ParentId==parent&&found.Has("source-input")&&found.Has("render-only")&&found.Has(marker)&&found.X==o.WorldPosition[0]&&found.Y==o.WorldPosition[1]&&found.Z==o.WorldPosition[2]
                        &&found.Get("source.active-self")== (o.ActiveSelf?1:0)&&found.Get("source.active-hierarchy")== (o.ActiveInHierarchy?1:0)&&found.Get("source.physical-eligible")== (o.PhysicalEligible?1:0)&&found.Get("source.bounds-supported")== (o.BoundsSupported?1:0),"source object collision or stale inventory: "+o.Id);existing++;continue;
                }
                var e=new Entity{Id=o.Id,ParentId=parent,Kind="source-object",X=o.WorldPosition[0],Y=o.WorldPosition[1],Z=o.WorldPosition[2]};
                foreach(var tag in new[]{"source-input","render-only","decoration",marker})e.Tags.Add(tag);
                e.Set("source.active-self",o.ActiveSelf?1:0);e.Set("source.active-hierarchy",o.ActiveInHierarchy?1:0);e.Set("source.physical-eligible",o.PhysicalEligible?1:0);e.Set("source.bounds-supported",o.BoundsSupported?1:0);
                if(binding!=null)e.Links.Add(binding.EntityId);pending.Add(e);
            }
            // Every rejection above precedes mutation, including a collision at the final sorted source row.
            foreach(var pair in metadata)world.Fields[pair.Key]=pair.Value;
            foreach(var e in pending)world.Add(e);
            var physical=PhysicalIdentities(associations);
            return new WorldObjectApplyResult{AddedObjects=pending.Count,ExistingObjects=existing,BoundObjects=associations.Length,PhysicalGroups=objects.Values.Where(o=>o.PhysicalEligible).Select(o=>physical[o.Id]).Distinct(StringComparer.Ordinal).Count(),BindingFingerprint=bindingFingerprint};
        }
        Dictionary<string,string> PhysicalIdentities(IEnumerable<WorldObjectBinding> associations)
        {
            var direct=associations.ToDictionary(b=>b.ObjectId,b=>b.EntityId,StringComparer.Ordinal);var ancestor=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var o in objects.Values)
            {
                var chain=new List<string>();string cursor=o.Id,target=null;
                while(cursor!=null)
                {
                    if(direct.TryGetValue(cursor,out target)||ancestor.TryGetValue(cursor,out target))break;
                    chain.Add(cursor);cursor=objects[cursor].ParentId;
                }
                foreach(var id in chain)ancestor[id]=target;
                if(!ancestor.ContainsKey(o.Id))ancestor[o.Id]=target;
            }
            return objects.Keys.ToDictionary(id=>id,id=>ancestor[id]??id,StringComparer.Ordinal);
        }
        public string PhysicalIdentityOf(string objectId){Require(objects.ContainsKey(objectId),"registered physical object required");return physicalIdentities[objectId];}
        /// <summary>All individual source collider candidates before typed physical-parent deduplication.</summary>
        public IReadOnlyList<WorldObjectRecord> QueryObjectCandidates(ObjectBounds bounds)
        {Require(bounds!=null,"query bounds required");var hits=new List<WorldObjectRecord>();Search(spatialRoot,bounds,hits);return Array.AsReadOnly(hits.OrderBy(o=>o.Id,StringComparer.Ordinal).ToArray());}
        /// <summary>Conservative collider candidates, canonical source bindings deduplicated. Unknown bounds excluded and reported.</summary>
        public IReadOnlyList<WorldObjectRecord> Query(ObjectBounds bounds)
        {
            return Array.AsReadOnly(QueryObjectCandidates(bounds).GroupBy(o=>physicalIdentities[o.Id],StringComparer.Ordinal).Select(g=>g.First()).ToArray());
        }
        public IReadOnlyList<WorldObjectRecord> QuerySegmentCandidates(double[] from,double[] to)
        {
            Require(from!=null&&to!=null&&from.Length==3&&to.Length==3&&from.Concat(to).All(x=>!double.IsNaN(x)&&!double.IsInfinity(x)),"finite segment endpoints required");
            var box=new ObjectBounds(Math.Min(from[0],to[0]),Math.Min(from[1],to[1]),Math.Min(from[2],to[2]),Math.Max(from[0],to[0]),Math.Max(from[1],to[1]),Math.Max(from[2],to[2]));
            return Array.AsReadOnly(QueryObjectCandidates(box).Where(o=>o.WorldBounds.IntersectsSegment(from,to)).GroupBy(o=>physicalIdentities[o.Id],StringComparer.Ordinal).Select(g=>g.First()).ToArray());
        }
        public IReadOnlyList<WorldObjectRelation> RelationsFor(string objectId,double padding=0)
        {
            WorldObjectRecord o;Require(objects.TryGetValue(objectId,out o),"registered relation object required");var result=new List<WorldObjectRelation>();
            if(o.ParentId!=null)result.Add(new WorldObjectRelation{ObjectId=objectId,OtherObjectId=o.ParentId,Kind="source-parent"});
            foreach(var b in o.SourceBindings)result.Add(new WorldObjectRelation{ObjectId=objectId,OtherObjectId=b.EntityId,Kind=b.Relation});
            if(o.WorldBounds!=null)foreach(var near in Query(o.WorldBounds.Expand(padding)).Where(n=>n.Id!=objectId))result.Add(new WorldObjectRelation{ObjectId=objectId,OtherObjectId=near.Id,Kind="collider-aabb-candidate"});
            else Require(padding>=0&&!double.IsNaN(padding)&&!double.IsInfinity(padding),"finite relation padding required");
            return Array.AsReadOnly(result.OrderBy(r=>r.Kind,StringComparer.Ordinal).ThenBy(r=>r.OtherObjectId,StringComparer.Ordinal).ToArray());
        }
        sealed class BvhNode{public ObjectBounds Bounds;public BvhNode Left,Right;public WorldObjectRecord[] Leaf;}
        static BvhNode Build(WorldObjectRecord[] values,int start,int count)
        {
            if(count==0)return null;var node=new BvhNode();for(int i=start;i<start+count;i++)node.Bounds=ObjectBounds.Union(node.Bounds,values[i].WorldBounds);
            if(count<=8){node.Leaf=values.Skip(start).Take(count).ToArray();return node;}
            int axis=node.Bounds.Width(0)>=node.Bounds.Width(1)?0:1;if(node.Bounds.Width(2)>node.Bounds.Width(axis))axis=2;
            Array.Sort(values,start,count,Comparer<WorldObjectRecord>.Create((a,b)=>{int c=a.WorldBounds.Center(axis).CompareTo(b.WorldBounds.Center(axis));return c!=0?c:StringComparer.Ordinal.Compare(a.Id,b.Id);}));
            int half=count/2;node.Left=Build(values,start,half);node.Right=Build(values,start+half,count-half);return node;
        }
        static void Search(BvhNode node,ObjectBounds bounds,List<WorldObjectRecord> hits)
        {if(node==null||!node.Bounds.Intersects(bounds))return;if(node.Leaf!=null){foreach(var o in node.Leaf)if(o.WorldBounds.Intersects(bounds))hits.Add(o);return;}Search(node.Left,bounds,hits);Search(node.Right,bounds,hits);}
        static JToken Canonical(JToken token)
        {
            var obj=token as JObject;if(obj!=null){var result=new JObject();foreach(var p in obj.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal))result[p.Name]=Canonical(p.Value);return result;}
            var array=token as JArray;return array!=null?new JArray(array.Select(Canonical)):token.DeepClone();
        }
        static JObject CanonicalObject(JObject row)
        {row["components"]=new JArray(((JArray)row["components"]).OrderBy(c=>(string)c["fileId"],StringComparer.Ordinal));row["sourceBindings"]=new JArray();return row;}
        static double[] Identity()=>new[]{1.0,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1};
        static double ColumnLength(double[] m,int column)=>Math.Sqrt(m[column]*m[column]+m[4+column]*m[4+column]+m[8+column]*m[8+column]);
        static double[] Multiply(double[] a,double[] b)
        {var r=new double[16];for(int row=0;row<4;row++)for(int col=0;col<4;col++)for(int k=0;k<4;k++)r[row*4+col]+=a[row*4+k]*b[k*4+col];return r;}
        static double[] QMultiply(double[] a,double[] b)=>new[]{a[3]*b[0]+a[0]*b[3]+a[1]*b[2]-a[2]*b[1],a[3]*b[1]-a[0]*b[2]+a[1]*b[3]+a[2]*b[0],a[3]*b[2]+a[0]*b[1]-a[1]*b[0]+a[2]*b[3],a[3]*b[3]-a[0]*b[0]-a[1]*b[1]-a[2]*b[2]};
        static double[] Trs(double[] p,double[] q,double[] s)
        {
            double x=q[0],y=q[1],z=q[2],w=q[3];return new[]{(1-2*(y*y+z*z))*s[0],2*(x*y-z*w)*s[1],2*(x*z+y*w)*s[2],p[0],2*(x*y+z*w)*s[0],(1-2*(x*x+z*z))*s[1],2*(y*z-x*w)*s[2],p[1],2*(x*z-y*w)*s[0],2*(y*z+x*w)*s[1],(1-2*(x*x+y*y))*s[2],p[2],0,0,0,1};
        }
    }
}
