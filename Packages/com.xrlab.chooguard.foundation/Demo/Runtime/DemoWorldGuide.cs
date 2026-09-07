using UnityEngine;
using System.Linq;
namespace ChooGuard.Foundation.Demo
{
    public sealed class DemoWorldGuide : MonoBehaviour
    {
        [SerializeField] private LineRenderer path;
        [SerializeField] private Transform marker;
        [SerializeField] private TextMesh label;
        [SerializeField] private Shader textShader;
        private Font worldFont;
        private Material fontMaterial;
        private float refresh;
        public void ConfigureFontShader(Shader shader){textShader=shader;}
        private void Start()
        {
            var available=Font.GetOSInstalledFontNames();
            var choice=new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Noto Sans KR"}.FirstOrDefault(available.Contains);
            if(choice==null)return;
            worldFont=Font.CreateDynamicFontFromOSFont(choice,64);
            worldFont.RequestCharactersInTexture("[E] 상황 패널 경보 체험 버튼 무전 콘솔 방향 안내 표지 게이트 우회 확인 서측 동측 인솔 시작 경로 집결 인원 0123456789 / m",64);
            label.font=worldFont;
            fontMaterial=new Material(textShader){mainTexture=worldFont.material.mainTexture};
            label.GetComponent<Renderer>().sharedMaterial=fontMaterial;
            Font.textureRebuilt+=RefreshFontTexture;
        }
        private void RefreshFontTexture(Font font){if(font==worldFont && fontMaterial!=null)fontMaterial.mainTexture=font.material.mainTexture;}
        private void OnDestroy(){Font.textureRebuilt-=RefreshFontTexture;if(fontMaterial!=null)Destroy(fontMaterial);if(worldFont!=null)Destroy(worldFont);}
        private string previous;
        public string DestinationId { get; private set; }
        public void Configure(LineRenderer route,Transform targetMarker,TextMesh worldLabel)
        {path=route;marker=targetMarker;label=worldLabel;Hide();}
        public void Hide(){if(path!=null)path.enabled=false;if(marker!=null)marker.gameObject.SetActive(false);DestinationId=null;previous=null;refresh=0;if(path!=null)path.positionCount=0;}
        private int graphVersion=-1;
        public void ShowContext(Camera camera,Vector3 point,string text)
        {
            if(label==null||marker==null)return;
            path.enabled=false;marker.gameObject.SetActive(true);marker.position=point+Vector3.up*.65f;
            label.text=text;label.transform.rotation=camera.transform.rotation;
            if(worldFont!=null)worldFont.RequestCharactersInTexture(text,64);
        }
        public void InvalidateRoute(){refresh=0;previous=null;}
        public void Tick(float dt,DemoWalkGraph graph,DemoPlayerController player,string id,Vector3 destination,Vector3 approach,string verb)
        {
            if(path==null||marker==null||label==null)return;
            if(graphVersion!=graph.Version){graphVersion=graph.Version;InvalidateRoute();}
            DestinationId=id;path.enabled=true;marker.gameObject.SetActive(true);
            marker.position=destination+Vector3.up*(.85f+Mathf.Sin(Time.time*2.5f)*.08f);
            label.text=verb+"\n"+Vector3.Distance(player.transform.position,destination).ToString("0")+" m";
            // World text faces the camera; depth testing keeps it behind real geometry.
            label.transform.rotation=player.ViewCamera.transform.rotation;
            refresh-=dt;
            if(refresh>0&&previous==id)return;
            refresh=.45f;previous=id;
            approach.y=.1f;
            var route=graph.Route(player.transform.position,approach);
            path.positionCount=route.Length;
            for(var i=0;i<route.Length;i++)path.SetPosition(i,route[i]+Vector3.up*.035f);
            path.textureMode=LineTextureMode.Tile;
        }
    }
}
