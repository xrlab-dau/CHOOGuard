using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    public sealed partial class IncidentDirector
    {
        private readonly HandoverChecklist handoverReview = new HandoverChecklist();
        private bool reviewingHandover;
        private readonly Dictionary<Hazard, float> observedAt = new Dictionary<Hazard, float>();
        private readonly Dictionary<Hazard, float> workFinishedAt = new Dictionary<Hazard, float>();
        private readonly HashSet<Hazard> verifiedWork = new HashSet<Hazard>();
        private int assignmentRevision;
        private static readonly string[] ReviewNames = { "인지·상황", "관측·부상자", "보고·통제·대피", "기관·작업·미확인 사항" };

        private string[] HandoverSections()
        {
            // Only facts available to this player enter the handover. Hazard.Handover reads hidden world state.
            var observed = knownOrder.Where(h => known.Contains(h)).ToArray();
            string situation = observed.Length == 0 ? "확인한 비상상황 없음" : string.Join(" / ", observed.Select(h => h.Named));
            string conditions = observed.Length == 0 ? "현장 상태 미확인" : string.Join(" / ", observed.Select(h => h.Named + ": " + (seen.TryGetValue(h, out var value) ? value : "직접 살펴본 상태 없음")));
            string teams = string.Join(" / ", calledBy.Keys.OrderBy(a => a).Select(a => Teams.Name(callTeam.TryGetValue(a, out var unit) ? unit : Teams.For(a, TargetFor(a))) + ": " + (Lead(a) is Responder lead ? lead.OnScene ? "현장 도착" : "이동 중" : "출동 중") + " · 지정 현장: " + (TargetFor(a) is Hazard assigned && known.Contains(assigned) ? assigned.Named : "미확인")));
            string work = string.Join(" / ", workFinishedAt.Keys.Where(h => known.Contains(h)).OrderBy(h => h.Id, StringComparer.Ordinal).Select(h => h.Named + (verifiedWork.Contains(h) ? " · 직접 재확인 보고" : " · 기관 완료 통보, 직접 재확인 전")));
            return new[]
            {
                situation + (PlayerKnowsIncident ? " · 인지 경로: " + knownHow : " · 인지 시점 미확인"),
                conditions + " · 직접 확인한 부상자 " + injuredKnown.Count(p => p != null) + "명; 전체 인원은 미확인",
                "보고 " + reported.Count(h => known.Contains(h)) + "건 · 직접 대피 안내 " + log.Guided + "명 · 설치한 통제선 " + staffCordons.Count + "개",
                (teams.Length > 0 ? teams : "기관 요청 없음") + " · 지시 기록 " + assignmentRevision + " · " + (work.Length > 0 ? work : "확인된 기관 작업 완료 통보 없음") + " · 추가 위험/누락 인원/설비 복구 여부는 확인한 범위 밖에서 미확인"
            };
        }

        private List<EmergencySession.RadioOption> WorkflowOptions()
        {
            var options = new List<EmergencySession.RadioOption>();
            if (handedOver || Stage == Phase.Ended) return options;
            var target = RequestTarget();
            if (target != null && known.Contains(target))
            {
                foreach (var agency in calledBy.Keys.OrderBy(a => a))
                {
                    var a = agency;
                    options.Add(Option(Responder.AgencyName(a) + " → " + target.Named + " 재배치", () =>
                    {
                        TryReassignTeam(a, target, out var feedback);
                        session.Hud.Toast(feedback, 5f);
                    }));
                }
            }
            // Completed non-fire tasks are inactive and no longer RequestTarget candidates. They still need review.
            foreach (var completedTarget in workFinishedAt.Keys.Where(h => known.Contains(h) && !verifiedWork.Contains(h)).OrderBy(h => h.Id, StringComparer.Ordinal))
            {
                var reviewTarget = completedTarget;
                options.Add(Option(reviewTarget.Named + " · 작업 결과 재확인 보고", () =>
                    {
                        if (!known.Contains(reviewTarget) || !workFinishedAt.TryGetValue(reviewTarget, out float latestCompletion) || !seen.ContainsKey(reviewTarget) || !observedAt.TryGetValue(reviewTarget, out float observed) || observed <= latestCompletion)
                        { Office("완료 통보 뒤 현장을 직접 살펴보고 재확인 바랍니다."); return; }
                        verifiedWork.Add(reviewTarget);
                        Spoke();
                        Say(reviewTarget.Named + " 작업 뒤 재확인: " + seen[reviewTarget]);
                        log.Add("기관 작업 결과 직접 재확인 보고 · " + reviewTarget.Named);
                    }));
            }
            if (reviewingHandover)
            {
                handoverReview.Update(HandoverSections());
                for (int i = 0; i < ReviewNames.Length; i++)
                {
                    int section = i;
                    options.Add(Option("인계 검토 · " + ReviewNames[i] + (handoverReview.Reviewed(i) ? " ✓" : ""), () =>
                    {
                        handoverReview.Update(HandoverSections());
                        handoverReview.Review(section);
                        Say("인계 검토 · " + ReviewNames[section] + ": " + handoverReview.Section(section));
                        Fact("인계 검토 · " + ReviewNames[section]);
                    }));
                }
            }
            return options;
        }

        /// <summary>Reassigns an existing team to a perceived, compatible task without teleporting or silently discarding hands-on work.</summary>
        public bool TryReassignTeam(Agency agency, Hazard target, out string feedback)
        {
            feedback = "";
            if (handedOver || Stage != Phase.Incident || target == null || !known.Contains(target) || !target.Localized)
            { feedback = "직접 인지한 현장만 재배치 대상으로 지정할 수 있습니다"; return false; }
            if (!calledBy.ContainsKey(agency)) { feedback = "먼저 해당 기관 출동을 요청하세요"; return false; }
            var team = callTeam.TryGetValue(agency, out var unit) ? unit : Teams.For(agency, TargetFor(agency));
            if (!Teams.Fits(team, target)) { feedback = Teams.Name(team) + "의 작업 범위에 맞지 않습니다"; return false; }
            var members = responders.Where(r => r != null && r.Agency == agency).ToArray();
            if (members.Any(r => !r.CanReassign)) { feedback = "현재 작업·환자 지원·승강 이동을 마친 뒤 재배치할 수 있습니다"; return false; }
            callTarget[agency] = target;
            assignmentRevision++;
            float standOff = agency == Agency.Police ? 9 : agency == Agency.Fire ? 4 : agency == Agency.Medical ? 1 : 3;
            foreach (var member in members)
                member.Reassign(target, world.Approach(target.Scene, member.transform.position, standOff + member.Index * 1.2f));
            verifiedWork.Remove(target);
            nextTeamMark = 0;
            log.Add(Teams.Name(team) + " 재배치 지시 · " + target.Named);
            Say(Teams.Name(team) + ", " + target.Named + " 현장으로 재배치 바랍니다.");
            feedback = members.Length == 0 ? "출동 중인 팀의 도착 목적지를 변경했습니다" : "기관에 재배치를 지시했습니다 · 실제 이동 후 도착";
            return true;
        }

        private string ReviewAndHandOver(Responder responder)
        {
            if (responder == null || !CanHandOver(responder)) return "현장 지휘 기관의 도착한 팀장에게 인계해야 합니다";
            handoverReview.Update(HandoverSections());
            if (!reviewingHandover)
            {
                reviewingHandover = true;
                return "Q 업무·인계에서 네 항목을 검토한 뒤 팀장에게 다시 인계하세요";
            }
            if (!handoverReview.Ready) return "상황이 바뀌었거나 검토가 남았습니다 · Q 업무·인계에서 네 항목을 확인하세요";
            handedOver = true;
            string team = Teams.Name(responder.Team);
            string summary = string.Join("; ", Enumerable.Range(0, handoverReview.Count).Select(i => ReviewNames[i] + ": " + handoverReview.Section(i)));
            Say(team + "에 인계합니다: " + summary + ".");
            session.Hud.Radio.Push(ChannelOf(responder.Agency), "검토 내용을 인계받았습니다. 미확인 사항을 포함해 후속 대응하겠습니다.");
            log.Add(team + "에 검토한 현장 인계: " + summary);
            Invoke(nameof(FinishAfterHandover), 3f);
            return "검토 내용을 " + team + "에 인계했습니다";
        }
    }
}
