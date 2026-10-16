using System;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Seeded;
using ChooGuard.Seeded.Unity;
using TMPro;
using UnityEngine;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>Account settings on the title do not load station geometry or start simulation/inference.</summary>
    public sealed class TitleProviderMenuHost : MonoBehaviour,INativeProviderMenuHost
    {
        public NativeProviderState ProviderState {get;private set;}
        public NativeAuthFlow ActiveProviderLogin {get;private set;}
        public bool ProviderMenuOpen {get;private set;}
        public bool ProviderBusy=>pending!=null;
        public bool CanChangeProvider=>!SceneFlow.Loading;
        public string ProviderMessage {get;private set;}="AI 계정과 모델을 확인하고 있습니다.";
        public string Summary=>ProviderState==null?ProviderMessage:
            ProviderState.Subscription.Selection==null?"랜덤 비상상황 · AI 모델 선택이 필요합니다. 튜토리얼은 바로 시작할 수 있습니다.":
            ProviderState.Subscription.Ready?"랜덤 비상상황 AI 준비됨 · "+ProviderState.Subscription.Model:
            "선택 모델 · "+ProviderState.Subscription.Model+" · 구독 사용 허용을 확인해주세요.";
        SubscriptionConnection connection;
        NativeProviderControlClient controls;
        Task<NativeProviderControlReply> pending;
        CancellationTokenSource cancellation;
        ProviderSelectionPanel panel;
        TMP_FontAsset font;
        string action;
        bool cancelQueued,refreshQueued;
        float nextPoll;

        public void Initialize(TMP_FontAsset koreanFont){font=koreanFont;RefreshProviders();}
        bool Ensure()
        {
            if(controls!=null)return true;
            if(!SubscriptionConnection.TryLoad(out connection,out _)){ProviderMessage="AI 계정 연결을 준비한 뒤 다시 확인해주세요.";return false;}
            controls=connection.CreateControlClient(()=>CanChangeProvider);return true;
        }
        void Begin(string name,Func<CancellationToken,Task<NativeProviderControlReply>> request,bool mutation=true)
        {
            if(ProviderBusy)return;
            if(mutation&&!CanChangeProvider){ProviderMessage="불러오기가 끝난 뒤 계정 설정을 변경해주세요.";return;}
            if(!Ensure())return;
            cancellation?.Dispose();cancellation=new CancellationTokenSource();action=name;
            try{pending=request(cancellation.Token);ProviderMessage="AI 계정 설정을 처리하고 있습니다.";}
            catch(Exception){pending=null;ProviderMessage="AI 연결 요청을 완료하지 못했습니다. 다시 확인해주세요.";}
        }
        public void SetProviderMenuOpen(bool open)
        {
            if(open==ProviderMenuOpen)return;
            if(open)
            {
                if(panel==null)panel=ProviderSelectionPanel.Create(transform,font,this);
                ProviderMenuOpen=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;RefreshProviders();
            }
            else{CancelProviderLogin();ProviderMenuOpen=false;}
        }
        public void RefreshProviders()=>Begin("refresh",ct=>controls.RefreshAsync(ct),false);
        public void RefreshProviderModels()=>Begin("catalog",ct=>controls.RefreshCatalogAsync(ct));
        public void StartProviderLogin(string providerId,string accountId=null,bool reconsent=false)
        {
            if(!ProviderMenuOpen||ActiveProviderLogin?.Pending==true)return;cancelQueued=false;
            Begin("login",ct=>controls.StartLoginAsync(providerId,accountId,reconsent,ct));
        }
        public void PollProviderLogin()
        {if(ActiveProviderLogin?.Pending==true)Begin("poll",ct=>controls.PollLoginAsync(ActiveProviderLogin.FlowId,ct));}
        public void CancelProviderLogin()
        {
            if(ProviderBusy){if(action=="login"||action=="poll")cancelQueued=true;return;}
            if(ActiveProviderLogin?.Pending==true)Begin("cancel",ct=>controls.CancelLoginAsync(ActiveProviderLogin.FlowId,ct));
        }
        public void SelectProviderModel(NativeProviderModel model)=>Begin("selection",ct=>controls.SelectModelAsync(model,ct));
        public void SetProviderSubscriptionEnabled(bool enabled)=>Begin("consent",ct=>controls.SetConsentAsync(enabled,null,null,ct));
        public void ArmProviderInitialOnce(long inputUnits=SubscriptionConnection.DefaultInitialInputUnits,long outputTokens=2048)=>Begin("initial",ct=>controls.SetConsentAsync(true,inputUnits,outputTokens,ct));
        public void LogoutProviderAccount(string accountId)=>Begin("logout",ct=>controls.LogoutAsync(accountId,ct));

        void Update()
        {
            if(pending!=null&&pending.IsCompleted)
            {
                var task=pending;pending=null;
                try
                {
                    var reply=task.GetAwaiter().GetResult();ProviderState=reply.State;
                    if(reply.Flow!=null)
                    {
                        ActiveProviderLogin=reply.Flow.PreserveStartHints(ActiveProviderLogin);
                        nextPoll=Time.realtimeSinceStartup+Mathf.Max(.5f,ActiveProviderLogin.PollAfterMs/1000f);
                        if(action=="login"&&ActiveProviderLogin.Pending)
                        {
                            if(!ProviderMenuOpen||cancelQueued)cancelQueued=true;
                            else
                            {
                                string url=ActiveProviderLogin.AuthorizationUrl??ActiveProviderLogin.VerificationUri;
                                if(!string.IsNullOrEmpty(url))UnityEngine.Application.OpenURL(url);
                            }
                        }
                        if(!ActiveProviderLogin.Pending)cancelQueued=false;
                    }
                    ProviderMessage=ActiveProviderLogin?.Pending==true?"브라우저에서 계정 로그인을 완료해주세요.":action=="initial"?"초기 조건 생성 1회를 허용했습니다. 비상상황 플레이에서 생성을 시작하세요.":"AI 계정 설정을 반영했습니다.";
                }
                catch(NativeProviderControlException error)
                {
                    refreshQueued=error.Code=="provider_session_changed"||error.Code=="provider_selection_mismatch";
                    ProviderMessage=refreshQueued?"계정 상태가 변경돼 다시 확인하고 있습니다.":"AI 연결 요청을 완료하지 못했습니다. 다시 확인해주세요.";
                }
                catch(Exception){ProviderMessage="AI 연결 요청을 완료하지 못했습니다. 다시 확인해주세요.";}
            }
            if(ProviderBusy)return;
            if(refreshQueued){refreshQueued=false;RefreshProviders();return;}
            if(cancelQueued)
            {
                if(ActiveProviderLogin?.Pending==true){CancelProviderLogin();return;}cancelQueued=false;
            }
            if(ProviderMenuOpen&&ActiveProviderLogin?.Pending==true&&Time.realtimeSinceStartup>=nextPoll)
            {nextPoll=Time.realtimeSinceStartup+1;PollProviderLogin();}
        }
        void OnDestroy(){cancellation?.Cancel();cancellation?.Dispose();controls?.Invalidate();}
    }
}
