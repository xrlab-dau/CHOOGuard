using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Tutorial;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor
{
    public static class TrainTutorialSceneBuilder
    {
        public const string ScenePath = "Assets/ChooGuard/Scenes/CrewTrainTutorial.unity";
        public const string ArtRoot = "Assets/ChooGuard/Art/Tutorial";
        public const string PlanPath = ArtRoot + "/CrewInspectionPlan.asset";
        public const string CatalogPath = "Assets/ChooGuard/Resources/TutorialCatalog.asset";
        [Serializable] private sealed class Survey { public Train train; }
        [Serializable] private sealed class Train { public Vector3 axis; public Car[] cars; }
        [Serializable] private sealed class Car { public int number; public float floor; public Door door; public Vector3[] aisle; public Seat[] seats; }
        [Serializable] private sealed class Seat { public string id; public int row; public Vector3 anchor, aisle; }
        [Serializable] private sealed class Door { public string[] leaves; public Vector3 inside, openDelta; }

        [MenuItem("ChooGuard/Tutorial/Build crew train inspection")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop play mode before building the tutorial.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save open scenes first.");
            try
            {
                var source = EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity", OpenSceneMode.Single);
                var sourceWorld = source.GetRootGameObjects().First(x => x.name == "FPSWorld");
                var survey = JsonUtility.FromJson<Survey>(File.ReadAllText("Assets/ChooGuard/Art/Emergency/station-points.json"));
                var car = survey.train.cars.Single(c => c.number == 2);
                var axis = survey.train.axis.normalized;
                var right = Vector3.Cross(Vector3.up, axis);
                var aisle = car.aisle[0];
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset") ?? throw new FileNotFoundException("Korean font");
                Directory.CreateDirectory(ArtRoot); Directory.CreateDirectory("Assets/ChooGuard/Resources"); AssetDatabase.Refresh();
                var plan = BuildPlan(); BuildCatalog();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.55f, .58f, .62f);
                RenderSettings.ambientEquatorColor = new Color(.42f, .44f, .47f); RenderSettings.ambientGroundColor = new Color(.24f, .25f, .27f);
                RenderSettings.skybox = null; RenderSettings.fog = false;
                var root = new GameObject("승무원 열차점검");
                var world = Object.Instantiate(sourceWorld, root.transform); world.name = "기존 역사·객실 시각 자료";
                foreach (var behaviour in world.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                var train = world.transform.Find("KTXSource");
                // Keep the connected cars as scenery; deleting them leaves rails visible through the gangway.
                foreach (var mesh in train.GetComponentsInChildren<MeshFilter>().Where(m => m.name.StartsWith("Car02", StringComparison.Ordinal)))
                    if (mesh.GetComponent<Collider>() == null && mesh.sharedMesh != null)
                        mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                foreach (var collider in train.GetComponentsInChildren<Collider>()) collider.enabled = true;
                foreach (var light in source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).Where(l => !l.transform.IsChildOf(sourceWorld.transform)))
                    Object.Instantiate(light.gameObject, root.transform).name = "기존 조명 " + light.name;
                for (int i = 0; i < 3; i++)
                {
                    var light = new GameObject("객실 보조 조명 " + i).AddComponent<Light>(); light.transform.SetParent(root.transform, false);
                    light.transform.position = aisle + axis * (2 + i * 5) + Vector3.up * 2.1f;
                    light.type = LightType.Point; light.range = 7; light.intensity = 1.1f; light.color = new Color(1, .96f, .9f); light.shadows = LightShadows.None;
                }
                var player = new GameObject("튜토리얼 승무원", typeof(CharacterController)); player.transform.SetParent(root.transform, false);
                player.transform.position = aisle + axis * 1.5f + Vector3.up * .04f;
                player.transform.rotation = Quaternion.LookRotation(-axis);
                var responder = player.AddComponent<FirstPersonResponder>();
                var camera = new GameObject("튜토리얼 시점", typeof(Camera), typeof(AudioListener)); camera.transform.SetParent(player.transform, false);
                responder.PlayerCamera = camera.GetComponent<Camera>(); responder.PlayerCamera.fieldOfView = 65; responder.PlayerCamera.farClipPlane = 300;
                responder.PlayerCamera.clearFlags = CameraClearFlags.SolidColor; responder.PlayerCamera.backgroundColor = new Color(.43f, .49f, .55f);
                var session = root.AddComponent<CrewInspectionSession>(); session.KoreanFont = font; session.Responder = responder; session.Plan = plan;
                session.ConfirmationSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ChooGuard/ThirdParty/Audio/Interface/confirmation_001.ogg");
                session.DoorOpenSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ChooGuard/ThirdParty/Audio/Station/door_open.wav");
                session.DoorCloseSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ChooGuard/ThirdParty/Audio/Station/door_close.wav");
                session.StartFeet = player.transform.position; session.StartYaw = player.transform.eulerAngles.y;
                session.DoorLeaves = car.door.leaves.Select(name => train.GetComponentsInChildren<Transform>().Single(t => t.name == name)).ToArray();
                // The surveyed FBX has its door leaf at the open pose. Store the closed pose as the session's baseline.
                foreach (var leaf in session.DoorLeaves) leaf.localPosition -= car.door.openDelta;
                session.DoorSlide = car.door.openDelta;
                var radio = Radio(root.transform, aisle - axis * .72f + right * .60f + Vector3.up * 1.43f, axis);
                session.RadioScreen = radio.transform.Find("상태 화면").GetComponent<Renderer>();
                session.RadioScreen.sharedMaterial.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(session.RadioScreen.sharedMaterial);
                var display = new GameObject("무전기 상태 표시", typeof(TextMeshPro)); display.transform.SetParent(radio.transform, false);
                display.transform.localPosition = new Vector3(0, .028f, .030f); display.transform.localRotation = Quaternion.Euler(0, 180, 0);
                session.RadioDisplay = display.GetComponent<TextMeshPro>(); session.RadioDisplay.font = font;
                session.RadioDisplay.rectTransform.sizeDelta = new Vector2(.059f, .035f);
                session.RadioDisplay.fontSize = .24f; session.RadioDisplay.enableAutoSizing = false;
                session.RadioDisplay.alignment = TextAlignmentOptions.Center; session.RadioDisplay.color = new Color(.03f, .09f, .04f);
                var pa = MountedSpeaker(root.transform, world.transform, aisle + axis * 4.8f + Vector3.up * 1.65f);
                // Reuse the extinguisher already visible in the train's textured cabinet. This surface aligns
                // with Coach02_InnerFixed_Block004, verified from the actual render and ray intersections.
                var extinguisher = new GameObject("기존 객실 소화기 수납함 점검"); extinguisher.transform.SetParent(root.transform, false);
                extinguisher.transform.SetPositionAndRotation(aisle - axis * .73f - right * .70f + Vector3.up * 1.43f, Quaternion.LookRotation(axis));
                extinguisher.AddComponent<BoxCollider>().size = new Vector3(.28f, .58f, .08f);
                // Use the native door-side panel, not a fire-shutter box whose printed label teaches the wrong device.
                var control = new GameObject("기존 출입문 옆 취급부"); control.transform.SetParent(root.transform, false);
                control.transform.SetPositionAndRotation(new Vector3(70.733f, 1.464f, 30.494f) + right * .06f, Quaternion.LookRotation(right));
                control.AddComponent<BoxCollider>().size = new Vector3(.18f, .32f, .07f);
                var suitcase = Model("Assets/ChooGuard/ThirdParty/Models/Objaverse/Suitcase/Suitcase.obj", root.transform, car.door.inside - right * .4f);
                session.Luggage = suitcase.transform; session.LuggageClearPosition = aisle + axis * .9f + right * .6f;
                // Native emergency intercom: microphone/speaker and the call button on the cabin doorway.
                // The legacy simplified collider is in front of this visible panel. Anchor to the
                // actual rendered interior mesh surface, measured with triangle 372 of this mesh.
                // A learner aims at the visible speaker/panel, not an exact 6.5 cm button hitbox.
                // Keep the usable surface on this real panel, slightly in front of its solid mesh.
                var intercomMesh = train.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Car02_T2 2car_08_T2-1_in_002");
                var intercom = Surface(root.transform, "기존 비상통화 인터폰 패널", new Vector3(71.174f,1.990f,31.692f) + right * .02f, right, new Vector3(.15f,.28f,.04f));
                intercom.transform.SetParent(intercomMesh.transform, true);
                var climate = Surface(root.transform, "기존 객실 냉난방 제어반", new Vector3(71.746f,2.145f,28.835f) - right * .025f, -right, new Vector3(.30f,.42f,.05f));
                var climateDisplay = new GameObject("냉방 설정 표시", typeof(TextMeshPro)); climateDisplay.transform.SetParent(climate.transform, false);
                climateDisplay.transform.localPosition = new Vector3(0,.11f,.033f); climateDisplay.transform.localRotation = Quaternion.Euler(0,180,0);
                session.ClimateDisplay = climateDisplay.GetComponent<TextMeshPro>(); session.ClimateDisplay.font = font;
                session.ClimateDisplay.rectTransform.sizeDelta = new Vector2(.15f,.055f); session.ClimateDisplay.fontSize=.16f;
                session.ClimateDisplay.enableAutoSizing=false; session.ClimateDisplay.alignment=TextAlignmentOptions.Center; session.ClimateDisplay.color=new Color(.03f,.12f,.04f);
                var cabinLeft = CabinPoint(root.transform, car.seats.Single(s=>s.id=="02-03B"), "cabin-left", "객실 앞쪽 점검", axis, right, -1);
                var cabinRight = CabinPoint(root.transform, car.seats.Single(s=>s.id=="02-07C"), "cabin-right", "객실 중앙 점검", axis, right, 1);
                var doorPoint = new GameObject("출입문 상태 점검"); doorPoint.transform.SetParent(root.transform, false); doorPoint.transform.position = car.door.inside - right * .6f + Vector3.up * 1.2f;
                var doorCollider = doorPoint.AddComponent<BoxCollider>(); doorCollider.size = new Vector3(.45f, .75f, .35f);
                session.Points = new[] { Point(radio, "radio", "무전기"), Point(pa, "pa", "방송 장치"), Point(intercom,"intercom","비상통화 인터폰 · 호출 버튼"), Point(extinguisher, "extinguisher", "소화기"), cabinLeft, cabinRight, Point(climate,"climate","객실 냉난방 제어반 · 온도 낮춤"), Point(doorPoint, "door", "출입문"), Point(control, "control", "출입문 취급 장치"), Point(suitcase, "luggage", "승객의 짐 · 정리 요청") };
                foreach (var point in session.Points) point.Session = session;
                var paPoint=session.Points.Single(p=>p.Id=="pa");
                paPoint.Marker.position=Bounds(pa).center;
                var intercomPoint = session.Points.Single(p => p.Id == "intercom");
                intercomPoint.Marker.position = intercom.transform.position - right * .02f;
                intercomPoint.FocusSize = new Vector2(.11f, .24f);
                var events = new GameObject("튜토리얼 입력", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(root.transform, false);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException(ScenePath);
                var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList(); scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("CG_TUTORIAL_SCENE " + ScenePath + " targets=" + session.Points.Length + " steps=" + plan.Steps.Length);
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static CrewInspectionPlan BuildPlan()
        {
            var plan = AssetDatabase.LoadAssetAtPath<CrewInspectionPlan>(PlanPath);
            if (plan == null) { plan = ScriptableObject.CreateInstance<CrewInspectionPlan>(); AssetDatabase.CreateAsset(plan, PlanPath); }
            plan.Steps = new[]
            {
                Step("통신 점검", "무전기 전원·배터리 확인", "radio", CrewInspectionAction.RadioStatus, "무전기를 바라보고 E를 누르세요.\n영상에서처럼 근무 전 무전기의 전원과 배터리를 확인합니다.", "무전기 전원과 배터리를 확인했습니다.\n방송 출력과 비상통화 인터폰은 각각의 장비에서 점검합니다."),
                Step("통신 점검", "점검용 방송 확인", "pa", CrewInspectionAction.PaTest, "스피커 표식에서 E를 누르세요.\n교육용 시험 방송이 객실 스피커에서 나오는지 듣고 확인합니다. 방송이 끝난 뒤 인터폰 점검으로 이어갑니다.", "시험 방송 출력을 확인했고 방송이 끝났습니다.\n이제 객실의 비상통화 인터폰을 시험합니다."),
                Step("통신 점검", "비상통화 인터폰 시험", "intercom", CrewInspectionAction.IntercomTest, "출입문 옆 호출 버튼이 있는 인터폰 패널을 바라보고 E를 누르세요.\n호출음을 듣고 ‘비상통화 점검 중입니다’라고 전달한 뒤 상대의 응답을 확인합니다.", "호출음과 인터폰의 점검 통화·응답을 확인했습니다.\n영상처럼 승객이 객실에서 승무원을 호출하는 장비를 시험했습니다."),
                Step("안전 장비", "소화기 위치·외관 확인", "extinguisher", CrewInspectionAction.Extinguisher, "소화기 수납함에서 E를 누르세요.\n위치와 접근 공간을 확인합니다. 분사하지 않습니다. 이 항목은 추가 안전 장비 교육입니다.", "소화기 위치와 외관을 확인했습니다.\n이상이 있으면 보고해야 합니다."),
                Step("객실 점검", "객실 앞쪽 좌석·주변 설비 점검", "cabin-left", CrewInspectionAction.CabinInspect, "객실 앞쪽 표식으로 이동해 좌석에서 E를 누르세요.\n이어서 표식이 가리키는 좌석 → 창가·주변 설비 → 선반을 하나씩 바라보세요. 외관과 이상 유무를 살핍니다.", "객실 앞쪽의 좌석·창가·선반을 관찰했습니다.\n영상의 객실 순회처럼 다음 구역으로 이동하며 점검합니다."),
                Step("객실 점검", "객실 중앙 좌석·주변 설비 점검", "cabin-right", CrewInspectionAction.CabinInspect, "표시된 객실 중앙으로 이동해 반대쪽 좌석에서 E를 누르세요.\n좌석 → 창가·주변 설비 → 선반을 차례로 바라보며 차량 이상을 확인합니다.", "두 구역의 좌석과 주변 설비를 이동하며 점검했습니다.\n영상처럼 객실의 쾌적한 상태도 살핍니다."),
                Step("객실 점검", "더운 객실의 냉방 설정 조절", "climate", CrewInspectionAction.ClimateAdjust, "영상에서처럼 더운 객실의 온도 설정을 낮추는 교육 상황입니다.\n냉난방 제어반 표식으로 이동해 E를 누르세요. 설정 변경 표시를 확인합니다.", "더운 객실에 맞춰 제어반의 온도 설정을 낮췄습니다.\n승객이 편안하게 이용하도록 객실 상태를 살피고 조절하는 업무입니다."),
                Step("출입문 점검", "출입문 이상 확인", "door", CrewInspectionAction.DoorInspect, "출입문 표식에서 E를 눌러 상태를 확인하세요.\n이 교육 사례에서는 짐 때문에 문이 완전히 닫히지 않았습니다. 문 틈과 짐을 함께 확인합니다.", "문이 덜 닫혀 있고 짐이 문 주변을 막고 있습니다.\n먼저 출발 보류를 전달합니다."),
                Step("출입문 조치", "출발 보류 전달", "", CrewInspectionAction.HoldDeparture, "Q를 누른 채 무전 문장을 선택하고 Q를 떼세요.\n표준·실전은 ‘요청 · 설비’를 좌클릭으로 열고 ‘열차 출발 보류’를 선택합니다. 견학은 문장을 바로 선택합니다.", "출발 보류와 상대의 수신 확인을 마쳤습니다.\n짐 정리를 위해 문을 개방합니다.", true),
                Step("출입문 조치", "문 개방", "control", CrewInspectionAction.OpenDoor, "출입문 취급 장치에서 E를 누르세요.\n출발 보류 상태에서 문을 개방하고 실제 움직임을 확인합니다.", "문을 개방했습니다.\n승객에게 짐 정리를 요청합니다."),
                Step("출입문 조치", "승객에게 짐 정리 요청", "luggage", CrewInspectionAction.ClearLuggage, "짐 표식에서 E를 눌러 정리를 요청하세요.\n승객의 응답과 짐이 문 주변에서 이동하는 모습을 확인합니다.", "승객이 짐을 문 주변에서 정리했습니다.\n장애물이 사라진 뒤 문을 닫습니다."),
                Step("출입문 조치", "문 닫힘 확인", "control", CrewInspectionAction.CloseDoor, "출입문 취급 장치에서 E를 눌러 문을 닫으세요.\n문 쪽을 바라보고 닫힘 상태를 확인합니다.", "문이 닫힘 상태로 바뀌었습니다.\n조치 결과를 보고하며 마칩니다."),
                Step("완료 보고", "조치 결과 전달", "", CrewInspectionAction.ReportComplete, "Q를 누른 채 무전 문장을 선택하고 Q를 떼세요.\n표준·실전은 ‘보고’를 좌클릭으로 열고 ‘짐 정리 · 문 닫힘 확인 보고’를 선택합니다. 견학은 문장을 바로 선택합니다.", "조치 결과 전달을 마쳤습니다.\n무전기 준비·방송·인터폰·객실 점검과 출입문 대응을 체험했습니다.", true),
            };
            string[] observations = {
                "전원 켜짐 · 배터리 85%\n근무 전 무전기 준비", "객실 스피커의 실제 음성\n방송 종료 후 인터폰 시험",
                "기존 비상통화 인터폰\n호출음 → 점검 통화 → 응답", "소화기 수납함의 위치\n앞의 접근 공간",
                "좌석 → 창가·주변 설비 → 선반\n표식대로 직접 관찰",
                "다음 구역으로 이동\n반대쪽 좌석과 주변 설비", "더운 객실의 설정 조절\n제어반의 온도 낮춤 표시",
                "덜 닫힌 문과 승객의 짐\n출발 전 상태 확인", "출발 보류와 수신 확인\n휴대 무전기 사용",
                "실제 문이 열리는 모습\n짐 정리 공간 확보", "승객 응답과 짐 이동\n문 주변의 장애물 정리",
                "장애물 정리 후 닫힌 문\n조치 결과 관찰", "짐 정리·문 닫힘 결과 전달\n상대의 수신 확인"
            };
            var voiceIndices = new[] { 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
            for (int i = 0; i < plan.Steps.Length; i++)
            { plan.Steps[i].GuidanceVoice = Voice("guide-" + voiceIndices[i].ToString("00")); plan.Steps[i].Observation = observations[i]; }
            plan.Steps[1].ResponseVoice = Voice("reply-pa");
            plan.Steps[2].ResponseVoice = Voice("reply-intercom");
            plan.Steps[8].ResponseVoice = Voice("reply-hold");
            plan.Steps[10].ResponseVoice = Voice("reply-luggage");
            plan.Steps[12].ResponseVoice = Voice("reply-complete");
            if (!plan.Validate(out var reason)) throw new InvalidOperationException(reason);
            EditorUtility.SetDirty(plan); return plan;
        }

        private static AudioClip Voice(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(ArtRoot + "/Audio/" + name + ".wav") ?? throw new FileNotFoundException(name);
        private static GameObject Surface(Transform parent,string name,Vector3 position,Vector3 normal,Vector3 size)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.SetPositionAndRotation(position,Quaternion.LookRotation(normal));
            go.AddComponent<BoxCollider>().size=size; return go;
        }
        private static CrewInspectionPoint CabinPoint(Transform parent,Seat seat,string id,string label,Vector3 axis,Vector3 right,int side)
        {
            var owner=Surface(parent,label,seat.anchor+axis*.1f+Vector3.up*.75f,axis,new Vector3(.5f,.85f,.65f));
            var point=Point(owner,id,label);
            var locations=new[] { owner.transform.position, seat.aisle+right*side*1.25f+Vector3.up*1.25f, seat.aisle+right*side*1.1f+Vector3.up*1.88f };
            var names=new[] { "좌석 외관", "창가와 주변 설비", "상부 선반" };
            point.InspectionTargets=locations.Select((location,index)=> { var target=new GameObject(names[index]).transform; target.SetParent(owner.transform,true); target.position=location; return target; }).ToArray();
            return point;
        }
        private static CrewInspectionStep Step(string chapter, string title, string target, CrewInspectionAction action, string instruction, string result, bool radio = false) =>
            new CrewInspectionStep { Chapter = chapter, Title = title, TargetId = target, Action = action, Instruction = instruction, Result = result, HandheldRadio = radio };
        private static void BuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TutorialCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<TutorialCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            var category = new TutorialCategory { Id = "crew-train-inspection", Title = "승무원 열차점검", Description = "통신·안전 장비를 점검하고 출입문 이상을 안내에 따라 조치합니다.", SceneName = "CrewTrainTutorial", ExpectedMinSeconds = 150, ExpectedMaxSeconds = 300 };
            catalog.Categories = (catalog.Categories ?? Array.Empty<TutorialCategory>()).Where(c => c != null && c.Id != category.Id).Concat(new[] { category }).ToArray();
            EditorUtility.SetDirty(catalog);
        }
        private static CrewInspectionPoint Point(GameObject owner, string id, string label)
        {
            var point = owner.AddComponent<CrewInspectionPoint>(); point.Id = id; point.Label = label;
            var bounds = Bounds(owner);
            var marker = new GameObject("안내 표식").transform; marker.SetParent(owner.transform, true); marker.position = bounds.center + Vector3.up * Mathf.Min(bounds.extents.y, .5f); point.Marker = marker;
            return point;
        }
        private static GameObject Prop(string name, Transform parent, Vector3 position) => Model("Assets/ChooGuard/Art/Emergency/Equipment/Prefabs/" + name + ".prefab", parent, position);
        private static GameObject MountedSpeaker(Transform parent, Transform visibleCabin, Vector3 rayOrigin)
        {
            // This speaker is added only for the authored tutorial. Mount the reused prefab
            // against the visible train ceiling, never at an arbitrary eye-height position.
            MeshRenderer ceiling=null; Vector3 surface=default,normal=default; float distance=float.PositiveInfinity;
            var ray=new Ray(rayOrigin,Vector3.up);
            foreach(var renderer in visibleCabin.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&(r.name.StartsWith("Car02",StringComparison.Ordinal)||r.name.StartsWith("Coach02_InnerFixed",StringComparison.Ordinal))))
            {
                var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;
                // Interior ceilings may have reversed triangle winding. Measure both sides
                // of the actual rendered triangles so the outer roof cannot hide the cabin ceiling.
                var vertices=mesh.vertices.Select(v=>renderer.localToWorldMatrix.MultiplyPoint3x4(v)).ToArray();var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=vertices[triangles[i]];var e1=vertices[triangles[i+1]]-a;var e2=vertices[triangles[i+2]]-a;
                    var h=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<1e-7f)continue;
                    float inverse=1/det;var s=ray.origin-a;float u=Vector3.Dot(s,h)*inverse;if(u<0||u>1)continue;
                    var q=Vector3.Cross(s,e1);float v=Vector3.Dot(ray.direction,q)*inverse;if(v<0||u+v>1)continue;
                    float d=Vector3.Dot(e2,q)*inverse;var n=Vector3.Cross(e1,e2).normalized;
                    if(d<.05f||d>1.5f||d>=distance||Mathf.Abs(n.y)<.85f)continue;
                    ceiling=renderer;surface=ray.GetPoint(d);normal=n;distance=d;
                }
            }
            if(ceiling==null)throw new InvalidOperationException("The tutorial speaker has no visible ceiling mount.");
            if(normal.y<0)normal=-normal;
            var speaker=Prop("CeilingSpeaker",parent,rayOrigin);
            speaker.transform.rotation=Quaternion.FromToRotation(Vector3.up,normal);
            float highest=float.NegativeInfinity;
            foreach(var filter in speaker.GetComponentsInChildren<MeshFilter>())
                foreach(var vertex in filter.sharedMesh.vertices)
                    highest=Mathf.Max(highest,Vector3.Dot(filter.transform.TransformPoint(vertex),normal));
            speaker.transform.position+=normal*(Vector3.Dot(surface,normal)-highest+.002f);
            speaker.transform.SetParent(ceiling.transform,true);
            Debug.Log("CG_TUTORIAL_PA_MOUNT mesh="+ceiling.name+" surface="+surface.ToString("F4")+" normal="+normal.ToString("F4"));
            return speaker;
        }
        private static GameObject Model(string path, Transform parent, Vector3 position, float scale = 1)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException(path);
            var model = Object.Instantiate(asset, parent); model.name = Path.GetFileNameWithoutExtension(path);
            model.transform.localScale *= scale;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            var bounds = Bounds(model); model.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (model.GetComponentsInChildren<Collider>().Length == 0)
            {
                bounds = Bounds(model); var box = model.AddComponent<BoxCollider>(); box.center = model.transform.InverseTransformPoint(bounds.center); box.size = Vector3.Scale(bounds.size, new Vector3(1 / model.transform.lossyScale.x, 1 / model.transform.lossyScale.y, 1 / model.transform.lossyScale.z));
            }
            return model;
        }
        private static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return root.GetComponent<Collider>() != null ? root.GetComponent<Collider>().bounds : new Bounds(root.transform.position, Vector3.one * .1f);
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        // Adapted portable-radio proportions from musehq/spacesvr's MIT TalkieModel (recorded in the tutorial asset research).
        // The beveled extrusion is an authored Unity mesh; no original train mesh or material is replaced.
        private static GameObject Radio(Transform parent, Vector3 position, Vector3 facing)
        {
            var root = new GameObject("교육용 무전기"); root.transform.SetParent(parent, false); root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            RadioPart(root.transform, "본체", new Vector3(.085f, .16f, .042f), Vector3.zero, new Color(.08f, .09f, .11f), .008f);
            RadioPart(root.transform, "안테나", new Vector3(.018f, .10f, .022f), new Vector3(-.027f, .115f, 0), new Color(.035f, .035f, .04f), .005f);
            RadioPart(root.transform, "상태 화면", new Vector3(.063f, .041f, .008f), new Vector3(0, .028f, .024f), new Color(.32f, .54f, .36f), .004f);
            for (int i = 0; i < 5; i++) RadioPart(root.transform, "스피커 " + i, new Vector3(.057f, .004f, .005f), new Vector3(0, -.012f - i * .008f, .024f), new Color(.01f, .01f, .013f), .001f);
            var box = root.AddComponent<BoxCollider>(); box.size = new Vector3(.1f, .25f, .055f); box.center = new Vector3(0, .035f, 0);
            return root;
        }
        private static void RadioPart(Transform parent, string name, Vector3 size, Vector3 offset, Color color, float bevel)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(parent, false); part.transform.localPosition = offset;
            string safeName = name.Replace(" ", "-"); string meshPath = ArtRoot + "/Radio-" + safeName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                float x = size.x * .5f, y = size.y * .5f, z = size.z * .5f, r = Mathf.Min(bevel, x * .45f, y * .45f);
                var outline = new[] { new Vector2(-x + r, -y), new Vector2(x - r, -y), new Vector2(x, -y + r), new Vector2(x, y - r), new Vector2(x - r, y), new Vector2(-x + r, y), new Vector2(-x, y - r), new Vector2(-x, -y + r) };
                var vertices = outline.Select(p => new Vector3(p.x, p.y, -z)).Concat(outline.Select(p => new Vector3(p.x, p.y, z))).Concat(new[] { new Vector3(0, 0, -z), new Vector3(0, 0, z) }).ToArray();
                var triangles = new System.Collections.Generic.List<int>();
                for (int i = 0; i < 8; i++) { int next = (i + 1) % 8; triangles.AddRange(new[] { 16, next, i, 17, i + 8, next + 8, i, next, next + 8, i, next + 8, i + 8 }); }
                mesh = new Mesh { name = "Portable radio " + name, vertices = vertices, triangles = triangles.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, meshPath);
            }
            string materialPath = ArtRoot + "/Radio-" + safeName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, materialPath); }
            part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        public static void InspectVestibule()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var axis = new Vector3(.347234f, 0, .937778f).normalized;
            var aisle = new Vector3(71.99928f, .4500034f, 30.76221f);
            var camera = new GameObject("Asset inspection camera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(aisle + axis * 1.15f + Vector3.up * 1.6f, Quaternion.LookRotation(-axis));
            camera.fieldOfView = 65; camera.nearClipPlane = .05f;
            var rt = new RenderTexture(1200, 900, 24); camera.targetTexture = rt; camera.Render();
            var image = new Texture2D(1200, 900, TextureFormat.RGB24, false); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); image.Apply();
            var folder = Environment.GetEnvironmentVariable("CG_TUTORIAL_REVIEW");
            File.WriteAllBytes(Path.Combine(folder, "vestibule-inspection.png"), image.EncodeToPNG());
            Physics.SyncTransforms(); var report = new StringBuilder();
            for (float x = .1f; x < .91f; x += .05f)
                for (float y = .15f; y < .91f; y += .05f)
                    if (Physics.Raycast(camera.ViewportPointToRay(new Vector3(x,y,0)), out var hit, 5))
                        report.AppendLine(x.ToString("F2") + "," + y.ToString("F2") + " " + hit.point.ToString("F3") + " " + hit.collider.name);
            File.WriteAllText(Path.Combine(folder, "vestibule-rays.txt"), report.ToString());
            camera.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(camera.gameObject);
        }
        public static void MeasureNativeIntercomMesh()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var intersect = typeof(HandleUtility).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Single(m => m.Name == "IntersectRayMesh" && m.GetParameters().Length == 4);
            var camera = new GameObject("Native panel triangle measurement").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(72.6417f,2.0800f,32.4971f), Quaternion.Euler(1.3263f,240.0676f,0));
            camera.fieldOfView=65; camera.aspect=1.6f;
            var report = new StringBuilder();
            foreach (var uv in new[] { new Vector2(.50f,.47f),new Vector2(.51f,.47f),new Vector2(.52f,.47f),new Vector2(.50f,.50f),new Vector2(.52f,.50f) })
            {
                var ray = camera.ViewportPointToRay(new Vector3(uv.x,uv.y,0));
                var hits = new List<(float distance,string text)>();
                foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("Car02",StringComparison.Ordinal) && r.enabled))
                {
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh; if(mesh==null) continue;
                    var args = new object[] { ray,mesh,renderer.localToWorldMatrix,new RaycastHit() };
                    if(!(bool)intersect.Invoke(null,args)) continue;
                    var hit = (RaycastHit)args[3];
                    hits.Add((hit.distance,"mesh="+renderer.name+" point="+hit.point.ToString("F5")+" normal="+hit.normal.ToString("F4")+" uv="+hit.textureCoord.ToString("F4")+" triangle="+hit.triangleIndex+" distance="+hit.distance.ToString("F5")+" material="+string.Join(",",renderer.sharedMaterials.Select(m=>m.name))));
                }
                report.AppendLine("VIEW "+uv.ToString("F3"));
                foreach(var hit in hits.OrderBy(h=>h.distance).Take(8))report.AppendLine(hit.text);
            }
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("CG_TUTORIAL_REVIEW"),"native-mesh-hits.txt"),report.ToString());
            Object.DestroyImmediate(camera.gameObject);
        }
        public static void InspectBroadcastMount()
        {
            var folder=Environment.GetEnvironmentVariable("CG_TUTORIAL_REVIEW"); Directory.CreateDirectory(folder);
            var report=new StringBuilder();
            var source=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            report.AppendLine("AUTONOMOUS_SOURCE ceiling speaker objects="+source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Count(t=>t.name=="CeilingSpeaker"));
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var session=Object.FindFirstObjectByType<CrewInspectionSession>(); var point=session.Points.Single(p=>p.Id=="pa");
            var bounds=Bounds(point.gameObject); report.AppendLine("TUTORIAL speaker="+bounds+" rotation="+point.transform.eulerAngles+" parent="+point.transform.parent.name);
            var intersect=typeof(HandleUtility).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Single(m=>m.Name=="IntersectRayMesh"&&m.GetParameters().Length==4);
            var ray=new Ray(bounds.center,Vector3.up);
            foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("Car02",StringComparison.Ordinal)))
            {
                var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh; if(mesh==null)continue;
                var args=new object[]{ray,mesh,renderer.localToWorldMatrix,new RaycastHit()};
                if(!(bool)intersect.Invoke(null,args))continue;var hit=(RaycastHit)args[3];
                report.AppendLine("UP mesh="+renderer.name+" point="+hit.point.ToString("F5")+" normal="+hit.normal+" distance="+hit.distance+" triangle="+hit.triangleIndex);
            }
            File.WriteAllText(Path.Combine(folder,"broadcast-mount.txt"),report.ToString());
            var camera=new GameObject("Broadcast mount inspection").AddComponent<Camera>();
            camera.transform.position=bounds.center+new Vector3(-.937778f,-.45f,.347234f)*1.1f;
            camera.transform.rotation=Quaternion.LookRotation(bounds.center-camera.transform.position);camera.fieldOfView=65;camera.aspect=1.6f;camera.nearClipPlane=.05f;
            var target=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(folder,"broadcast-mount.png"),pixels.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(pixels);target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(camera.gameObject);
        }
        public static void AuditIntercomFrame()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var session = Object.FindFirstObjectByType<CrewInspectionSession>();
            var report = new StringBuilder();
            foreach (var leaf in session.DoorLeaves)
                report.AppendLine(leaf.name + " parent=" + leaf.parent.name + " delta=" + leaf.parent.TransformVector(session.DoorSlide).ToString("F5"));
            var point = session.Points.Single(p => p.Id == "intercom");
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name.StartsWith("Car02", StringComparison.Ordinal) && r.bounds.Contains(point.Marker.position)))
                report.AppendLine("SURFACE " + renderer.name + " parent=" + renderer.transform.parent.name + " center=" + renderer.bounds.center.ToString("F4"));
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("CG_TUTORIAL_REVIEW"), "intercom-frame-audit.txt"), report.ToString());
        }
        public static void InspectReferenceEquipment()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var axis = new Vector3(.347234f, 0, .937778f).normalized;
            var right = Vector3.Cross(Vector3.up, axis);
            var aisle = new Vector3(71.99928f, .4500034f, 30.76221f);
            var folder = Environment.GetEnvironmentVariable("CG_TUTORIAL_REVIEW");
            Directory.CreateDirectory(folder);
            var climate = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Single(r => r.name == "Car02_T2 2car_08_T2-1_in_007");
            var intercom = Object.FindFirstObjectByType<CrewInspectionSession>().Points.Single(p=>p.Id=="intercom");
            intercom.GetComponent<Collider>().enabled = false;
            var positions = new[] { aisle + axis * 1.35f, aisle + axis * 1.35f, aisle + axis * 4.8f, aisle + axis * .2f, new Vector3(72.53f,.47f,32.18f) };
            var headings = new[] { -right, right, -axis, (climate.bounds.center - (positions[3] + Vector3.up * 1.6f)).normalized, (intercom.Marker.position-(positions[4]+Vector3.up*1.6f)).normalized };
            for (int sample = 0; sample < positions.Length; sample++)
            {
                var camera = new GameObject("Reference geometry camera").AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(positions[sample] + Vector3.up * 1.6f, Quaternion.LookRotation(headings[sample]));
                camera.fieldOfView = 65; camera.nearClipPlane = .05f; camera.aspect=1440f/900f;
                var target = new RenderTexture(1440, 900, 24); camera.targetTexture = target; camera.Render();
                var image = new Texture2D(1440, 900, TextureFormat.RGB24, false); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,1440,900),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(folder, "equipment-" + sample + ".png"), image.EncodeToPNG());
                Physics.SyncTransforms(); var rays = new StringBuilder();
                for (float x=sample == 4 ? .35f : .05f; x<(sample == 4 ? .76f : .96f); x+=sample == 4 ? .005f : .05f)
                    for(float y=sample == 4 ? .35f : .05f; y<(sample == 4 ? .76f : .96f); y+=sample == 4 ? .005f : .05f)
                        if(Physics.Raycast(camera.ViewportPointToRay(new Vector3(x,y,0)),out var hit,5)) rays.AppendLine(x.ToString("F3")+","+y.ToString("F3")+" "+hit.point.ToString("F4")+" normal="+hit.normal.ToString("F3")+" "+hit.collider.name);
                File.WriteAllText(Path.Combine(folder,"equipment-"+sample+"-rays.txt"),rays.ToString());
                camera.targetTexture=null; RenderTexture.active=null; Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(camera.gameObject);
            }
        }
        public static void Probe()
        {
            var report = new StringBuilder();
            var scene = EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity", OpenSceneMode.Single);
            var train = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).First(x => x.name == "KTXSource");
            report.AppendLine("TRAIN " + train.position + " scale=" + train.lossyScale + " rotation=" + train.rotation.eulerAngles);
            foreach (var mesh in train.GetComponentsInChildren<MeshRenderer>(true).Where(x => x.name.StartsWith("Car02", StringComparison.Ordinal)))
                report.AppendLine(mesh.name + " world=" + mesh.bounds + " local=" + mesh.transform.localPosition + " asset=" + AssetDatabase.GetAssetPath(mesh.GetComponent<MeshFilter>()?.sharedMesh));
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] {"Assets/ChooGuard/Art/Emergency/Equipment"}).Select(AssetDatabase.GUIDToAssetPath))
                report.AppendLine("PROP " + path);
            foreach (var path in new[] {"Assets/ChooGuard/Art/ResponseSet/CrewOperations.fbx", "Assets/ChooGuard/Art/ResponseSet/CrewMedical.fbx"})
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;
                foreach (var mesh in asset.GetComponentsInChildren<MeshRenderer>(true)) report.AppendLine("CREW_PROP " + path + " " + mesh.name + " " + mesh.bounds);
            }
            string output = Environment.GetEnvironmentVariable("CG_TUTORIAL_REVIEW") ?? "Builds/TutorialReview";
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "asset-inspection.txt"), report.ToString());
            Debug.Log("CG_TUTORIAL_PROBE " + output);
        }
    }
}

