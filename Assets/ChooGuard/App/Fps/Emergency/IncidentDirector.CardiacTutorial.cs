using System.Linq;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    public sealed partial class IncidentDirector
    {
        public CollapseHazard StartCardiacLessonCase(Passenger person)
        {
            if (!session.IsCardiacTutorial || person == null || Stage != Phase.Calm) return null;
            StartMedical(person, ChestPain, 1f);
            return casualties.LastOrDefault(c => c.Person == person);
        }

        private bool RequestCardiacHelp()
        {
            var lesson = session.CardiacLesson;
            if (lesson == null) return false;
            if (!lesson.CanRequestHelp(out var reason)) { session.Hud.Toast(reason); return true; }
            var target = lesson.Patient;
            reported.Add(target); lastReported = target; focus = target;
            Say("역무실, " + target.Where + "에 반응 없는 승객1명 있습니다. 119 신고와 AED 지원 바랍니다. " +
                (lesson.BreathingChecked ? "정상 호흡이 없습니다." : "호흡은 확인하겠습니다."));
            Call(Agency.Medical, "역무원이 위치·반응을 전달하고119 신고 요청", target, Team.Ems);
            Office("위치 접수했습니다. 가까운 동료가119 신고를 맡습니다. 환자 호흡을 확인하고 압박을 이어가세요.");
            lesson.OnHelpRequested();
            return true;
        }

        public bool ArriveCardiacLessonTeam(Vector3 entrance)
        {
            var lesson = session.CardiacLesson;
            if (lesson == null || !lesson.AllowsAgencyTreatment) return false;
            if (responders.Any(r => r.Agency == Agency.Medical)) return true;
            var target = lesson.Patient;
            var prefabs = Teams.Members(art.Crowd, Team.Ems);
            calledBy[Agency.Medical] = "역무원119 신고 요청";
            arrived.Add(Agency.Medical);
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;
                var start = StationWorld.OnNavMesh(entrance + Vector3.right * i * .8f, .4f);
                var go = Instantiate(prefabs[i], start, Quaternion.identity, root);
                go.name = Teams.Member(Team.Ems, i) + " · 학습 시간 경과 후 접근";
                var responder = go.AddComponent<Responder>();
                var goal = world.Approach(target.Position, start, 1.2f + i * .8f);
                responder.Setup(this, Agency.Medical, Team.Ems, i, goal, target, art);
                responders.Add(responder);
            }
            session.Hud.Radio.Push(RadioChannel.Medical, "신고받고 도착했습니다. 환자 쪽으로 접근합니다. 확인한 상태와 실시한 조치를 전달해 주세요.");
            log.Add("교육용 시간 경과 후 구급대가 같은층 통로에서 실제 접근 시작");
            return responders.Any(r => r.Agency == Agency.Medical);
        }

        public Responder CardiacLessonLead => responders.FirstOrDefault(r => r.Agency == Agency.Medical && r.Lead && r.OnScene);
        public bool CardiacLessonHasMedicalContact => session.CardiacLesson != null && responders.Any(r => r.Agency == Agency.Medical && r.ProvidingCareTo(session.CardiacLesson.Patient.Person));

        public void FinishCardiacLesson(bool stopped)
        {
            if (!session.IsCardiacTutorial) return;
            handedOver = !stopped;
            session.Log.Add(stopped ? "심정지 연습 중단 · 미이수" : "심정지 조작 학습 완료 · 의복 제거 표현 미구현 · 실제 구조 자격을 의미하지 않음");
            Finish(stopped ? "심정지 연습 중단 · 미이수" : "심정지 조작 학습 완료", stopped ? "tutorial_stopped" : "tutorial_completed");
        }
    }
}
