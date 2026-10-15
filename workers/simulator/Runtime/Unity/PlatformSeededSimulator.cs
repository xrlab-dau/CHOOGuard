using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ChooGuard.Seeded.Unity
{
    public sealed class PlatformSeededSimulator : MonoBehaviour,INativeProviderMenuHost
    {
        public GameObject KtxPrefab;
        public GameObject[] PlatformPrefabs;
        public StationWorldAdapter WorldSnapshot;
        public string InitialSeed="";
        public int Population=32;
        public double ModelDecisionDeadlineSeconds=60;
        public bool UseSubscription;
        public TMP_FontAsset ProviderMenuFont;
        public ResponseRole PlayerRole = ResponseRole.StationStaff;
        public bool AddLocalTrainingTeam = true;
        public DetailedManipulationController DetailedControls { get; private set; }
        public ResponseInteractionController ResponseControls { get; private set; }
        public bool DetailedMode => WorldSnapshot != null;
        public bool IsReplaying => replayTicks != null;
        SubscriptionConnection subscription;SubscriptionStatus subscriptionStatus;
        SubscriptionConnection.SubscriptionSession preparedSubscriptionSession,runSubscriptionSession;
        JObject runProviderSelection;
        NativeProviderControlClient providerControl;Task<NativeProviderControlReply> providerControlTask;CancellationTokenSource providerControlCancellation;
        string providerControlAction;bool restoreProviderCapture,restoreProviderDetailed,restoreProviderResponse,refreshAfterProviderConflict,cancelProviderFlowQueued,suppressProviderBrowserLaunch,pendingProviderCaptureRestore;int providerCaptureRestoreFrame;double nextProviderPoll;
        ProviderSelectionPanel providerPanel;
        public NativeProviderState ProviderState {get;private set;}
        public NativeAuthFlow ActiveProviderLogin {get;private set;}
        public bool ProviderMenuOpen {get;private set;}
        public bool ProviderBusy=>providerControlTask!=null||providerControl!=null&&providerControl.IsBusy;
        public bool CanChangeProvider=>!Running&&emergenceTask==null&&!IsReplaying;
        public string ProviderMessage {get;private set;}="플레이에 사용할 AI 계정과 모델을 선택하세요.";
        Task<EmergenceRecord> emergenceTask;CancellationTokenSource emergenceCancellation;World preparedWorld;EmergenceRecord preparedRecord;ulong preparedSeed;int preparedPopulation;string preparedFingerprint;
        string emergenceMessage="Generate initial world conditions once, then run the local causal simulation.";
        public World State { get; private set; }
        public PlatformNavigation Navigation { get; private set; }
        public SeededFpsController Player { get; private set; }
        public PlatformSource Source { get; private set; }
        public string Status="Choose a seed, then start. Every run records fixed-tick inputs and state digests.";
        public JObject Verification { get; private set; }
        public bool Running { get; private set; }
        public string LastRecordingPath=>logPath;
        public JObject LastInitialProjectionFailure {get;private set;}
        public JObject LastReplayOutcomeFailure {get;private set;}
        public JObject StationInferenceReport {get;private set;}
        readonly Dictionary<string,SeededNpcController> npcs=new Dictionary<string,SeededNpcController>(StringComparer.Ordinal);
        readonly Dictionary<string,GameObject> markers=new Dictionary<string,GameObject>(StringComparer.Ordinal);
        readonly Dictionary<string,string> inferredSubjects=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly Dictionary<int,List<(Transform leaf,Vector3 open)>> doors=new Dictionary<int,List<(Transform,Vector3)>>();
        readonly List<JObject> pendingCommands=new List<JObject>();
        readonly List<GameObject> dynamicObjects=new List<GameObject>();
        readonly CausalKernel kernel=new CausalKernel();readonly Community community=new Community();
        OperationCatalog catalog;PlanExecutor executor;StreamWriter tape;
        PlanningCoordinator planner;
        readonly Dictionary<string,string> planningSignatures=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly List<ActivePlan> activePlans=new List<ActivePlan>();
        int planningCursor;
        sealed class ActivePlan{public ActionPlan Plan;public int Index;public bool Started;public long LastProgressTick;public double LastDistance;}
        Transform visualRoot,simulationRoot;GameObject train,official;
        double elapsed;bool prepared;string targetId="",feedback="",logPath="";
        int selectedAction,journalCursor;
        string replayPath="";Queue<JObject> replayTicks;
        Queue<JArray> replayNavigation;
        JArray navigationObservations;
        Material npcMaterial,toolMaterial,fireMaterial;

        void Awake(){try{if(string.IsNullOrEmpty(InitialSeed))NewSeed();Prepare();}catch(Exception e){Status="Preparation failed: "+e.Message;Debug.LogException(e);}}
        public void NewSeed()
        {
            if(Running)return;var bytes=new byte[8];using(var random=System.Security.Cryptography.RandomNumberGenerator.Create())random.GetBytes(bytes);
            InitialSeed=BitConverter.ToUInt64(bytes,0).ToString(CultureInfo.InvariantCulture);preparedWorld=null;preparedRecord=null;preparedSubscriptionSession=null;
        }
        public void Prepare()
        {
            if(prepared)return;
            Source=PlatformSource.Parse(Resources.Load<TextAsset>("PlatformSource").text);
            visualRoot=new GameObject("Supplied station and KTX resources").transform;visualRoot.SetParent(transform,false);
            if(WorldSnapshot!=null)
            {
                WorldSnapshot.Prepare(visualRoot);WorldSnapshot.ActivateGeometry();
                official=WorldSnapshot.OfficialFrame;train=WorldSnapshot.TrainVisual;
            }
            else
            {
                official=new GameObject("Official platform source frame");official.transform.SetParent(visualRoot,false);
                official.transform.localPosition=new Vector3(-41.342197f,0,-19.307789f);official.transform.localRotation=new Quaternion(0,.9999927f,0,-.003813778f);
                foreach(var asset in PlatformPrefabs){var instance=Instantiate(asset,official.transform,false);instance.name=asset.name;}
                train=Instantiate(KtxPrefab,visualRoot,false);train.name="KTXSource";
                train.transform.localPosition=new Vector3(88.91879f,-.73000026f,76.442825f);train.transform.localRotation=new Quaternion(.12476154f,.6960133f,.6960134f,-.12476152f);train.transform.localScale=Vector3.one*100;
            }
            official.AddComponent<SemanticBinding>().EntityId="@platform";
            train.AddComponent<SemanticBinding>().EntityId="train:ktx";
            simulationRoot=new GameObject("Training supports and agents").transform;simulationRoot.SetParent(transform,false);
            // Keep immutable visuals separate from generated training support and runtime navigation.
            Navigation=new PlatformNavigation(Source,transform);Navigation.Bake(WorldSnapshot==null?new[]{official}:WorldSnapshot.PlatformSourceRoots,train);
            var car04Access=Resources.Load<TextAsset>("Car04Access");
            if(car04Access!=null)
            {
                string reason="baseline-prefab-contract";
                if(WorldSnapshot==null||WorldSnapshot.VerifyCar04SourceContract(car04Access.text,out reason))
                {
                    try{Navigation.AttachInterior(new Car04InteriorAccess(train,transform,car04Access.text));}
                    catch(Exception e){if(WorldSnapshot==null)throw;Navigation.Report["car04_disabled_reason"]="Current source contract refused: "+e.Message;}
                }
                else Navigation.Report["car04_disabled_reason"]=reason;
            }
            if(WorldSnapshot!=null)
            {
                try{WorldSnapshot.BakeStationNavigation();}
                catch(Exception e){WorldSnapshot.Report["station_navigation_disabled_reason"]=e.Message;}
                Navigation.AttachStation(WorldSnapshot);
            }
            catalog=OperationCatalog.CreateDefault();executor=new PlanExecutor(catalog);
            executor.NavigationInvalidated+=reason=>{ProjectDoors();ProjectCordons();feedback="Navigation changed: "+reason;};
            foreach(var c in Source.train.cars)
            {
                var root=train.transform.Find(c.name);var bindings=new List<(Transform,Vector3)>();
                if(root!=null)foreach(var leafName in c.door.leaves??Array.Empty<string>())
                {var leaf=root.Find(leafName);if(leaf==null)continue;bindings.Add((leaf,leaf.localPosition));leaf.gameObject.AddComponent<SemanticBinding>().EntityId="door:"+c.number;}
                doors[c.number]=bindings;
                if(root!=null)root.gameObject.AddComponent<SemanticBinding>().EntityId="car:"+c.number;
            }
            foreach(var space in WorldCoverage.Parse(Resources.Load<TextAsset>("SemanticCoverage").text).visual_consist.Where(x=>!string.IsNullOrEmpty(x.physical_entity_id)))
            {
                int slash=space.node_path.IndexOf('/');var node=train.transform.Find(slash>=0?space.node_path.Substring(slash+1):space.node_path);if(node==null)continue;
                var binding=node.GetComponent<SemanticBinding>();if(binding==null)binding=node.gameObject.AddComponent<SemanticBinding>();binding.EntityId=space.physical_entity_id;
            }
            var playerObject=new GameObject("Staff FPS",typeof(CharacterController));playerObject.layer=2;playerObject.AddComponent<SemanticBinding>().EntityId="player";playerObject.transform.SetParent(simulationRoot,false);Player=playerObject.AddComponent<SeededFpsController>();
            var desire=PlatformNavigation.V(Source.train.cars[1%Source.train.cars.Length].door.outside)+new Vector3(-(float)Source.train.axis.z,0,(float)Source.train.axis.x)*2;
            bool sourceSpawn=Navigation.TrySpawn(desire,out var feet);
            if(!sourceSpawn&&WorldSnapshot!=null)WorldSnapshot.Report["primary_player_spawn_failure"]="No current source-supported capsule-clear platform candidate near the blueprint desire";
            if(!sourceSpawn&&WorldSnapshot?.Report["station_places"]?["places"] is JArray stationPlaces)
                foreach(JObject place in stationPlaces)
                {
                    var point=place["feet"].Select(v=>(float)v).ToArray();var candidate=new Vector3(point[0],point[1],point[2]);
                    if(!Navigation.ValidStoredSpawn(candidate))continue;feet=candidate;sourceSpawn=true;WorldSnapshot.Report["player_spawn_fallback_source_place"]=(string)place["id"];break;
                }
            if(!sourceSpawn)throw new InvalidOperationException("No source-supported capsule-clear platform or qualified station spawn");
            Player.Pose(feet,20);Player.Capture(false);
            var sun=new GameObject("Platform daylight",typeof(Light));sun.transform.SetParent(transform,false);sun.transform.rotation=Quaternion.Euler(42,-30,0);sun.GetComponent<Light>().type=LightType.Directional;sun.GetComponent<Light>().intensity=1.35f;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.61f,.68f);RenderSettings.ambientIntensity=1;
            Player.View.backgroundColor=new Color(.55f,.68f,.81f);Player.View.clearFlags=CameraClearFlags.SolidColor;
            npcMaterial=Material("NPC training body",new Color(.15f,.42f,.65f));toolMaterial=Material("Training equipment",new Color(.85f,.18f,.06f));fireMaterial=Material("State fire indicator",new Color(1,.2f,.01f));
            Verification=new JObject{["navigation"]=Navigation.Report.DeepClone(),["source_platform_prefabs"]=WorldSnapshot==null?PlatformPrefabs.Length:0,["ktx_renderers"]=train.GetComponentsInChildren<MeshRenderer>().Length,["source_cars_connected"]=Source.train.cars.Length,["visual_consist_resource"]=10,["spawn"]=Point(feet),["source_claim"]=Source.claim};
            if(WorldSnapshot!=null)Verification["full_world_snapshot"]=WorldSnapshot.Report.DeepClone();
            Verification["ktx_bounds"]=BoundsJson(BoundsOf(train));Verification["platform_bounds"]=BoundsJson(BoundsOf(official));
            Verification["source_assembly_hash"]=SourceFingerprint;
            prepared=true;Status="Ready. Source KTX and platforms loaded; "+Navigation.DoorLinks.Count+" verified door links. Select seed and start.";
        }
        static Material Material(string name,Color colour)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");var material=new Material(shader){name=name};material.color=colour;return material;
        }
        void InitializeDetailedWorld(bool savedState = false)
        {
            if(!DetailedMode)return;
            PopulateSourceInteractionCenters();
            if(ResponseControls==null)ResponseControls=gameObject.AddComponent<ResponseInteractionController>();
            ResponseRole role=savedState?(ResponseRole)(int)State.Entities["player"].Get("response.role",(int)PlayerRole):PlayerRole;
            ResponseControls.Initialize(this,role,!savedState&&AddLocalTrainingTeam);
            if(DetailedControls==null)DetailedControls=gameObject.AddComponent<DetailedManipulationController>();
            DetailedControls.Initialize(this);
            foreach(var car in Source.train.cars)
                foreach(var pair in doors[car.number])DetailedControls.RegisterLeaf("door:"+car.number,pair.leaf,pair.open-PlatformNavigation.V(car.door.openDelta),pair.open);
            if(InteriorAccess!=null)
            {
                // These exact generated leaf components are emitted only by the qualified Car04 partition adapter.
                foreach(var binding in train.GetComponentsInChildren<SemanticBinding>(true).Where(b=>InteriorAccess.DoorIds.Contains(b.EntityId)&&b.name.EndsWith("_SOURCE_CABIN_LEAF",StringComparison.Ordinal)))
                {
                    var config=JObject.Parse(Resources.Load<TextAsset>("Car04Access").text);var row=config["cabin_doors"].Single(r=>(string)r["id"]==binding.EntityId);
                    var delta=new Vector3((float)row["training_open_delta_world"]["x"],(float)row["training_open_delta_world"]["y"],(float)row["training_open_delta_world"]["z"]);
                    Vector3 localDelta=binding.transform.parent.InverseTransformVector(delta), open=binding.transform.localPosition;
                    if(State.Entities.TryGetValue(binding.EntityId,out var entity))open-=localDelta*(1-(float)entity.Get("open",1));
                    DetailedControls.RegisterLeaf(binding.EntityId,binding.transform,open-localDelta,open);
                }
            }
        }
        void PopulateSourceInteractionCenters()
        {
            if(WorldSnapshot==null||State.Fields.ContainsKey("response.native-centers.initialized"))return;
            var bounds=new Dictionary<Transform,Bounds>();
            foreach(var pair in WorldSnapshot.Objects.OrderByDescending(p=>p.Value.GetComponentsInParent<Transform>(true).Length))
            {
                var node=pair.Value;Bounds? box=null;var collider=node.GetComponent<Collider>();var renderer=node.GetComponent<Renderer>();
                if(collider!=null&&collider.bounds.size.sqrMagnitude>1e-12f)box=collider.bounds;
                else if(renderer!=null&&renderer.bounds.size.sqrMagnitude>1e-12f)box=renderer.bounds;
                foreach(Transform child in node)if(bounds.TryGetValue(child,out var childBounds)){if(box.HasValue){var b=box.Value;b.Encapsulate(childBounds);box=b;}else box=childBounds;}
                if(box.HasValue)bounds[node]=box.Value;
            }
            foreach(var pair in WorldSnapshot.Objects.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                if(!State.Entities.TryGetValue(pair.Key,out var source))continue;
                bool known=bounds.TryGetValue(pair.Value,out var b);source.Set("response.bounds-known",known?1:0);
                if(!known)continue;
                foreach(var owner in new[]{source,pair.Value.GetComponent<SemanticBinding>() is SemanticBinding semantic&&State.Entities.TryGetValue(semantic.EntityId,out var typed)?typed:null}.Where(e=>e!=null))
                {if(owner.Get("mech.center.qualified")==1)continue;owner.Set("mech.center.x",b.center.x);owner.Set("mech.center.y",b.center.y);owner.Set("mech.center.z",b.center.z);owner.Set("mech.center.qualified",1);}
            }
            State.Fields["response.native-centers.initialized"]=1;
        }
        public void QueueMechanicalInput(MechanicalInput input)
        {if(DetailedControls==null)throw new InvalidOperationException("Detailed controls are not initialized");DetailedControls.Queue(input);}
        public void QueueResponseInput(ResponseInput input)
        {if(ResponseControls==null)throw new InvalidOperationException("Response controls are not initialized");ResponseControls.Queue(input);}
        void ApplyStationInference(World world)
        {
            if(WorldSnapshot==null)return;
            var profile=Resources.Load<TextAsset>("StationCapabilityProfiles");
            if(profile==null)throw new InvalidOperationException("Station capability profiles are required for full-world detailed mode");
            StationInferenceReport=new StationCapabilityInference(profile.text).Apply(world,WorldSnapshot.Registry).Json;
            inferredSubjects.Clear();
            foreach(var assembly in StationInferenceReport["assemblies"].OfType<JObject>().Where(a=>(string)a["role"]!="public-space").OrderBy(a=>a["sourceObjectIds"].Count()).ThenBy(a=>(string)a["id"],StringComparer.Ordinal))
            {
                string entityId=(string)assembly["id"];
                foreach(string sourceId in assembly["sourceObjectIds"].Values<string>())if(!inferredSubjects.ContainsKey(sourceId))inferredSubjects.Add(sourceId,entityId);
                var entity=world.Entities[entityId];
                for(int axis=0;axis<3;axis++){string name=new[]{"x","y","z"}[axis];entity.Set("mech.bounds.min."+name,(double)assembly["bounds"]["min"][axis]);entity.Set("mech.bounds.max."+name,(double)assembly["bounds"]["max"][axis]);}
            }
            if(Verification!=null)Verification["station_capability_inference"]=StationInferenceReport.DeepClone();
        }
        public bool StartRun(string seedText,bool record=true,bool offlineReplay=false)
        {
            if(emergenceTask!=null){Status="Wait for initial condition generation to complete.";return false;}
            if(!offlineReplay&&(ProviderBusy||ActiveProviderLogin!=null&&ActiveProviderLogin.Pending)){Status="Finish or cancel the account connection before starting play.";return false;}
            if(!ulong.TryParse(seedText,NumberStyles.None,CultureInfo.InvariantCulture,out var seed)){Status="Seed must be an unsigned integer.";return false;}
            if(!offlineReplay&&UseSubscription&&(subscription==null||!subscription.IsSelectionCurrent(subscriptionStatus))){Status="Refresh and explicitly enable the AI connection before starting this provider mode.";return false;}
            bool generated=!offlineReplay&&preparedWorld!=null&&preparedRecord!=null&&preparedSeed==seed&&preparedPopulation==Population&&preparedFingerprint==SourceFingerprint&&preparedWorld.Digest()==preparedRecord.PostDigest;
            if(generated&&((preparedSubscriptionSession!=null)!=UseSubscription)){Status="The AI mode changed after initial generation. Generate conditions again with the provider/model chosen for this play session.";return false;}
            if(generated&&UseSubscription&&(!preparedSubscriptionSession.IsCurrent||!preparedSubscriptionSession.Selection.SameAs(subscriptionStatus.Selection))){Status="Provider/account/model changed after initial generation. Refresh the connection and generate conditions again.";return false;}
            Prepare();StopRun();
            if(!generated){preparedWorld=null;preparedRecord=null;preparedSubscriptionSession=null;}
            runSubscriptionSession=!offlineReplay&&UseSubscription?(generated?preparedSubscriptionSession:subscription.LockSelection(subscriptionStatus)):null;
            runProviderSelection=runSubscriptionSession?.Selection.ToRecord();
            State=generated?WorldStateCodec.Clone(preparedWorld):PlatformWorldFactory.Create(Resources.Load<TextAsset>("PlatformSource").text,Resources.Load<TextAsset>("SemanticCoverage").text,seed,Population);
            var staff=State.Entities["player"];if(generated)Player.Pose(new Vector3((float)staff.X,(float)staff.Y,(float)staff.Z),Player.transform.eulerAngles.y);var p=Player.transform.position;staff.X=p.x;staff.Y=p.y;staff.Z=p.z;
            if(WorldSnapshot!=null){WorldSnapshot.Apply(State);ApplyStationInference(State);if(!generated)WorldSnapshot.DistributeStationNpcs(State);}
            else if(!State.Entities.Values.Any(e=>e.Has("decoration"))){RegisterDecorations(State,official.transform,"visual:official","platform:p56");RegisterDecorations(State,train.transform,"visual:ktx","train:ktx");}
            ProjectDoors();ProjectCordons();
            foreach(var e in State.Entities.Values.Where(x=>x.Kind=="service-cabinet"||x.Kind=="extinguisher"||x.Kind=="public-address"||x.Kind=="luggage").ToArray())SpawnProxy(e);
            InitializeDetailedWorld();
            foreach(var e in State.Entities.Values.Where(x=>x.Has("npc")&&x.Id!="player").ToArray())SpawnNpc(e,generated);
            elapsed=0;journalCursor=DetailedMode?State.Journal.Count:0;pendingCommands.Clear();Running=true;InitialSeed=seedText;
            planner?.Dispose();planner=offlineReplay?new PlanningCoordinator(catalog):CreatePlanner();planningCursor=0;planningSignatures.Clear();activePlans.Clear();
            ProjectDoors();ProjectCordons();
            if(generated&&State.Digest()!=preparedRecord.PostDigest){LastInitialProjectionFailure=ProjectionDifferences(preparedWorld,State);StopRun();Status="Prepared world changed during physical projection; generate conditions again.";return false;}
            if(record)
            {
                string runRoot=Application.isEditor?Path.GetFullPath(Path.Combine(Application.dataPath,"..")):Application.persistentDataPath;string directory=Path.Combine(runRoot,"Runs");Directory.CreateDirectory(directory);
                logPath=Path.Combine(directory,"seed-"+seed+"-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+".jsonl");tape=new StreamWriter(logPath,false);
                Write(new JObject{["type"]="RunHeader",["engine"]=World.EngineVersion,["rng"]=Stable.RandomVersion,["seed"]=seed.ToString(CultureInfo.InvariantCulture),["config"]=new JObject{["population"]=Population,["step_seconds"]=InitialConditionMaterial.FloatBits(State.StepSeconds),["rate_multiplier"]=InitialConditionMaterial.FloatBits(State.RateMultiplier),["detailed_mode"]=DetailedMode,["mechanics_version"]=DetailedMode?MechanicalInteractions.Version:null,["response_version"]=DetailedMode?ResponseWorkflows.Version:null,["player_role"]=(int)PlayerRole,["local_training_team"]=AddLocalTrainingTeam},["source_manifest"]=LocalTapeCodec.ParseJsonData(Resources.Load<TextAsset>("SourceManifest").text),["source_manifest_hash"]=SourceFingerprint,["full_world_fingerprint"]=WorldSnapshot?.Fingerprint,["nav_hash"]=Navigation.Hash,["state_encoding"]=WorldStateCodec.Encoding,["initial_state"]=WorldStateCodec.ToToken(State),["initial_digest"]=State.Digest(),["emergence_generation"]=generated?LocalTapeCodec.FromObject(preparedRecord):null,["provider_selection"]=runProviderSelection?.DeepClone(),["initial_provider_selection"]=generated?preparedSubscriptionSession?.Selection.ToRecord():null,["model_policy"]=planner.GeneratorStatus,["claim"]="Recorded-observation replay of seeded causal state, not independent resimulation of Unity physics. One browser-selected provider/account/model is locked for initial conditions and NPC planning; replay makes no model calls."});
            }
            Status="Running seed "+seed+". WASD / Shift; click Capture, E interact, Escape release. "+(record?"JSONL recording enabled.":"Replay validation.");return true;
        }
        static JObject ProjectionDifferences(World before,World after)
        {
            var changes=new JArray();
            foreach(var pair in before.Entities)
            {
                var a=pair.Value;var b=after.Entities[pair.Key];
                if(a.X!=b.X||a.Y!=b.Y||a.Z!=b.Z)changes.Add(new JObject{["id"]=a.Id,["before"]=new JArray(a.X,a.Y,a.Z),["after"]=new JArray(b.X,b.Y,b.Z)});
                foreach(var key in a.State.Keys.Union(b.State.Keys))if(a.Get(key)!=b.Get(key)||a.State.ContainsKey(key)!=b.State.ContainsKey(key))changes.Add(new JObject{["id"]=a.Id,["property"]=key,["before"]=a.Get(key),["after"]=b.Get(key)});
            }
            foreach(var key in before.Fields.Keys.Union(after.Fields.Keys))if(before.Field(key)!=after.Field(key)||before.Fields.ContainsKey(key)!=after.Fields.ContainsKey(key))changes.Add(new JObject{["id"]="@world",["property"]=key,["before"]=before.Field(key),["after"]=after.Field(key)});
            return new JObject{["expected"]=before.Digest(),["actual"]=after.Digest(),["differences"]=changes};
        }
        PlanningCoordinator CreatePlanner(IEnumerable<PlanningRecord> records=null)
        {
            if(records!=null)return new PlanningCoordinator(catalog,replayRecords:records);
            if(runSubscriptionSession!=null)
            {
                return new PlanningCoordinator(catalog,runSubscriptionSession.CreateGenerator(),null,new DecisionBudget{MaxRequests=12,MaxInputTokens=12000,MaxOutputTokens=3000,MaxConcurrent=1,TimeoutSeconds=Math.Max(60,Math.Min(300,ModelDecisionDeadlineSeconds))});
            }
            if(UseSubscription)throw new InvalidOperationException("subscription_selection_not_locked");
            return new PlanningCoordinator(catalog);
        }
        void RegisterDecorations(World world,Transform root,string prefix,string parentId)
        {
            foreach(Transform child in root)
            {
                string id=prefix+"/"+Uri.EscapeDataString(child.name)+"#"+child.GetSiblingIndex();
                var binding=child.GetComponent<SemanticBinding>();string logicalParent=binding!=null&&world.Entities.ContainsKey(binding.EntityId)?binding.EntityId:parentId;
                var e=new Entity{Id=id,ParentId=logicalParent,Kind="decoration",X=child.position.x,Y=child.position.y,Z=child.position.z};e.Tags.Add("decoration");world.Add(e);
                RegisterDecorations(world,child,id,id);
            }
        }
        void SpawnNpc(Entity e,bool exactPose=false)
        {
            var feet=new Vector3((float)e.X,(float)e.Y,(float)e.Z);bool safe=exactPose?Navigation.ValidStoredSpawn(feet):Navigation.TrySpawn(feet,out feet);
            if(!safe){e.Set("navigation.unavailable",1);return;}
            var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.name=e.Id;go.transform.SetParent(simulationRoot,false);go.transform.position=feet;
            DestroyImmediate(go.GetComponent<Collider>());var mesh=go.GetComponent<MeshFilter>();var renderer=go.GetComponent<MeshRenderer>();
            var body=new GameObject("body",typeof(MeshFilter),typeof(MeshRenderer),typeof(CapsuleCollider));body.transform.SetParent(go.transform,false);body.transform.localPosition=Vector3.up*.86f;body.transform.localScale=new Vector3(.48f,.86f,.48f);body.GetComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;body.GetComponent<MeshRenderer>().sharedMaterial=npcMaterial;body.GetComponent<CapsuleCollider>().isTrigger=true;
            DestroyImmediate(mesh);DestroyImmediate(renderer);go.AddComponent<SemanticBinding>().EntityId=e.Id;go.AddComponent<NavMeshAgent>();var npc=go.AddComponent<SeededNpcController>();npc.Initialize();npc.EntityId=e.Id;npcs[e.Id]=npc;dynamicObjects.Add(go);
            e.X=feet.x;e.Y=feet.y;e.Z=feet.z;
        }
        void SpawnProxy(Entity e)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Training parameter prop · "+e.Id;go.transform.SetParent(simulationRoot,false);
            go.transform.position=new Vector3((float)e.X,(float)e.Y+.35f,(float)e.Z);go.transform.localScale=e.Kind=="luggage"?new Vector3(.3f,.4f,.2f):new Vector3(.45f,.7f,.3f);go.GetComponent<MeshRenderer>().sharedMaterial=toolMaterial;go.AddComponent<SemanticBinding>().EntityId=e.Id;markers[e.Id]=go;dynamicObjects.Add(go);
            // Proxies never silently carve source walking surfaces; agent crowd avoidance owns people.
            go.GetComponent<Collider>().isTrigger=true;
        }
        public void StopRun()
        {
            if(Running&&replayTicks!=null)Status="Replay aborted by user before verified completion.";
            Running=false;Player?.Capture(false);planner?.Dispose();planner=null;if(tape!=null){tape.Flush();tape.Dispose();tape=null;}
            runSubscriptionSession=null;runProviderSelection=null;
            pendingProviderCaptureRestore=false;
            DetailedControls?.ResetInputState();ResponseControls?.ResetInputState();
            foreach(var go in dynamicObjects)if(go!=null)DestroyImmediate(go);dynamicObjects.Clear();npcs.Clear();markers.Clear();replayTicks=null;replayNavigation=null;navigationObservations=null;
        }
        void Update()
        {
            PollEmergence();PollProviderControls();
            if(pendingProviderCaptureRestore&&Time.frameCount>=providerCaptureRestoreFrame)
            {pendingProviderCaptureRestore=false;if(!ProviderMenuOpen&&Running&&Application.isFocused)Player?.Capture(true);}
            if(Keyboard.current!=null&&Keyboard.current.f8Key.wasPressedThisFrame)SetProviderMenuOpen(!ProviderMenuOpen);
            if(ProviderMenuOpen)Player?.Capture(false);
            if(!Running||State==null)return;
            elapsed+=Mathf.Min(Time.deltaTime,.25f);
            while(elapsed>=State.StepSeconds){elapsed-=State.StepSeconds;Tick();}
            if(replayTicks==null)foreach(var entry in npcs)if(State.Entities.TryGetValue(entry.Key,out var e))entry.Value.Project(e);
            if(!ProviderMenuOpen)RefreshTarget();else targetId="";
            if(!ProviderMenuOpen&&!IsReplaying&&Player.Captured&&Keyboard.current!=null&&Keyboard.current.eKey.wasPressedThisFrame)Interact();
        }
        void RefreshTarget()
        {
            string previous=targetId;targetId="";
            if(Physics.Raycast(Player.View.transform.position,Player.View.transform.forward,out var hit,3,~0,QueryTriggerInteraction.Collide))
            {var bind=hit.collider.GetComponentInParent<SemanticBinding>();if(bind!=null)targetId=bind.EntityId=="@platform"?PlatformAt(hit.point):bind.EntityId;if(string.IsNullOrEmpty(targetId)&&WorldSnapshot!=null)targetId=WorldSnapshot.ResolveSourceObject(hit.collider);}
            if(previous!=targetId)selectedAction=0;
        }
        string PlatformAt(Vector3 point)
        {
            if(Mathf.Abs(point.y)>.5f)return "";string selected="";float distance=float.PositiveInfinity;
            foreach(var strip in Source.platforms)
            {
                var a=PlatformNavigation.V(strip.a);var b=PlatformNavigation.V(strip.b);var axis=(b-a).normalized;var delta=point-a;float along=Vector3.Dot(delta,axis);
                if(along<0||along>Vector3.Distance(a,b))continue;float lateral=Vector3.Cross(axis,delta).magnitude;
                if(lateral<strip.halfWidth&&lateral<distance){distance=lateral;selected="platform:"+strip.id;}
            }
            if(State!=null&&!string.IsNullOrEmpty(selected))
            {
                var local=State.Entities.Values.Where(e=>e.Kind=="platform-wait").OrderBy(e=>Vector3.Distance(point,new Vector3((float)e.X,(float)e.Y,(float)e.Z))).FirstOrDefault();
                if(local!=null&&Vector3.Distance(point,new Vector3((float)local.X,(float)local.Y,(float)local.Z))<=1.5f)return local.Id;
            }
            return selected;
        }
        public void ProjectDoorState(int car,bool open)
        {
            if(State==null)throw new InvalidOperationException("Start a run before projecting a door state");
            State.Entities["door:"+car].Set("open",open?1:0);if(State.Entities["door:"+car].Get("mech.profile")==1)State.Entities["door:"+car].Set("mech.part.leaf",open?1:0);ProjectDoors();
        }
        public IReadOnlyDictionary<string,SeededNpcController> Npcs=>npcs;
        public GameObject TrainVisual=>train;public GameObject PlatformVisual=>official;
        public Car04InteriorAccess InteriorAccess=>Navigation?.InteriorAccess;
        public void QueueCommand(string actor,string target,string operation)
        {if(State!=null)pendingCommands.Add(new JObject{["tick"]=State.Tick+1,["actor"]=actor,["target"]=target,["operation"]=operation});}
        void Interact()=>InteractFromView();
        public string CurrentTarget=>targetId;
        public string ResolveResponseSubject(string sourceOrEntityId)
        {
            return sourceOrEntityId!=null&&inferredSubjects.TryGetValue(sourceOrEntityId,out var subject)?subject:sourceOrEntityId;
        }
        public bool InteractFromView(string requestedOperation=null)
        {
            if(State==null||!Running)return false;RefreshTarget();
            if(string.IsNullOrEmpty(targetId)||!State.Entities.ContainsKey(targetId))return false;
            if(DetailedMode&&requestedOperation==null)
            {
                ResponseControls.SubjectId=ResolveResponseSubject(targetId);ResponseControls.ObservationId=null;feedback="Condition selected. Use Observe in the local response panel, or grip an individual part.";return true;
            }
            var choices=catalog.Available(State,"player",targetId);if(choices.Count==0)return false;
            var selected=requestedOperation==null?choices[selectedAction%choices.Count]:choices.FirstOrDefault(x=>x.Name==requestedOperation);
            if(selected==null)return false;QueueCommand("player",targetId,selected.Name);return true;
        }
        public void Tick()
        {
            if(!Running||State==null||planner==null)return;
            var observations=new JArray();JObject replay=null;
            navigationObservations=new JArray();
            var failedActors=new HashSet<string>(StringComparer.Ordinal);
            if(replayTicks!=null)
            {
                if(replayTicks.Count==0)
                {
                    try{planner.EnsureReplayConsumed();Running=false;Status="Replay complete; all recorded tick digests and planning records match.";}
                    catch(InvalidOperationException){Running=false;Status="Replay refused: unconsumed or pending planning evidence.";}
                    return;
                }
                replay=replayTicks.Dequeue();pendingCommands.Clear();foreach(var c in (JArray)replay["commands"])pendingCommands.Add((JObject)c.DeepClone());
                replayNavigation=new Queue<JArray>(((JArray)replay["navigation_observations"]).Select(x=>(JArray)x));
                foreach(var obs in (JArray)replay["motion_inputs"]){ApplyObservation((JObject)obs);if((double?)obs["goal_failed"]>0)failedActors.Add((string)obs["id"]);}
            }
            else
            {
                foreach(var e in State.Entities.Values.Where(x=>x.Has("npc")))
                {
                    Transform t=e.Id=="player"?Player.transform:npcs.TryGetValue(e.Id,out var npc)?npc.transform:null;if(t==null)continue;
                    var occlusion=new JObject();foreach(var candidate in CausalKernel.SemanticEntities(State).Where(x=>x.Id!=e.Id&&e.Distance(x)<=e.Get("sight.radius",12)))
                    {
                        var from=t.position+Vector3.up*1.5f;var to=new Vector3((float)candidate.X,(float)candidate.Y+1,(float)candidate.Z);var ray=to-from;
                        bool blocked=Physics.Raycast(from,ray.normalized,out var hit,Mathf.Max(0,ray.magnitude-.25f),~0,QueryTriggerInteraction.Ignore);
                        if(blocked){var binding=hit.collider.GetComponentInParent<SemanticBinding>();if(binding!=null&&binding.EntityId==candidate.Id)blocked=false;}
                        occlusion[candidate.Id]=blocked?1:0;
                    }
                    bool failed=e.Id!="player"&&npcs[e.Id].PendingFailure,arrived=e.Id!="player"&&npcs[e.Id].PendingArrival;
                    Vector3 velocity=e.Id=="player"?Player.GetComponent<CharacterController>().velocity:npcs[e.Id].Agent.velocity;velocity.y=0;
                    var obs=new JObject{["id"]=e.Id,["x"]=LocalTapeCodec.Float(t.position.x),["y"]=LocalTapeCodec.Float(t.position.y),["z"]=LocalTapeCodec.Float(t.position.z),["speed"]=LocalTapeCodec.Float(velocity.magnitude),["goal_failed"]=failed?1:0,["goal_complete"]=arrived?1:0,["occlusion"]=occlusion};
                    if(e.Id!="player"){npcs[e.Id].PendingFailure=false;npcs[e.Id].PendingArrival=false;}
                    if(failed)failedActors.Add(e.Id);
                    observations.Add(obs);ApplyObservation(obs);
                }
            }
            var commands=new JArray();
            foreach(var command in pendingCommands)
            {
                string actor=(string)command["actor"],target=(string)command["target"],operation=(string)command["operation"];
                bool accepted=false;string reason;
                try
                {
                    if((operation=="cordon"||operation=="clear_cordon")&&State.Entities.TryGetValue(target,out var surface)&&surface.Kind=="platform")reason="unsupported_global_platform_cordon; aim near a registered platform waiting point for local control";
                    else{var plan=ActionPlan.Capture(State,"manual:"+actor+":"+target+":"+operation,"local-ui-v1","local-ui-v1",OperationCatalog.Version,Navigation.Version,new[]{new ActionStep{ActorId=actor,TargetId=target,Operation=operation}});accepted=executor.TryExecute(State,plan,0,Navigation.Version,out reason);}
                }
                catch(ArgumentException){reason="rejected_unknown_entity_or_operation";}
                var recorded=(JObject)command.DeepClone();recorded["accepted"]=accepted;recorded["reason"]=reason;commands.Add(recorded);feedback=reason;
            }
            var mechanicalInputs=new JArray();var mechanicalOutcomes=new JArray();var responseInputs=new JArray();var responseOutcomes=new JArray();var responseObservations=new JArray();
            if(DetailedControls!=null&&DetailedMode)
            {
                var input=replay!=null?LocalTapeCodec.ToObject<MechanicalInput[]>(replay["mechanical_inputs"]??new JArray()):DetailedControls.CollectInputs();
                mechanicalInputs=(JArray)LocalTapeCodec.FromObject(input);mechanicalOutcomes=DetailedControls.Tick(input);ProjectDoors();
                var responseInput=replay!=null?LocalTapeCodec.ToObject<ResponseInput[]>(replay["response_inputs"]??new JArray()):ResponseControls.CollectInputs();
                responseInputs=(JArray)LocalTapeCodec.FromObject(responseInput);
                responseObservations=replay!=null?(JArray)(replay["response_observations"]??new JArray()).DeepClone():ResponseControls.CaptureObservations(responseInput);
                ResponseControls.ApplyObservations(responseObservations);responseOutcomes=ResponseControls.Tick(responseInput);
            }
            pendingCommands.Clear();kernel.Step(State);community.Step(State);ProjectDoors();ProjectCordons();
            var modelActions=new JArray();
            foreach(var e in State.Entities.Values.Where(x=>x.Has("npc")&&x.Id!="player"&&x.Get("health",1)>.05&&x.Get("mobility",1)>0))
            {
                string signature=(e.Get("awareness")>=.15?"aware":"calm")+":"+e.Get("goal.kind")+":"+(e.Get("goal.failed")>0);
                bool changed=!planningSignatures.TryGetValue(e.Id,out var previous)||previous!=signature;planningSignatures[e.Id]=signature;
                if((changed&&e.Get("awareness")>=.15)||failedActors.Contains(e.Id))
                {if(failedActors.Contains(e.Id))planner.NotifyFailure(e.Id,"respond-to-local-evidence");var plan=planner.Request(State,e.Id,"respond-to-local-evidence",Navigation.Version);if(plan!=null)ReplaceActivePlan(plan);}
            }
            planner.Poll(State,Navigation.Version);while(planner.TryDequeue(out var ready))ReplaceActivePlan(ready);
            foreach(var active in activePlans.ToArray())
            {
                if(active.Index>=active.Plan.Steps.Count){activePlans.Remove(active);continue;}
                if(active.Started)
                {
                    var step=active.Plan.Steps[active.Index];
                    if(executor.IsComplete(State,active.Plan,active.Index))
                    {
                        if(active.Plan.Steps[active.Index].Operation=="move_to"&&!executor.AcknowledgeMovementCompletion(State,active.Plan,active.Index,Navigation.Version,out var completionReason))
                        {activePlans.Remove(active);planner.NotifyFailure(active.Plan.Steps[active.Index].ActorId,"respond-to-local-evidence");modelActions.Add(new JObject{["plan"]=active.Plan.Id,["step"]=active.Index,["accepted"]=false,["reason"]="completion_refused:"+completionReason});continue;}
                        active.Index++;active.Started=false;if(active.Index>=active.Plan.Steps.Count){activePlans.Remove(active);continue;}
                    }
                    else
                    {
                        State.Entities.TryGetValue(step.ActorId,out var actor);State.Entities.TryGetValue(step.TargetId,out var target);
                        bool failed=failedActors.Contains(step.ActorId)||active.Plan.NavVersion!=Navigation.Version||actor==null||target==null||actor.Get("goal.external")<=0||actor.Get("health",1)<=.05||actor.Get("mobility",1)<=0;
                        if(!failed)
                        {
                            double distance=actor.Distance(target);if(distance<active.LastDistance-.2){active.LastDistance=distance;active.LastProgressTick=State.Tick;}
                            failed=State.Tick-active.LastProgressTick>40||!PlanCache.Validate(State,active.Plan,active.Plan.Goal,active.Plan.ModelVersion,active.Plan.PromptVersion,OperationCatalog.Version,Navigation.Version,out _);
                        }
                        if(failed){activePlans.Remove(active);planner.NotifyFailure(step.ActorId,"respond-to-local-evidence");modelActions.Add(new JObject{["plan"]=active.Plan.Id,["step"]=active.Index,["accepted"]=false,["reason"]="started_plan_invalidated_or_navigation_failed"});}
                        continue;
                    }
                }
                bool accepted=executor.TryExecute(State,active.Plan,active.Index,Navigation.Version,out var reason);modelActions.Add(new JObject{["plan"]=active.Plan.Id,["step"]=active.Index,["accepted"]=accepted,["reason"]=reason});
                if(!accepted){activePlans.Remove(active);continue;}active.Started=true;active.LastProgressTick=State.Tick;
                var started=active.Plan.Steps[active.Index];active.LastDistance=State.Entities[started.ActorId].Distance(State.Entities[started.TargetId]);
            }
            var responses=new JArray();while(planningCursor<planner.Records.Count)
            {
                var response=(JObject)LocalTapeCodec.FromObject(planner.Records[planningCursor++]);
                if(runProviderSelection!=null)response["provider_selection"]=runProviderSelection.DeepClone();
                responses.Add(response);
            }
            var transitions=new JArray();while(journalCursor<State.Journal.Count)transitions.Add(LocalTapeCodec.FromObject(State.Journal[journalCursor++]));
            string digest=State.Digest();
            if(replay!=null&&(string)replay["state_digest"]!=digest){Running=false;Status="Replay mismatch at tick "+State.Tick;Debug.LogError(Status);return;}
            bool detailedEqual=replay==null||!DetailedMode||(JToken.DeepEquals(replay["mechanical_inputs"],mechanicalInputs)&&JToken.DeepEquals(replay["mechanical_outcomes"],mechanicalOutcomes)&&JToken.DeepEquals(replay["response_inputs"],responseInputs)&&JToken.DeepEquals(replay["response_outcomes"],responseOutcomes)&&JToken.DeepEquals(replay["response_observations"],responseObservations));
            if(replay!=null&&(!detailedEqual||!JToken.DeepEquals(replay["commands"],commands)||!JToken.DeepEquals(replay["model_actions"],modelActions)||!JToken.DeepEquals(replay["model_responses"],responses)||!JToken.DeepEquals(replay["navigation_observations"],navigationObservations)||!JToken.DeepEquals(replay["transitions"],transitions)||(string)replay["nav_version"]!=Navigation.Version||replayNavigation.Count!=0))
            {LastReplayOutcomeFailure=new JObject{["detailed_inputs_outcomes_observations_equal"]=detailedEqual,["commands_equal"]=JToken.DeepEquals(replay["commands"],commands),["model_actions_equal"]=JToken.DeepEquals(replay["model_actions"],modelActions),["model_responses_equal"]=JToken.DeepEquals(replay["model_responses"],responses),["navigation_observations_equal"]=JToken.DeepEquals(replay["navigation_observations"],navigationObservations),["transitions_equal"]=JToken.DeepEquals(replay["transitions"],transitions),["expected_navigation_version"]=(string)replay["nav_version"],["actual_navigation_version"]=Navigation.Version,["remaining_navigation_observations"]=replayNavigation.Count,["expected_navigation_observations"]=replay["navigation_observations"].DeepClone(),["actual_navigation_observations"]=navigationObservations.DeepClone()};Running=false;Status="Replay outcome, transition order or navigation mismatch at tick "+State.Tick;Debug.LogError(Status);return;}
            Write(new JObject{["type"]="Tick",["tick"]=State.Tick,["commands"]=commands,["motion_inputs"]=observations,["mechanical_inputs"]=mechanicalInputs,["mechanical_outcomes"]=mechanicalOutcomes,["response_inputs"]=responseInputs,["response_outcomes"]=responseOutcomes,["response_observations"]=responseObservations,["navigation_observations"]=navigationObservations,["model_responses"]=responses,["model_actions"]=modelActions,["nav_version"]=Navigation.Version,["state_digest"]=digest,["transitions"]=transitions,["npc_positions"]=new JArray(npcs.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>new JObject{["id"]=x.Key,["position"]=Point(x.Value.transform.position),["path_status"]=x.Value.Agent.enabled?x.Value.Agent.pathStatus.ToString():"recorded-pose",["failure"]=x.Value.LastFailure}))});
            navigationObservations=null;
            ProjectIndicators();
        }
        void ReplaceActivePlan(ActionPlan plan)
        {
            var actors=new HashSet<string>(plan.Steps.Select(x=>x.ActorId),StringComparer.Ordinal);
            activePlans.RemoveAll(x=>x.Plan.Steps.Any(s=>actors.Contains(s.ActorId)));activePlans.Add(new ActivePlan{Plan=plan});
        }
        void ApplyObservation(JObject observation)
        {
            if(!State.Entities.TryGetValue((string)observation["id"],out var e))return;e.X=LocalTapeCodec.Number(observation["x"]);e.Y=LocalTapeCodec.Number(observation["y"]);e.Z=LocalTapeCodec.Number(observation["z"]);e.Set("speed",LocalTapeCodec.Number(observation["speed"]));
            e.Set("goal.failed",(double?)observation["goal_failed"]??0);e.Set("goal.complete",(double?)observation["goal_complete"]??0);e.Set("goal.active",(double?)observation["goal_active"]??e.Get("goal.active"));
            if(e.Get("goal.failed")>0)foreach(var target in e.State.Where(x=>x.Key.StartsWith("goal.target.",StringComparison.Ordinal)&&x.Value>0).Select(x=>x.Key.Substring(12)).ToArray())e.Set("unreachable."+target,1);
            if(observation["occlusion"] is JObject occ)foreach(var entry in occ)e.Set("occlusion."+entry.Key,(double)entry.Value);
            if(replayTicks!=null){if(e.Id=="player")Player.Pose(new Vector3((float)e.X,(float)e.Y,(float)e.Z),Player.transform.eulerAngles.y);else if(npcs.TryGetValue(e.Id,out var actor)){actor.Agent.enabled=false;actor.transform.position=new Vector3((float)e.X,(float)e.Y,(float)e.Z);}}
            if(e.Id=="player")return;
            if(State.Entities.TryGetValue("bag:"+e.Id.Substring(4),out var bag)){bag.X=e.X+.3;bag.Y=e.Y;bag.Z=e.Z;}
        }
        void ProjectDoors()
        {
            if(State==null)return;
            JArray recorded=null;if(replayNavigation!=null){if(replayNavigation.Count==0)throw new InvalidDataException("Missing recorded navigation observation");recorded=replayNavigation.Dequeue();}
            var observed=new JArray();
            foreach(var car in Source.train.cars)
            {
                var e=State.Entities["door:"+car.number];bool open=e.Get("open")>.9&&e.Get("jammed")<.5;
                foreach(var pair in doors[car.number])pair.leaf.localPosition=pair.open-PlatformNavigation.V(car.door.openDelta)*(1-(float)e.Get("open",1));
                bool linked;string reason;
                if(recorded!=null){var row=recorded.First(x=>(int)x["car"]==car.number);if((bool)row["open"]!=open)throw new InvalidDataException("Recorded door state differs");linked=(bool)row["linked"];reason=(string)row["reason"];Navigation.ProjectRecordedDoor(car,open&&linked);}
                else{Physics.SyncTransforms();linked=Navigation.TryDoor(car,open,out reason);}
                e.Set("navigation.reachable",open&&linked?1:0);observed.Add(new JObject{["car"]=car.number,["open"]=open,["linked"]=linked,["reason"]=reason});
                if(open&&!linked)feedback="Door "+car.number+" refuses navigation: "+reason;
            }
            if(InteriorAccess!=null){var cabinRows=InteriorAccess.Project(State,recorded);foreach(var row in cabinRows)observed.Add(row);}
            navigationObservations?.Add(observed);
        }
        void ProjectCordons(){if(State==null)return;foreach(var e in State.Entities.Values.Where(x=>x.Kind=="platform-wait"))Navigation.ProjectCordon(e);}
        void ProjectIndicators()
        {
            foreach(var e in State.Entities.Values)if(markers.TryGetValue(e.Id,out var proxy)&&proxy!=null)proxy.transform.position=new Vector3((float)e.X,(float)e.Y+(e.Kind=="luggage"?.2f:.35f),(float)e.Z);
            foreach(var e in CausalKernel.SemanticEntities(State))
            {
                string id="fire:"+e.Id;float value=(float)e.Get("fire");
                if(value>.01f&&!markers.ContainsKey(id))
                {var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=id;go.transform.SetParent(simulationRoot,false);DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<MeshRenderer>().sharedMaterial=fireMaterial;markers[id]=go;dynamicObjects.Add(go);}
                if(markers.TryGetValue(id,out var indicator)){indicator.SetActive(value>.01);indicator.transform.position=new Vector3((float)e.X,(float)e.Y+.6f,(float)e.Z);indicator.transform.localScale=Vector3.one*Mathf.Clamp(value,.15f,2);}
            }
        }
        public bool LoadReplay(string path)
        {
            try
            {
                LastReplayOutcomeFailure=null;var lines=File.ReadAllLines(path);var header=LocalTapeCodec.Parse(lines[0]);Population=(int)header["config"]["population"];
                if((int?)header["config"]["player_role"] is int role&&Enum.IsDefined(typeof(ResponseRole),role))PlayerRole=(ResponseRole)role;
                if((bool?)header["config"]["local_training_team"] is bool localTeam)AddLocalTrainingTeam=localTeam;
                if((string)header["engine"]!=World.EngineVersion||(string)header["rng"]!=Stable.RandomVersion)throw new InvalidDataException("Engine or RNG version differs");
                if((string)header["state_encoding"]!=WorldStateCodec.Encoding)throw new InvalidDataException("Exact state encoding version differs");
                Prepare();if(DetailedMode&&((bool?)header["config"]["detailed_mode"]!=true||(string)header["config"]["mechanics_version"]!=MechanicalInteractions.Version||(string)header["config"]["response_version"]!=ResponseWorkflows.Version))throw new InvalidDataException("Detailed replay contract differs");if((string)header["source_manifest_hash"]!=SourceFingerprint)throw new InvalidDataException("Full resource/source snapshot hash differs");
                if(WorldSnapshot!=null)WorldSnapshot.VerifyReplayFingerprint((string)header["full_world_fingerprint"]);
                // Recorded exact configuration is validated against the decoded state below; never substitute a rate=1 default.
                replayNavigation=null;if(!StartRun((string)header["seed"],false,true))return false;
                runProviderSelection=header["provider_selection"] is JObject routing?ProviderSelection.ValidateRecord(routing):null;
                if(header["initial_provider_selection"] is JObject initialRouting&&(!JToken.DeepEquals(ProviderSelection.ValidateRecord(initialRouting),runProviderSelection)))throw new InvalidDataException("Initial generation and NPC provider selections differ");
                if((string)header["nav_hash"]!=Navigation.Hash)throw new InvalidDataException("Navigation hash differs");
                // Initial positions/topology are exact recorded inputs; visual resources are checked by hash.
                var saved=WorldStateCodec.FromToken(header["initial_state"]);
                if(saved==null||saved.Digest()!=(string)header["initial_digest"])throw new InvalidDataException("Invalid initial state digest");
                ReplayConfiguration.Validate(header["config"],saved);
                if(header["emergence_generation"] is JObject generation)
                {var record=LocalTapeCodec.ToObject<EmergenceRecord>(generation);if(record==null||record.Seed!=saved.Seed||record.PostDigest!=saved.Digest()||(record.Disposition!="accepted"&&record.Disposition!="accepted_no_change")||string.IsNullOrEmpty(record.RawReply)||string.IsNullOrEmpty(record.RequestJson))throw new InvalidDataException("Initial generation record differs from recorded state");}
                State=saved;var savedDigest=State.Digest();
                var savedPlayer=State.Entities["player"];Player.Pose(new Vector3((float)savedPlayer.X,(float)savedPlayer.Y,(float)savedPlayer.Z),Player.transform.eulerAngles.y);
                InitializeDetailedWorld(savedState:true);DetailedControls?.ResetInputState();ResponseControls?.ResetInputState();
                if(State.Digest()!=savedDigest)throw new InvalidDataException("Detailed replay initialization changed its saved baseline");
                journalCursor=DetailedMode?State.Journal.Count:0;
                var ticks=lines.Skip(1).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>LocalTapeCodec.Parse(x)).Where(x=>(string)x["type"]=="Tick").ToArray();
                var records=ticks.SelectMany(x=>LocalTapeCodec.ToObject<List<PlanningRecord>>(x["model_responses"])).ToArray();planner.Dispose();planner=records.Length>0?CreatePlanner(records):new PlanningCoordinator(catalog);planningCursor=0;
                replayTicks=new Queue<JObject>(ticks);Status="Recorded-observation replay loaded; network disabled. Recorded motion/LOS inputs are projected, Unity physics is not resimulated.";return true;
            }
            catch(Exception e){StopRun();Status="Replay refused: "+e.Message;return false;}
        }
        void Write(JObject row){if(tape==null)return;tape.WriteLine(LocalTapeCodec.Encode(row).ToString(Formatting.None));tape.Flush();}
        public JObject Probe()
        {
            Prepare();var report=(JObject)Verification.DeepClone();
            if(State!=null)
            {
                report["entities"]=State.Entities.Count;report["semantic_entities"]=CausalKernel.SemanticEntities(State).Count;report["state_digest"]=State.Digest();report["npcs_spawned"]=npcs.Count;
                report["visual_parts"]=State.Entities.Values.Count(x=>x.Kind=="decoration");report["action_connected_entities"]=State.Entities.Values.Count(x=>catalog.Available(State,"player",x.Id).Any(op=>op.Name!="move_to"));
                report["unsupported_render_only"]=new JArray(State.Entities.Values.Where(x=>x.Kind=="decoration").Select(x=>x.Id));
                report["claim_limit"]="Render-part registration is complete for staged hierarchy; explicit semantic/action coverage is bounded, unknown render-only parts do not gain invented causal capabilities.";
            }
            var paths=new JArray();foreach(var c in Source.train.cars)
            {var p=new NavMeshPath();bool ok=NavMesh.CalculatePath(Player.transform.position,PlatformNavigation.V(c.door.outside),NavMesh.AllAreas,p)&&p.status==NavMeshPathStatus.PathComplete;paths.Add(new JObject{["car"]=c.number,["complete_platform_path"]=ok});}report["platform_paths"]=paths;return report;
        }
        public static JObject Point(Vector3 v)=>new JObject{["x"]=LocalTapeCodec.Float(v.x),["y"]=LocalTapeCodec.Float(v.y),["z"]=LocalTapeCodec.Float(v.z)};
        static JObject BoundsJson(Bounds b)=>new JObject{["min"]=Point(b.min),["max"]=Point(b.max),["size"]=Point(b.size)};
        static Bounds BoundsOf(GameObject go){var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return new Bounds();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
        public string SourceFingerprint
        {get{var baseHash=(string)JObject.Parse(Resources.Load<TextAsset>("SourceManifest").text)["runtime_fingerprint"];return WorldSnapshot==null?baseHash:Stable.Hash(baseHash+"|"+WorldSnapshot.Fingerprint);}}
        void GenerateInitialConditions()
        {
            if(Running||emergenceTask!=null||ProviderBusy||ActiveProviderLogin!=null&&ActiveProviderLogin.Pending)return;
            if(ProviderState?.Subscription.Selection!=null&&!ProviderState.Subscription.CloudEnabled){emergenceMessage="AI 계정 메뉴에서 선택한 모델의 구독 사용을 허용하세요.";return;}
            if(!ulong.TryParse(InitialSeed,NumberStyles.None,CultureInfo.InvariantCulture,out var seed)){emergenceMessage="Choose a valid unsigned seed first.";return;}
            IEmergenceProvider provider;
            SubscriptionConnection.SubscriptionSession selectedSession=null;
            if(UseSubscription)
            {
                if(subscription==null||!subscription.IsSelectionCurrent(subscriptionStatus)){emergenceMessage="Open AI connection, choose a model, enable AI and allow one initial whole-world generation; then refresh.";return;}
                selectedSession=subscription.LockSelection(subscriptionStatus);provider=selectedSession.CreateEmergenceProvider();
            }
            else
            {
                emergenceMessage="AI 계정 메뉴에서 모델을 선택하고 구독 사용을 허용하세요. AI를 끄면 로컬 시뮬레이션으로 플레이합니다.";return;
            }
            if(BeginInitialGeneration(provider,authorized:true))preparedSubscriptionSession=selectedSession;
        }
        public bool InitialGenerationInFlight=>emergenceTask!=null;
        public EmergenceRecord PreparedGenerationRecord=>preparedRecord;
        public void PollInitialGeneration()=>PollEmergence();
        public bool BeginInitialGeneration(IEmergenceProvider provider,bool authorized=false)
        {
            if(!authorized||Running||emergenceTask!=null)return false;
            preparedWorld=null;preparedRecord=null;preparedSubscriptionSession=null;
            if(!ulong.TryParse(InitialSeed,NumberStyles.None,CultureInfo.InvariantCulture,out var seed))return false;
            Prepare();preparedRecord=null;preparedSeed=seed;preparedPopulation=Population;preparedFingerprint=SourceFingerprint;
            preparedWorld=PlatformWorldFactory.Create(Resources.Load<TextAsset>("PlatformSource").text,Resources.Load<TextAsset>("SemanticCoverage").text,seed,Population);
            var staff=preparedWorld.Entities["player"];var player=Player.transform.position;staff.X=player.x;staff.Y=player.y;staff.Z=player.z;
            foreach(var actor in preparedWorld.Entities.Values.Where(x=>x.Has("npc")&&x.Id!="player"))
            {if(Navigation.TrySpawn(new Vector3((float)actor.X,(float)actor.Y,(float)actor.Z),out var feet)){actor.X=feet.x;actor.Y=feet.y;actor.Z=feet.z;}else actor.Set("navigation.unavailable",1);}
            foreach(var car in Source.train.cars)preparedWorld.Entities["door:"+car.number].Set("navigation.reachable",Navigation.DoorLinks.ContainsKey(car.number)?1:0);
            InteriorAccess?.Project(preparedWorld,null);
            if(WorldSnapshot!=null){WorldSnapshot.Apply(preparedWorld);ApplyStationInference(preparedWorld);WorldSnapshot.DistributeStationNpcs(preparedWorld);}
            else{RegisterDecorations(preparedWorld,official.transform,"visual:official","platform:p56");RegisterDecorations(preparedWorld,train.transform,"visual:ktx","train:ktx");}
            var previousState=State;try{State=preparedWorld;ProjectDoors();ProjectCordons();InitializeDetailedWorld();}finally{State=previousState;}
            emergenceCancellation?.Dispose();emergenceCancellation=new CancellationTokenSource();
            var composer=new EmergenceComposer(provider,new DecisionBudget{MaxRequests=1,MaxInputTokens=256000,MaxOutputTokens=512,MaxConcurrent=1,TimeoutSeconds=Math.Max(60,Math.Min(300,ModelDecisionDeadlineSeconds))}){IsSessionRunning=()=>Running};
            emergenceTask=composer.GenerateAndApplyAsync(preparedWorld,false,WorldCoverage.Parse(Resources.Load<TextAsset>("SemanticCoverage").text),emergenceCancellation.Token,authorized:true);
            emergenceMessage="Generating one bounded set of upstream world conditions...";return true;
        }
        static string SafeEmergenceRefusal(EmergenceRecord record)
        {
            if(record==null)return "failed";
            try
            {
                var code=(string)JObject.Parse(record.RawReply??"{}")["error"]?["code"];
                if(new[]{"initial_world_allowance_not_enabled","local_input_budget_exceeded","local_session_budget_exceeded","local_total_request_budget_exceeded","cloud_disabled","not_connected","model_does_not_match_browser_selection","session_generation_changed","provider_request_already_running"}.Contains(code))return code;
            }catch(JsonException){}catch(InvalidOperationException){}
            return record.Disposition??"failed";
        }
        void PollEmergence()
        {
            if(emergenceTask==null||!emergenceTask.IsCompleted)return;
            var record=emergenceTask.Status==TaskStatus.RanToCompletion?emergenceTask.Result:null;emergenceTask=null;
            bool accepted=record!=null&&(record.Disposition=="accepted"||record.Disposition=="accepted_no_change");
            bool basis=ulong.TryParse(InitialSeed,out var seed)&&seed==preparedSeed&&preparedPopulation==Population&&preparedFingerprint==SourceFingerprint&&
                (preparedSubscriptionSession==null||preparedSubscriptionSession.IsCurrent&&preparedSubscriptionSession.Selection.SameAs(subscriptionStatus?.Selection));
            preparedRecord=accepted&&basis?record:null;
            if(preparedRecord==null)preparedWorld=null;
            emergenceMessage=preparedRecord!=null?"World conditions prepared for seed "+preparedSeed+". Start the run; the model reply is saved with its initial state.":"Initial generation refused: "+SafeEmergenceRefusal(record)+(basis?"":"; seed or source changed");
        }
        void DrawConnection()
        {
            if(GUILayout.Button("AI 계정과 모델 · F8"))SetProviderMenuOpen(true);
            if(runProviderSelection!=null)GUILayout.Label("선택 모델 · "+(string)runProviderSelection["model_id"]);
            else if(subscriptionStatus?.Selection!=null)GUILayout.Label("선택 모델 · "+subscriptionStatus.Model);
            GUILayout.Label(ProviderMessage);
        }
        public void SetProviderMenuOpen(bool open)
        {
            if(open==ProviderMenuOpen)return;
            if(open)
            {
                if(providerPanel==null)
                {
                    try{providerPanel=ProviderSelectionPanel.Create(transform,ProviderMenuFont,this);}
                    catch(Exception){ProviderMessage="AI 메뉴의 화면 글꼴 연결을 확인해주세요.";return;}
                }
                pendingProviderCaptureRestore=false;restoreProviderCapture=Player!=null&&Player.Captured;Player?.Capture(false);
                restoreProviderDetailed=DetailedControls!=null&&DetailedControls.enabled;restoreProviderResponse=ResponseControls!=null&&ResponseControls.enabled;
                if(DetailedControls!=null)DetailedControls.enabled=false;if(ResponseControls!=null)ResponseControls.enabled=false;ProviderMenuOpen=true;
                if(ProviderState==null&&!ProviderBusy)RefreshProviders();
            }
            else{CancelProviderLogin();ProviderMenuOpen=false;if(DetailedControls!=null)DetailedControls.enabled=restoreProviderDetailed;if(ResponseControls!=null)ResponseControls.enabled=restoreProviderResponse;pendingProviderCaptureRestore=restoreProviderCapture&&Running;providerCaptureRestoreFrame=Time.frameCount+1;restoreProviderCapture=false;}
        }
        bool EnsureProviderControl()
        {
            if(providerControl!=null)return true;
            if(subscription==null&&!SubscriptionConnection.TryLoad(out subscription,out _)){ProviderMessage="AI 계정 연결을 준비한 뒤 다시 열어주세요.";return false;}
            providerControl=subscription.CreateControlClient(()=>CanChangeProvider);return true;
        }
        void BeginProviderControl(string action,Func<CancellationToken,Task<NativeProviderControlReply>> request,bool mutation)
        {
            if(ProviderBusy){ProviderMessage="요청을 처리하고 있습니다.";return;}
            if(mutation&&!CanChangeProvider){ProviderMessage="플레이를 멈춘 뒤 계정이나 모델을 변경할 수 있습니다.";return;}
            if(!EnsureProviderControl())return;
            providerControlCancellation?.Dispose();providerControlCancellation=new CancellationTokenSource();providerControlAction=action;
            try{providerControlTask=request(providerControlCancellation.Token);ProviderMessage=action=="refresh"?"AI 연결 상태를 확인하고 있습니다.":"요청을 처리하고 있습니다.";}
            catch(NativeProviderControlException error){ProviderMessage=ProviderFailure(error.Code);providerControlTask=null;}
            catch(Exception){ProviderMessage="AI 연결 요청을 완료하지 못했습니다. 다시 확인해주세요.";providerControlTask=null;}
        }
        public void RefreshProviders(){BeginProviderControl("refresh",ct=>providerControl.RefreshAsync(ct),false);}
        public void RefreshProviderModels(){BeginProviderControl("catalog",ct=>providerControl.RefreshCatalogAsync(ct),true);}
        public void StartProviderLogin(string providerId,string accountId=null,bool reconsent=false)
        {
            if(!ProviderMenuOpen)return;
            if(ActiveProviderLogin?.Pending==true){ProviderMessage="진행 중인 로그인을 마치거나 취소해주세요.";return;}
            suppressProviderBrowserLaunch=false;
            BeginProviderControl("login",ct=>providerControl.StartLoginAsync(providerId,accountId,reconsent,ct),true);
        }
        public void PollProviderLogin(){if(ActiveProviderLogin?.Pending==true){nextProviderPoll=Time.realtimeSinceStartup+Math.Max(.2,ActiveProviderLogin.PollAfterMs/1000.0);BeginProviderControl("poll",ct=>providerControl.PollLoginAsync(ActiveProviderLogin.FlowId,ct),true);}}
        public void CancelProviderLogin()
        {
            if(providerControlTask!=null&&providerControlAction=="login"&&ActiveProviderLogin?.Pending!=true)
            {suppressProviderBrowserLaunch=true;providerControlCancellation?.Cancel();providerControl?.Invalidate();ProviderMessage="로그인 요청을 취소했습니다.";return;}
            if(ActiveProviderLogin?.Pending!=true)return;
            cancelProviderFlowQueued=true;
            if(ProviderBusy){providerControlCancellation?.Cancel();ProviderMessage="진행 중인 로그인을 취소하고 있습니다.";return;}
            BeginProviderControl("cancel",ct=>providerControl.CancelLoginAsync(ActiveProviderLogin.FlowId,ct),true);
        }
        public void SelectProviderModel(NativeProviderModel model){BeginProviderControl("selection",ct=>providerControl.SelectModelAsync(model,ct),true);}
        public void SetProviderSubscriptionEnabled(bool enabled){BeginProviderControl("consent",ct=>providerControl.SetConsentAsync(enabled,null,null,ct),true);}
        public void ArmProviderInitialOnce(long inputUnits=192000,long outputTokens=2048){BeginProviderControl("initial",ct=>providerControl.SetConsentAsync(true,inputUnits,outputTokens,ct),true);}
        public void LogoutProviderAccount(string accountId){BeginProviderControl("logout",ct=>providerControl.LogoutAsync(accountId,ct),true);}
        void PollProviderControls()
        {
            if(providerControlTask!=null&&providerControlTask.IsCompleted)
            {
                var task=providerControlTask;providerControlTask=null;
                try
                {
                    var reply=task.GetAwaiter().GetResult();string previous=ProviderState?.Subscription.Selection?.Fingerprint;
                    ProviderState=reply.State;subscriptionStatus=reply.State.Subscription;UseSubscription=subscriptionStatus.CloudEnabled;
                    if(previous!=null&&previous!=subscriptionStatus.Selection?.Fingerprint){preparedWorld=null;preparedRecord=null;preparedSubscriptionSession=null;}
                    if(reply.Flow!=null)
                    {
                        ActiveProviderLogin=reply.Flow.PreserveStartHints(ActiveProviderLogin);nextProviderPoll=Time.realtimeSinceStartup+Math.Max(.2,reply.Flow.PollAfterMs/1000.0);
                        if(providerControlAction=="login"&&ActiveProviderLogin.Pending&&(!ProviderMenuOpen||suppressProviderBrowserLaunch))cancelProviderFlowQueued=true;
                        if(!ActiveProviderLogin.Pending||providerControlAction=="cancel")cancelProviderFlowQueued=false;
                        if(providerControlAction=="login"&&ProviderMenuOpen&&!suppressProviderBrowserLaunch)
                        {
                            string url=ActiveProviderLogin.AuthorizationUrl??ActiveProviderLogin.VerificationUri;
                            if(!string.IsNullOrEmpty(url))Application.OpenURL(url);
                        }
                    }
                    ProviderMessage=ActiveProviderLogin?.Pending==true?"브라우저에서 계정 로그인을 완료해주세요.":providerControlAction=="initial"?"초기 월드 생성 1회를 허용했습니다. 생성은 직접 시작하세요.":providerControlAction=="logout"&&reply.RemoteRevocationConfirmed==false?"이 게임에서는 연결을 해제했습니다. 제공자 계정의 앱 접근도 확인해주세요.":"AI 계정 선택을 반영했습니다.";
                }
                catch(NativeProviderControlException error)
                {ProviderMessage=ProviderFailure(error.Code);refreshAfterProviderConflict=error.Code=="provider_session_changed"||error.Code=="provider_selection_mismatch";}
                catch(Exception){ProviderMessage="AI 연결 요청을 완료하지 못했습니다. 다시 확인해주세요.";}
            }
            if(!ProviderBusy&&refreshAfterProviderConflict){refreshAfterProviderConflict=false;RefreshProviders();return;}
            if(!ProviderBusy&&CanChangeProvider&&cancelProviderFlowQueued)
            {
                if(ActiveProviderLogin?.Pending==true){BeginProviderControl("cancel",ct=>providerControl.CancelLoginAsync(ActiveProviderLogin.FlowId,ct),true);return;}
                cancelProviderFlowQueued=false;
            }
            if(!ProviderBusy&&CanChangeProvider&&ActiveProviderLogin?.Pending==true&&Time.realtimeSinceStartup>=nextProviderPoll)PollProviderLogin();
        }
        static string ProviderFailure(string code)
        {
            if(code=="native_controls_locked_for_play")return "플레이를 멈춘 뒤 계정이나 모델을 변경할 수 있습니다.";
            if(code=="provider_session_changed"||code=="provider_selection_mismatch"||code=="stale_native_control_reply")return "계정 상태가 변경되어 다시 확인하고 있습니다.";
            if(code=="subscription_usage_paused")return "이 계정의 구독 사용량을 확인해주세요.";
            if(code=="native_control_cancelled")return "AI 연결 요청을 취소했습니다.";
            if(code=="provider_not_available_for_subscription_mode")return "이 제공자의 구독 연결은 아직 사용할 수 없습니다.";
            return "AI 연결 요청을 완료하지 못했습니다. 다시 확인해주세요.";
        }
        void OnGUI()
        {
            if(ProviderMenuOpen)return;
            GUILayout.BeginArea(new Rect(16,16,460,Screen.height-32),GUI.skin.box);GUILayout.Label("CHOOGuard · Seeded platform / KTX simulator");DrawConnection();
            if(DetailedMode)
            {
                GUILayout.Label("INFERRED_TRAINING · local parts and response workflows; field procedures and physical calibration are unverified.");
                if(!Running)
                {
                    GUI.enabled=emergenceTask==null;GUILayout.Label("Your role for the next run");
                    var roles=(ResponseRole[])Enum.GetValues(typeof(ResponseRole));var names=new[]{"Passenger","Station staff","Train crew","Control","Facility technician","Fire responder","Medical responder"};
                    int selected=GUILayout.SelectionGrid(Array.IndexOf(roles,PlayerRole),names,2);
                    if(selected>=0&&selected<roles.Length&&roles[selected]!=PlayerRole){PlayerRole=roles[selected];preparedWorld=null;preparedRecord=null;emergenceMessage="Role changed. Generate conditions for this role or start its local baseline.";}
                    bool team=GUILayout.Toggle(AddLocalTrainingTeam,"Include local virtual response participants");if(team!=AddLocalTrainingTeam){AddLocalTrainingTeam=team;preparedWorld=null;preparedRecord=null;}GUI.enabled=true;
                }
            }
            if(!Running){GUI.enabled=emergenceTask==null;GUILayout.Label("Immutable run seed");var updatedSeed=GUILayout.TextField(InitialSeed);if(updatedSeed!=InitialSeed){InitialSeed=updatedSeed;preparedWorld=null;preparedRecord=null;emergenceMessage="Seed changed. Generate conditions for this seed or start its local baseline.";}if(GUILayout.Button("Generate a fresh random seed"))NewSeed();if(GUILayout.Button("Generate world conditions with AI"))GenerateInitialConditions();GUILayout.Label(emergenceMessage);if(GUILayout.Button("Start new seeded run"))StartRun(InitialSeed);GUI.enabled=true;}
            else{GUILayout.Label("Seed "+State.Seed+" · Tick "+State.Tick+" · "+State.Entities.Count+" entities");if(GUILayout.Button(Player.Captured?"Release pointer":"Capture pointer · WASD / Shift / E"))Player.Capture(!Player.Captured);if(GUILayout.Button("Stop and save tape"))StopRun();}
            GUILayout.Label(Status);GUILayout.Label("Source visual resource: 10-car KTX; operational compartments: "+(Source.train.cars.Length+(InteriorAccess==null?0:1))+". Equipment/risk values marked training-model.");
            if(State!=null){GUILayout.Label("Rain "+State.Field("rain").ToString("F2")+" · wind "+State.Field("wind").ToString("F1")+" · seismic "+State.Field("seismic").ToString("F2")+" · alerts "+State.Field("regional-alert").ToString("F2"));GUILayout.Label("Transitions "+State.Journal.Count+" · "+(planner?.GeneratorStatus??"stopped"));if(planner!=null)GUILayout.Label("Model requests "+planner.Budget.Requests+" · cache hits "+planner.Cache.Hits+" · tokens "+planner.Budget.MeasuredInputTokens+" in / "+planner.Budget.MeasuredOutputTokens+" out");}
            if(Running&&!string.IsNullOrEmpty(targetId)&&State.Entities.ContainsKey(targetId))
            {GUILayout.Label("Target: "+targetId);var actions=catalog.Available(State,"player",targetId);for(int i=0;i<actions.Count;i++)if(GUILayout.Button((selectedAction==i?"E · ":"")+actions[i].Label)){selectedAction=i;QueueCommand("player",targetId,actions[i].Name);}}
            if(!string.IsNullOrEmpty(feedback))GUILayout.Label(feedback);
            GUILayout.Label("Replay JSONL path");replayPath=GUILayout.TextField(replayPath);if(GUILayout.Button("Load recorded replay"))LoadReplay(replayPath);
            if(!string.IsNullOrEmpty(logPath))GUILayout.Label("Tape: "+logPath);
            GUILayout.EndArea();
        }
        void OnDestroy(){providerControlCancellation?.Cancel();providerControlCancellation?.Dispose();providerControl?.Invalidate();emergenceCancellation?.Cancel();emergenceCancellation?.Dispose();StopRun();Navigation?.Dispose();}
    }
}
