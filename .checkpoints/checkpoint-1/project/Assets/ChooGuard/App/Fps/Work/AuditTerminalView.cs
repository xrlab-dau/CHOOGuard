using System;
using System.Text;
using TMPro;
using UnityEngine;
namespace ChooGuard.App.Fps.Work
{
    // 판정 단말 화면. 스크린스페이스이되 전체화면이 아니다 — 바깥 여백으로 월드가 계속 보인다.
    // 조사 결론(2026-09-22): My Summer Car 의 검사 영수증, Viscera Cleanup 의 사무실 보고 화면 모두
    // 월드를 가리지 않는 부분 화면이다. 자동으로 열리는 전체화면 정산은 Jev 006 에서 0.01 로 기각됐다.
    //
    // 문자열 구성은 정적 순수 함수로 빼 두었다. 렌더는 한국어 TMP 폰트를 요구하지만 구성은 요구하지 않으므로
    // 시험이 폰트 자산 없이 '무엇이 쓰이는가'를 단정할 수 있다.
    public sealed class AuditTerminalView : MonoBehaviour
    {
        public TMP_FontAsset KoreanFont;
        public string Title="점검 감사 단말";
        public string CleanLine="지적 사항 없음 — 절차를 모두 충족했습니다.";
        public string Footer="E · 닫기      Esc · 정지";

        public bool IsVisible { get; private set; }
        public string Body { get; private set; }="";
        public string Subtitle { get; private set; }="";

        private GameObject canvasRoot;
        private TMP_Text subtitleText,bodyText;

        // 지적을 줄바꿈으로 잇는다. TMP 가 줄바꿈과 자동 축소를 모두 처리하므로
        // VerticalLayoutGroup·ContentSizeFitter·ScrollRect 를 쓰지 않는다 — 저장소 전체에 앞의 둘은 0건이고,
        // 유일한 ScrollRect 선례(MvpWorkspace.cs:248~)는 content 높이가 고정이라 가변 길이에 이미 맞지 않는다.
        public static string ComposeBody(AuditReport report,string cleanLine)
        {
            if(report.Clean)return cleanLine;
            // 세션은 지적마다 대상 이름을 앞에 붙인다("{대상} · {지적}"). 부제가 이미 대상을 말하고 있으므로
            // 한 화면에서 같은 이름을 N+1 번 읽게 된다. 문장을 다시 짓지 않고 중복 접두사만 걷어낸다 —
            // 자료를 바꾸는 것이 아니라 같은 자료를 한 번만 보여주는 표시 계층의 일이다.
            string prefix=string.IsNullOrEmpty(report.Subject)?null:report.Subject+" · ";
            var text=new StringBuilder();
            for(int i=0;i<report.Findings.Count;i++)
            {
                var line=report.Findings[i]??"";
                if(prefix!=null&&line.StartsWith(prefix,StringComparison.Ordinal))line=line.Substring(prefix.Length);
                if(i>0)text.Append('\n');
                text.Append("· ").Append(line);
            }
            return text.ToString();
        }
        public static string ComposeSubtitle(AuditReport report)
        {
            string subject=string.IsNullOrEmpty(report.Subject)?"점검 대상":report.Subject;
            return report.Clean?subject+" · 지적 0건 · 절차 적합":subject+" · 지적 "+report.Count+"건";
        }

        public void Show(AuditReport report)
        {
            Subtitle=ComposeSubtitle(report);Body=ComposeBody(report,CleanLine);
            IsVisible=true;
            if(!EnsureBuilt())return;
            subtitleText.text=Subtitle;
            bodyText.text=Body;
            // 지적이 없으면 한 줄뿐이라 왼쪽 위에 붙여 두면 허전하다. 그때만 가운데로 모은다.
            bodyText.alignment=report.Clean?TextAlignmentOptions.Center:TextAlignmentOptions.TopLeft;
            canvasRoot.SetActive(true);
        }
        public void Hide(){IsVisible=false;if(canvasRoot!=null)canvasRoot.SetActive(false);}
        // 열림 상태를 유지한 채 보이기만 바꾼다. 정지 중 겹침을 피하는 용도다.
        public void SetVisible(bool visible){if(canvasRoot!=null)canvasRoot.SetActive(visible&&IsVisible);}

        private bool built;
        private bool EnsureBuilt()
        {
            if(built)return canvasRoot!=null;
            built=true;
            // 침묵 실패 금지 — 폰트가 없으면 화면이 안 뜨는 이유를 한 번 크게 알린다.
            if(KoreanFont==null){Debug.LogError("[AuditTerminalView] 한국어 폰트가 없어 감사 화면을 만들지 않습니다. Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset 를 지정하세요.",this);return false;}
            // HUD 캔버스는 order 0 이다. 감사 화면은 그 위에 온다.
            canvasRoot=FpsUiFactory.Canvas("AuditTerminalScreen",transform,10);
            var panel=canvasRoot.transform;
            // 940x440. 실측(2026-09-22): 8단계 절차를 끝까지 돌면 지적은 최대 몇 줄뿐이다 —
            // close-inspection 이 attach-tag 를 요구하는 의존 사슬 때문에 Unmet 은 감사 시점에 비어 있다.
            // 1440x900 기준 좌우 250px·상하 230px 가 남아 월드가 계속 보인다(부분 화면).
            FpsUiFactory.Panel(panel,"배경",Vector2.zero,new Vector2(940,440),new Color(.04f,.05f,.06f,.94f));
            FpsUiFactory.Label(panel,KoreanFont,"제목",new Vector2(0,186),new Vector2(880,40),26);
            subtitleText=FpsUiFactory.Label(panel,KoreanFont,"부제",new Vector2(0,150),new Vector2(880,32),20);
            FpsUiFactory.Panel(panel,"구분선",new Vector2(0,126),new Vector2(860,2),new Color(1,1,1,.25f));
            bodyText=FpsUiFactory.Label(panel,KoreanFont,"지적 열거",new Vector2(0,-18),new Vector2(880,264),19,TextAlignmentOptions.TopLeft);
            // 길이가 늘어도 판을 키우지 않고 글자를 줄인다. 판 크기가 내용에 따라 출렁이면 읽는 위치가 흔들린다.
            bodyText.enableAutoSizing=true;bodyText.fontSizeMin=14;bodyText.fontSizeMax=19;
            FpsUiFactory.Label(panel,KoreanFont,"안내",new Vector2(0,-186),new Vector2(880,30),17).color=new Color(1,1,1,.62f);
            canvasRoot.transform.Find("제목").GetComponent<TMP_Text>().text=Title;
            canvasRoot.transform.Find("안내").GetComponent<TMP_Text>().text=Footer;
            canvasRoot.SetActive(false);
            return true;
        }
        private void OnDestroy(){if(canvasRoot!=null)Destroy(canvasRoot);}
    }
}
