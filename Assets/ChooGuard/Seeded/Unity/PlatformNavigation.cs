using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Seeded.Unity
{
    public sealed class PlatformNavigation : IDisposable
    {
        public NavMeshData Data { get; private set; }
        public string Hash { get; private set; }
        public Car04InteriorAccess InteriorAccess { get; private set; }
        public StationWorldAdapter StationWorld { get; private set; }
        public string Version => Stable.Hash(Hash+"|doors:"+string.Join(",",DoorLinks.Keys.OrderBy(x=>x))+"|cordons:"+string.Join(",",cordons.Keys.OrderBy(x=>x,StringComparer.Ordinal))+"|car04:"+(InteriorAccess?.Version??"absent"));
        public void AttachInterior(Car04InteriorAccess access)
        {if(InteriorAccess!=null)throw new InvalidOperationException("Interior access already attached");InteriorAccess=access;Hash=Stable.Hash(Hash+"|car04:"+access.GeometryHash);Report["car04_access"]=access.Report.DeepClone();}
        public void AttachStation(StationWorldAdapter station)
        {if(StationWorld!=null)throw new InvalidOperationException("Station world already attached");if(station==null||!station.Activated)throw new InvalidOperationException("Qualified station navigation required");StationWorld=station;Hash=Stable.Hash(Hash+"|station:"+station.Fingerprint);Report["station_world"]=station.Report.DeepClone();Report["nav_hash"]=Hash;}
        public void UseCurrentSceneNavigation(StationWorldAdapter station)
        {
            if(station==null||station.NavigationHash=="absent")throw new InvalidOperationException("Current source navigation is unavailable");
            PreserveLiveGeometry=true;Hash=station.NavigationHash;AttachStation(station);
            Report["claim"]="Current scene collider navigation only; no generated floors or threshold supports.";
        }
        public readonly JObject Report=new JObject();
        public readonly Dictionary<int,NavMeshLinkInstance> DoorLinks=new Dictionary<int,NavMeshLinkInstance>();
        public bool PreserveLiveGeometry;
        readonly PlatformSource source;
        readonly Transform parent;
        readonly List<GameObject> supportObjects=new List<GameObject>();
        readonly Dictionary<int,GameObject> doorBlockers=new Dictionary<int,GameObject>();
        readonly Dictionary<string,GameObject> cordons=new Dictionary<string,GameObject>(StringComparer.Ordinal);
        NavMeshDataInstance instance;
        public PlatformNavigation(PlatformSource data,Transform owner){source=data;parent=owner;}
        public static Vector3 V(SourcePoint p)=>new Vector3((float)p.x,(float)p.y,(float)p.z);
        public void Bake(GameObject official,GameObject train)=>Bake(new[]{official},train);
        public void Bake(IEnumerable<GameObject> platformRoots,GameObject train)
        {
            var roots=platformRoots.Concat(new[]{train}).Distinct().ToArray();
            int sourceColliders=0;
            foreach(var root in roots)foreach(var f in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if(f.sharedMesh==null)continue;
                // Preserve every current source collider mesh/flag. A missing collider
                // is a bounded isolated support from the exact current visual mesh.
                var mc=f.GetComponent<MeshCollider>();
                if(mc==null)
                {
                    var support=new GameObject("Current source mesh collision support");support.transform.SetParent(f.transform,false);
                    mc=support.AddComponent<MeshCollider>();mc.sharedMesh=f.sharedMesh;mc.convex=false;supportObjects.Add(support);
                }
                if(mc.enabled&&!mc.isTrigger)sourceColliders++;
            }
            Vector3 axis=V(source.train.axis).normalized;
            foreach(var c in source.train.cars.Where(_=>!PreserveLiveGeometry))
            {
                var near=V(c.door.inside)-axis*Mathf.Sign(Vector3.Dot(V(c.aisle[c.aisle.Length-1])-V(c.door.inside),axis))*1.2f;
                var far=V(c.aisle[c.aisle.Length-1]);near.y=far.y=(float)c.floor;
                var centre=(near+far)*.5f;var lateral=new Vector3(axis.z,0,-axis.x);var mid=(V(c.min)+V(c.max))*.5f;
                centre+=lateral*Vector3.Dot(mid-centre,lateral);
                Box("Training floor support "+c.number,centre-Vector3.up*.06f,Quaternion.LookRotation(axis),new Vector3(2.7f,.12f,Vector3.Distance(near,far)+1.4f));
                var outside=V(c.door.outside);var inside=V(c.door.inside);var inward=(inside-outside);inward.y=0;inward.Normalize();
                if(inside.y-outside.y>.28f)
                    Box("Training threshold step "+c.number,(outside+inside)*.5f-Vector3.up*.05f-inward*.12f,Quaternion.LookRotation(inward),new Vector3(.95f,.1f,.6f));
            }
            Physics.SyncTransforms();
            var bounds=new Bounds(new Vector3(60,1,0),new Vector3(240,5,390));
            var sources=new List<NavMeshBuildSource>();
            // Unity clears the destination list on every CollectSources call.
            // Collect each authorized subtree into a separate list, then combine.
            foreach(var root in roots.Concat(supportObjects))
            {
                var collected=new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(root.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),collected);
                sources.AddRange(collected);
            }
            sources.RemoveAll(s=>s.component is Collider c&&(c.isTrigger||c is CharacterController));
            var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=.28f;settings.agentHeight=1.72f;settings.agentClimb=.28f;settings.agentSlope=36;settings.overrideVoxelSize=true;settings.voxelSize=.06f;settings.overrideTileSize=true;settings.tileSize=256;settings.minRegionArea=.5f;
            Data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if(Data==null)throw new InvalidOperationException("Scoped platform NavMesh bake returned no data");
            instance=NavMesh.AddNavMeshData(Data);
            var tri=NavMesh.CalculateTriangulation();var key=new System.Text.StringBuilder();
            foreach(var v in tri.vertices)key.Append(v.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(v.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(v.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            foreach(var index in tri.indices)key.Append(index).Append(',');Hash=Stable.Hash(key.ToString());
            Report["source_colliders"]=sourceColliders;Report["build_sources"]=sources.Count;Report["explicit_source_roots"]=new JArray(roots.Select(r=>r.name));Report["whole_station_parent_collected"]=false;Report["nav_vertices"]=tri.vertices.Length;Report["nav_triangles"]=tri.indices.Length/3;Report["nav_hash"]=Hash;
            var doors=new JArray();
            foreach(var car in source.train.cars)
            {
                var row=new JObject{["car"]=car.number};bool valid=TryDoor(car,true,out var reason);row["linked"]=valid;row["reason"]=reason;
                var a=V(car.door.outside);var b=V(car.door.inside);
                row["outside_floor"]=Floor(a);row["inside_floor"]=Floor(b);
                if(valid){var path=new NavMeshPath();row["complete_path"]=NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;}
                doors.Add(row);
            }
            Report["doors"]=doors;
            Report["claim"]="Runtime NavMesh on supplied model colliders plus explicit modeled train floor/threshold support; field fidelity unverified.";
        }
        void Box(string name,Vector3 centre,Quaternion rotation,Vector3 size)
        {
            var go=new GameObject(name,typeof(BoxCollider));go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(centre,rotation);go.GetComponent<BoxCollider>().size=size;supportObjects.Add(go);
        }
        static bool Floor(Vector3 feet)=>Physics.Raycast(feet+Vector3.up*.18f,Vector3.down,out var hit,.4f,~(1<<2),QueryTriggerInteraction.Ignore)&&Vector3.Dot(hit.normal,Vector3.up)>.7f;
        public bool TryDoor(SourceCar c,bool open,out string reason)
        {
            ProjectDoorBlocker(c,!open);
            if(DoorLinks.TryGetValue(c.number,out var old))
            {
                if(!open){if(NavMesh.IsLinkValid(old))NavMesh.RemoveLink(old);DoorLinks.Remove(c.number);reason="closed";return true;}
                reason="existing-valid-link";return NavMesh.IsLinkValid(old);
            }
            if(!open){reason="closed";return true;}
            Vector3 a=V(c.door.outside),b=V(c.door.inside);
            if(!NavMesh.SamplePosition(a,out var ha,.9f,NavMesh.AllAreas)||Mathf.Abs(ha.position.y-a.y)>.25f){reason="outside-not-attached";return false;}
            if(!NavMesh.SamplePosition(b,out var hb,.9f,NavMesh.AllAreas)||Mathf.Abs(hb.position.y-b.y)>.25f){reason="inside-not-attached";return false;}
            if(!Floor(ha.position)||!Floor(hb.position)){reason="endpoint-floor-missing";return false;}
            var delta=hb.position-ha.position;
            // Capsule spans above the modeled threshold step. Missing head clearance refuses the link.
            var start=ha.position+Vector3.up*.52f;
            if(Physics.CapsuleCast(start,start+Vector3.up*.9f,.22f,delta.normalized,out var hit,delta.magnitude,~(1<<2),QueryTriggerInteraction.Ignore))
            {reason="capsule-blocked:"+hit.collider.name;return false;}
            var link=NavMesh.AddLink(new NavMeshLinkData{startPosition=ha.position,endPosition=hb.position,width=.55f,bidirectional=true,agentTypeID=0,area=0,costModifier=-1});
            if(!NavMesh.IsLinkValid(link)){reason="invalid-link";return false;}
            DoorLinks[c.number]=link;reason="supported-endpoints-and-head-clearance";return true;
        }
        void ProjectDoorBlocker(SourceCar car,bool closed)
        {
            if(!closed){if(doorBlockers.TryGetValue(car.number,out var old)){UnityEngine.Object.DestroyImmediate(old);doorBlockers.Remove(car.number);}return;}
            if(doorBlockers.ContainsKey(car.number))return;
            var outside=V(car.door.outside);var inside=V(car.door.inside);var inward=inside-outside;inward.y=0;inward.Normalize();
            var go=new GameObject("Closed door navigation aperture "+car.number,typeof(NavMeshObstacle));go.transform.SetParent(parent,false);var center=(outside+inside)*.5f;center.y=Mathf.Min(outside.y,inside.y)+1;go.transform.position=center;go.transform.rotation=Quaternion.LookRotation(inward);
            var obstacle=go.GetComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=new Vector3(1.5f,2.6f,.8f);obstacle.carving=true;obstacle.carveOnlyStationary=false;doorBlockers[car.number]=go;
        }
        public void ProjectRecordedDoor(SourceCar car,bool linked)
        {
            ProjectDoorBlocker(car,!linked);
            if(DoorLinks.TryGetValue(car.number,out var old)){if(linked)return;if(NavMesh.IsLinkValid(old))NavMesh.RemoveLink(old);DoorLinks.Remove(car.number);return;}
            if(!linked)return;
            var instance=NavMesh.AddLink(new NavMeshLinkData{startPosition=V(car.door.outside),endPosition=V(car.door.inside),width=.55f,bidirectional=true,agentTypeID=0,area=0,costModifier=-1});DoorLinks[car.number]=instance;
        }
        public bool ValidStoredSpawn(Vector3 feet)
        {
            return NavMesh.SamplePosition(feet,out var hit,.12f,NavMesh.AllAreas)&&Vector3.Distance(hit.position,feet)<.08f&&Floor(feet)&&!Physics.CheckCapsule(feet+Vector3.up*.30f,feet+Vector3.up*1.42f,.28f,~0,QueryTriggerInteraction.Ignore);
        }
        public bool TrySpawn(Vector3 desired,out Vector3 feet)
        {
            feet=default;
            for(int i=-1;i<16;i++)
            {
                var p=i<0?desired:desired+new Vector3((i%4-1.5f)*.5f,0,(i/4-1.5f)*.5f);
                if(!NavMesh.SamplePosition(p,out var hit,1,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-desired.y)>.3f||!Floor(hit.position))continue;
                if(Physics.CheckCapsule(hit.position+Vector3.up*.30f,hit.position+Vector3.up*1.42f,.28f,~0,QueryTriggerInteraction.Ignore))continue;
                feet=hit.position+Vector3.up*.04f;return true;
            }
            return false;
        }
        public void ProjectCordon(Entity entity)
        {
            bool blocked=entity.Get("blocked")>.5;
            if(!blocked){if(cordons.TryGetValue(entity.Id,out var old)){UnityEngine.Object.DestroyImmediate(old);cordons.Remove(entity.Id);}return;}
            if(cordons.ContainsKey(entity.Id))return;
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Training cordon · "+entity.Id;go.transform.SetParent(parent,false);go.transform.position=new Vector3((float)entity.X,(float)entity.Y+1,(float)entity.Z);go.transform.localScale=new Vector3(3,2,.4f);
            var obstacle=go.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=Vector3.one;obstacle.carving=true;obstacle.carveOnlyStationary=false;cordons[entity.Id]=go;
        }
        public JArray PhysicalDoorProbes(int? onlyCar=null)
        {
            var rows=new JArray();
            foreach(var car in source.train.cars.Where(c=>!onlyCar.HasValue||c.number==onlyCar.Value))
            {
                var row=new JObject{["car"]=car.number};var a=V(car.door.outside);var b=V(car.door.inside);
                if(!TrySpawn(a,out var feet)){row["passed"]=false;row["reason"]="no-safe-outside-pose";rows.Add(row);continue;}
                var go=new GameObject("Door physical probe",typeof(CharacterController));go.layer=2;var cc=go.GetComponent<CharacterController>();cc.height=1.72f;cc.radius=.28f;cc.center=Vector3.up*.86f;cc.skinWidth=.025f;cc.stepOffset=.28f;cc.slopeLimit=36;go.transform.position=feet;Physics.SyncTransforms();
                float vertical=0;CollisionFlags flags=CollisionFlags.None;
                for(int step=0;step<180;step++)
                {
                    var direction=b-go.transform.position;direction.y=0;if(direction.magnitude<.15f&&Mathf.Abs(go.transform.position.y-b.y)<.2f)break;
                    if(cc.isGrounded&&vertical<0)vertical=-2;vertical=Mathf.Max(-30,vertical-9.81f*.02f);
                    flags|=cc.Move(direction.normalized*.024f+Vector3.up*vertical*.02f);
                }
                float delta=Vector3.Distance(go.transform.position,b);row["end"]=PlatformSeededSimulator.Point(go.transform.position);row["distance_to_inside"]=delta;row["passed"]=delta<.5f;row["collision_flags"]=flags.ToString();UnityEngine.Object.DestroyImmediate(go);rows.Add(row);
            }
            return rows;
        }
        public void Dispose(){InteriorAccess?.Dispose();InteriorAccess=null;foreach(var support in supportObjects)if(support!=null)UnityEngine.Object.DestroyImmediate(support);supportObjects.Clear();foreach(var blocker in doorBlockers.Values)if(blocker!=null)UnityEngine.Object.DestroyImmediate(blocker);doorBlockers.Clear();foreach(var cordon in cordons.Values)if(cordon!=null)UnityEngine.Object.DestroyImmediate(cordon);cordons.Clear();foreach(var link in DoorLinks.Values)if(NavMesh.IsLinkValid(link))NavMesh.RemoveLink(link);DoorLinks.Clear();if(instance.valid)instance.Remove();if(Data!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(Data);else UnityEngine.Object.DestroyImmediate(Data);}}
    }
}
