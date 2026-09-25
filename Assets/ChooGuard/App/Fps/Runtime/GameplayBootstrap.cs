using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using ChooGuard.Application.Gameplay;
using ChooGuard.Application.Gameplay.Content;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using ChooGuard.Domain.Gameplay;
using ChooGuard.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace ChooGuard.App.Fps.Runtime
{
    /// <summary>Explicit build-scene entry only. Nothing is injected into a modelling or legacy scene.</summary>
    public sealed class GameplayBootstrap : MonoBehaviour
    {
        public string WorldSceneName;
        public TextAsset SeedJson, TransitionJson;
        public TMP_FontAsset KoreanFont;
        public int NpcCount = 300;
        public WorldSession Session => current?.Session;
        public FirstPersonResponder Responder => current?.Player;
        public bool MenuVisible => current == null || current.Player.IsPaused;
        public string Message { get; private set; } = "진입 모드와 직무를 선택하세요. 본세계와 튜토리얼의 변경은 서로 섞이지 않습니다.";
        public string StatusText { get; private set; } = "CHOOGuard · 명시적 게임 진입";
        public string DetailText { get; private set; } = "실습 장면을 아직 시작하지 않았습니다.";
        public string FutureText { get; private set; } = "JEV · 연결 전 / 미래 없음";
        public string PromptText { get; private set; } = "실습실은 독립적인 합성 공간입니다.";
        public string SelectionText { get; private set; } = "직무: 정비 · 동작: 대상 기본값";

        private sealed class RuntimeWorld
        {
            public GameObject Root;
            public WorldSession Session;
            public FirstPersonResponder Player;
            public DetailedInteractionController Interaction;
            public GameplayNavigation Navigation;
            public NpcAgentRuntime Agents;
            public ActorVisualPool Visuals;
            public GameplayFutureRuntime Future;
            public GameplayInferenceClient Inference;
            public List<ActorNavigationBinding> Actors = new List<ActorNavigationBinding>();
            public RoleTrainingProgram Training;
            public RoleTrainingCheckpoint Checkpoint;
            public FpsEntityBinding LastTarget;
            public string Speech = "아직 들은 대사 없음";
            public Scene ModelScene;
        }
        private RuntimeWorld current, main;
        private GameplayContent content;
        private GameplayPersistenceStore storage;
        private Process broker;
        private IDisposable brokerLifetime;
        private string brokerUrl, capability, brokerState = "연결 준비 전";
        private ActorRole selectedRole = ActorRole.Maintenance;
        private ActionVerb? selectedVerb;
        private string selectedPoint, selectedTool;
        private bool entering, initialized;
        private float hudAt;
        private Scene loadedModel;

        private void Awake()
        {
            try
            {
                if (KoreanFont == null)
                    foreach (var candidate in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                        if (candidate.HasCharacter('한')) { KoreanFont = candidate; break; }
                if (KoreanFont == null) throw new InvalidOperationException("한국어 TMP 폰트 바인딩이 없습니다.");
                gameObject.AddComponent<GameplayHud>().Initialize(this, KoreanFont);
                if (SeedJson == null || TransitionJson == null) throw new InvalidOperationException("명시적 초기 세계/원자 전이 TextAsset이 필요합니다.");
                content = GameplayContentLoader.Load(SeedJson.text, TransitionJson.text);
                RuntimePackage.ConfigureRoot(UnityEngine.Application.isEditor
                    ? Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../workers/runtime"))
                    : Path.Combine(UnityEngine.Application.streamingAssetsPath, "ChooGuardRuntime"));
                Directory.CreateDirectory(UnityEngine.Application.persistentDataPath);
                storage = new GameplayPersistenceStore(Path.Combine(UnityEngine.Application.persistentDataPath, "gameplay.sqlite"));
                initialized = true;
                StartCoroutine(StartBroker());
            }
            catch (Exception error) { Block(error); }
        }

        private IEnumerator StartBroker()
        {
            brokerUrl = Environment.GetEnvironmentVariable("CHOOGUARD_GAMEPLAY_URL");
            capability = Environment.GetEnvironmentVariable("CHOOGUARD_GAMEPLAY_TOKEN");
            if (string.IsNullOrWhiteSpace(brokerUrl))
            {
                int port = 8788;
                if (int.TryParse(Environment.GetEnvironmentVariable("CHOOGUARD_GAMEPLAY_PORT"), out var configured) && configured > 0 && configured <= 65535) port = configured;
                brokerUrl = "http://127.0.0.1:" + port;
                var bytes = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
                capability = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                try
                {
                    broker = new Process { StartInfo = RuntimePackage.LoadConfigured().BrokerStartInfo(capability), EnableRaisingEvents = true };
                    broker.OutputDataReceived += DiscardProcessOutput; broker.ErrorDataReceived += DiscardProcessOutput;
                    if (!broker.Start()) throw new InvalidOperationException("패키지 추론 브로커 시작 실패");
                    brokerLifetime = WorkerProcessLifetime.Attach(broker);
                    broker.BeginOutputReadLine(); broker.BeginErrorReadLine();
                }
                catch (Exception error)
                {
                    brokerState = "사용 불가 · " + error.GetType().Name;
                    DisposeBroker();
                }
                if (broker == null) yield break;
            }
            if (!Uri.TryCreate(brokerUrl, UriKind.Absolute, out var endpoint) ||
                endpoint.Scheme != "https" && !(endpoint.Scheme == "http" && endpoint.IsLoopback) ||
                !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
            { brokerState = "사용 불가 · 안전한 명시적 추론 주소가 필요합니다."; DisposeBroker(); yield break; }
            if (!string.IsNullOrWhiteSpace(capability))
            {
                brokerState = "브로커 준비 확인 중";
                float deadline = Time.realtimeSinceStartup + 12;
                do
                {
                    using (var request = UnityWebRequest.Get(brokerUrl.TrimEnd('/') + "/status"))
                    {
                        request.SetRequestHeader("Authorization", "Bearer " + capability); request.timeout = 2;
                        yield return request.SendWebRequest();
                        if (request.result == UnityWebRequest.Result.Success)
                        { brokerState = "인증된 브로커 연결 · 모델 가용성은 각 실제 응답으로 확인"; yield break; }
                    }
                    yield return new WaitForSecondsRealtime(.4f);
                } while (Time.realtimeSinceStartup < deadline && (broker == null || !broker.HasExited));
            }
            brokerState = "JEV 사용 불가 · 인증/제공자 설정 확인 필요; 임의 후보 선택 안 함";
            DisposeBroker();
        }
        private static void DiscardProcessOutput(object sender, DataReceivedEventArgs args) { }

        public void BeginMain(bool modelScene)
        {
            if (!initialized || entering) { if (!initialized) Message = "저장소/콘텐츠 초기화 실패를 해결해야 시작할 수 있습니다."; return; }
            if (current?.Training != null) { ExitTutorial(); return; }
            if (main != null) { current = main; SetWorldActive(current, true); Resume(); return; }
            if (modelScene) StartCoroutine(OpenModel());
            else StartMainLaboratory();
        }
        private void StartMainLaboratory()
        {
            try
            {
                var initial = GameplayInitialWorld.Create(content.Seed, NpcCount, GameplayMode.RandomOperationsLab);
                var session = storage.Open(initial);
                main = BuildWorld(session, null, initial); current = main;
                session.ChangeRole(current.Interaction.ActorId, selectedRole);
                Message = "본세계 · 합성 조작 실습실. 수백 인물의 상태는 저장되며 JEV 미연결은 그대로 표시합니다.";
            }
            catch (Exception error) { Block(error); }
        }
        private IEnumerator OpenModel()
        {
            if (string.IsNullOrWhiteSpace(WorldSceneName)) { Message = "명시적 모델 장면 이름이 없습니다. 합성 실습실은 독립적으로 사용할 수 있습니다."; yield break; }
            entering = true;
            AsyncOperation load = null;
            try { load = SceneManager.LoadSceneAsync(WorldSceneName, LoadSceneMode.Additive); }
            catch (Exception error) { Message = "모델 장면 로드 실패: " + error.Message; }
            if (load != null)
            {
                yield return load;
                loadedModel = SceneManager.GetSceneByName(WorldSceneName);
                GameplayModelOverlay overlay = null;
                foreach (var root in loadedModel.GetRootGameObjects())
                {
                    overlay = root.GetComponentInChildren<GameplayModelOverlay>(true);
                    if (overlay != null) break;
                }
                if (overlay == null)
                {
                    Message = "모델 장면은 별도 로드했지만 의미 오버레이/실제 이동면 바인딩이 없습니다. 해당 역사 게임 진행은 미지원입니다. 합성 실습실을 선택하세요.";
                    yield return SceneManager.UnloadSceneAsync(loadedModel);
                }
                else
                {
                    try
                    {
                        overlay.Validate();
                        var authored = GameplayContentLoader.Load(overlay.SeedJson.text, overlay.TransitionJson.text);
                        var session = storage.Open(GameplayInitialWorld.Create(authored.Seed, NpcCount, GameplayMode.RandomOperationsLab));
                        main = BuildWorld(session, overlay); main.ModelScene = loadedModel; current = main;
                        Message = "명시적 모델 장면 + 독립 의미 오버레이를 연결했습니다. 안전 자료 공백은 미확인으로 남습니다.";
                    }
                    catch (Exception error) { Block(error); }
                }
            }
            if (main == null && loadedModel.IsValid() && loadedModel.isLoaded) yield return SceneManager.UnloadSceneAsync(loadedModel);
            entering = false;
        }

        public void BeginTutorial()
        {
            if (!initialized || entering) return;
            try
            {
                if (current?.Training != null) { DisposeWorld(current); current = null; }
                else if (current != null) { current.Player.Pause(); storage.Save(current.Session); SetWorldActive(current, false); current = null; }
                var fixture = RoleTrainingFixtures.Create(new StableId("tutorial-" + Guid.NewGuid().ToString("N")), selectedRole);
                var session = storage.Open(fixture.Snapshot, false);
                current = BuildWorld(session, null, fixture.Snapshot);
                current.Training = new RoleTrainingProgram();
                current.Training.Initialize(session, selectedRole, fixture.Bindings, current.Interaction);
                current.Checkpoint = current.Training.CaptureCheckpoint();
                Message = "독립 " + GameplayHud.RoleLabel(selectedRole) + " 튜토리얼 · 현재 작업만 제시하며 본세계 변경과 격리됩니다.";
            }
            catch (Exception error) { Block(error); }
        }

        private RuntimeWorld BuildWorld(WorldSession session, GameplayModelOverlay overlay, WorldSnapshot laboratoryLayout = null)
        {
            var world = new RuntimeWorld { Root = new GameObject("독립 Gameplay World"), Session = session,
                Inference = new GameplayInferenceClient(brokerUrl ?? "http://127.0.0.1:8788", capability,
                    configuredDialogueModel: Environment.GetEnvironmentVariable("CHOOGUARD_DIALOGUE_MODEL")) };
            world.Root.transform.SetParent(transform, false);
            var snapshot = session.Snapshot();
            try
            {
                if (overlay == null)
                {
                    var laboratory = world.Root.AddComponent<GameplayPracticeLaboratory>();
                    world.Navigation = laboratory.Build(snapshot, KoreanFont, laboratoryLayout); laboratory.Attach(session);
                }
                else
                {
                    world.Navigation = world.Root.AddComponent<GameplayNavigation>(); world.Navigation.BeginGeometry(overlay.GeometryRevision);
                    foreach (var floor in overlay.Floors)
                        if (!world.Navigation.RegisterFloor(floor, out var reason)) throw new InvalidOperationException(reason);
                    foreach (var portal in overlay.Portals)
                        if (!world.Navigation.RegisterPortal(portal, out var reason)) throw new InvalidOperationException(reason);
                    foreach (var door in overlay.Doors)
                        if (!world.Navigation.RegisterDoor(door, out var reason)) throw new InvalidOperationException(reason);
                    var bound = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var entity in overlay.Entities)
                        if (!bound.Add(entity.EntityId)) throw new InvalidOperationException("모델 의미 ID 중복: " + entity.EntityId);
                    foreach (var entity in snapshot.Entities.Values)
                        if (entity.Kind != EntityKind.Actor && !bound.Contains(entity.Id.Value)) throw new InvalidOperationException("모델 실물 바인딩 누락: " + entity.Id.Value);
                }
                WorldEntity human = null;
                foreach (var actor in snapshot.Actors.Values) if (actor.IsHuman) human = snapshot.Entities[actor.Id];
                if (human == null) throw new InvalidOperationException("세계에 실습자가 없습니다.");
                var route = new List<Vector3>();
                var start = Vector(human.Position);
                if (!world.Navigation.TryRoute(start, start, route, out var spawnReason)) throw new InvalidOperationException("실습자 초기 위치가 실제 이동면 밖입니다: " + spawnReason);
                var player = new GameObject("FPS 실습자"); player.transform.SetParent(world.Root.transform, false); player.transform.position = route[0];
                world.Player = player.AddComponent<FirstPersonResponder>(); world.Player.PlayerCamera.depth = 100;
                world.Player.PlayerCamera.farClipPlane = 120;
                world.Interaction = player.AddComponent<DetailedInteractionController>(); world.Interaction.Bind(session, human.Id, world.Player);
                var hand = new GameObject("실물 손 목표"); hand.transform.SetParent(world.Player.PlayerCamera.transform, false); hand.transform.localPosition = new Vector3(.18f, -.18f, .55f);
                world.Interaction.HandAnchor = hand.transform;
                world.Navigation.PlayerObstacle = player.GetComponent<CharacterController>();
                world.Visuals = world.Root.AddComponent<ActorVisualPool>(); world.Visuals.KoreanFont = KoreanFont; world.Visuals.ViewCamera = world.Player.PlayerCamera;
                world.Visuals.MaximumVisible = 100; world.Visuals.Templates = new[] { VisualTemplate(world.Root.transform) };
                world.Visuals.LabelDistance = 3;
                foreach (var actor in snapshot.Actors.Values)
                {
                    if (actor.IsHuman) continue;
                    var entity = snapshot.Entities[actor.Id]; var position = Vector(entity.Position);
                    if (!world.Navigation.TryRoute(position, position, route, out var reason)) throw new InvalidOperationException("NPC 초기 위치가 실제 이동면 밖입니다: " + actor.Id.Value + " " + reason);
                    var root = new GameObject(entity.Label); root.transform.SetParent(world.Root.transform, false); root.transform.position = route[0];
                    var motion = root.AddComponent<ActorNavigationBinding>(); motion.ActorId = actor.Id.Value; motion.Navigation = world.Navigation;
                    world.Actors.Add(motion); session.SetActorPose(actor.Id, Point(root.transform.position));
                    var actorBinding = root.AddComponent<FpsEntityBinding>(); actorBinding.Configure(actor.Id.Value, root.GetComponent<CharacterController>());
                    actorBinding.Label = entity.Label; actorBinding.HandReach = 1.4f; actorBinding.Verb = ActionVerb.RequestHelp;
                    if (!world.Visuals.RegisterActor(motion, entity.Label, GameplayHud.RoleLabel(actor.Role), actor.Plan?.Reason ?? "주변 관측 준비", out reason)) throw new InvalidOperationException(reason);
                }
                world.Interaction.RefreshBindings();
                world.Agents = world.Root.AddComponent<NpcAgentRuntime>(); world.Agents.Initialize(session, world.Inference, world.Navigation, world.Actors);
                world.Agents.RegisterHumanObserver(human.Id, player.transform);
                world.Agents.Speech += speech =>
                {
                    if (world.Session.TryGetEntity(speech.ActorId, out var speaker) && CanHear(world, speaker))
                        world.Speech = speaker.Label + ": " + speech.Text + " [" + speech.Source + "]";
                };
                world.Future = world.Root.AddComponent<GameplayFutureRuntime>();
                var definitions = overlay == null ? content.Transitions : GameplayContentLoader.Load(overlay.SeedJson.text, overlay.TransitionJson.text).Transitions;
                world.Future.Initialize(session, new TransitionKernel(definitions), world.Inference);
                world.Player.Pause(); Time.timeScale = 0;
                return world;
            }
            catch { DisposeWorld(world); throw; }
        }
        private static GameObject VisualTemplate(Transform parent)
        {
            var template = new GameObject("합성 인물 표현 원형"); template.transform.SetParent(parent, false);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule); mesh.transform.SetParent(template.transform, false);
            mesh.transform.localPosition = Vector3.up * .85f; mesh.transform.localScale = new Vector3(.5f, .85f, .5f);
            mesh.GetComponent<Collider>().enabled = false; template.SetActive(false); return template;
        }

        private void Update()
        {
            if (current == null) return;
            bool paused = current.Player.IsPaused; Time.timeScale = paused ? 0 : 1;
            if (!paused)
            {
                try
                {
                    var target = current.Player.CurrentTargetCollider;
                    if (target != null) current.LastTarget = target.GetComponentInParent<FpsEntityBinding>();
                    var humanId = current.Interaction.ActorId;
                    current.Session.SetActorPose(humanId, Point(current.Player.transform.position));
                    current.Session.AdvanceTime(new SimTick(checked(current.Session.Tick.Microseconds + (long)(Math.Min(Time.unscaledDeltaTime, .1f) * 1000000))));
                    current.Agents.TickAgents(Mathf.Min(Time.unscaledDeltaTime, .1f)); current.Future.TickFuture(); current.Training?.Tick();
                }
                catch (Exception error) { current.Player.Pause(); Block(error); }
            }
            if (Time.unscaledTime < hudAt) return; hudAt = Time.unscaledTime + .2f;
            if (current.Session.TryGetActor(current.Interaction.ActorId, out var playerState)) selectedRole = playerState.Role;
            StatusText = (current.Training == null ? "본세계 · " : "독립 튜토리얼 · ") + GameplayHud.RoleLabel(selectedRole) + " · " + (paused ? "일시정지" : "진행") + " · revision " + current.Session.Revision;
            DetailText = "손: " + (current.Interaction.HeldEntity?.Label ?? "빈손") + " · " + current.Interaction.ActivePrompt + "\n" +
                (current.Training == null ? "자율 인물 " + current.Agents.RegisteredActorCount + "명 / 표현 " + current.Visuals.VisibleCount + "명 · " + current.Navigation.Status :
                current.Training.CurrentLabel + " · " + current.Training.BlockedReason) + "\n" + current.Speech;
            if (current.LastTarget != null && current.Session.TryGetActor(new StableId(current.LastTarget.EntityId), out var observed) && !observed.IsHuman)
            {
                string goal = observed.Plan?.Reason ?? "관측 후 목표 검토 중";
                DetailText += "\n" + current.LastTarget.Label + " · 목표: " + goal + " · " + current.Agents.GetStatus(observed.Id);
                current.Visuals.SetPresentation(observed.Id.Value, current.LastTarget.Label, GameplayHud.RoleLabel(observed.Role), goal);
            }
            FutureText = "JEV " + brokerState + "\n현재 상태: " + current.Future.Status + "\n조건부 미래 " + current.Future.Branches.Count + "개 · 실제 완료 증거 아님";
            for (int i = 0; i < Math.Min(2, current.Future.Branches.Count); i++)
            {
                var branch = current.Future.Branches[i];
                FutureText += "\n" + branch.Status + " · " + (branch.Steps.Count == 0 ? branch.Reason : branch.Steps[branch.Steps.Count - 1].Candidate.Summary);
            }
            PromptText = current.Player.CurrentPrompt + "\n" + current.Interaction.LastFeedback + "\nWASD 이동 · E 접수 · 좌클릭/Space 작업 · Enter 확인 · R/F 회전 · 휠/PageUp/Down 깊이 · Esc 메뉴";
            SelectionText = "직무: " + GameplayHud.RoleLabel(selectedRole) + " · 동작: " + (selectedVerb.HasValue ? GameplayHud.VerbLabel(selectedVerb.Value) : "대상 기본값") + " · 공구: " + (selectedTool ?? "대상 지정") + " · 작업점: " + (selectedPoint ?? current.LastTarget?.WorkPointId ?? "미선택");
        }
        public void Resume() { if (current != null && current.Player.Resume()) Time.timeScale = 1; }
        public void SelectRole(ActorRole role)
        {
            selectedRole = role;
            if (current?.Training != null) { BeginTutorial(); return; }
            if (current != null) { current.Interaction.ClearSelection(); current.Session.ChangeRole(current.Interaction.ActorId, role); }
            Message = GameplayHud.RoleLabel(role) + " 선택 · 실제 철도 작업권한을 부여하지 않습니다.";
        }
        public void SelectVerb(ActionVerb verb)
        {
            if (verb == ActionVerb.Cancel) { ClearSelection(); Message = "작업 취소 · 이미 반영된 부분 결과/보유 상태는 유지됩니다."; return; }
            if (verb == ActionVerb.Wait) { current?.Player.Pause(); Message = "일시정지 · 계속을 누를 때까지 세계 시간을 멈춥니다."; return; }
            if (verb == ActionVerb.MoveTo) { ClearSelection(); Message = "사람은 WASD로 실제 이동합니다. 목적지 순간 이동은 제공하지 않습니다."; Resume(); return; }
            selectedVerb = verb; current?.Interaction.SelectVerb(verb);
        }
        public void SelectHeldTool()
        {
            selectedTool = current?.Interaction.HeldEntity?.EntityId;
            if (selectedTool == null) { Message = "먼저 실제 공구를 집으세요. 멀리 있는 도구를 소환하지 않습니다."; return; }
            current.Interaction.SelectTool(selectedTool);
        }
        public void CycleWorkPoint()
        {
            var binding = current?.LastTarget; if (binding == null || binding.WorkPoints.Length == 0) { Message = "바라본 대상에 저작된 작업점이 없습니다."; return; }
            int next = 0; for (int i = 0; i < binding.WorkPoints.Length; i++) if (binding.WorkPoints[i].Id == selectedPoint) next = (i + 1) % binding.WorkPoints.Length;
            selectedPoint = binding.WorkPoints[next].Id; current.Interaction.SelectWorkPoint(selectedPoint);
        }
        public void CycleRecipient()
        {
            var binding = current?.Interaction.HeldEntity ?? current?.LastTarget; if (binding == null) return;
            var candidates = new List<FpsEntityBinding>();
            foreach (var target in FpsEntityBinding.Live)
                if (target != binding && target != null && !string.IsNullOrEmpty(target.EntityId) && Vector3.Distance(target.transform.position, current.Player.transform.position) <= 3 &&
                    current.Session.TryGetEntity(new StableId(target.EntityId), out var entity) &&
                    (entity.Kind == EntityKind.Actor || entity.Kind == EntityKind.Container || entity.Kind == EntityKind.Surface)) candidates.Add(target);
            candidates.Sort((a, b) => StringComparer.Ordinal.Compare(a.EntityId, b.EntityId));
            if (candidates.Count == 0) { Message = "실제 가까이에 수신 인물/보관 용기가 없습니다."; return; }
            int next = 0;
            for (int i = 0; i < candidates.Count; i++) if (candidates[i].EntityId == binding.RecipientId) next = i + 1;
            binding.RecipientId = next == candidates.Count ? null : candidates[next].EntityId;
            Message = "수신/보관 대상: " + (binding.RecipientId ?? "지정 없음 · 실제 바닥에 놓기") + " · 동의/도착/지지는 별도로 확인합니다.";
        }
        public void ClearSelection() { selectedVerb = null; selectedPoint = selectedTool = null; current?.Interaction.ClearSelection(); }
        public void Save()
        {
            if (current == null) return;
            try { current.Interaction.ClearSelection(); storage.Save(current.Session); Message = "SQLite 의미 상태·위치 저장 완료. 재시작 시 물리 회전/속도·진행 중 접촉은 복원/완료 처리하지 않습니다."; }
            catch (Exception error) { Block(error); }
        }
        public void CaptureTutorial()
        {
            if (current?.Training == null) { Message = "튜토리얼에서만 복원점을 만들 수 있습니다."; return; }
            try { current.Checkpoint = current.Training.CaptureCheckpoint(); Message = "독립 튜토리얼 복원점을 기록했습니다."; }
            catch (Exception error) { Block(error); }
        }
        public void RewindTutorial()
        {
            if (current?.Training == null || current.Checkpoint == null) return;
            try
            {
                current.Training.RestoreCheckpoint(current.Checkpoint);
                foreach (var actor in current.Actors)
                    if (current.Session.TryGetEntity(new StableId(actor.ActorId), out var entity) &&
                        !actor.RestoreCheckpointPosition(Vector(entity.Position), out var reason)) throw new InvalidOperationException("실습 인물 물리 복원 실패: " + reason);
                ClearSelection(); Message = "튜토리얼을 복원했습니다. 본세계는 바뀌지 않았습니다.";
            }
            catch (Exception error) { Block(error); }
        }
        public void ExitTutorial()
        {
            if (current?.Training == null) return;
            DisposeWorld(current); current = main;
            if (main != null) { SetWorldActive(main, true); main.Player.Pause(); }
            Time.timeScale = 0; Message = "튜토리얼 종료 · 실습 결과를 본세계에 복사하지 않았습니다.";
        }
        public void ReturnToEntry()
        {
            if (current?.Training != null) ExitTutorial();
            if (current != null) { Save(); SetWorldActive(current, false); current = null; }
            Time.timeScale = 0; StatusText = "CHOOGuard · 진입 선택"; Message = "본세계 세션은 보존됩니다. 다시 선택하여 계속하세요.";
        }
        private void Block(Exception error) { Message = "진행 차단 · " + error.Message; UnityEngine.Debug.LogException(error, this); }
        private static void SetWorldActive(RuntimeWorld world, bool active)
        {
            if (world.ModelScene.IsValid() && world.ModelScene.isLoaded)
                foreach (var root in world.ModelScene.GetRootGameObjects()) root.SetActive(active);
            world.Root.SetActive(active);
        }
        private static void DisposeWorld(RuntimeWorld world)
        {
            if (world == null) return;
            SetWorldActive(world, false); world.Training?.Dispose(); world.Inference?.Dispose(); world.Session?.Dispose(); Destroy(world.Root);
        }
        private void DisposeBroker()
        {
            var owned = broker; var lifetime = brokerLifetime;
            broker = null; brokerLifetime = null;
            if (!WorkerProcessLifetime.ShutdownOwnedBroker(owned, lifetime))
                UnityEngine.Debug.LogWarning("Gameplay broker graceful shutdown did not complete; the account writer lock may require manual recovery.", this);
        }
        private void OnDestroy()
        {
            Time.timeScale = 1;
            try
            {
                if (current != null && current != main) DisposeWorld(current); DisposeWorld(main);
                storage?.Dispose();
            }
            finally { DisposeBroker(); }
        }
        private static bool CanHear(RuntimeWorld world, WorldEntity speaker)
        {
            var source = Vector(speaker.Position) + Vector3.up * 1.3f;
            var listener = world.Player.PlayerCamera.transform.position;
            if (Vector3.Distance(source, listener) > world.Agents.HearingDistance) return false;
            if (!Physics.Linecast(listener, source, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            var binding = hit.collider.GetComponentInParent<FpsEntityBinding>();
            return binding != null && binding.EntityId == speaker.Id.Value;
        }
        private static Vector3 Vector(WorldPoint p) => new Vector3(p.X, p.Y, p.Z);
        private static WorldPoint Point(Vector3 p) => new WorldPoint(p.x, p.y, p.z);
    }

}
