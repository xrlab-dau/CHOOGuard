using System;
using System.Linq;
using ChooGuard.App.Fps.Hud;
using ChooGuard.App.Fps.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChooGuard.App.Fps.Tutorial
{
    /// <summary>Authored, offline equipment practice with observed actions and spoken coaching.</summary>
    public sealed class CrewInspectionSession : MonoBehaviour
    {
        public TMP_FontAsset KoreanFont;
        public FirstPersonResponder Responder;
        public CrewInspectionPlan Plan;
        public CrewInspectionPoint[] Points = Array.Empty<CrewInspectionPoint>();
        public Transform[] DoorLeaves = Array.Empty<Transform>();
        public Vector3 DoorSlide;
        public Transform Luggage;
        public Vector3 LuggageClearPosition, StartFeet;
        public float StartYaw;
        public AudioClip ConfirmationSound, DoorOpenSound, DoorCloseSound;
        public Renderer RadioScreen;
        public TMP_Text RadioDisplay;
        public TMP_Text ClimateDisplay;
        public RadioWheel Wheel { get; private set; }
        public bool RadioGrouped { get; private set; }
        public int RadioGroup { get; private set; } = -1;
        public string RadioInstructions => GameSettings.Guided ? "Q 누른 채 · 마우스로 문장 선택 · Q를 떼면 전송" : "Q 누른 채 · 묶음 선택 후 좌클릭 · 문장 선택 후 Q를 떼면 전송 · 우클릭 뒤로";
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public bool ReadingResult { get; private set; }
        public bool Acting { get; private set; }
        public bool VoiceEnabled { get; private set; } = true;
        public bool CoachSpeaking => coach != null && coach.isPlaying;
        public float ActionProgress => Acting ? Mathf.Clamp01(actionElapsed / actionDuration) : ReadingResult ? 1 : 0;
        public string ActionFeedback { get; private set; } = "";
        public int StepIndex { get; private set; }
        public string LastResult { get; private set; } = "";
        public bool DepartureHeld { get; private set; }
        public bool LuggageCleared { get; private set; }
        public bool DoorInspected { get; private set; }
        public float DoorOpenAmount { get; private set; } = .35f;
        public bool IntercomTested { get; private set; }
        public string IntercomState { get; private set; } = "대기";
        public int CabinSectionsChecked { get; private set; }
        public int CabinLookIndex { get; private set; }
        public bool CoolingAdjusted { get; private set; }
        public bool PaTested { get; private set; }
        public int RadioBatteryPercent = 85;
        public bool RadioPowerOn = true;
        public CrewInspectionStep Current => Plan != null && Plan.Steps != null && StepIndex < Plan.Steps.Length ? Plan.Steps[StepIndex] : null;
        public CrewInspectionPoint CurrentPoint => Current == null || Current.HandheldRadio ? null : Points.FirstOrDefault(p => p != null && p.Id == Current.TargetId);
        public CrewInspectionPoint ObservationPoint => Current != null && (Current.Action == CrewInspectionAction.OpenDoor || Current.Action == CrewInspectionAction.CloseDoor) ? Points.FirstOrDefault(p => p.Id == "door") : CurrentPoint;
        public Transform AttentionTarget => Current?.Action == CrewInspectionAction.CabinInspect && Acting && CurrentPoint != null && CabinLookIndex < CurrentPoint.InspectionTargets.Length ? CurrentPoint.InspectionTargets[CabinLookIndex] : ObservationPoint?.Marker;
        public bool AwaitingObservation => Acting && (Current?.Action == CrewInspectionAction.CabinInspect || actionElapsed >= actionDuration && NeedsObservation);
        private bool NeedsObservation => Current != null && (Current.Action == CrewInspectionAction.OpenDoor || Current.Action == CrewInspectionAction.CloseDoor || Current.Action == CrewInspectionAction.ClearLuggage);
        private Vector3[] closedDoorPositions;
        private Vector3 originalLuggage, actionLuggageStart;
        private CrewInspectionView view;
        private AudioSource sound, coach, effects;
        private AudioLowPassFilter radioFilter;
        private MaterialPropertyBlock screenProperties;
        private float actionElapsed, actionDuration, actionDoorStart;
        private float cabinLookElapsed;
        private Vector3 lastCabinInspectionFeet;
        private bool responseStarted;
        private AudioClip intercomCallTone;
        private bool ready;

        private void Start()
        {
            if (!Initialize(out var reason)) { Debug.LogError("[CrewInspection] " + reason); enabled = false; return; }
            view = CrewInspectionView.Create(transform, KoreanFont, this);
            view.ShowIntro();
        }
        public bool Initialize(out string reason)
        {
            if (ready) { reason = null; return true; }
            reason = "튜토리얼 플레이어·자료·장비 참조를 확인해 주세요.";
            if (Responder == null || KoreanFont == null || Plan == null || Points == null || Luggage == null ||
                DoorLeaves == null || DoorLeaves.Length == 0 || DoorLeaves.Any(t => t == null)) return false;
            if (!Plan.Validate(out reason)) return false;
            if (Points.Any(p => p == null) || Points.Select(p => p.Id).Distinct().Count() != Points.Length ||
                Plan.Steps.Any(s => !s.HandheldRadio && !Points.Any(p => p.Id == s.TargetId)))
            { reason = "단계의 점검 대상이 누락되었거나 중복되었습니다."; return false; }
            foreach (var point in Points) point.Session = this;
            closedDoorPositions = DoorLeaves.Select(t => t.localPosition).ToArray();
            originalLuggage = Luggage.position;
            coach = gameObject.AddComponent<AudioSource>(); coach.playOnAwake = false; coach.spatialBlend = 0; coach.volume = .85f;
            var device = new GameObject("점검 장비 응답"); device.transform.SetParent(transform, false);
            sound = device.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.minDistance = 2; sound.maxDistance = 18;
            effects = device.AddComponent<AudioSource>(); effects.playOnAwake = false; effects.spatialBlend = 1; effects.minDistance = 2; effects.maxDistance = 18;
            radioFilter = device.AddComponent<AudioLowPassFilter>(); radioFilter.cutoffFrequency = 3200; radioFilter.enabled = false;
            screenProperties = new MaterialPropertyBlock();
            // Reuse the station's short electronic-call synthesis approach; no remote speech service.
            const int rate = 16000;
            var samples = new float[rate / 4];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                samples[i] = Mathf.Sin(2 * Mathf.PI * 880 * t) * Mathf.Min(1, t / .005f) * Mathf.Min(1, (.25f - t) / .025f) * .35f;
            }
            intercomCallTone = AudioClip.Create("비상통화 호출음", samples.Length, 1, rate, false); intercomCallTone.SetData(samples, 0);
            Responder.PauseChanged += OnPause;
            Wheel = RadioWheel.Create(transform, KoreanFont);
            ready = true; ResetState(); reason = null; return true;
        }
        public void Begin()
        {
            if (!ready || !Responder.Resume()) return;
            Started = true; view?.HideModal(); SpeakGuidance();
        }
        public void Restart()
        {
            if (!ready) return;
            Responder.Pause(); ResetState(); Responder.RestorePhysicalPose(StartFeet, StartYaw, 0); view?.ShowIntro();
        }
        private void ResetState()
        {
            CancelRadioSelection();
            Started = Finished = ReadingResult = Acting = DepartureHeld = LuggageCleared = DoorInspected = IntercomTested = PaTested = CoolingAdjusted = false;
            CabinSectionsChecked = CabinLookIndex = 0; cabinLookElapsed = 0; IntercomState = "대기";
            if (ClimateDisplay != null) ClimateDisplay.text = "냉방 설정\n기존 단계";
            StepIndex = 0; LastResult = ActionFeedback = ""; actionElapsed = 0;
            coach.Stop(); sound.Stop(); effects.Stop(); Luggage.position = originalLuggage; SetDoor(.35f); SetRadio("READY", false);
        }
        public bool Accepts(CrewInspectionPoint point, FirstPersonResponder player) => ready && Started && !Finished && !ReadingResult && !Acting &&
            player == Responder && !player.IsPaused && Current != null && !Current.HandheldRadio && point == CurrentPoint;
        public bool Perform(CrewInspectionPoint point, FirstPersonResponder player)
        {
            if (!Accepts(point, player) || !ReferenceEquals(player.CurrentInteraction, point)) return false;
            return Act(Current.Action);
        }
        private bool CanUseRadio => ready && Started && !Finished && !ReadingResult && !Acting && !Responder.IsPaused && Current != null && Current.HandheldRadio;
        public bool StepRadioInput(bool pressed, bool held, Vector2 mouseDelta, bool openGroup = false, bool back = false)
        {
            if (!CanUseRadio) { CancelRadioSelection(); return false; }
            if (pressed && !Wheel.Open)
            {
                RadioGrouped = !GameSettings.Guided; RadioGroup = -1;
                if (RadioGrouped) ShowRadioGroups();
                else Wheel.Show(new[] { Current.Action == CrewInspectionAction.HoldDeparture ? "열차 출발 보류" : "짐 정리 · 문 닫힘 확인 보고" });
                Responder.SuppressLookInput = Responder.SuppressInteractionInput = true;
            }
            if (!Wheel.Open) return false;
            Wheel.Steer(mouseDelta);
            if (RadioGrouped)
            {
                if (openGroup && RadioGroup < 0 && Wheel.Selected >= 0)
                {
                    RadioGroup = Wheel.Selected;
                    Wheel.Show(new[] { RadioGroup == 0 ? "짐 정리 · 문 닫힘 확인 보고" : "열차 출발 보류" },
                        (RadioGroup == 0 ? "보고" : "요청 · 설비") + "\n<size=11>떼면 보냄\n우클릭 뒤로</size>", preservePointer: RadioGroup == 0);
                }
                if (back && RadioGroup >= 0) ShowRadioGroups();
            }
            if (held) return false;
            int chosen = Wheel.Close(), group = RadioGroup;
            Responder.SuppressLookInput = Responder.SuppressInteractionInput = false; RadioGroup = -1;
            if (chosen < 0 || RadioGrouped && group < 0)
            {
                Responder.ShowFeedback(RadioGrouped && group < 0 ? "묶음을 좌클릭으로 열고 무전 문장을 선택한 뒤 Q를 떼세요." : "무전 문장이 선택되지 않아 전송을 취소했습니다. 문장을 선택한 뒤 Q를 떼세요.");
                return false;
            }
            var message = RadioGrouped ? group == 0 ? CrewInspectionAction.ReportComplete : CrewInspectionAction.HoldDeparture : Current.Action;
            if (message != Current.Action) { Responder.ShowFeedback("화면 안내에 맞는 무전 문장을 선택하세요."); return false; }
            if (!RadioPowerOn || RadioBatteryPercent <= 0) { Responder.ShowFeedback("무전기의 전원과 배터리를 확인하세요."); return false; }
            return Act(message);
        }
        private void ShowRadioGroups() { RadioGroup = -1; Wheel.Show(new[] { "보고", "요청 · 설비" }, "무전\n<size=11>방향 고른 뒤\n좌클릭 열기</size>"); }
        private void CancelRadioSelection() { if (Wheel != null && Wheel.Open) Wheel.Close(); RadioGroup = -1; if (Responder != null) Responder.SuppressLookInput = Responder.SuppressInteractionInput = false; }
        private bool Act(CrewInspectionAction action)
        {
            string reason = null;
            switch (action)
            {
                case CrewInspectionAction.RadioStatus:
                    if (!RadioPowerOn || RadioBatteryPercent <= 0) reason = "전원·배터리를 확인해야 합니다. 사용할 수 있는 장비로 준비하세요.";
                    break;
                case CrewInspectionAction.IntercomTest:
                    if (!PaTested) reason = "방송이 끝난 뒤 인터폰 시험을 진행하세요.";
                    break;
                case CrewInspectionAction.CabinInspect:
                    if (CurrentPoint.InspectionTargets.Length != 3) reason = "객실 점검 대상 자료를 확인해 주세요.";
                    else if (CabinSectionsChecked > 0 && Vector3.Distance(lastCabinInspectionFeet, Responder.transform.position) < 2.5f) reason = "표시된 다음 객실 구역으로 이동하며 점검하세요.";
                    break;
                case CrewInspectionAction.ClimateAdjust:
                    if (CabinSectionsChecked < 2) reason = "안내된 객실 두 구역을 먼저 점검하세요.";
                    break;
                case CrewInspectionAction.DoorInspect:
                    if (DoorOpenAmount <= .01f || LuggageCleared) reason = "교육 사례의 문·짐 배치를 확인해 주세요. 처음부터 다시 시작할 수 있습니다.";
                    break;
                case CrewInspectionAction.HoldDeparture:
                    if (!DoorInspected) reason = "먼저 안내된 출입문 상태를 확인하세요.";
                    break;
                case CrewInspectionAction.OpenDoor:
                    if (!DepartureHeld) reason = "먼저 출발 보류를 전달하세요.";
                    break;
                case CrewInspectionAction.ClearLuggage:
                    if (!DepartureHeld || DoorOpenAmount < .99f) reason = "출발 보류와 문 개방 안내를 먼저 따라가세요.";
                    break;
                case CrewInspectionAction.CloseDoor:
                    if (!LuggageCleared) reason = "짐이 문을 막고 있습니다. 먼저 정리 요청을 안내받으세요.";
                    break;
                case CrewInspectionAction.ReportComplete:
                    if (!DepartureHeld || !LuggageCleared || DoorOpenAmount > .01f) reason = "문 상태가 정상으로 바뀐 뒤 완료를 전달하세요.";
                    break;
            }
            if (reason != null) { Responder.ShowFeedback(reason); return false; }
            coach.Stop(); sound.Stop(); Acting = true; actionElapsed = 0;
            actionDoorStart = DoorOpenAmount; actionLuggageStart = Luggage.position;
            LastResult = "";
            responseStarted = false; CabinLookIndex = 0; cabinLookElapsed = 0;
            bool radio = IsRadio(action);
            sound.transform.position = CurrentPoint != null ? CurrentPoint.transform.position : Responder.PlayerCamera.transform.position;
            sound.spatialBlend = Current.HandheldRadio ? 0 : 1; radioFilter.enabled = radio || action == CrewInspectionAction.IntercomTest;
            actionDuration = action == CrewInspectionAction.OpenDoor || action == CrewInspectionAction.CloseDoor ? 1.6f : 1.8f;
            if (Current.ResponseVoice != null)
            {
                if (VoiceEnabled) actionDuration = Mathf.Max(actionDuration, Current.ResponseVoice.length + (action == CrewInspectionAction.IntercomTest ? .85f : .2f));
                if (VoiceEnabled && action != CrewInspectionAction.IntercomTest) { sound.PlayOneShot(Current.ResponseVoice, .85f); responseStarted = true; }
            }
            if (action == CrewInspectionAction.IntercomTest) { IntercomState = "호출 중"; effects.PlayOneShot(intercomCallTone, .7f); }
            if (action == CrewInspectionAction.OpenDoor && DoorOpenSound != null) effects.PlayOneShot(DoorOpenSound, .45f);
            if (action == CrewInspectionAction.CloseDoor && DoorCloseSound != null) effects.PlayOneShot(DoorCloseSound, .45f);
            ActionFeedback = action == CrewInspectionAction.IntercomTest ? "인터폰 호출음 · 상대와 연결하고 있습니다" : radio ? "무전 송신 · 상대의 응답을 들어보세요" : action == CrewInspectionAction.PaTest ? "시험 방송 · 객실 스피커 출력을 들어보세요" :
                action == CrewInspectionAction.OpenDoor ? "문 개방 중 · 실제 움직임을 확인하세요" : action == CrewInspectionAction.CloseDoor ? "문 닫힘 중 · 닫히는 모습을 확인하세요" :
                action == CrewInspectionAction.ClearLuggage ? "승객에게 정리 요청 · 짐의 이동을 확인하세요" : "대상 관찰 중 · 표시된 확인 사항을 살펴보세요";
            if (radio || action == CrewInspectionAction.RadioStatus) SetRadio(radio ? "TX" : "BAT", true);
            return true;
        }
        private static bool IsRadio(CrewInspectionAction action) => action == CrewInspectionAction.HoldDeparture || action == CrewInspectionAction.ReportComplete;
        private void AdvanceAction(float delta)
        {
            actionElapsed += delta;
            var action = Current.Action;
            if (action == CrewInspectionAction.IntercomTest && actionElapsed >= .65f)
            {
                IntercomState = "통화 연결"; ActionFeedback = "비상통화 인터폰 · 점검 통화와 상대의 응답을 확인하세요";
                if (!responseStarted && VoiceEnabled && Current.ResponseVoice != null) sound.PlayOneShot(Current.ResponseVoice, .85f);
                responseStarted = true;
            }
            if (action == CrewInspectionAction.CabinInspect && CabinLookIndex < CurrentPoint.InspectionTargets.Length)
            {
                var attention = CurrentPoint.InspectionTargets[CabinLookIndex];
                ActionFeedback = $"객실 관찰 {CabinLookIndex + 1}/3 · {attention.name}을 바라보세요";
                var direction = attention.position - Responder.PlayerCamera.transform.position;
                cabinLookElapsed = direction.magnitude < 6 && Vector3.Dot(Responder.PlayerCamera.transform.forward, direction.normalized) > .985f ? cabinLookElapsed + delta : 0;
                if (cabinLookElapsed >= .2f) { CabinLookIndex++; cabinLookElapsed = 0; }
                if (CabinLookIndex < CurrentPoint.InspectionTargets.Length) return;
            }
            if (action == CrewInspectionAction.ClimateAdjust && actionElapsed >= .35f)
            {
                CoolingAdjusted = true;
                if (ClimateDisplay != null) ClimateDisplay.text = "온도 낮춤\n설정 적용";
                ActionFeedback = "제어반 온도 설정 변경 · 냉방 설정을 낮췄습니다";
            }
            if (IsRadio(action) && ActionProgress > .35f)
            { SetRadio("RX", true); ActionFeedback = "무전 수신 · 상대의 수신 확인을 듣고 있습니다"; }
            float movement = Mathf.SmoothStep(0, 1, Mathf.Clamp01(actionElapsed / 1.6f));
            if (action == CrewInspectionAction.OpenDoor) SetDoor(Mathf.Lerp(actionDoorStart, 1, movement));
            if (action == CrewInspectionAction.CloseDoor) SetDoor(Mathf.Lerp(actionDoorStart, 0, movement));
            if (action == CrewInspectionAction.ClearLuggage)
            {
                // Acknowledgement precedes rolling the luggage into the clear position.
                float roll = Mathf.SmoothStep(0, 1, Mathf.Clamp01((actionElapsed - actionDuration * .35f) / (actionDuration * .55f)));
                Luggage.position = Vector3.Lerp(actionLuggageStart, LuggageClearPosition, roll);
            }
            if (actionElapsed < actionDuration) return;
            if (NeedsObservation && !LookingAtObservation())
            {
                ActionFeedback = "표식 쪽을 바라보고 실제 조치 결과를 확인하세요";
                return;
            }
            if (action == CrewInspectionAction.IntercomTest) { IntercomTested = true; IntercomState = "점검 통화 종료"; }
            if (action == CrewInspectionAction.CabinInspect) { CabinSectionsChecked++; lastCabinInspectionFeet = Responder.transform.position; }
            if (action == CrewInspectionAction.PaTest) PaTested = true;
            if (action == CrewInspectionAction.DoorInspect) DoorInspected = true;
            if (action == CrewInspectionAction.HoldDeparture) DepartureHeld = true;
            if (action == CrewInspectionAction.ClearLuggage) { Luggage.position = LuggageClearPosition; LuggageCleared = true; }
            Acting = false; ReadingResult = true; LastResult = Current.Result;
            if (action == CrewInspectionAction.RadioStatus) LastResult = $"전원 켜짐 · 배터리 {RadioBatteryPercent}%\n" + LastResult;
            ActionFeedback = "확인 완료 · 관찰 결과와 설명을 읽어보세요";
            if (IsRadio(action) || action == CrewInspectionAction.RadioStatus) SetRadio("READY", true);
            if (ConfirmationSound != null) effects.PlayOneShot(ConfirmationSound, .25f);
        }
        private bool LookingAtObservation()
        {
            var point = ObservationPoint;
            if (point == null) return false;
            var collider = point.GetComponent<Collider>();
            var direction = (collider != null ? collider.bounds.center : point.transform.position) - Responder.PlayerCamera.transform.position;
            return direction.magnitude < 6 && Vector3.Dot(Responder.PlayerCamera.transform.forward, direction.normalized) > .94f;
        }
        private void SetRadio(string mode, bool lit)
        {
            if (RadioDisplay != null) RadioDisplay.text = !RadioPowerOn ? "OFF" : mode == "TX" || mode == "RX" ? mode : $"{RadioBatteryPercent}%";
            if (RadioScreen == null) return;
            var color = !RadioPowerOn ? Color.black : mode == "TX" ? new Color(.85f, .57f, .16f) : new Color(.25f, .7f, .42f);
            RadioScreen.GetPropertyBlock(screenProperties);
            screenProperties.SetColor("_BaseColor", color * (lit ? 1 : .7f));
            screenProperties.SetColor("_EmissionColor", color * (lit ? .8f : .15f));
            RadioScreen.SetPropertyBlock(screenProperties);
        }
        private void SetDoor(float amount)
        {
            DoorOpenAmount = amount;
            if (closedDoorPositions == null) return;
            for (int i = 0; i < DoorLeaves.Length; i++) DoorLeaves[i].localPosition = closedDoorPositions[i] + DoorSlide * amount;
        }
        public void ReplayGuidance() { if (Started && !Finished && !Acting && !Responder.IsPaused) SpeakGuidance(); }
        private void SpeakGuidance()
        {
            coach.Stop(); if (VoiceEnabled && Current?.GuidanceVoice != null) { coach.clip = Current.GuidanceVoice; coach.Play(); }
        }
        public void ToggleVoice()
        {
            VoiceEnabled = !VoiceEnabled;
            if (VoiceEnabled) return;
            coach.Stop(); sound.Stop();
            if (!Acting || Current.ResponseVoice == null) return;
            if (Current.Action == CrewInspectionAction.ClearLuggage)
            {
                // Keep the luggage continuous when speech is cut during a request.
                actionLuggageStart = Luggage.position; actionElapsed = 0; actionDuration = 1.8f;
            }
            else actionDuration = Mathf.Max(1.8f, actionElapsed + .2f);
        }
        public void Continue()
        {
            if (!ReadingResult || Acting || Responder.IsPaused) return;
            coach.Stop(); sound.Stop(); ReadingResult = false; StepIndex++;
            if (StepIndex < Plan.Steps.Length) { SpeakGuidance(); return; }
            Finished = true; Responder.Pause(); view?.ShowComplete();
        }
        public void Resume() { if (Responder.Resume()) view?.HideModal(); }
        public void Exit() { CancelRadioSelection(); SceneFlow.ToTutorialMenu(); }
        private void OnPause(bool paused)
        {
            if (paused) { CancelRadioSelection(); coach.Pause(); sound.Pause(); effects.Pause(); } else { coach.UnPause(); sound.UnPause(); effects.UnPause(); }
        }
        private void OnDestroy() { if (Responder != null) Responder.PauseChanged -= OnPause; if (intercomCallTone != null) Destroy(intercomCallTone); }
        private void Update()
        {
            if (!ready || !Started || Finished) return;
            if (Responder.IsPaused) { view?.ShowPause(); return; }
            view?.HideModal();
            // Observe the action in game time; keyboard input remains blocked without focus.
            if (Acting) AdvanceAction(Time.deltaTime);
            if (!Responder.ExternalInputMode && !Responder.HasFocus) { CancelRadioSelection(); return; }
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var mouse = Mouse.current;
            StepRadioInput(keyboard.qKey.wasPressedThisFrame, keyboard.qKey.isPressed, mouse != null ? mouse.delta.ReadValue() : Vector2.zero,
                mouse != null && mouse.leftButton.wasPressedThisFrame, mouse != null && mouse.rightButton.wasPressedThisFrame);
            if (keyboard.enterKey.wasPressedThisFrame) Continue();
            if (keyboard.rKey.wasPressedThisFrame) ReplayGuidance();
            if (keyboard.vKey.wasPressedThisFrame) ToggleVoice();
        }
    }
}

