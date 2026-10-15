using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Mvp
{
    public sealed class MvpJevForecastView : MonoBehaviour
    {
        public MvpAgencyDispatchController Controller;
        public MvpJevForecastClient Client;
        private GameObject panel;
        private TMP_Text body;
        private int revision=-1;
        public void Toggle()
        {
            if(panel==null)Build();if(panel==null)return;
            panel.SetActive(!panel.activeSelf);if(panel.activeSelf)Render();
        }
        private void Update() { if(panel!=null&&panel.activeSelf&&Client!=null&&revision!=Client.Revision)Render(); }
        private void OnDestroy() { if(panel!=null)Destroy(panel); }
        private void Build()
        {
            if(Controller?.Workspace==null)return;
            var canvas=Controller.Workspace.transform.Find("WorkspaceCanvas");if(canvas==null)return;
            panel=new GameObject("JevForecastPanel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas,false);
            var rect=(RectTransform)panel.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-24,-76);rect.sizeDelta=new Vector2(360,292);
            panel.GetComponent<Image>().color=new Color(.025f,.055f,.075f,.98f);
            Text(rect,"AI 예측 코어",14,12,270,30,19);
            var close=new GameObject("Close",typeof(RectTransform),typeof(Image),typeof(Button));close.transform.SetParent(rect,false);Rect((RectTransform)close.transform,296,12,50,28);close.GetComponent<Image>().color=new Color(.08f,.22f,.28f);close.GetComponent<Button>().onClick.AddListener(()=>panel.SetActive(false));Text(close.transform,"닫기",7,2,42,24,13);
            body=Text(rect,"예측 준비 중",14,54,332,224,14);panel.SetActive(false);
        }
        private void Render()
        {
            if(Client==null||body==null)return;revision=Client.Revision;
            var response=Client.Current;
            if(response==null) { body.text=Client.Status+"\n\n현재 계산을 바탕으로 미래 가능성을 갱신합니다.\nAI 추정 · 관찰로 검증 중";return; }
            string text="기준 시각 "+response.basisSimTime.ToString("0.0")+"초 · 응답 "+(response.latencyMs/1000f).ToString("0.00")+"초\n";
            foreach(var f in response.forecasts)
            {
                string label=f.id=="support_arrival_120"?"지원팀 현장 도착":f.id=="evacuation_complete_120"?"대상 인원 전원 출구 도달":"밀도 0.5명/㎡ 이상 상승";
                text+="\n"+f.horizonSeconds+"초 이내 "+label+"\n"+(f.probability*100).ToString("0")+"%\n";
            }
            if(response.forecasts.Length==0)text+="\n현재 조건에서 적용할 예측이 없습니다.\n";
            text+="\nAI 추정 · 관찰로 검증 중";
            body.text=text;
        }
        private TMP_Text Text(Transform parent,string value,float x,float y,float w,float h,float size) { var g=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(parent,false);Rect((RectTransform)g.transform,x,y,w,h);var t=g.GetComponent<TextMeshProUGUI>();t.font=Controller.Workspace.Font;t.fontSize=size;t.color=Color.white;t.text=value;t.raycastTarget=false;t.overflowMode=TextOverflowModes.Ellipsis;return t; }
        private static void Rect(RectTransform r,float x,float y,float w,float h) { r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h); }
    }
}
