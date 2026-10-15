using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ChooGuard.Seeded.Unity
{
    /// <summary>Unity product menu. Browser OAuth and all network work belong to the host.</summary>
    public sealed class ProviderSelectionPanel : MonoBehaviour
    {
        const int ModelsPerPage=40;
        INativeProviderMenuHost host;
        TMP_FontAsset font;
        Canvas canvas;
        GraphicRaycaster raycaster;
        GameObject ownedEvents;
        GameObject previousFocus;
        NativeProviderState shownState;
        NativeAuthFlow shownFlow;
        bool shownOpen,shownBusy,shownCanChange;
        string shownMessage,providerId,accountId,query="";
        int page;
        NativeProviderModel candidate;
        TMP_InputField search;
        TMP_Text selectedText,statusText,lockText,providerReason,modelCount,pageText;
        TMP_Text candidateText,candidateReason,accountText,flowText,codeText;
        RectTransform providersContent,accountsContent,modelsContent;
        ScrollRect providersScroll,accountsScroll,modelsScroll,detailsScroll;
        readonly List<Button> modelButtons=new List<Button>();
        Button refresh,loadModels,login,relogin,logout,choose,consent,initial,poll,cancel,copyCode,previous,next;

        public bool ModalOpen {get {return host!=null&&host.ProviderMenuOpen;}}

        public static ProviderSelectionPanel Create(Transform parent,TMP_FontAsset font,INativeProviderMenuHost host)
        {
            if(font==null)throw new ArgumentNullException(nameof(font),"A staged Korean TMP font is required for the native provider menu.");
            if(host==null)throw new ArgumentNullException(nameof(host));
            var root=new GameObject("구독 모델 연결",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(parent,false);
            var panel=root.AddComponent<ProviderSelectionPanel>();panel.Initialize(host,font);return panel;
        }

        public void Initialize(INativeProviderMenuHost menuHost,TMP_FontAsset koreanFont)
        {
            if(host!=null)throw new InvalidOperationException("Provider panel is already initialized.");
            host=menuHost??throw new ArgumentNullException(nameof(menuHost));font=koreanFont??throw new ArgumentNullException(nameof(koreanFont));
            canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
            raycaster=GetComponent<GraphicRaycaster>();var scale=GetComponent<CanvasScaler>();
            scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1440,900);scale.matchWidthOrHeight=.5f;
            if(EventSystem.current==null)
            {
                ownedEvents=new GameObject("구독 메뉴 EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                ownedEvents.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            Build();Render();SetVisible(host.ProviderMenuOpen);
        }

        void Build()
        {
            var shade=ProviderUiTheme.Block(transform,"배경",new Color(.01f,.015f,.025f,.90f),true);ProviderUiTheme.Stretch(shade.rectTransform);
            var shell=ProviderUiTheme.Block(transform,"메뉴",ProviderUiTheme.Panel,true);ProviderUiTheme.Stretch(shell.rectTransform,28,24,28,24);
            var title=Label(shell.transform,"구독 모델 연결",32,ProviderUiTheme.Accent);Top(title.rectTransform,24,18,660,48);
            var about=Label(shell.transform,"여러 계정으로 로그인하고, 플레이에 사용할 모델 하나를 선택하세요.",17,ProviderUiTheme.Dim);Top(about.rectTransform,24,72,1000,32);
            var close=ProviderUiTheme.Button(shell.transform,font,"돌아가기  Esc",Close);ProviderUiTheme.Place(close.GetComponent<RectTransform>(),new Vector2(1,1),new Vector2(-24,-24),new Vector2(174,42));

            var body=ProviderUiTheme.Node(shell.transform,"연결과 모델");ProviderUiTheme.Stretch(body,16,178,16,120);
            var left=Column(body,"제공자와 계정",0,.27f);
            var middle=Column(body,"계정 모델",.28f,.68f);
            var right=Column(body,"선택 안내",.69f,1);
            var h=Label(left,"제공자",21);Top(h.rectTransform,16,14,240,32);
            providersScroll=ProviderUiTheme.List(left,"제공자 목록",out providersContent);ProviderUiTheme.Stretch(providersScroll.GetComponent<RectTransform>(),14,282,14,56);
            providerReason=Label(left,"",14,ProviderUiTheme.Dim);Bottom(providerReason.rectTransform,16,238,320,38);
            h=Label(left,"로그인 계정",20);Bottom(h.rectTransform,16,204,270,32);
            accountsScroll=ProviderUiTheme.List(left,"계정 목록",out accountsContent);
            var ar=accountsScroll.GetComponent<RectTransform>();ar.anchorMin=Vector2.zero;ar.anchorMax=new Vector2(1,0);ar.offsetMin=new Vector2(14,58);ar.offsetMax=new Vector2(-14,200);
            login=ProviderUiTheme.Button(left,font,"선택한 제공자에 계정 추가",()=>host.StartProviderLogin(providerId));
            var lr=login.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=new Vector2(1,0);lr.offsetMin=new Vector2(14,12);lr.offsetMax=new Vector2(-14,50);

            h=Label(middle,"로그인한 모든 계정의 모델",21);Top(h.rectTransform,16,14,470,32);
            search=ProviderUiTheme.Search(middle,font,value=>{query=value??"";page=0;RenderModels();});
            var sr=search.GetComponent<RectTransform>();sr.anchorMin=new Vector2(0,1);sr.anchorMax=Vector2.one;sr.offsetMin=new Vector2(14,-100);sr.offsetMax=new Vector2(-14,-56);
            search.onSubmit.AddListener(_=>{if(modelButtons.Count>0)modelButtons[0].Select();});
            loadModels=ProviderUiTheme.Button(middle,font,"모든 계정 모델 불러오기",()=>host.RefreshProviderModels());
            var mr=loadModels.GetComponent<RectTransform>();mr.anchorMin=new Vector2(0,1);mr.anchorMax=Vector2.one;mr.offsetMin=new Vector2(14,-146);mr.offsetMax=new Vector2(-14,-110);
            modelCount=Label(middle,"모델을 불러오세요.",14,ProviderUiTheme.Dim);Top(modelCount.rectTransform,16,156,460,28);
            modelsScroll=ProviderUiTheme.List(middle,"모델 목록",out modelsContent);ProviderUiTheme.Stretch(modelsScroll.GetComponent<RectTransform>(),14,56,14,188);
            previous=ProviderUiTheme.Button(middle,font,"이전",()=>{page=Math.Max(0,page-1);RenderModels();});Bottom(previous.GetComponent<RectTransform>(),14,12,80,36);
            next=ProviderUiTheme.Button(middle,font,"다음",()=>{page++;RenderModels();});ProviderUiTheme.Place(next.GetComponent<RectTransform>(),new Vector2(1,0),new Vector2(-14,12),new Vector2(80,36));
            pageText=Label(middle,"",14,ProviderUiTheme.Dim);ProviderUiTheme.Place(pageText.rectTransform,new Vector2(.5f,0),new Vector2(0,16),new Vector2(240,26));pageText.alignment=TextAlignmentOptions.Center;

            h=Label(right,"선택과 로그인 안내",21);Top(h.rectTransform,16,14,370,32);
            RectTransform detail;detailsScroll=ProviderUiTheme.List(right,"선택 안내 내용",out detail);ProviderUiTheme.Stretch(detailsScroll.GetComponent<RectTransform>(),14,12,14,56);
            candidateText=StackText(detail,"목록에서 사용할 모델을 선택하세요.",19,100);
            candidateReason=StackText(detail,"계정의 사용 권한을 확인한 모델만 적용할 수 있습니다.",15,70,ProviderUiTheme.Dim);
            choose=StackButton(detail,"이 모델로 선택",()=>{if(candidate!=null)host.SelectProviderModel(candidate);},true);
            StackText(detail,"선택한 계정",18,32,ProviderUiTheme.Accent);
            accountText=StackText(detail,"",15,72,ProviderUiTheme.Dim);
            relogin=StackButton(detail,"이 계정 다시 로그인",()=>{var a=PickedAccount();if(a!=null)host.StartProviderLogin(a.ProviderId,a.Id,true);});
            logout=StackButton(detail,"이 계정 로그아웃",()=>{var a=PickedAccount();if(a!=null)host.LogoutProviderAccount(a.Id);});
            StackText(detail,"브라우저 로그인",18,32,ProviderUiTheme.Accent);
            flowText=StackText(detail,"로그인은 제공자의 브라우저 화면에서만 진행합니다.",15,100,ProviderUiTheme.Dim);
            codeText=StackText(detail,"",24,36,ProviderUiTheme.Accent);
            copyCode=StackButton(detail,"인증 코드 복사",()=>{var f=host.ActiveProviderLogin;if(f!=null&&f.Pending&&!string.IsNullOrEmpty(f.UserCode))GUIUtility.systemCopyBuffer=f.UserCode;});
            poll=StackButton(detail,"브라우저 승인 확인",()=>host.PollProviderLogin());
            cancel=StackButton(detail,"로그인 취소",()=>host.CancelProviderLogin());

            var footer=ProviderUiTheme.Block(shell.transform,"현재 플레이 선택",ProviderUiTheme.Strong,true);
            var fr=footer.rectTransform;fr.anchorMin=Vector2.zero;fr.anchorMax=new Vector2(1,0);fr.offsetMin=new Vector2(16,16);fr.offsetMax=new Vector2(-16,166);
            selectedText=Label(footer.transform,"",18);Top(selectedText.rectTransform,16,12,980,52);
            lockText=Label(footer.transform,"",14,ProviderUiTheme.Dim);Top(lockText.rectTransform,16,66,980,26);
            consent=ProviderUiTheme.Button(footer.transform,font,"내 구독 사용 허용",()=>host.SetProviderSubscriptionEnabled(!(host.ProviderState?.Subscription?.CloudEnabled??false)),true);
            Bottom(consent.GetComponent<RectTransform>(),16,12,224,40);
            initial=ProviderUiTheme.Button(footer.transform,font,"초기 월드 생성 1회 허용",()=>host.ArmProviderInitialOnce());Bottom(initial.GetComponent<RectTransform>(),252,12,270,40);
            refresh=ProviderUiTheme.Button(footer.transform,font,"연결 상태 확인",()=>host.RefreshProviders());ProviderUiTheme.Place(refresh.GetComponent<RectTransform>(),new Vector2(1,0),new Vector2(-16,12),new Vector2(178,40));
            statusText=Label(footer.transform,"",14,ProviderUiTheme.Dim);ProviderUiTheme.Place(statusText.rectTransform,new Vector2(1,1),new Vector2(-16,-12),new Vector2(340,74));
        }

        void Update()
        {
            if(host==null)return;
            if(shownOpen!=host.ProviderMenuOpen)SetVisible(host.ProviderMenuOpen);
            if(!shownOpen)return;
            if(!ReferenceEquals(shownState,host.ProviderState))Render();
            else if(!ReferenceEquals(shownFlow,host.ActiveProviderLogin)||shownBusy!=host.ProviderBusy||shownCanChange!=host.CanChangeProvider||shownMessage!=host.ProviderMessage)
            {shownFlow=host.ActiveProviderLogin;shownBusy=host.ProviderBusy;shownCanChange=host.CanChangeProvider;shownMessage=host.ProviderMessage;RenderDetails();}
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if(keyboard.escapeKey.wasPressedThisFrame){Close();return;}
            if(keyboard.tabKey.wasPressedThisFrame)MoveFocus(keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed);
        }

        void SetVisible(bool value)
        {
            shownOpen=value;canvas.enabled=value;raycaster.enabled=value;
            if(EventSystem.current==null)return;
            if(value){previousFocus=EventSystem.current.currentSelectedGameObject;Render();refresh.Select();}
            else
            {
                if(previousFocus!=null&&previousFocus.activeInHierarchy)EventSystem.current.SetSelectedGameObject(previousFocus);
                else if(EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))EventSystem.current.SetSelectedGameObject(null);
                previousFocus=null;
            }
        }

        // The auth-start reply may not have supplied a flow yet. The host also
        // cancels that in-flight work; a hidden menu must never open its browser.
        void Close(){host.CancelProviderLogin();host.SetProviderMenuOpen(false);}

        void Render()
        {
            shownState=host.ProviderState;shownFlow=host.ActiveProviderLogin;shownBusy=host.ProviderBusy;shownCanChange=host.CanChangeProvider;shownMessage=host.ProviderMessage;
            if(shownState?.Providers!=null&&!shownState.Providers.Any(p=>p.Id==providerId))providerId=shownState.Providers.FirstOrDefault(p=>p.Registered)?.Id??shownState.Providers.FirstOrDefault()?.Id;
            if(candidate!=null)candidate=shownState?.Models?.FirstOrDefault(m=>SameModel(m,candidate));
            RenderProviders();RenderAccounts();RenderModels();RenderDetails();
        }

        void RenderProviders()
        {
            ProviderUiTheme.Clear(providersContent);
            var rows=shownState?.Providers??Array.Empty<NativeProviderInfo>();
            foreach(var item in rows)
            {
                var row=item;var button=ProviderUiTheme.Button(providersContent,font,row.Label+(row.Registered?"":" · 연결 불가"),()=>{providerId=row.Id;accountId=null;Render();});
                ProviderUiTheme.Height(button.gameObject,44);Mark(button,row.Id==providerId);
            }
            if(rows.Count==0)Empty(providersContent,"연결 상태를 확인해주세요.");
            var selected=PickedProvider();providerReason.text=selected==null?"":selected.Registered?"내 계정으로 로그인합니다.":Unsupported(selected);
        }

        void RenderAccounts()
        {
            ProviderUiTheme.Clear(accountsContent);
            var all=shownState?.Accounts??Array.Empty<NativeProviderAccount>();
            if(!all.Any(a=>a.Id==accountId))accountId=all.FirstOrDefault(a=>a.ProviderId==providerId&&a.Active)?.Id??all.FirstOrDefault(a=>a.ProviderId==providerId)?.Id;
            foreach(var item in all)
            {
                var a=item;var button=ProviderUiTheme.Button(accountsContent,font,AccountLabel(a)+"\n"+ProviderLabel(a.ProviderId)+" · "+AccountStatus(a),()=>{accountId=a.Id;providerId=a.ProviderId;Render();});
                var text=button.GetComponentInChildren<TMP_Text>();text.fontSize=14;text.textWrappingMode=TextWrappingModes.Normal;
                ProviderUiTheme.Height(button.gameObject,66);Mark(button,a.Id==accountId);
            }
            if(all.Count==0)Empty(accountsContent,"아직 로그인한 계정이 없습니다.");
        }

        void RenderModels()
        {
            if(modelsContent==null)return;
            ProviderUiTheme.Clear(modelsContent);modelButtons.Clear();
            var rows=(shownState?.Models??Array.Empty<NativeProviderModel>()).Where(m=>Matches(m,query)).ToList();
            var pages=Math.Max(1,(rows.Count+ModelsPerPage-1)/ModelsPerPage);page=Math.Max(0,Math.Min(page,pages-1));
            modelCount.text=rows.Count==0?string.IsNullOrEmpty(query)?"계정 모델을 불러오세요.":"검색 결과가 없습니다.":rows.Count+"개 모델 · 사용할 모델 하나를 선택";
            foreach(var item in rows.Skip(page*ModelsPerPage).Take(ModelsPerPage))
            {
                var model=item;bool active=IsCurrent(model);
                var button=ProviderUiTheme.Button(modelsContent,font,(active?"현재 사용 · ":"")+model.DisplayName+"\n"+ProviderLabel(model.ProviderId)+" · "+AccountLabel(model.AccountId)+(model.Selectable?"":" · 사용 불가"),()=>{candidate=model;RenderDetails();MarkModelRows();});
                var text=button.GetComponentInChildren<TMP_Text>();text.fontSize=15;text.textWrappingMode=TextWrappingModes.Normal;
                ProviderUiTheme.Height(button.gameObject,72);Mark(button,SameModel(model,candidate));modelButtons.Add(button);
            }
            if(rows.Count==0)Empty(modelsContent,string.IsNullOrEmpty(query)?"여러 계정으로 로그인하면\n각 계정의 모델이 함께 표시됩니다.":"다른 모델명이나 계정명으로 검색해보세요.");
            pageText.text=rows.Count==0?"0개":(page*ModelsPerPage+1)+"–"+Math.Min(rows.Count,(page+1)*ModelsPerPage)+" / "+rows.Count;
            previous.interactable=page>0;next.interactable=page+1<pages;
            modelsScroll.verticalNormalizedPosition=1;
        }

        void MarkModelRows()
        {
            var rows=(shownState?.Models??Array.Empty<NativeProviderModel>()).Where(m=>Matches(m,query)).Skip(page*ModelsPerPage).Take(ModelsPerPage).ToList();
            for(int i=0;i<modelButtons.Count&&i<rows.Count;i++)Mark(modelButtons[i],SameModel(rows[i],candidate));
        }

        void RenderDetails()
        {
            var selected=shownState?.Subscription?.Selection;var a=PickedAccount();var p=PickedProvider();bool allowed=shownCanChange&&!shownBusy;
            var flow=shownFlow;bool pending=flow?.Pending==true;
            candidateText.text=candidate==null?"목록에서 사용할 모델을 선택하세요.":candidate.DisplayName+"\n"+ProviderLabel(candidate.ProviderId)+"\n"+AccountLabel(candidate.AccountId);
            candidateReason.text=candidate==null?"모델 선택은 초기 상황 생성과 NPC 계획에 함께 적용됩니다.":candidate.Selectable?IsCurrent(candidate)?"현재 플레이에 사용할 모델입니다.":"이 모델을 적용하면 이전 선택은 해제됩니다. 아직 구독 사용량을 소모하지 않습니다.":ModelReason(candidate.Reason);
            choose.interactable=allowed&&candidate!=null&&candidate.Selectable&&!IsCurrent(candidate);
            accountText.text=a==null?"계정을 추가하면 로그인 상태를 확인할 수 있습니다.":AccountLabel(a)+"\n"+ProviderLabel(a.ProviderId)+" · "+AccountStatus(a);
            login.interactable=allowed&&!pending&&p!=null&&p.Registered;
            relogin.interactable=allowed&&!pending&&a!=null;logout.interactable=allowed&&a!=null&&a.SignedIn;
            loadModels.interactable=allowed&&(shownState?.Accounts?.Any(x=>x.SignedIn)??false);
            refresh.interactable=!shownBusy;
            flowText.text=pending?ProviderLabel(flow.ProviderId)+" 로그인 대기 중\n열린 브라우저에서 본인 계정으로 로그인하고 허용해주세요.":flow?.Completed==true?"로그인을 완료했습니다. 계정 모델을 불러오세요.":!string.IsNullOrEmpty(flow?.ErrorCode)?Error(flow.ErrorCode):"로그인은 제공자의 브라우저 화면에서만 진행합니다.";
            codeText.text=pending&&!string.IsNullOrEmpty(flow.UserCode)?flow.UserCode:"";
            copyCode.gameObject.SetActive(pending&&!string.IsNullOrEmpty(flow.UserCode));poll.gameObject.SetActive(pending);cancel.gameObject.SetActive(pending);
            poll.interactable=allowed&&pending;cancel.interactable=pending;
            selectedText.text=selected==null?"현재 플레이 모델: 선택되지 않음": "현재 플레이 모델: "+ModelLabel(selected.ProviderId,selected.AccountId,selected.ModelId)+"\n"+ProviderLabel(selected.ProviderId)+" · "+AccountLabel(selected.AccountId);
            bool enabled=shownState?.Subscription?.CloudEnabled??false;
            lockText.text=!shownCanChange?"플레이 중에는 계정과 모델 설정이 잠깁니다. 훈련을 종료한 뒤 변경하세요.":enabled?"내 계정의 구독 사용량을 사용합니다. 초기 생성 허용은 한 번만 유효합니다.":"내 구독 사용을 허용하기 전에는 모델을 호출하지 않습니다.";
            ProviderUiTheme.Caption(consent,enabled?"내 구독 사용 중단":"내 구독 사용 허용");consent.interactable=allowed&&selected!=null;
            ProviderUiTheme.Caption(initial,shownState?.InitialAllowed==true?"초기 월드 생성 1회 허용됨":"초기 월드 생성 1회 허용");
            initial.interactable=allowed&&selected!=null&&enabled&&shownState?.InitialAllowed!=true;
            statusText.text=shownBusy?"연결 처리 중…":!string.IsNullOrEmpty(shownMessage)?ProductMessage(shownMessage):!string.IsNullOrEmpty(shownState?.Error)?Error(shownState.Error):shownState==null?"연결 상태를 확인해주세요.":enabled?"구독 모델 사용이 허용됐습니다.":"계정 로그인과 모델 선택을 완료해주세요.";
        }

        bool Matches(NativeProviderModel m,string value){return string.IsNullOrWhiteSpace(value)||new[]{m.DisplayName,ProviderLabel(m.ProviderId),AccountLabel(m.AccountId)}.Any(x=>(x??"").IndexOf(value,StringComparison.OrdinalIgnoreCase)>=0);}
        bool IsCurrent(NativeProviderModel m){var s=shownState?.Subscription?.Selection;return s!=null&&m!=null&&s.ProviderId==m.ProviderId&&s.AccountId==m.AccountId&&s.ModelId==m.ModelId;}
        static bool SameModel(NativeProviderModel a,NativeProviderModel b){return a!=null&&b!=null&&a.ProviderId==b.ProviderId&&a.AccountId==b.AccountId&&a.ModelId==b.ModelId&&a.CatalogVersion==b.CatalogVersion;}
        NativeProviderInfo PickedProvider(){return shownState?.Providers?.FirstOrDefault(p=>p.Id==providerId);}
        NativeProviderAccount PickedAccount(){return shownState?.Accounts?.FirstOrDefault(a=>a.Id==accountId);}
        string ProviderLabel(string id){return shownState?.Providers?.FirstOrDefault(p=>p.Id==id)?.Label??"제공자";}
        string AccountLabel(string id){return AccountLabel(shownState?.Accounts?.FirstOrDefault(a=>a.Id==id));}
        static string AccountLabel(NativeProviderAccount a){return a==null?"계정":a.Label;}
        string ModelLabel(string p,string a,string id){return shownState?.Models?.FirstOrDefault(m=>m.ProviderId==p&&m.AccountId==a&&m.ModelId==id)?.DisplayName??id;}
        static string AccountStatus(NativeProviderAccount a)
        {
            if(!a.SignedIn)return "로그인 필요";
            switch(a.InferenceState){case "quota_stopped":return "구독 한도 확인 필요";case "permission_required":return "사용 권한 확인 필요";case "catalog_unavailable":return "모델 목록 확인 필요";case "no_entitled_models":return "사용 가능한 모델 없음";case "catalog_required":return "모델 불러오기 필요";default:return "로그인됨";}
        }
        static string Unsupported(NativeProviderInfo p)
        {
            if(p.Id=="anthropic")return "제공자 정책상 이 앱에서 구독 로그인 연결을 사용할 수 없습니다.";
            if(p.Id=="github-copilot")return "앱 운영자의 연결 설정이 필요합니다.";
            if(p.Id=="openrouter"||p.Id=="prime-intellect")return "현재 확인된 연결 방식은 이 앱의 구독 사용 방식에 맞지 않습니다.";
            return "이 앱에서 사용할 구독 연결과 모델 권한이 아직 확인되지 않았습니다.";
        }
        static string ModelReason(string reason){return reason=="automatic_model_routing_not_supported"?"자동 모델 선택은 지원하지 않습니다. 사용할 모델 하나를 직접 선택하세요.":"이 계정에서는 현재 이 모델을 사용할 수 없습니다. 제공자의 구독과 계정 권한을 확인해주세요.";}
        static string ProductMessage(string message)
        {
            if(message.Any(c=>c>='가'&&c<='힣'))return message;
            return Error(message);
        }
        static string Error(string code)
        {
            switch(code)
            {
                case "provider_session_changed":case "provider_selection_mismatch":return "선택이 변경됐습니다. 연결 상태를 확인하고 다시 선택해주세요.";
                case "subscription_usage_paused":case "subscription_sharing_usage_limit_exceeded":return "계정의 구독 한도를 확인해주세요. 다른 계정으로 자동 전환하지 않습니다.";
                case "authorization_cancelled":case "access_denied":return "로그인이 취소됐습니다. 필요할 때 다시 로그인하세요.";
                case "invalid_or_expired_oauth_state":case "native_flow_expired":return "로그인 시간이 만료됐습니다. 다시 로그인해주세요.";
                case "sign_in_required":return "계정에 다시 로그인해주세요.";
                case "select_account_model_first":case "authorized_account_model_required":return "계정 모델을 불러온 뒤 사용할 모델 하나를 선택해주세요.";
                case "cloud_use_not_enabled":return "선택한 모델에 내 구독 사용을 허용해주세요.";
                case "provider_not_available_for_subscription_mode":case "provider_policy_restricted":return "이 제공자는 현재 이 앱의 구독 연결을 지원하지 않습니다.";
                default:return "연결 상태를 확인해주세요. 계속 연결되지 않으면 앱 운영자에게 문의하세요.";
            }
        }

        void MoveFocus(bool reverse)
        {
            if(EventSystem.current==null)return;
            var choices=GetComponentsInChildren<Selectable>().Where(s=>s.IsActive()&&s.IsInteractable()).ToList();if(choices.Count==0)return;
            var current=EventSystem.current.currentSelectedGameObject;int i=choices.FindIndex(s=>s.gameObject==current);
            if(current!=null){var field=current.GetComponent<TMP_InputField>();if(field!=null)field.DeactivateInputField();}
            var index=i<0?(reverse?choices.Count-1:0):(i+(reverse?-1:1)+choices.Count)%choices.Count;
            var selected=choices[index];selected.Select();
            foreach(var scroll in new[]{providersScroll,accountsScroll,modelsScroll,detailsScroll})Reveal(scroll,selected.transform as RectTransform);
        }
        static void Reveal(ScrollRect scroll,RectTransform selected)
        {
            if(scroll==null||selected==null||!selected.IsChildOf(scroll.content))return;
            Canvas.ForceUpdateCanvases();var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,selected);var rect=scroll.viewport.rect;
            var delta=bounds.min.y<rect.yMin?rect.yMin-bounds.min.y:bounds.max.y>rect.yMax?rect.yMax-bounds.max.y:0;
            var position=scroll.content.anchoredPosition;position.y+=delta;scroll.content.anchoredPosition=position;
        }
        TMP_Text Label(Transform parent,string value,float size,Color? color=null){var text=ProviderUiTheme.Text(parent,font,value,size,color);text.text=value;return text;}
        TMP_Text StackText(Transform parent,string value,float size,float height,Color? color=null){var text=Label(parent,value,size,color);ProviderUiTheme.Height(text.gameObject,height);return text;}
        Button StackButton(Transform parent,string value,UnityEngine.Events.UnityAction action,bool primary=false){var button=ProviderUiTheme.Button(parent,font,value,action,primary);ProviderUiTheme.Height(button.gameObject,42);return button;}
        void Empty(Transform parent,string value){StackText(parent,value,15,90,ProviderUiTheme.Dim);}
        static RectTransform Column(Transform parent,string name,float min,float max){var image=ProviderUiTheme.Block(parent,name,ProviderUiTheme.Strong,true);var r=image.rectTransform;r.anchorMin=new Vector2(min,0);r.anchorMax=new Vector2(max,1);r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        static void Top(RectTransform rect,float x,float y,float width,float height){ProviderUiTheme.Place(rect,new Vector2(0,1),new Vector2(x,-y),new Vector2(width,height));}
        static void Bottom(RectTransform rect,float x,float y,float width,float height){ProviderUiTheme.Place(rect,Vector2.zero,new Vector2(x,y),new Vector2(width,height));}
        static void Mark(Button button,bool selected){var c=button.colors;c.normalColor=selected?new Color(1,.69f,.13f,.20f):new Color(1,1,1,.10f);button.colors=c;}
        void OnDestroy()
        {
            if(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))EventSystem.current.SetSelectedGameObject(null);
            if(ownedEvents!=null)Destroy(ownedEvents);
        }
    }
}
