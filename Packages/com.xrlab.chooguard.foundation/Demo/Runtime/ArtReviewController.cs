using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ChooGuard.Foundation.Demo
{
    // Dedicated developer review player, never part of the station's training scene or progression.
    public sealed class ArtReviewController : MonoBehaviour
    {
        [SerializeField] private Transform[] models;
        [SerializeField] private string[] identifiers;
        [SerializeField] private Camera view;
        [SerializeField] private Vector3[] detailCenters;
        [SerializeField] private float[] detailZoom;
        private Vector3[] cellPositions;
        private int page,angle,detailIndex=-1;
        public void Configure(Transform[] assets,string[] names,Camera camera,Vector3[] focusCenters,float[] focusZoom){models=assets;identifiers=names;view=camera;detailCenters=focusCenters;detailZoom=focusZoom;}
        private void Start()
        {
            Application.runInBackground=true;
            cellPositions=System.Array.ConvertAll(models,m=>m.localPosition);
            Show(0,0);
            var args=Environment.GetCommandLineArgs();var at=Array.IndexOf(args,"--choo-art-captures");
            if(at>=0&&at+1<args.Length)StartCoroutine(CaptureAll(Path.GetFullPath(args[at+1])));
        }
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.RightArrow))Show((page+1)%PageCount,angle);
            if(Input.GetKeyDown(KeyCode.LeftArrow))Show((page+PageCount-1)%PageCount,angle);
            if(Input.GetKeyDown(KeyCode.Space))Show(page,1-angle);
        }
        private int PageCount=>(models.Length+5)/6;
        private void Show(int selectedPage,int selectedAngle)
        {
            page=selectedPage;angle=selectedAngle;detailIndex=-1;
            for(var i=0;i<models.Length;i++)
            {
                models[i].gameObject.SetActive(i/6==page);
                if(cellPositions!=null)models[i].localPosition=cellPositions[i];models[i].localScale=Vector3.one;
                // A shallow downward view exposes horizontal panels and the front control surfaces.
                models[i].localRotation=Quaternion.Euler(-18,0,0) * Quaternion.Euler(0,angle==0?20:110,0) *
                    (identifiers[i]=="Evacuee"?Quaternion.Euler(0,180,0):Quaternion.identity);
            }
        }
        private void ShowDetail(int selected,int selectedAngle)
        {
            Show(selected/6,selectedAngle);detailIndex=selected;
            for(var i=0;i<models.Length;i++)models[i].gameObject.SetActive(i==selected);
            models[selected].localScale=Vector3.one*detailZoom[selected];
            models[selected].localPosition=-(models[selected].localRotation*(detailCenters[selected]*detailZoom[selected]));
        }
        private IEnumerator CaptureAll(string directory)
        {
            Directory.CreateDirectory(directory);
            for(var p=0;p<PageCount;p++)for(var a=0;a<2;a++)
            {
                Show(p,a);yield return new WaitForSecondsRealtime(.8f);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,$"models-{p:00}-{a}.png"));
                yield return new WaitForSecondsRealtime(.4f);
            }
            for(var i=0;i<models.Length;i++)for(var a=0;a<2;a++)
            {
                ShowDetail(i,a);yield return new WaitForSecondsRealtime(.45f);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,$"detail-{identifiers[i]}-{a}.png"));
                yield return new WaitForSecondsRealtime(.25f);
            }
            File.WriteAllText(Path.Combine(directory,"capture-complete.json"),JsonUtility.ToJson(new CaptureReceipt
                {assetIds=identifiers,pages=PageCount,viewsPerPage=2,graphicsDevice=SystemInfo.graphicsDeviceType.ToString(),
                 method="Native realtime review player screen captures; no offline/path-traced render or bake"},true));
            Show(0,0);
        }
        [Serializable] private sealed class CaptureReceipt
        {public string[] assetIds;public int pages;public int viewsPerPage;public string graphicsDevice;public string method;}
        private void OnGUI()
        {
            var scale=Screen.width/1440f;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            var style=new GUIStyle(GUI.skin.label){fontSize=19,normal={textColor=new Color(.12f,.16f,.20f)}};
            GUI.Label(new Rect(30,18,1380,40),$"CHOOGuard | Object geometry review  {page+1}/{PageCount}  |  {(angle==0?"FRONT + TOP":"SIDE + TOP")}",style);
            if(detailIndex>=0){GUI.Label(new Rect(30,62,1380,40),identifiers[detailIndex]+" | control/body detail",style);GUI.matrix=Matrix4x4.identity;return;}
            for(var i=page*6;i<Math.Min(models.Length,(page+1)*6);i++)
            {
                var rs=models[i].GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
                var point=view.WorldToScreenPoint(new Vector3(bounds.center.x,bounds.min.y-.06f,bounds.center.z));
                var rect=new Rect(point.x/scale-160,(Screen.height-point.y)/scale+6,320,35);
                style.alignment=TextAnchor.MiddleCenter;GUI.Label(rect,identifiers[i],style);
            }
            style.alignment=TextAnchor.MiddleLeft;style.fontSize=14;
            GUI.Label(new Rect(30,Screen.height/scale-35,1380,30),"Arrow keys: page   Space: side view   |   Normalized preview scale; actual dimensions and source photographs are in the reference registry.",style);
            GUI.matrix=Matrix4x4.identity;
        }
    }
}
