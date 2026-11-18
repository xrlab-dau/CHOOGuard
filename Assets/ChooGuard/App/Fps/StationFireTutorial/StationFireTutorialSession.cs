using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Shell;
using ChooGuard.Seeded;
using ChooGuard.Seeded.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SimulationWorld = ChooGuard.Seeded.World;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    public enum StationFireStep { Warning, Report, Extinguish, Release, ResultReport, Specialist, Complete }

    /// <summary>One authored offline lesson. Progress observes real equipment and people; it does not execute their actions.</summary>
    public sealed partial class StationFireTutorialSession : MonoBehaviour, IDetailedManipulationHost
    {
        public TMP_FontAsset KoreanFont;
        public GameObject ExtinguisherPrefab, StaffPrefab, SpecialistPrefab;
        public GameObject[] PassengerPrefabs;
        public RuntimeAnimatorController PersonController;
        public AudioClip FireLoop, SprayLoop, ConfirmSound;
        public AudioClip[] GuideVoice, ReplyVoice;
        public static StationFireTutorialSession Current { get; private set; }
        public SimulationWorld State { get; private set; }
        public Camera View => Player != null ? Player.View : null;
        public Transform Actor => Player != null ? Player.transform : null;
        public Transform Root => transform;
        public bool Running => Ready && Step != StationFireStep.Complete;
        public bool Captured => Player != null && Player.Captured;
        public bool IsReplaying => false;
        public StationWorldAdapter WorldSnapshot { get; private set; }
        public string ResolveResponseSubject(string id) => id;
        public SeededFpsController Player { get; private set; }
        public DetailedManipulationController Controls { get; private set; }
        public MechanicalInteractions Mechanics { get; private set; }
        public StationFireStep Step { get; private set; }
        public bool Ready { get; private set; }
        public string Failure { get; private set; } = "";
        public IReadOnlyList<StationFireActor> Passengers => passengers;
        public StationFireActor StaffA { get; private set; }
        public StationFireActor StaffB { get; private set; }
        public StationFireActor Specialist { get; private set; }
        public Transform FireTarget { get; private set; }
        public Transform ExtinguisherBody { get; private set; }
        public Vector3 StartFeet { get; private set; }
        public Vector3 ExtinguisherAccessPoint=>ExtinguisherPickupPoint;
        public Vector3 ReceiptPoint { get; private set; }
        public Vector3 ControlPoint { get; private set; }
        public Vector3 OnwardPoint { get; private set; }
        public bool SafetyCorrection { get; private set; }
        public string SafetyMessage { get; private set; } = "";
        public bool StaffReceived { get; private set; }
        public bool ReportReceived { get; private set; }
        public bool SpecialistReceived { get; private set; }
        public int PhysicalActionCount { get; private set; }
        public readonly List<string> Evidence = new List<string>();
        readonly List<StationFireActor> passengers = new List<StationFireActor>();
        OfflineStationStartupScope startup;
        Scene stationScene;
        Vector3 anchor;
        Quaternion layoutRotation;
        float elapsed;
        NavMeshPath safetyPath;
        readonly Vector3[] safetyCorners=new Vector3[128];
        bool paused = true, externalInteraction;
        string feedback = "역사 장면을 준비하고 있습니다";

        void Awake() { safetyPath=new NavMeshPath();Current = this; Time.timeScale = 1; }
        IEnumerator Start()
        {
            CreateView();
            UpdateCoaching(false);
            startup = new OfflineStationStartupScope();
            SceneManager.sceneLoaded += IsolateSourceScene;
            var operation = SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Additive);
            if (operation == null) { Fail("역사 씬을 불러올 수 없습니다"); yield break; }
            yield return operation;
            SceneManager.sceneLoaded -= IsolateSourceScene;
            bool initialized=false;
            try
            {
                var source = stationScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlatformSeededSimulator>(true)).First();
                WorldSnapshot = source.WorldSnapshot;
                SceneManager.SetActiveScene(stationScene);
                if (WorldSnapshot == null) throw new InvalidOperationException("현재 역사 형상 연결이 없습니다");
                WorldSnapshot.Prepare(transform); WorldSnapshot.ActivateGeometry(); WorldSnapshot.BakeStationNavigation();
                ResolveIncidentLayout();
                InitializeLesson();
                initialized=true;
            }
            catch (Exception error) { Fail(error.Message); Debug.LogException(error); }
            finally { startup.Dispose(); startup = null; }
            if(!initialized)yield break;
            yield return PrepareLessonPresentation();
            Ready=true;Step=StationFireStep.Warning;feedback="클릭하여 시작하세요";
            SetPaused(true);UpdateCoaching(true);
        }
        void IsolateSourceScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneFlow.StationScene) return;
            stationScene = scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(behaviour is FirstPersonResponder || behaviour is SeededFpsController)
                    {var body=behaviour.GetComponent<CharacterController>();if(body!=null)body.enabled=false;}
                    if (behaviour is PlatformSeededSimulator || behaviour is FirstPersonResponder ||
                        behaviour is SeededFpsController ||
                        behaviour.GetType().Name == "StationPlayPanel" || behaviour.GetType().Name == "FirstPersonInteractionHud" ||
                        behaviour.GetType().Name == "EventSystem" || behaviour.GetType().Name == "InputSystemUIInputModule") behaviour.enabled = false;
                }
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            }
        }
        Vector3 At(float x, float z) => anchor + layoutRotation * new Vector3(x, 0, z);
        Vector3 WalkPoint(float x, float z)
        {
            var desired = At(x, z);
            if (!NavMesh.SamplePosition(desired, out var hit, .65f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - anchor.y) > .3f)
                throw new InvalidOperationException("현재 현장에 연결된 보행면이 없습니다");
            return hit.position;
        }
        void InitializeLesson()
        {
            State = new SimulationWorld { Seed = 31820261030UL, StepSeconds = .05 };
            var playerObject = new GameObject("화재 교육 역무원"); playerObject.transform.SetParent(transform, false);
            Player = playerObject.AddComponent<SeededFpsController>(); Player.Pose(StartFeet, layoutRotation.eulerAngles.y);
            var obstacle=playerObject.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Capsule;
            obstacle.radius=playerObject.GetComponent<CharacterController>().radius+.05f;obstacle.height=1.72f;obstacle.center=Vector3.up*.86f;obstacle.carving=false;
            Player.View.clearFlags = CameraClearFlags.Skybox; Player.Capture(false);
            AddEntity("player", "staff", Player.transform.position, "staff");
            Mechanics = new MechanicalInteractions(State);
            Controls = gameObject.AddComponent<DetailedManipulationController>(); Controls.ShowHints = false; Controls.Initialize(this, Mechanics, false);
            InitializeEquipment();
            Player.MovementConstraint=movement=>arrivalTransition?Vector3.zero:Controls.ConstrainActorMovement(movement);
            Player.PointerConsumed=()=>RadioInputBlocked||Controls.ConsumesPointerMotion;
            Controls.InputBlocked=()=>RadioInputBlocked;
            InitializeRadio();
            InitializeGroundGuide();
            InitializeIncidentActors();
            // Source input/simulation is isolated above. The batcher skips live
            // controllers, the real gear and burning chair; colliders stay original.
            gameObject.AddComponent<FixedStationBatching>().Prepare(stationScene,ExtinguisherBody,incidentBench,Player.transform.position);
            Physics.SyncTransforms();
        }
        StationFireActor CreateActor(string id, GameObject prefab, Vector3 feet)
        {
            var go = new GameObject(id); go.SetActive(false); go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(feet, layoutRotation * Quaternion.Euler(0, 180, 0));
            var actor = go.AddComponent<StationFireActor>(); actor.Initialize(id, prefab, PersonController); go.SetActive(true); return actor;
        }
        Entity AddEntity(string id, string kind, Vector3 point, params string[] tags)
        {
            var entity = new Entity { Id = id, Kind = kind, X = point.x, Y = point.y, Z = point.z };
            foreach (var tag in tags) entity.Tags.Add(tag); State.Add(entity); return entity;
        }
        void Update()
        {
            if (!Ready) return;
            TickDialogue();
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetPaused(true);
            if(Captured&&!RadioOpen&&keyboard!=null&&keyboard.enterKey.wasPressedThisFrame)AdvanceDialogue();
            if (!Captured) { if (!paused) SetPaused(true); UpdateCoaching(false); return; }
            if (paused) SetPaused(false);
            if(!externalInteraction)ReadRadioInput();
            if (!externalInteraction && !RadioInputBlocked && keyboard != null && keyboard.eKey.wasPressedThisFrame) InteractAimed();
            var p = State.Entities["player"]; p.X = Player.transform.position.x; p.Y = Player.transform.position.y; p.Z = Player.transform.position.z;
            RefreshSafetyGate();
            elapsed += Mathf.Min(Time.deltaTime, .1f);
            while (elapsed >= State.StepSeconds)
            {
                Controls.Tick(Controls.CollectInputs()); State.Tick++; elapsed -= (float)State.StepSeconds;
            }
            ObserveIncident(); ProjectEquipment(); UpdateCoaching(false);
        }
        public void SetVerificationInput(bool enabled)
        { externalInteraction = enabled; Player.SetExternalInputMode(enabled); Controls.SetExternalInputMode(enabled); }
        public void ResumeLesson() { if (!Ready || Step == StationFireStep.Complete) return; Player.Capture(true); Controls.GuardCaptureClick(); SetPaused(false); }
        public void SetPaused(bool value)
        {
            if(value&&!paused)reviewPage=0;
            paused = value; Time.timeScale = value ? 0 : 1;
            if (value)
            {
                CloseRadio(false); Player?.Capture(false); Controls?.ResetInputState();
                if (Mechanics != null) ReleaseEquipmentPressure();
            }
            UpdatePauseView(value);
        }
        public bool SafePassageOpen()
        {
            if(Player==null||FireTarget==null)return false;
            if(!NavMesh.SamplePosition(Player.transform.position,out var own,.65f,NavMesh.AllAreas)||Mathf.Abs(own.position.y-Player.transform.position.y)>.35f)return false;
            return RouteKeepsClearOfFire(own.position,OnwardPoint);
        }
        bool RouteKeepsClearOfFire(Vector3 from,Vector3 to)
        {
            if(!NavMesh.CalculatePath(from,to,NavMesh.AllAreas,safetyPath)||safetyPath.status!=NavMeshPathStatus.PathComplete)return false;
            int count=safetyPath.GetCornersNonAlloc(safetyCorners);
            if(count>=safetyCorners.Length)return false; // Never treat a truncated route as a safe escape.
            for(int i=1;i<count;i++)
            {
                var a=safetyCorners[i-1];var b=safetyCorners[i];a.y=b.y=FireTarget.position.y;
                var segment=b-a;float f=segment.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(FireTarget.position-a,segment)/segment.sqrMagnitude):0;
                if(Vector3.Distance(a+segment*f,FireTarget.position)<1.15f)return false;
            }
            return true;
        }
        void RefreshSafetyGate()
        {
            bool passage=SafePassageOpen();
            var delta=Player.transform.position-FireTarget.position;delta.y=0;
            bool distance=delta.magnitude>=1.25f; // MODEL interaction margin, not a staff SOP distance.
            bool safe=warningsIssued&&ReportReceived&&passage&&distance&&!WithdrawalRequested&&!arrivalTransition;
            State.Entities[ExtinguisherId].Set("mech.discharge-enabled",safe?1:0);
            SafetyCorrection=Step==StationFireStep.Extinguish&&!safe;
            SafetyMessage=!warningsIssued?"가까운 승객에게 위험과 안전한 대피 방향을 알려 주세요":
                !ReportReceived?"Q 무전으로 위치를 보고하고 신고·현장 지원을 요청하세요":
                !passage?"분사 중지 · 화점을 지나지 않는 퇴로를 확보하세요":
                !distance?"불에 너무 가깝습니다. 안전한 쪽으로 물러서세요":
                WithdrawalRequested?"초기소화를 중단했습니다. 안전한 쪽에서 상황을 전달하세요":"";
        }
        void SetStep(StationFireStep value) { Step = value; UpdateCoaching(true); Record("step:" + value); }
        void Record(string evidence) { Evidence.Add(State == null ? evidence : State.Tick + ":" + evidence); }
        void Fail(string reason) { Failure = reason; feedback = reason; Ready = false; UpdateCoaching(false); }
        public void RestartLesson() { Time.timeScale = 1; SceneManager.LoadScene("StationFireTutorial", LoadSceneMode.Single); }
        public void ExitLesson() { Time.timeScale = 1; SceneFlow.ToTutorialMenu(); }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= IsolateSourceScene; startup?.Dispose();
            if (WorldSnapshot != null) WorldSnapshot.Dispose();
            Time.timeScale = 1; if (Current == this) Current = null;
        }
    }
}
