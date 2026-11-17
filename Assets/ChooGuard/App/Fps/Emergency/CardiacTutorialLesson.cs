using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Shell;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    public enum CardiacLessonStep
    {
        Preparing, Response, Help, Breathing, FirstCompressions, Relief, FetchAed, PlaceAed,
        OpenLid, Pads, Clear, Analysing, Shock, ResumeCompressions, Arrival, Handover, Complete, Stopped, Failed
    }

    /// <summary>Owns teaching and agency timing for the explicitly chosen cardiac lesson, never the ordinary shift.</summary>
    public sealed partial class CardiacTutorialLesson : MonoBehaviour
    {
        // Short teaching samples, not clinical CPR-cycle or survival thresholds.
        public const float FirstPracticeSeconds = 6f, ResumePracticeSeconds = 8f;
        public const int FixedSeed = 3222026;
        public CardiacLessonStep Step { get; private set; } = CardiacLessonStep.Preparing;
        public EmergencySession Session { get; private set; }
        public CollapseHazard Patient { get; private set; }
        public AedCabinet Cabinet { get; private set; }
        public PersonBody Colleague { get; private set; }
        public bool Ready { get; private set; }
        public bool ResponseChecked { get; private set; }
        public bool HelpRequested { get; private set; }
        public bool CallAcknowledged { get; private set; }
        public bool BreathingChecked { get; private set; }
        public bool ReliefRequested { get; private set; }
        public bool CorePracticeComplete { get; private set; }
        public bool AllowsAgencyTreatment => CorePracticeComplete || stopping;
        public bool Finished => Step == CardiacLessonStep.Complete || Step == CardiacLessonStep.Stopped;
        public string PreparationFailure { get; private set; } = "";
        public string DeviceInstruction { get; private set; } = "";
        public Vector3 PlayerStart { get; private set; }
        public Vector3 PatientStart { get; private set; }
        public Vector3 CabinetApproach { get; private set; }
        public readonly List<string> Evidence = new List<string>();
        public float PlayerFirstCompressionSeconds => firstShownSeconds;
        public float PlayerResumeCompressionSeconds => resumedSeconds;
        public const string ModelLimitation = "의복 제거 표현은 미구현입니다. 이 모델에서 패드 조작만 연습합니다. 실제 사용은 상의를 벗긴 맨가슴에 패드를 붙입니다.";

        Vector3 helperStart, helperKneel, agencyStart;
        float firstShownSeconds, resumedSeconds, liftConfirmedUntil;
        bool firstPracticeDone, helperWorking, arrivalStarted, stopping;
        CardiacCompressionVisual compressionVisual;
        CardiacColleagueCompressionVisual colleagueVisual;
        bool reliefEstablished, medicalTakenOver, hasFetchedAed;
        int appliedPads;
        public bool MayFetchAed => reliefEstablished && (helperWorking || hasFetchedAed);
        public Vector3 PadAimPoint
        {
            get
            {
                return PadSite(Cabinet.Unit.Procedure.Pads == 0 ? 0 : 1);
            }
        }
        AedProcedure wiredDevice;

        public void Initialize(EmergencySession session)
        {
            Session = session;
            CreateLessonView();
            try
            {
                ResolveLayout();
                var person = session.Crowd.SpawnLessonPatient(PatientStart, patientFacing);
                if (person == null) throw new InvalidOperationException("고정 환자 모델을 준비하지 못했습니다.");
                Patient = session.Incidents.StartCardiacLessonCase(person);
                if (Patient == null) throw new InvalidOperationException("현재 실내 형상에서 환자를 준비하지 못했습니다.");
                person.Body.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                person.gameObject.AddComponent<CardiacPatientBodyTarget>().Initialize(person.Body);
                var helper = Instantiate(session.Art.Crowd.Colleague, helperStart, Quaternion.identity, transform);
                helper.name = "응급처치 교육을 받은 동료 · 학습용 역할";
                Colleague = helper.GetComponent<PersonBody>();
                if (Colleague == null) throw new InvalidOperationException("기존 동료의 이동·자세 모델이 없습니다.");
                Colleague.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Colleague.Agent.stoppingDistance = .04f;
                Colleague.Home = transform;
                Colleague.Rescuer = true;
                Colleague.Stop();
                var facing = PatientStart - PlayerStart;
                session.Player.RestorePhysicalPose(PlayerStart, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 38);
                session.Crowd.StandBackFrom(PatientStart);
                compressionVisual = gameObject.AddComponent<CardiacCompressionVisual>();
                compressionVisual.Initialize(person.Body, session.Art.Crowd.Colleague, transform, session.Player.PlayerCamera);
                if (!compressionVisual.Ready) throw new InvalidOperationException(compressionVisual.FailureReason);
                InitializePosture();
                colleagueVisual = gameObject.AddComponent<CardiacColleagueCompressionVisual>();
                colleagueVisual.Initialize(Colleague, person.Body);
                if (!colleagueVisual.Ready) throw new InvalidOperationException(colleagueVisual.FailureReason);
                Ready = true;
                SetStep(CardiacLessonStep.Response);
                Note("fixed-patient-and-player-ready; original-aed=" + Cabinet.transform.position);
            }
            catch (Exception error)
            {
                PreparationFailure = error.Message;
                SetStep(CardiacLessonStep.Failed);
                Debug.LogException(error);
            }
            if (Session.Pause != null) Session.Pause.ContinueButton.interactable = Ready;
        }

        void Note(string fact, string visibleRecord = null)
        {
            Evidence.Add((Session != null ? Session.ShiftSeconds.ToString("F2") : "0") + ":" + fact);
            Debug.Log("CG_CARDIAC_LESSON " + fact);
            if (!string.IsNullOrEmpty(visibleRecord)) Session?.Log?.Add("심정지 학습 · " + visibleRecord);
        }

        void SetStep(CardiacLessonStep next)
        {
            if (Step == next) return;
            Step = next;
            Note("step:" + next);
        }

        public string CheckPatient(Passenger person)
        {
            if (!Ready || Patient?.Person != person) return null;
            if (!ResponseChecked)
            {
                ResponseChecked = true;
                Note("player-checked-response; no-response", "반응 없음 직접 확인");
                return "불러도 반응이 없습니다. Q → 요청·기관 → 119 구급으로 신고와 동료 지원을 요청하세요.";
            }
            if (!HelpRequested) return "반응 없음 확인 · 먼저 Q로119 신고와 AED 도움을 요청하세요.";
            if (!BreathingChecked)
            {
                BreathingChecked = true;
                Note("player-observed-breathing; not-normal", "정상 호흡 없음 직접 확인");
                return "정상 호흡이 없습니다. 환자를 바라보고 R로 가슴압박을 시작하세요. 동료가119 신고를 맡았습니다.";
            }
            return null;
        }

        public bool CanRequestHelp(out string reason)
        {
            reason = !ResponseChecked ? "먼저 가까이에서 환자를 보고 E로 반응을 확인하세요." : "";
            return ResponseChecked;
        }

        public void OnHelpRequested()
        {
            if (HelpRequested) return;
            HelpRequested = true;
            Colleague.SetPhone(true);
            StartCoroutine(AcknowledgeCall());
            Note("player-requested-119-and-aed-support-with-observed-location", "위치·반응을 전달하고119 신고와 AED 지원 요청");
        }

        IEnumerator AcknowledgeCall()
        {
            yield return new WaitForSeconds(1.5f);
            if (Finished) yield break;
            Colleague.SetPhone(false);
            CallAcknowledged = true;
            Session.Hud.Radio.Push(Hud.RadioChannel.Colleague, "119에 위치와 반응 없음 전달했습니다. 환자 호흡을 확인하고 압박을 시작하세요. 교대 요청을 받으면 제가 이어가겠습니다.");
            Note("colleague-119-call-acknowledged", "동료의119 신고 전달 확인");
        }

        public void RequestRelief()
        {
            if (!Ready || !firstPracticeDone) { Session.Hud.Toast("이 연습에서는 먼저 직접 압박을 익힌 뒤 동료에게 교대를 요청합니다."); return; }
            if (helperWorking || ReliefRequested && !reliefEstablished) { Session.Hud.Toast(helperWorking ? "동료가 환자 곁에서 압박을 이어가고 있습니다." : "동료가 환자 곁으로 이동 중입니다. 압박을 이어가세요."); return; }
            ReliefRequested = true;
            reliefEstablished = false;
            reliefRequestedAt = Time.time;
            var side = Vector3.Cross(Vector3.up, PatientAxis).normalized;
            if (Vector3.Dot(side, Session.Player.transform.position - PatientChest) > 0) side = -side;
            helperKneel = StationWorld.OnNavMesh(new Vector3(PatientChest.x, PatientStart.y, PatientChest.z) + side * .48f, .3f);
            if (!Colleague.GoTo(helperKneel, helperWalkSpeed)) { ReliefRequested = false; Session.Hud.Toast("동료가 접근할 경로를 확인하지 못했습니다. 환자 곁 통로를 확인하세요."); return; }
            Session.Hud.Radio.Push(Hud.RadioChannel.Colleague, "현재 순회 위치에서 갑니다. 약40초 걸립니다. 제가 곁에서 교대를 알릴 때까지 압박을 계속해 주세요.");
            Note("player-requested-compression-relief", "동료에게 압박 교대 요청");
        }

        public bool CanStartPlayerCompressions(out string reason)
        {
            reason = !BreathingChecked ? "이 연습에서는 반응·신고 요청·호흡 확인을 먼저 익힙니다. 현재 목표를 확인하세요." : "";
            if (!BreathingChecked) return false;
            if (medicalTakenOver) { reason = "구급대가 처치를 이어받았습니다. 구급대장에게 확인한 상황을 전달하세요."; return false; }
            if (helperWorking) { reason = "동료가 압박을 이어가고 있습니다. 현재 AED 조작 목표를 수행하세요."; return false; }
            if (!SurfaceReady) { reason = "환자가 바닥에 누운 자세를 잡는 중입니다. 가슴 곁에서 잠시 확인하세요."; return false; }
            float feetDistance = Vector3.ProjectOnPlane(PatientChest - Session.Player.transform.position, Vector3.up).magnitude;
            if (feetDistance > .65f || !FindCompressionEyePosition())
            {
                Debug.Log("CG_CARDIAC_REACH_REFUSED feet=" + feetDistance + " " + compressionVisual.ReachShape);
                reason = "환자 가슴 옆으로 더 가까이 다가가세요. 압박할 자리가 표시됩니다."; return false;
            }
            return true;
        }

        public void PlayerStartedCompressions()
        {
            compressionFeet = Session.Player.transform.position;
            if (!helperWorking) return;
            Patient.AssistedCompressions = false;
            helperWorking = false;
            Colleague.SetTreat(false);
            Note("player-resumed; colleague-released-contact");
        }

        public void StandClear()
        {
            Patient.AssistedCompressions = false;
            helperWorking = false;
            compressionVisual.Hide(); colleagueVisual.Hide();
            Colleague.SetTreat(false);
            StartCoroutine(ColleagueStepBack());
            Note("player-called-clear; all-assisting-contact-stopped", "모두 물러나도록 알리고 접촉 중단");
        }

        IEnumerator ColleagueStepBack()
        {
            while (Colleague != null && Colleague.Kneeling && !Finished) yield return null;
            if (Colleague != null && !Finished) Colleague.GoTo(helperStandBack, 1.5f);
        }

        public bool GuardAedLift(out string reason)
        {
            reason = "";
            if (!Ready || Finished || Cabinet.Unit.Patient != Patient) return true;
            if (Cabinet.Unit.Procedure.Current == AedProcedure.Stage.Compressions)
            {
                if (Time.unscaledTime <= liftConfirmedUntil) { liftConfirmedUntil = 0; return true; }
                liftConfirmedUntil = Time.unscaledTime + 3f;
                reason = "압박 재개: 환자를 바라보고 R. AED를 옮기려면3초 안에 본체에서 R을 다시 누르세요 (전원이 꺼지고 패드를 회수합니다).";
                return false;
            }
            return true;
        }

        void Update()
        {
            RefreshLessonView();
            if (!Ready || Finished || Session.Player.IsPaused || Time.timeScale <= 0) { compressionVisual?.Hide(); colleagueVisual?.Hide(); return; }
            if (Patient == null || Patient.Person == null) { PreparationFailure = "학습 대상이 사라졌습니다. 다시 연습하세요."; SetStep(CardiacLessonStep.Failed); return; }
            hasFetchedAed |= !Cabinet.Stored;
            appliedPads = Mathf.Max(appliedPads, Cabinet.Unit.Procedure.Pads);
            var device = Cabinet.Unit.Procedure;
            if (Patient.Aed != null && wiredDevice != Patient.Aed)
            {
                if (wiredDevice != null) wiredDevice.Voice -= DeviceSaid;
                wiredDevice = Patient.Aed;
                wiredDevice.Voice += DeviceSaid;
            }
            bool presenting = Patient.Compressing && compressionVisual.HandsInView(Session.Player.PlayerCamera) && !Session.Wheel.Open;
            if (presenting && !firstPracticeDone && Patient.Aed == null) firstShownSeconds += Time.deltaTime;
            if (firstShownSeconds >= FirstPracticeSeconds) firstPracticeDone = true;
            if (presenting && Patient.Aed != null && Patient.Aed.Current == AedProcedure.Stage.Compressions && Patient.Aed.Analyses > 0) resumedSeconds += Time.deltaTime;
            bool reliefPose = UpdateColleagueRelief(presenting);
            compressionVisual.Present(Session.Player.transform.position, PatientChest, PatientAxis,
                Patient.Compressing && !Session.Wheel.Open, Patient.CompressionSeconds);
            if (Colleague.Kneeling) Colleague.FacePose(PatientChest, 240);
            colleagueVisual.Present(PatientChest, PatientAxis, helperWorking || reliefPose, helperWorking ? Time.time : 0);
            if (helperWorking) Patient.AssistedCompressions = colleagueVisual.IsPresenting;
            CorePracticeComplete |= ResponseChecked && HelpRequested && CallAcknowledged && BreathingChecked && firstPracticeDone && reliefEstablished &&
                Patient.Aed != null && Patient.Aed.Pads >= 2 && Patient.Aed.ClearCalls > 0 && Patient.Aed.Analyses > 0 &&
                (Patient.Aed.LastAdvice == false || Patient.Aed.Shocks > 0) && resumedSeconds >= ResumePracticeSeconds;
            if (CorePracticeComplete && !arrivalStarted) StartCoroutine(ArriveAfterTeachingTransition());
            if (arrivalStarted && !stopping)
            {
                if (!medicalTakenOver && Session.Incidents.CardiacLessonHasMedicalContact)
                {
                    medicalTakenOver = true;
                    Patient.StopCompressions("구급대원이 실제 환자 곁에서 압박 교대");
                    Patient.Aed?.HandOver();
                    compressionVisual.Hide();
                    Note("medical-actual-contact-takeover", "구급대원이 환자 곁에서 처치와 AED 관리 교대");
                }
                if (!Patient.Compressing && !medicalTakenOver) SetStep(CardiacLessonStep.ResumeCompressions);
                else if (Step != CardiacLessonStep.Arrival) SetStep(CardiacLessonStep.Handover);
            }
            if (!arrivalStarted && !stopping) UpdateTeachingStep();
        }

        void UpdateTeachingStep()
        {
            var p = Cabinet.Unit.Procedure;
            var next = !ResponseChecked ? CardiacLessonStep.Response : !HelpRequested ? CardiacLessonStep.Help : !BreathingChecked ? CardiacLessonStep.Breathing :
                !firstPracticeDone ? CardiacLessonStep.FirstCompressions : !reliefEstablished || !helperWorking && Patient.Aed == null && Session.Hands.Aed == null ? CardiacLessonStep.Relief :
                Session.Hands.Aed != null ? CardiacLessonStep.PlaceAed : Cabinet.Unit.Patient != Patient ? CardiacLessonStep.FetchAed :
                p.Current == AedProcedure.Stage.Off ? CardiacLessonStep.OpenLid : p.Current == AedProcedure.Stage.PadsWanted ? CardiacLessonStep.Pads :
                p.Current == AedProcedure.Stage.ClearWanted ? CardiacLessonStep.Clear : p.Current == AedProcedure.Stage.Analysing ? CardiacLessonStep.Analysing :
                p.Current == AedProcedure.Stage.ShockAdvised ? CardiacLessonStep.Shock : CardiacLessonStep.ResumeCompressions;
            SetStep(next);
        }

        IEnumerator ArriveAfterTeachingTransition()
        {
            arrivalStarted = true; SetStep(CardiacLessonStep.Arrival);
            ShowTimeTransition(true);
            yield return new WaitForSeconds(2.5f);
            if (Finished) { ShowTimeTransition(false); yield break; }
            bool arrived = Session.Incidents.ArriveCardiacLessonTeam(agencyStart);
            ShowTimeTransition(false);
            if (!arrived) { arrivalStarted = false; UpdateTeachingStep(); yield break; }
            SetStep(CardiacLessonStep.Handover);
            Note("explicit-teaching-time-transition; medical-team-walks-in", "도착 대기 시간을 교육용으로 축약한 뒤 구급대 접근");
        }

        public string TryHandover(Responder responder)
        {
            if (!CorePracticeComplete && !stopping) return "현재 목표의 실습이 남아 있습니다. 정상 이수 또는 연습 중단을 선택하세요.";
            if (responder == null || responder.Agency != Agency.Medical || !responder.OnScene || !responder.Lead) return "접근을 마친 구급대장에게 상황을 전달하세요.";
            if (!medicalTakenOver) return "구급대원이 실제로 처치를 이어받기 전까지 환자에 R로 압박을 이어가세요. 교대 후 상황을 전달합니다.";
            SetStep(stopping ? CardiacLessonStep.Stopped : CardiacLessonStep.Complete);
            RestorePosture();
            Patient.AssistedCompressions = false; Patient.StopCompressions("구급대에 직접 상황 전달"); Colleague.SetTreat(false);
            Note("player-direct-handover; observed-actions-only; clothing-removal-not-implemented", "관찰한 상태와 실시한 조치를 구급대장에게 직접 인계 · 의복 제거 표현 미구현");
            Session.Incidents.FinishCardiacLesson(stopping);
            return "관찰한 상태와 실제 실시한 조치·미구현 범위를 전달했습니다. 구급대가 이어서 확인합니다.";
        }

        public void StopPractice()
        {
            if (!Ready || Finished || stopping) return;
            stopping = true;
            RestorePosture();
            Patient.AssistedCompressions = false; Patient.StopCompressions("사용자가 연습 중단 선택"); Colleague.SetTreat(false);
            SetStep(CardiacLessonStep.Stopped);
            Note("learner-stopped; not-a-completed-practice");
            Session.Incidents.FinishCardiacLesson(true);
        }

        public void Restart()
        {
            if (!PracticeShift.Arm(PracticeShift.CardiacFamily)) return;
            SceneFlow.StartShift(Session.KoreanFont, null);
        }

        void DeviceSaid(string line) { DeviceInstruction = line; }
        public void ReceiveDeviceInstruction(string line) => DeviceSaid(line);
        public bool AimingAtPadSite()
        {
            // The product ray has already selected the nearest solid patient collider, so walls are never skipped.
            if (!ReferenceEquals(Session.Player.CurrentInteraction, Patient.Person)) return false;
            var eye = Session.Player.PlayerCamera.transform;
            var delta = PadAimPoint - eye.position;
            float along = Vector3.Dot(delta, eye.forward);
            return along > 0 && along <= Session.Player.InteractionDistance + .25f &&
                (delta - eye.forward * along).sqrMagnitude <= .22f * .22f;
        }
        void OnDestroy()
        {
            RestorePosture();
            if (wiredDevice != null) wiredDevice.Voice -= DeviceSaid;
            if (Patient != null) Patient.AssistedCompressions = false;
        }
    }
}
