using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChooGuard.App.Fps.Work
{
    // 감사관이 읽는 자료 한 묶음. 단말과 세션 사이를 오가는 유일한 형태다.
    // 문자열 열거는 세션이 이미 만들어 둔 것을 그대로 옮긴다 — 단말은 문장을 짓지 않는다.
    public readonly struct AuditReport
    {
        public readonly IReadOnlyList<string> Findings;
        public readonly string Subject;
        public AuditReport(IReadOnlyList<string> findings,string subject){Findings=findings;Subject=subject;}
        public int Count=>Findings==null?0:Findings.Count;
        public bool Clean=>Count==0;
        public static readonly AuditReport Empty=new AuditReport(Array.Empty<string>(),"");
    }

    // 점검 판정 단말. 역무실 벽의 물리 오브젝트이고 걸어가 E 를 눌러 연다.
    //
    // 모드 중립이다 — TutorialSession 도 Emergency 도 참조하지 않는다. FacilityInspectable 이
    // GateReason 델리게이트로 세션을 모르는 채 게이트를 받는 것과 같은 규율이다
    // (FacilityInspectable.cs 의 GateReason). 컴파일러는 이것을 막아주지 않는다 —
    // App/Fps 와 App/Mvp 가 ChooGuard.App 단일 어셈블리이므로 이것은 설계 규율이다.
    //
    // 절차 진행 중에도 단말은 월드에 존재하고 거부만 한다(Jev 006 present_but_refuses 0.74).
    // 갑자기 나타나는 물체는 디제틱 파탄이고, 플레이어가 단말의 위치를 미리 학습해야 하기 때문이다.
    public sealed class AuditTerminal : FpsInteractable
    {
        [Header("결선 (세션이 꽂는다. 비어 있으면 안전하게 거부한다)")]
        public Func<string> GateReason;
        public Func<string> BeforeOpen;
        public Func<AuditReport> ReportSource;

        [Header("표시")]
        public AuditTerminalView View;
        public string OpenPrompt="감사 결과 확인";
        public string ClosePrompt="감사 결과 닫기";
        public string UnboundReason="단말이 점검 세션에 연결되어 있지 않습니다";

        public bool IsOpen { get; private set; }
        public AuditReport LastReport { get; private set; }=AuditReport.Empty;

        private FirstPersonResponder reader;

        private void Awake(){if(View==null)View=GetComponentInChildren<AuditTerminalView>();RefreshPrompt();}

        public override bool CanInteract(FirstPersonResponder responder,out string reason)
        {
            if(!base.CanInteract(responder,out reason))return false;
            // 열려 있으면 닫는 것은 언제나 허용한다 — 열어놓고 못 닫는 상태를 만들지 않는다.
            if(IsOpen){reason=null;return true;}
            if(GateReason==null||ReportSource==null){reason=UnboundReason;return false;}
            var gate=GateReason();
            if(!string.IsNullOrEmpty(gate)){reason=gate;return false;}
            reason=null;return true;
        }

        // 토글을 먼저 적용하고 SuccessMessage 를 세운 뒤 기반 구현에 넘긴다.
        // InteractionPerformed 는 FpsInteractable 이 선언한 event 라 파생 클래스가 직접 발화할 수 없다 —
        // 기반 TryInteract 를 타야 구독자(세션·시험)가 기존 계약대로 신호를 받는다.
        public override bool TryInteract(FirstPersonResponder responder,out string feedback)
        {
            if(!CanInteract(responder,out feedback))return false;
            if(IsOpen){SuccessMessage="감사 단말을 닫았습니다";Close();}
            else
            {
                var reason=BeforeOpen?.Invoke();
                if(!string.IsNullOrEmpty(reason)){feedback=reason;return false;}
                Open(responder);SuccessMessage=LastReport.Clean?"지적 사항 없음":"지적 "+LastReport.Count+"건";
            }
            return base.TryInteract(responder,out feedback);
        }

        private void Open(FirstPersonResponder responder)
        {
            LastReport=ReportSource!=null?ReportSource():AuditReport.Empty;
            reader=responder;IsOpen=true;
            if(View!=null)View.Show(LastReport);
            RefreshPrompt();
        }
        public void Close()
        {
            IsOpen=false;reader=null;
            if(View!=null)View.Hide();
            RefreshPrompt();
        }

        private void Update()
        {
            if(!IsOpen)return;
            // 게이트가 다시 닫히면(되감기·재시행으로 Finished=false) 열린 화면은 거짓이 된다. 즉시 닫는다.
            if(GateReason!=null&&!string.IsNullOrEmpty(GateReason())){Close();return;}
            if(reader==null){Close();return;}
            // 정지 중에는 닫지 않고 숨긴다 — 정지 상태에서는 E 가 막혀(FpsInteractable.CanInteract)
            // 플레이어가 화면을 닫을 방법이 없어지기 때문이다. 재개하면 읽던 화면이 그대로 돌아온다.
            if(View!=null)View.SetVisible(!reader.IsPaused);
            // 걸어서 멀어지면 닫는다. 시선 이탈로 닫으면 화면을 읽는 동안의 마우스 흔들림이 곧 닫힘이 되어
            // 읽을 수가 없다 — 단말에서 물러나는 것이 '다 읽었다'의 디제틱한 표현이다.
            if(Vector3.Distance(reader.transform.position,transform.position)>Mathf.Max(reader.InteractionDistance,.25f)+1f)Close();
        }

        private void OnDisable(){if(IsOpen)Close();}
        private void RefreshPrompt(){Prompt=IsOpen?ClosePrompt:OpenPrompt;}
    }
}
