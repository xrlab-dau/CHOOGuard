#if CHOOGUARD_DOOR_CLOSER_FAULT
using System;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Facilities
{
    /// <summary>
    /// A lost closer return on an existing leaf. The existing door owns the hand animation and collider.
    /// Staff diagnose by watching a released leaf, close it by hand, secure it, and test the repaired closer.
    /// No object is spawned, moved into place, or certified as surveyed by this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorOperatorFault : MonoBehaviour
    {
        public StationDoor Door { get; private set; }
        public bool Active => faulted && !Verified;
        public bool SuppressCloser => Active && !Serviced;
        public bool ManualBlocked => Active && Secured;
        public bool Diagnosed { get; private set; }
        public bool Secured { get; private set; }
        public bool ServiceAuthorized { get; private set; }
        public bool Serviced { get; private set; }
        public bool VerificationReleased { get; private set; }
        public bool Verified { get; private set; }
        public bool FailedReturnWitnessed => failedReturnSeconds >= WitnessSeconds;
        public const float WitnessSeconds = StationDoor.CloserDelay + StationDoor.SwingCloseSeconds + .4f;
        public event Action<string> Changed;

        private FirstPersonResponder staff;
        private Responder serviceWorker;
        private bool faulted, previousHeld, verificationOpened, releaseWitnessed;
        private float failedReturnSeconds, previousOpen, automaticReturn;

        public string StateText => Verified ? "도어 클로저 자동 닫힘 확인 · 복구" :
            Serviced ? (Secured ? "시설 수리 완료 · 현장 개방 시험 대기" : "실제 문짝 개방·놓기·자동 닫힘 확인 필요") :
            ServiceAuthorized ? "출입 통제 · 시설 담당 현장 작업" :
            Secured ? "문 수동 폐쇄·출입 통제 · 시설 담당 점검 대기" :
            Diagnosed ? "도어 클로저 복귀 불량 확인 · 문짝 수동 폐쇄 필요" :
            "놓은 문짝이 자동으로 닫히지 않음 · 가까이에서 복귀 확인";

        public string SecondaryPrompt => !Active ? "" :
            !Diagnosed ? "놓은 문짝의 자동 복귀 불량 확인" :
            Serviced && Secured ? "수리 후 개방 시험을 위해 통제 해제" :
            Secured ? "출입 통제 상태 확인" :
            VerificationReleased ? "문짝을 열고 놓아 자동 닫힘 확인" :
            "닫힌 문 출입 통제";

        public static bool CanFail(StationDoor door)
        {
            if (door == null || !door.isActiveAndEnabled || !door.Usable || door.Kind != StationDoor.DoorKind.Swing ||
                door.IsBeingHeld || door.Open < .2f || door.OperatorFault != null && door.OperatorFault.Active) return false;
            foreach (var leaf in door.Leaves)
            {
                if (leaf.Pivot == null || !leaf.Pivot.gameObject.activeInHierarchy || Mathf.Abs(leaf.Swing) < 1 || leaf.Length < .2f) continue;
                foreach (var collider in leaf.Pivot.GetComponentsInChildren<Collider>())
                    if (collider.enabled && !collider.isTrigger) return true;
            }
            return false;
        }

        public bool BeginFailure(StationDoor door, FirstPersonResponder onDutyStaff)
        {
            if (faulted || onDutyStaff == null || !CanFail(door) || door.gameObject != gameObject) return false;
            Door = door; staff = onDutyStaff; faulted = true; previousOpen = door.Open;
            door.OperatorFault = this;
            return true;
        }

        private bool AtHandle(FirstPersonResponder responder)
        {
            if (responder == null || responder != staff || responder.IsPaused || Door == null || responder.PlayerCamera == null) return false;
            var local = Door.Local(responder.transform.position);
            if (Mathf.Abs(local.y) > .7f || new Vector2(local.x, local.z).magnitude > 2.1f) return false;
            var eye = responder.PlayerCamera.transform;
            var handle = Door.World(new Vector3(0, 1.05f, 0));
            var ray = handle - eye.position;
            if (Vector3.Dot(eye.forward, ray.normalized) < .65f) return false;
            if (Physics.Linecast(eye.position, handle, out var hit, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(Door.transform)) return false;
            return true;
        }

        private bool OpeningOccupied()
        {
            if (staff != null && Door.InDoorway(staff.transform.position)) return true;
            foreach (var body in PersonBody.All)
                if (body != null && body.Visible && Door.InDoorway(body.transform.position)) return true;
            return false;
        }

        /// <summary>Called by the existing door driver once per physical frame, before its normal closer calculation.</summary>
        public void Advance(float dt, bool held)
        {
            if (!Active || Door == null || !Door.isActiveAndEnabled || dt <= 0) return;
            bool watching = AtHandle(staff);
            if (!Serviced)
            {
                // A staff member must actually stay at the released leaf for one failed return stroke.
                if (!Diagnosed) failedReturnSeconds = watching && !held && Door.Open >= .2f ? failedReturnSeconds + dt : 0;
            }
            else if (VerificationReleased)
            {
                if (held)
                {
                    if (Door.Open >= .35f) verificationOpened = true;
                    releaseWitnessed = false; automaticReturn = 0;
                }
                else if (previousHeld && verificationOpened && watching)
                {
                    releaseWitnessed = true; automaticReturn = 0;
                }
                if (releaseWitnessed && !held)
                {
                    if (!watching) { releaseWitnessed = false; verificationOpened = false; automaticReturn = 0; }
                    else
                    {
                        automaticReturn += Mathf.Max(0, previousOpen - Door.Open);
                        if (automaticReturn >= .25f && Door.Open <= .03f)
                        {
                            Verified = true;
                            Changed?.Invoke("실제 문짝을 열었다 놓고 도어 클로저 자동 닫힘을 확인했습니다");
                        }
                    }
                }
            }
            previousHeld = held; previousOpen = Door.Open;
        }

        public bool TrySecondary(FirstPersonResponder responder, out string feedback)
        {
            feedback = "";
            if (!Active) return false;
            if (!AtHandle(responder)) { feedback = "근무 중인 직원이 같은 층의 문 가까이에서 직접 확인하세요"; return false; }
            var hands = EmergencySession.Current != null && EmergencySession.Current.Player == responder ? EmergencySession.Current.Hands : null;
            if (responder.Holding || Door.IsBeingHeld || hands != null && (hands.Held != null || hands.Hose != null || hands.Aed != null))
            { feedback = "장비와 문짝을 놓은 뒤 현장 상태를 확인하세요"; return false; }
            if (!Diagnosed)
            {
                if (!FailedReturnWitnessed) { feedback = "문을 놓은 채 가까이에서 자동 닫힘이 돌아오는지 충분히 지켜보세요"; return false; }
                Diagnosed = true;
                feedback = "자동 복귀 불량 확인 · E를 누른 채 문짝을 직접 닫고, 문턱을 비운 뒤 R로 출입 통제하세요";
            }
            else if (Serviced && Secured)
            {
                if (OpeningOccupied()) { feedback = "문턱에 사람이 있습니다 · 개방 시험 전 주변을 비워 주세요"; return false; }
                Secured = false; VerificationReleased = true; verificationOpened = releaseWitnessed = previousHeld = false;
                automaticReturn = 0; previousOpen = Door.Open;
                feedback = "수리 후 개방 시험 · E로 문을 충분히 열고 놓은 뒤, 가까이서 자동 닫힘까지 확인하세요";
            }
            else if (Secured || VerificationReleased)
            { feedback = StateText; return false; }
            else
            {
                if (Door.Open > .03f) { feedback = "문짝을 E로 직접 끝까지 닫아 주세요 · 통제만으로 문은 닫히지 않습니다"; return false; }
                if (OpeningOccupied()) { feedback = "문턱에 사람이 있습니다 · 비운 뒤 출입 통제하세요"; return false; }
                Secured = true;
                feedback = "문을 수동 폐쇄하고 출입을 통제했습니다 · 무전으로 시설 담당 현장 점검을 요청하세요";
            }
            Changed?.Invoke(feedback); return true;
        }

        /// <summary>JEV's related service clearance; physical isolation remains a live precondition.</summary>
        public bool AuthorizeService(Responder worker)
        {
            if (!Active || !Diagnosed || !Secured || Serviced || ServiceAuthorized || Door.Open > .03f || OpeningOccupied() || !WorkerAtDoor(worker, 4f)) return false;
            serviceWorker = worker;
            ServiceAuthorized = true;
            Changed?.Invoke("시설 담당이 현장에서 폐쇄·통제를 확인하고 클로저 정비를 준비했습니다");
            return true;
        }

        /// <summary>The existing facility worker calls this after the existing timed work, never from staff R.</summary>
        public bool CompleteService()
        {
            if (!Active || !Secured || !ServiceAuthorized || Serviced || Door == null || Door.Open > .03f || OpeningOccupied() || !WorkerAtDoor(serviceWorker, 2f)) return false;
            Serviced = true;
            Changed?.Invoke("시설 담당이 클로저를 정비했습니다 · 통제를 유지하고 실제 개방·자동 닫힘 시험을 기다립니다");
            return true;
        }

        private void OnDestroy()
        {
            if (Door != null && Door.OperatorFault == this) Door.OperatorFault = null;
        }

        private bool WorkerAtDoor(Responder worker, float reach)
        {
            if (worker == null || !worker.gameObject.activeInHierarchy || !worker.OnScene || !worker.Worker ||
                worker.Team != Team.Facility || worker.Agency != Agency.Facility || Door == null) return false;
            var at = Door.World(new Vector3(0, 0, .8f));
            return Mathf.Abs(worker.transform.position.y - at.y) < .7f && Vector3.Distance(worker.transform.position, at) < reach;
        }
    }
}
#endif
