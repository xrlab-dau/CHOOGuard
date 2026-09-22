using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ChooGuard.App.Mvp
{
    // A measured horizontal slice projected onto the floor, never a smoke volume.
    public sealed class MvpHazardFieldView : MonoBehaviour
    {
        public MvpStationView Station;
        public MvpWorkspace Workspace;
        public MvpPhysicsBridge Bridge;
        public TMP_FontAsset Font;
        [SerializeField] private bool overlayEnabled;
        private MvpPhysicsBridge subscribed;
        private MvpPhysicsResult accepted;
        private MvpHazardFieldFrame frame;
        private float[] values;
        private GameObject quad, panel;
        private Mesh mesh;
        private Material material;
        private Texture2D texture;
        private TMP_Text toggleLabel, legend;
        private Renderer[] markerRenderers;
        private bool[] markerEnabled;
        private bool markersHidden;
        public bool OverlayVisible => quad != null && quad.activeInHierarchy;
        public int Width => frame == null ? 0 : frame.width;
        public int Height => frame == null ? 0 : frame.height;
        public float SampledIncidentTime => frame == null ? float.NaN : frame.sampledIncidentTime;
        public void SetOverlayEnabled(bool enabled) { overlayEnabled = enabled; RefreshVisibility(); }
        public float SampleAt(float localFDSX, float localFDSY)
        {
            if (frame == null || values == null || float.IsNaN(localFDSX) || float.IsNaN(localFDSY) ||
                localFDSX < frame.originX || localFDSY < frame.originY ||
                localFDSX > frame.originX + (Width-1)*frame.stepX || localFDSY > frame.originY + (Height-1)*frame.stepY)
                return float.NaN;
            // Native nearest-node tie goes to the lower coordinate, as numpy argmin does.
            int x = Mathf.Clamp(Mathf.CeilToInt((localFDSX-frame.originX)/frame.stepX-.5f),0,Width-1);
            int y = Mathf.Clamp(Mathf.CeilToInt((localFDSY-frame.originY)/frame.stepY-.5f),0,Height-1);
            return values[y*Width+x];
        }
        private void OnEnable() { Subscribe(); }
        private void Subscribe()
        {
            if (subscribed == Bridge) return;
            if (subscribed != null) { subscribed.ResultReceived -= Receive; subscribed.StatusChanged -= Status; }
            subscribed = Bridge;
            Clear();
            if (subscribed != null) { subscribed.ResultReceived += Receive; subscribed.StatusChanged += Status; }
        }
        private void Status(string _) { if (Bridge == null || !Bridge.Ready || Bridge.Failed) Clear(); }
        private void Receive(MvpPhysicsResult result)
        {
            Clear();
            if (Station == null || result == null || !result.fireRequired || !result.fieldCurrent || result.phase != "incident" ||
                result.caseId != "reference-hall-30x20-v1" || result.hazardField == null ||
                !MvpPhysicsBridge.TryDecodeHazardField(result.hazardField,out var decoded,out _)) return;
            accepted = result; frame = result.hazardField; values = decoded;
            EnsureQuad();
            if (texture == null || texture.width != Width || texture.height != Height)
            {
                if (texture != null) Destroy(texture);
                texture = new Texture2D(Width,Height,TextureFormat.RGBA32,false,true) { name="FDS native node palette", filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
            }
            var colors = new Color[values.Length];
            for(int i=0;i<colors.Length;i++)
            {
                float t = Mathf.Sqrt(Mathf.Clamp01((values[i]-frame.displayMin)/(frame.displayMax-frame.displayMin)));
                colors[i] = t < .5f ? Color.Lerp(new Color(.06f,.2f,.6f,.32f),new Color(.95f,.7f,.08f,.68f),t*2) : Color.Lerp(new Color(.95f,.7f,.08f,.68f),new Color(.9f,.03f,.12f,.85f),(t-.5f)*2);
            }
            texture.SetPixels(colors); texture.Apply(false,false); material.SetTexture("_BaseMap",texture);
            float u=.5f/Width,v=.5f/Height;
            mesh.uv = new[]{new Vector2(u,v),new Vector2(u,1-v),new Vector2(1-u,1-v),new Vector2(1-u,v)};
            RefreshVisibility();
        }
        private void EnsureQuad()
        {
            if (quad != null) return;
            quad = new GameObject("FDS 1.5m slice projection",typeof(MeshFilter),typeof(MeshRenderer));
            quad.transform.SetParent(Station.transform,false); quad.layer=Station.gameObject.layer;
            mesh = new Mesh { name="30x20m native-node field" };
            mesh.vertices = new[]{MvpStationView.ReferenceLocal(0,0)+Vector3.up*.03f,MvpStationView.ReferenceLocal(0,20)+Vector3.up*.03f,MvpStationView.ReferenceLocal(30,20)+Vector3.up*.03f,MvpStationView.ReferenceLocal(30,0)+Vector3.up*.03f};
            mesh.triangles=new[]{0,1,2,0,2,3}; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            quad.GetComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name="FDS analysis palette",renderQueue=(int)RenderQueue.Transparent };
            material.SetFloat("_Surface",1); material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One); material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0); material.SetFloat("_Cull",(float)CullMode.Off); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            var renderer=quad.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            quad.SetActive(false);
        }
        private void LateUpdate()
        {
            Subscribe();
            if (Bridge == null || !Bridge.Ready || Bridge.Failed || (accepted != null && Bridge.LastResult != accepted)) Clear();
            EnsureUI(); RefreshVisibility();
        }
        private void RefreshVisibility()
        {
            var context=Bridge!=null ? Bridge.LastResult : null;
            if(panel!=null)panel.SetActive(context!=null && context.fireRequired && context.phase!="ordinary");
            bool visible=overlayEnabled && values != null && Station != null && Workspace != null && (Workspace.CurrentFloor==0 || Workspace.CurrentFloor==2);
            if(quad!=null)quad.SetActive(visible);
            HideMarker(visible);
            if(toggleLabel!=null)toggleLabel.text="연기 단면 분석 " + (overlayEnabled?"켜짐":"꺼짐");
            if(legend!=null)legend.text=frame==null ? "유효한 사건 단면 없음 · 안전 판정 아님" :
                $"높이 {frame.sampleHeight:0.0}m의 2D 단면 · 입체 연기 아님\n표본 {frame.sampledIncidentTime:0.00}초 · 소광계수 1/m\n파랑 {frame.displayMin:0.###} → 노랑 → 빨강 {frame.displayMax:0.###} (제곱근 색상)";
        }
        private void HideMarker(bool hide)
        {
            if(hide==markersHidden)return;
            if(hide && Station!=null && Station.IncidentMarker!=null)
            {
                markerRenderers=Array.FindAll(Station.IncidentMarker.GetComponentsInChildren<Renderer>(true), r => r.gameObject.name=="상황 표지"); markerEnabled=new bool[markerRenderers.Length];
                for(int i=0;i<markerRenderers.Length;i++){markerEnabled[i]=markerRenderers[i].enabled;markerRenderers[i].enabled=false;}
            }
            else if(markerRenderers!=null)for(int i=0;i<markerRenderers.Length;i++)if(markerRenderers[i]!=null)markerRenderers[i].enabled=markerEnabled[i];
            markersHidden=hide;
        }
        private void EnsureUI()
        {
            if(Workspace==null)return;
            var canvas=Workspace.transform.Find("WorkspaceCanvas"); if(canvas==null)return;
            if(panel!=null && panel.transform.parent==canvas)return;
            if(panel!=null)Destroy(panel);
            var rect=Rect(canvas,"HazardFieldPanel",new Vector2(390,-58),new Vector2(330,92));panel=rect.gameObject;
            var background=panel.AddComponent<Image>();background.color=new Color(.015f,.05f,.075f,.88f);background.raycastTarget=false;
            var buttonRect=Rect(rect,"HazardFieldToggle",new Vector2(8,-4),new Vector2(314,25));
            var image=buttonRect.gameObject.AddComponent<Image>();image.color=new Color(.03f,.16f,.22f,.95f);
            var button=buttonRect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            button.onClick.AddListener(()=>{if(Workspace.Router==null || Workspace.Router.ActiveModal==null)SetOverlayEnabled(!overlayEnabled);});
            toggleLabel=Label(buttonRect,"Text",Vector2.zero,new Vector2(314,25),13);
            legend=Label(rect,"Legend",new Vector2(8,-31),new Vector2(314,58),11);
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        private TMP_Text Label(Transform parent,string name,Vector2 position,Vector2 size,float fontSize)
        {
            var label=Rect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();label.font=Font!=null?Font:Workspace.Font;
            label.fontSize=fontSize;label.color=new Color(.8f,.94f,1);label.raycastTarget=false;return label;
        }
        private void Clear(){accepted=null;frame=null;values=null;if(quad!=null)quad.SetActive(false);HideMarker(false);}
        private void OnDisable(){if(subscribed!=null){subscribed.ResultReceived-=Receive;subscribed.StatusChanged-=Status;}subscribed=null;Clear();if(panel!=null)Destroy(panel);}
        private void OnDestroy(){Clear();if(quad!=null)Destroy(quad);if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);if(texture!=null)Destroy(texture);if(panel!=null)Destroy(panel);}
    }
}
