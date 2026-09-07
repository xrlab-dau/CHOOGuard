using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChooGuard.Foundation.Demo
{
    public sealed class DemoGameController : MonoBehaviour
    {
        [SerializeField] private TextAsset scenario;
        [SerializeField] private DemoPlayerController player;
        [SerializeField] private Transform spawn;
        [SerializeField] private DemoInteractable[] targets;
        [SerializeField] private Transform assemblyPoint;
        [SerializeField] private Light incidentCue;
        public const float DefaultAssemblyRadius = 2;
        [SerializeField, Min(.25f)] private float assemblyRadius = DefaultAssemblyRadius;
        [SerializeField] private Transform assemblyGuide;
        [SerializeField] private TextAsset drillCatalog;
        [SerializeField] private DemoInteractable[] sharedTargets = new DemoInteractable[0];
        [SerializeField] private DemoEvacuee[] evacuees = new DemoEvacuee[0];
        [SerializeField] private DemoWalkGraph walkGraph;
        [SerializeField] private DemoWorldGuide worldGuide;
        private DemoDrillCatalog drills;
        private int drillIndex;
        public DemoExercise Exercise { get; private set; }
        public IReadOnlyList<DemoEvacuee> Evacuees { get { return evacuees; } }
        public bool AllEvacueesArrived { get { return evacuees.Length > 0 && evacuees.All(x => x.State == EvacueeState.AssemblyWait && DemoEvacuee.Horizontal(x.transform.position,x.WaitingSlot)<.45f && Mathf.Abs(x.transform.position.y-assemblyPoint.position.y)<.5f && DemoEvacuee.Horizontal(x.transform.position,assemblyPoint.position)<DefaultAssemblyRadius); } }
        public DemoInteractable CurrentTarget { get { var id = Exercise != null ? Exercise.Current : Flow != null && Flow.Phase == DemoPhase.Incident ? Flow.SelectedRole.TargetAnchorId : null; return targets.Concat(sharedTargets).FirstOrDefault(x=>x.AnchorId==id); } }
        public void ConfigureExercise(TextAsset catalog,DemoInteractable[] extras,DemoEvacuee[] crowd,DemoWalkGraph graph,DemoWorldGuide guide)
        { drillCatalog=catalog;sharedTargets=extras;evacuees=crowd;walkGraph=graph;worldGuide=guide; }
        private bool ContinuousMode {get {var world=GetComponent<StationWorldController>();return world!=null&&!world.LegacyOverride;}}
        private string configurationError;
        private string message;
        private bool paused;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private Font runtimeFont;
        private Vector2 panelScroll;

        public DemoFlow Flow { get; private set; }
        public DemoPlayerController Player { get { return player; } }
        public IReadOnlyList<DemoInteractable> Targets { get { return targets ?? new DemoInteractable[0]; } }
        public Transform AssemblyPoint { get { return assemblyPoint; } }
        public bool IsConfigured { get { return Flow != null; } }
        public bool IsPaused { get { return paused; } }

        public void Configure(TextAsset profile, DemoPlayerController actor, Transform spawnPoint,
            DemoInteractable[] interactables, Transform assembly)
        {
            scenario = profile;
            player = actor;
            spawn = spawnPoint;
            targets = interactables;
            assemblyPoint = assembly;
            if (Application.isPlaying) Initialize();
        }

        public void ConfigureAssemblyGuide(Transform guide)
        {
            assemblyGuide = guide;
            UpdateGuidance();
        }

        private void UpdateGuidance()
        {
            if (assemblyGuide != null) assemblyGuide.gameObject.SetActive(Flow != null && Flow.Phase == DemoPhase.ReachAssembly && (Exercise == null || Exercise.Current == "assembly-register" || Exercise.ReadyForAssembly));
        }

        public void ConfigureIncidentCue(Light cue)
        {
            incidentCue = cue;
            if (cue != null) cue.enabled = false;
        }

        private void Start()
        {
            if (Flow == null) Initialize();
        }

        private void Initialize()
        {
            try
            {
                if (scenario == null || player == null || player.ViewCamera == null || spawn == null || assemblyPoint == null)
                    throw new InvalidOperationException("Scenario, player camera, spawn and assembly references are required.");
                var profile = JsonUtility.FromJson<ScenarioProfile>(scenario.text);
                var nextFlow = new DemoFlow(profile);
                if (targets == null || targets.Length != 5 || targets.Any(item => item == null) ||
                    targets.Select(item => item.AnchorId).Distinct(StringComparer.Ordinal).Count() != 5 ||
                    nextFlow.Session.AvailableRoles.Any(role => !targets.Any(item => item.AnchorId == role.TargetAnchorId)))
                    throw new InvalidOperationException("Five distinct scenario-matching interactable anchors are required.");
                if (drillCatalog != null)
                {
                    drills=JsonUtility.FromJson<DemoDrillCatalog>(drillCatalog.text);
                    if(drills==null || drills.disclaimer!=ScenarioValidation.RequiredDisclaimer || drills.drills==null || drills.drills.Length!=3 || evacuees.Length!=6 || walkGraph==null || worldGuide==null)
                        throw new InvalidOperationException("Three provisional drills, six evacuees and world navigation references are required.");
                    ValidateCrowd(evacuees,assemblyPoint.position);
                    var ids=targets.Concat(sharedTargets).Select(x=>x.AnchorId).ToArray();
                    if(ids.Distinct().Count()!=ids.Length)throw new InvalidOperationException("Scene anchors must be unique.");
                    foreach(var drill in drills.drills)
                    {
                        new DemoExercise(drill,targets[0].AnchorId);
                        if(drill.steps.Any(id=>!ids.Contains(id)))throw new InvalidOperationException("Missing drill object reference.");
                    }
                }
                Flow = nextFlow;
                configurationError = null;
                RestartDemo();
            }
            catch (Exception exception)
            {
                Flow = null;
                configurationError = exception.Message;
                if (player != null) player.SetControlEnabled(false);
                Debug.LogError("Foundation demo configuration: " + configurationError, this);
            }
        }

        public static void ValidateCrowd(DemoEvacuee[] crowd,Vector3 assembly)
        {
            if(crowd==null||crowd.Length!=6||crowd.Any(x=>x==null)||crowd.Distinct().Count()!=6||
                crowd.Count(x=>x.GroupId=="west")!=3||crowd.Count(x=>x.GroupId=="east")!=3||
                crowd.Select(x=>x.WaitingSlot).Distinct().Count()!=6||crowd.Any(x=>Mathf.Abs(x.WaitingSlot.y-assembly.y)>.5f||DemoEvacuee.Horizontal(x.WaitingSlot,assembly)>DefaultAssemblyRadius-.1f))
                throw new InvalidOperationException("Six distinct west/east evacuees and distinct assembly slots are required.");
        }
        public void SelectDrill(int index)
        {
            if(Flow.Phase!=DemoPhase.RoleSelection||drills==null||index<0||index>=drills.drills.Length)throw new InvalidOperationException("Select an available drill before selecting a role.");
            drillIndex=index;
        }

        public void SelectRole(string roleId)
        {
            if (Flow == null) return;
            Flow.SelectRole(roleId);
            if(drills!=null)Exercise=new DemoExercise(drills.drills[drillIndex],Flow.SelectedRole.TargetAnchorId);
            panelScroll = Vector2.zero;
        }

        public void BeginIncident()
        {
            if (Flow == null) return;
            Flow.BeginIncident();
            paused = false;
            if(walkGraph!=null)walkGraph.Rebuild();
            player.SetControlEnabled(true);
            message = "가상 설비 이상 신호가 발생했습니다. 표시된 임시 목표물을 찾아 E를 누르세요.";
        }

        public bool TryInteract()
        {
            if(Flow==null || paused || (Flow.Phase!=DemoPhase.Incident && Flow.Phase!=DemoPhase.ReachAssembly))return false;
            DemoInteractable target;
            if(!player.TryGetTarget(out target) || target!=CurrentTarget)return false;
            if(Exercise!=null)
            {
                if(target.AnchorId=="assembly-register" && !AllEvacueesArrived)return false;
                if(Exercise.Index==0 && !Flow.TryInteract(target.AnchorId))return false;
                if(!Exercise.TryAdvance(target.AnchorId))return false;
                if(target.AnchorId.StartsWith("rally-"))
                    foreach(var npc in evacuees.Where(x=>x.GroupId==target.AnchorId.Substring(6)))npc.Recruit();
            }
            else if(!targets.Contains(target)||!Flow.TryInteract(target.AnchorId))return false;
            target.ApplyInteraction();
            var hands=player.GetComponentInChildren<DemoHands>(true);if(hands!=null)hands.Pulse();
            UpdateGuidance();return true;
        }

        public bool TryCompleteAssembly()
        {
            if (Flow == null || Flow.Phase != DemoPhase.ReachAssembly || paused) return false;
            if(Exercise!=null && (!Exercise.ReadyForAssembly || !AllEvacueesArrived))return false;
            var offset = player.transform.position - assemblyPoint.position;
            if (Mathf.Abs(offset.y) > 2 || new Vector2(offset.x, offset.z).sqrMagnitude > assemblyRadius * assemblyRadius)
                return false;
            if (!Flow.TryReachAssembly()) return false;
            UpdateGuidance();
            player.SetControlEnabled(false);
            if (incidentCue != null) incidentCue.enabled = false;
            panelScroll = Vector2.zero;
            return true;
        }

        public void RestartDemo()
        {
            if (Flow == null) return;
            Flow.Restart();
            Exercise=null;
            if(worldGuide!=null)worldGuide.Hide();
            foreach(var npc in evacuees)npc.ResetActor();
            foreach(var extra in sharedTargets)extra.ResetVisual();
            UpdateGuidance();
            foreach (var target in targets) target.ResetVisual();
            player.SetControlEnabled(false);
            player.Teleport(spawn);
            if (incidentCue != null) incidentCue.enabled = false;
            message = string.Empty;
            paused = false;
            panelScroll = Vector2.zero;
        }

        public void PauseDemo()
        {
            if (Flow == null || (Flow.Phase != DemoPhase.Incident && Flow.Phase != DemoPhase.ReachAssembly)) return;
            paused = true;
            player.SetControlEnabled(false);
            if (incidentCue != null) incidentCue.enabled = false;
        }

        public void ResumeDemo()
        {
            if(Flow==null||!paused||!player.InputAvailable)return;
            paused=false;player.SetControlEnabled(true);
        }

        private void Update()
        {
            if(ContinuousMode)return;
            if (Flow == null) return;
            var playing = Flow.Phase == DemoPhase.Incident || Flow.Phase == DemoPhase.ReachAssembly;
            if (!playing) return;
            if (!player.InputAvailable)
            {
                PauseDemo();
                return;
            }
            if (DemoInput.EscapePressed)
            {
                paused = !paused;
                player.SetControlEnabled(!paused);
                return;
            }
            if (incidentCue != null)
            {
                incidentCue.enabled = !paused;
                incidentCue.intensity = 1.1f + Mathf.Sin(Time.time * 4) * .4f;
            }
            if (paused) return;
            TickExercise(Time.deltaTime);
            if (DemoInput.InteractPressed) TryInteract();
            TryCompleteAssembly();
            if (player.transform.position.y < -8)
            {
                player.Teleport(spawn);
                message = "맵 밖으로 벗어나 시작 지점으로 복귀했습니다.";
            }
        }

        public void TickExercise(float dt)
        {
            if(Flow==null||paused||Exercise==null||(Flow.Phase!=DemoPhase.Incident&&Flow.Phase!=DemoPhase.ReachAssembly))return;
            foreach(var npc in evacuees)npc.Tick(dt,player.transform,walkGraph,assemblyPoint.position);
            var target=CurrentTarget;
            var waiting=target!=null && target.AnchorId=="assembly-register"&&!AllEvacueesArrived;
            var destination=target==null||waiting?assemblyPoint.position:target.InteractionPoint;
            var approach=target==null||waiting?assemblyPoint.position:target.transform.position-target.transform.forward*1.3f;
            var id=target==null||waiting?"assembly":target.AnchorId;
            worldGuide.Tick(dt,walkGraph,player,id,destination,approach,waiting?"인솔  "+evacuees.Count(x=>x.State==EvacueeState.AssemblyWait)+" / 6":target==null?"집결":"[E] "+target.DisplayName);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if(ContinuousMode)return;
            if (hasFocus || Flow == null || (Flow.Phase != DemoPhase.Incident && Flow.Phase != DemoPhase.ReachAssembly)) return;
            PauseDemo();
        }

        private void OnDisable()
        {
            if (player != null && Application.isPlaying) player.SetControlEnabled(false);
        }

        private void PrepareStyles()
        {
            if (bodyStyle != null) return;
            // Use a locally installed Korean system font; no font download or remote asset service.
            var availableFonts = Font.GetOSInstalledFontNames();
            var candidates = new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR" };
            var chosen = candidates.FirstOrDefault(candidate => availableFonts.Contains(candidate));
            if (chosen != null) runtimeFont = Font.CreateDynamicFontFromOSFont(chosen, 18);
            bodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 18 };
            titleStyle = new GUIStyle(bodyStyle) { fontSize = 28, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(bodyStyle) { fontSize = 15 };
            if (runtimeFont != null)
            {
                bodyStyle.font = runtimeFont;
                titleStyle.font = runtimeFont;
                smallStyle.font = runtimeFont;
            }
        }

        private void OnGUI()
        {
            if(ContinuousMode)return;
            PrepareStyles();
            var oldMatrix = GUI.matrix;
            var oldFont = GUI.skin.font;
            var oldButtonSize = GUI.skin.button.fontSize;
            var scale = ScaleForScreen();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            if (runtimeFont != null) GUI.skin.font = runtimeFont;
            GUI.skin.button.fontSize = 18;
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            try
            {

                GUI.Label(new Rect(24, 19, width - 48, 34), "CHOOguard   |   KORAIL 검증 전 예시", smallStyle);
                if (Flow == null)
                {
                    GUI.Label(new Rect(50, 100, width - 100, 200), "데모 구성을 확인하세요.\n" + configurationError, bodyStyle);
                    return;
                }
                var playing = Flow.Phase == DemoPhase.Incident || Flow.Phase == DemoPhase.ReachAssembly;
                if (playing) DrawGameHud(width, height);
                if (!playing || paused || !player.InputAvailable) DrawPanel(width, height);
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.skin.font = oldFont;
                GUI.skin.button.fontSize = oldButtonSize;
            }
        }

        private static void DrawCard(Rect rect, GUIContent unused)
        {
            var color = GUI.color;
            GUI.color = new Color(.025f, .038f, .055f, .97f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(.15f, .65f, .76f, 1);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), Texture2D.whiteTexture);
            GUI.color = color;
        }

        private void DrawGameHud(float width, float height)
        {
            // Gameplay has no quest panel or screen-space objective labels. Instructions live in the map.
            GUI.Label(new Rect(width/2-8,height/2-15,25,30),"+",titleStyle);
            if(Exercise!=null)
                GUI.Label(new Rect(24,height-38,width-48,28),
                    (Exercise.Index+" / "+Exercise.Steps.Count)+"     "+evacuees.Count(x=>x.State==EvacueeState.AssemblyWait)+" / 6",smallStyle);
        }

        private static float ScaleForScreen() { return Mathf.Max(.65f, Mathf.Min(Screen.width / 1100f, Screen.height / 760f)); }

        private void DrawPanel(float width, float height)
        {
            var panelWidth = Mathf.Min(840, width - 32);
            var panel = new Rect((width - panelWidth) / 2, 82, panelWidth, height - 104);
            DrawCard(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 26, panel.y + 22, panel.width - 52, panel.height - 44));
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            if (!player.InputAvailable)
            {
                GUILayout.Label("키보드 / 마우스 입력을 확인하세요", titleStyle);
                GUILayout.Label(player.InputError ?? "Legacy Input Manager 또는 설치된 Input System과 키보드·마우스가 필요합니다. 학교 PC에서 Player Settings의 Active Input Handling 설정과 장치 연결을 확인하세요.", bodyStyle);
                if (GUILayout.Button("입력 장치 다시 확인", GUILayout.Height(50))) DemoInput.Retry();
                if (GUILayout.Button("역할 선택으로 초기화", GUILayout.Height(50))) RestartDemo();
            }
            else if (paused)
            {
                GUILayout.Label("일시정지", titleStyle);
                GUILayout.Label("데스크톱 싱글플레이 • 외부 데이터와 네트워크 연결 없이 실행", bodyStyle);
                if (GUILayout.Button("계속하기", GUILayout.Height(50)))
                {
                    ResumeDemo();
                }
                if (GUILayout.Button("역할 선택으로 다시 시작", GUILayout.Height(50))) RestartDemo();
            }
            else if (Flow.Phase == DemoPhase.RoleSelection)
            {
                GUILayout.Label("CHOOguard  |  FIRST RESPONSE", titleStyle);
                GUILayout.Label("가상 역사에서 여러 장치를 조작하고 대기 중인 6명을 인솔합니다. 훈련과 첫 역할을 선택하세요.", bodyStyle);
                GUILayout.Space(12);
                if(drills!=null)
                {
                    for(var i=0;i<drills.drills.Length;i++)
                        if(GUILayout.Button((i==drillIndex?"● ":"○ ")+drills.drills[i].title,GUILayout.Height(38)))SelectDrill(i);
                    GUILayout.Space(12);
                }
                foreach (var role in Flow.Session.AvailableRoles)
                    if (GUILayout.Button(role.TemporaryDisplayName.Replace(" · 임시 역할", "") + " — " + TargetName(role.TargetAnchorId), GUILayout.Height(44))) SelectRole(role.RoleId);
            }
            else if (Flow.Phase == DemoPhase.Briefing)
            {
                GUILayout.Label(Flow.SelectedRole.TemporaryDisplayName + " 브리핑", titleStyle);
                GUILayout.Label(Exercise!=null ? drills.drills[drillIndex].title+"\n첫 조작: "+TargetName(Flow.SelectedRole.TargetAnchorId) : Flow.SelectedRole.Text, bodyStyle);
                GUILayout.Space(16);
                GUILayout.Label("WASD 이동 · 마우스 시점 · E 상호작용 · Esc 일시정지\n맵에 표시되는 청록색 경로와 표식을 따라갑니다.\n인솔 지점에서 E를 누르면 동일한 대피자들이 따라옵니다. 집결 지점에서 6명이 도착할 때까지 기다린 뒤 인원 확인 단말을 조작하세요.", bodyStyle);
                GUILayout.Space(18);
                GUILayout.Label("이 순서는 게임 조작 확인용 예시이며 실제 철도 대응 절차나 교육 평가 기준이 아닙니다.", smallStyle);
                if (GUILayout.Button("브리핑 확인 · 가상 상황 시작", GUILayout.Height(54))) BeginIncident();
                if (GUILayout.Button("다른 역할 선택", GUILayout.Height(44))) RestartDemo();
            }
            else if (Flow.Phase == DemoPhase.Results)
            {
                GUILayout.Label("데모 플레이 완료", titleStyle);
                GUILayout.Label(Flow.SelectedRole.TemporaryDisplayName + "으로 " + TargetName(Flow.SelectedRole.TargetAnchorId) +
                    (Exercise!=null?"를 포함한 "+Exercise.Steps.Count+"개 상호작용과 대피자 6명 인솔을 마쳤습니다.":"를 조작하고 집결 지점에 도착했습니다."), bodyStyle);
                GUILayout.Space(16);
                GUILayout.Label(Flow.Session.Feedback.Text, bodyStyle);
                GUILayout.Label("가상 팀 4명: 알림 수신 표시. 다른 역할의 업무 수행이나 성공을 의미하지 않습니다.\n점수·합격·철도 절차 준수 판정은 제공하지 않습니다.", bodyStyle);
                GUILayout.Space(18);
                if (GUILayout.Button("다른 역할로 다시 플레이", GUILayout.Height(54))) RestartDemo();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public string TargetName(string anchorId)
        {
            var target = targets == null ? null : targets.Concat(sharedTargets).FirstOrDefault(item => item != null && item.AnchorId == anchorId);
            return target != null && !string.IsNullOrWhiteSpace(target.DisplayName) ? target.DisplayName : "목표물";
        }

        private void OnDestroy()
        {
            if (runtimeFont != null) Destroy(runtimeFont);
        }
    }
}
