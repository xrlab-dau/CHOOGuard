using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using ChooGuard.Contracts;
using UnityEngine;
namespace ChooGuard.App.Fps.Work
{
    // 절차 정의를 읽어 단계 가용성을 판정한다. 모드를 모른다 — 두 모드가 이 하나를 공유한다.
    //
    // App/Mvp/MvpProgressionGraph.cs 에서 순수 로직만 이식했다: 자료 크기 상한, SHA-256 지문,
    // 구조 검증, 화이트리스트, guards[id]=RuleExpression.All(...) 컴파일, Revision CAS 커밋,
    // '대응 경로 없음'을 완료가 아니라 unavailable 로 거부하는 처리.
    // 바꾼 것 둘 — MonoBehaviour 결합 제거, 어휘를 RTS 대피·의료에서 정비·점검으로 교체.
    public sealed class ProcedureRunner
    {
        [Serializable] private sealed class StepDefinition
        {
            public string id,label,basis,effect,observe,failReason;
            public string[] requires,allFacts,notFacts;
            // 조건부 단계. 가드가 TRUE 가 아니면 막지 않고 건너뛴다.
            // (적합 판정에서는 '폐기·교체 요구 인계'가 발생하지 않는 것이 정상이다.)
            public bool conditional;
        }
        [Serializable] private sealed class Definition
        {
            public string id,title,basis,sourceNote;
            public int version;
            public StepDefinition[] steps;
        }

        public sealed class Step
        {
            public string Id,Label,Basis,Effect,Observe,FailReason;
            public IReadOnlyList<string> Requires;
            public bool Done;
            public bool Conditional;
            public int Index;
        }

        // 정비·점검 어휘. 정의 파일이 이 밖의 이름을 쓰면 로드가 실패한다.
        private static readonly HashSet<string> AllowedFacts=new HashSet<string>
        {
            "serial-gazed","spec-plate-gazed","gauge-gazed","body-gazed",
            "serial-recorded","verdict-recorded","verdict-fit","verdict-unfit",
            "corroded","mechanically-defective","pressure-out-of-range","expiry-passed",
            "repair-order-issued","tag-attached",
        };
        private static readonly HashSet<string> AllowedEffects=new HashSet<string>
        {
            "none","record-serial","record-verdict","issue-repair-order","attach-tag","close-inspection",
        };
        // v2 추가분 — 기술자 인계. 요청·도착·완료·확인을 따로 읽어야 "요청이 곧 완료"가 되지 않는다.
        // 판본을 올리지 않은 자료가 이 어휘를 쓰면 거부한다. 판본이 장식이 되면 검사할 이유가 없다.
        private static readonly HashSet<string> FactsAddedInV2=new HashSet<string>
        {
            "technician-received","technician-onsite","technician-completed",
            "replacement-witnessed","service-completed",
        };
        private static readonly HashSet<string> EffectsAddedInV2=new HashSet<string>{"witness-replacement"};
        // v3 추가분 — 플레이어가 적합·부적합을 실제로 골랐는가. 고르지 않았으면 판정 기재를 막는다.
        // 이것이 없으면 생성기가 정답을 미리 넣어 둔 채 E 만 누르면 되는 상태가 된다.
        private static readonly HashSet<string> FactsAddedInV3=new HashSet<string>{"verdict-selected"};
        // 읽을 수 있는 자료 판본. 알 수 없는 판본을 조용히 받아들이지 않는다.
        // v1·v2 자료는 계속 그대로 읽는다 — 기존 시험과 배포된 자료가 깨지지 않아야 한다.
        private const int LatestVersion=3;
        private static bool FactAllowed(string fact,int version)
            =>AllowedFacts.Contains(fact)
              ||(version>=2&&FactsAddedInV2.Contains(fact))
              ||(version>=3&&FactsAddedInV3.Contains(fact));
        private static bool EffectAllowed(string effect,int version)
            =>AllowedEffects.Contains(effect)||(version>=2&&EffectsAddedInV2.Contains(effect));

        public bool Ready { get; private set; }
        public string DefinitionHash { get; private set; }
        public string StatusReason { get; private set; }="절차 자료 확인 전";
        public string Id { get; private set; }="";
        public string Title { get; private set; }="";
        public string Basis { get; private set; }="";
        public int Revision { get; private set; }
        public IReadOnlyList<Step> Steps=>steps;
        public bool Completed { get; private set; }

        private readonly List<Step> steps=new List<Step>();
        private readonly Dictionary<string,RuleExpression> guards=new Dictionary<string,RuleExpression>();
        private readonly Dictionary<string,Step> byId=new Dictionary<string,Step>();

        public bool Load(TextAsset asset)
        {
            steps.Clear();guards.Clear();byId.Clear();Ready=false;Completed=false;Revision++;
            try
            {
                if(asset==null)throw new InvalidOperationException("절차 자료 없음");
                var bytes=asset.bytes;
                if(bytes.Length>131072)throw new InvalidOperationException("절차 자료 크기 초과");
                using(var sha=SHA256.Create())DefinitionHash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                var definition=JsonUtility.FromJson<Definition>(asset.text);
                if(definition==null||definition.version<1||definition.version>LatestVersion||string.IsNullOrEmpty(definition.id)||definition.steps==null
                   ||definition.steps.Length<1||definition.steps.Length>64)throw new InvalidOperationException("절차 구조 오류");
                ValidateId(definition.id);
                Id=definition.id;Title=definition.title??definition.id;Basis=definition.basis??"";
                for(int index=0;index<definition.steps.Length;index++)
                {
                    var source=definition.steps[index];
                    if(source==null)throw new InvalidOperationException("빈 단계");
                    ValidateId(source.id);
                    if(byId.ContainsKey(source.id))throw new InvalidOperationException("중복 단계 식별자 · "+source.id);
                    if(string.IsNullOrWhiteSpace(source.label))throw new InvalidOperationException("이름 없는 단계 · "+source.id);
                    if(string.IsNullOrWhiteSpace(source.basis))throw new InvalidOperationException("근거 조항 없는 단계 · "+source.id);
                    if(!EffectAllowed(source.effect??"none",definition.version))throw new InvalidOperationException("이 판본에서 허용되지 않은 효과 · "+source.effect+" (version "+definition.version+")");
                    var requires=source.requires??new string[0];
                    if(requires.Length>8)throw new InvalidOperationException("선행 단계 수 초과 · "+source.id);
                    // 선행 단계는 배열에서 앞에 나와야 한다. 이 규칙만으로 순환이 구조적으로 불가능해진다.
                    foreach(var required in requires)
                        if(!byId.ContainsKey(required))throw new InvalidOperationException("앞서 정의되지 않은 선행 단계 · "+source.id+" → "+required);
                    var expressions=new List<RuleExpression>();
                    AddFacts(expressions,source.allFacts,false,definition.version);
                    AddFacts(expressions,source.notFacts,true,definition.version);
                    guards[source.id]=RuleExpression.All(expressions);
                    var step=new Step
                    {
                        Id=source.id,Label=source.label,Basis=source.basis,Effect=source.effect??"none",
                        Observe=source.observe??"",FailReason=source.failReason??"지금은 이 단계를 수행할 수 없습니다",
                        Requires=Array.AsReadOnly(requires),Index=index,Conditional=source.conditional,
                    };
                    steps.Add(step);byId.Add(step.Id,step);
                }
                Ready=true;StatusReason="절차 준비됨 · "+Title;return true;
            }
            catch(Exception ex)
            {
                Ready=false;steps.Clear();guards.Clear();byId.Clear();
                StatusReason="절차 사용 불가 · "+ex.Message;
                Debug.LogError("[ProcedureRunner] "+StatusReason);  // 침묵 실패를 만들지 않는다.
                return false;
            }
        }

        private static void ValidateId(string id)
        {
            if(string.IsNullOrWhiteSpace(id)||id.Length>96)throw new InvalidOperationException("단계 식별 정보 오류");
            new StableId(id);
        }
        private static void AddFacts(List<RuleExpression> expressions,string[] facts,bool negate,int version)
        {
            if(facts==null)return;
            if(facts.Length>24)throw new InvalidOperationException("조건 수 초과");
            foreach(var fact in facts)
            {
                if(!FactAllowed(fact,version))throw new InvalidOperationException("이 판본에서 허용되지 않은 점검 조건 · "+fact+" (version "+version+")");
                var expression=RuleExpression.Fact(new StableId(fact));
                expressions.Add(negate?RuleExpression.Not(expression):expression);
            }
        }
        public static RuleFacts FactsFrom(IDictionary<string,RuleTruth> source)
        {
            var facts=new List<KeyValuePair<StableId,RuleTruth?>>();
            if(source!=null)foreach(var pair in source)
                facts.Add(new KeyValuePair<StableId,RuleTruth?>(new StableId(pair.Key),pair.Value));
            return new RuleFacts(facts);
        }

        public Step Find(string stepId)=>stepId!=null&&byId.TryGetValue(stepId,out var step)?step:null;
        // 선행 단계가 전부 끝났는가. 순서는 배열 위치가 아니라 requires 가 정한다(별표2 식 의존행렬 대비).
        public bool RequirementsMet(Step step)
        {
            if(step?.Requires==null)return false;
            foreach(var required in step.Requires){var prior=Find(required);if(prior==null||!prior.Done)return false;}
            return true;
        }
        public RuleTruth GuardTruth(Step step,RuleFacts facts)
            =>step!=null&&guards.TryGetValue(step.Id,out var guard)?guard.Evaluate(facts):RuleTruth.UNKNOWN;

        public sealed class Decision
        {
            public Step Step;
            public RuleTruth Guard;
            public string Reason;
            public bool Allowed;
            public RuleFacts Facts;
            internal int Revision;
        }

        // 지금 수행 가능한 첫 단계. 없으면 이유를 담아 거부한다 — 거부는 완료가 아니다.
        //
        // 조건부 단계는 가드가 TRUE 가 아니면 **막지 않고 건너뛴다.** 필수 단계는 막는다.
        // 이 구분이 데이터(conditional)에 있어야 하는 이유: 어느 단계가 분기인지는 규정이 정하고
        // 러너는 그것을 읽을 뿐이다. 코드에 단계 이름을 박으면 절차를 추가할 때마다 러너를 고쳐야 한다.
        public Decision Next(RuleFacts facts)
        {
            if(!Ready)return new Decision{Allowed=false,Reason=StatusReason,Guard=RuleTruth.UNKNOWN,Facts=facts,Revision=Revision};
            foreach(var step in steps)
            {
                if(step.Done)continue;
                if(!RequirementsMet(step))continue;
                var truth=GuardTruth(step,facts);
                if(truth==RuleTruth.TRUE)return new Decision{Step=step,Guard=truth,Allowed=true,Reason=step.Label,Facts=facts,Revision=Revision};
                if(step.Conditional)continue;                       // 해당 없음 — 다음 단계를 본다
                return new Decision
                {
                    Step=step,Guard=truth,Allowed=false,Facts=facts,Revision=Revision,
                    Reason=truth==RuleTruth.CONFLICTED?"관측이 서로 일치하지 않아 진행할 수 없습니다":step.FailReason,
                };
            }
            return new Decision{Allowed=false,Reason=IsComplete(facts)?"절차 완료":"수행할 단계가 없습니다",Guard=RuleTruth.UNKNOWN,Facts=facts,Revision=Revision};
        }

        public bool Commit(Decision decision)
        {
            if(!Ready||decision==null||!decision.Allowed||decision.Step==null)return false;
            if(decision.Revision!=Revision)return false;                 // CAS — 사이에 상태가 바뀌면 거부
            if(decision.Step.Done||!RequirementsMet(decision.Step))return false;
            decision.Step.Done=true;Revision++;
            Completed=IsComplete(decision.Facts);
            return true;
        }

        // 아직 해야 할 일이 남았는가. 조건부 단계는 **지금 해당하는 경우에만** 센다.
        private bool Applies(Step step,RuleFacts facts)
            =>!step.Conditional||GuardTruth(step,facts)==RuleTruth.TRUE;
        public bool IsComplete(RuleFacts facts)
        {
            foreach(var step in steps)if(!step.Done&&Applies(step,facts))return false;
            return true;
        }

        // 감사관이 다시 읽을 미충족 목록. 버튼이 아니라 남은 단계와 그 근거를 열거한다.
        public List<Step> Unmet(RuleFacts facts)
        {
            var unmet=new List<Step>();
            foreach(var step in steps)if(!step.Done&&Applies(step,facts))unmet.Add(step);
            return unmet;
        }
        public void ResetRun()
        {
            foreach(var step in steps)step.Done=false;
            Completed=false;Revision++;
        }
        // 단계 되감기 — 튜토리얼 전용 동작이지만 데이터 조작이라 러너에 둔다. 비상 세션은 호출하지 않는다.
        public bool Rewind(string stepId)
        {
            var step=Find(stepId);if(step==null||!step.Done)return false;
            step.Done=false;
            // 되감은 단계를 선행으로 삼는 뒤 단계도 함께 풀린다. 한 번만 훑으면 되도록 배열 순서를 쓴다
            // (Load 가 선행 단계를 앞에만 허용하므로 배열 순서가 곧 위상 순서다).
            foreach(var later in steps)
            {
                if(!later.Done||later.Requires==null)continue;
                bool dependsOnCleared=false;
                foreach(var required in later.Requires)
                {
                    var prior=Find(required);
                    if(prior!=null&&!prior.Done){dependsOnCleared=true;break;}
                }
                if(dependsOnCleared)later.Done=false;
            }
            Completed=false;Revision++;return true;
        }
    }
}
