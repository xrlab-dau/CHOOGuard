using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ChooGuard.Foundation.Demo
{
    // Uncalibrated observation viewer. Never attach this to the training world.
    public sealed class ReconstructionReviewController : MonoBehaviour
    {
        [SerializeField] private Transform[] surfaces;
        [SerializeField] private string[] identifiers;
        [SerializeField] private Bounds[] sourceBounds;
        [SerializeField] private Camera view;
        [SerializeField] private string sourceHash;
        [SerializeField] private int selectedView;
        private float yaw, pitch, speed=.5f;
        private string inputError;
        private bool capturing;
        public int ViewCount=>surfaces==null?0:surfaces.Length;
        public int SelectedView=>selectedView;
        public int ActiveViewCount
        {
            get {var count=0;if(surfaces!=null)foreach(var surface in surfaces)if(surface!=null&&surface.gameObject.activeSelf)count++;return count;}
        }

        public void Configure(Transform[] models,string[] names,Bounds[] bounds,Camera camera,string hash)
        {
            if(models==null||names==null||bounds==null||models.Length==0||models.Length!=names.Length||models.Length!=bounds.Length||camera==null)
                throw new ArgumentException("Reconstruction view bindings are incomplete.");
            surfaces=models;identifiers=names;sourceBounds=bounds;view=camera;sourceHash=hash;
            SelectView(0);ResetCamera();
        }

        public void SelectView(int index)
        {
            if(index<0||index>=ViewCount)throw new ArgumentOutOfRangeException(nameof(index));
            selectedView=index;for(var i=0;i<surfaces.Length;i++)surfaces[i].gameObject.SetActive(i==index);
        }

        public void ResetCamera()
        {
            view.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);yaw=pitch=0;
            speed=Mathf.Max(.01f,sourceBounds[selectedView].size.magnitude*.25f);ReleaseCursor();
        }

        public void FrameSelected()
        {
            var bounds=sourceBounds[selectedView];
            var vertical=view.fieldOfView*Mathf.Deg2Rad*.5f;
            var horizontal=Mathf.Atan(Mathf.Tan(vertical)*Mathf.Max(.1f,view.aspect));
            var distance=bounds.extents.magnitude/Mathf.Sin(Mathf.Min(vertical,horizontal))*1.1f;
            view.transform.SetPositionAndRotation(bounds.center-Vector3.forward*distance,Quaternion.identity);yaw=pitch=0;
            ReleaseCursor();
        }

        private void Start()
        {
            Application.runInBackground=true;SelectView(0);ResetCamera();
            var args=Environment.GetCommandLineArgs();var at=Array.IndexOf(args,"--choo-reconstruction-captures");
            if(at>=0&&at+1<args.Length)StartCoroutine(CaptureViews(args[at+1]));
        }

        private void Update()
        {
            if(capturing||view==null||!Application.isFocused)return;
            try
            {
                if(Input.GetKeyDown(KeyCode.Escape)){ReleaseCursor();return;}
                if(Input.GetKeyDown(KeyCode.Alpha1))SelectView(0);
                if(Input.GetKeyDown(KeyCode.Alpha2)&&ViewCount>1)SelectView(1);
                if(Input.GetKeyDown(KeyCode.RightArrow))SelectView((selectedView+1)%ViewCount);
                if(Input.GetKeyDown(KeyCode.LeftArrow))SelectView((selectedView+ViewCount-1)%ViewCount);
                if(Input.GetKeyDown(KeyCode.R))ResetCamera();
                if(Input.GetKeyDown(KeyCode.F))FrameSelected();
                if(Input.GetMouseButton(1))
                {
                    Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
                    yaw+=Input.GetAxisRaw("Mouse X")*2;pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2,-89,89);
                    view.transform.rotation=Quaternion.Euler(pitch,yaw,0);
                    var motion=new Vector3((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),
                        (Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0),
                        (Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0));
                    view.transform.position+=view.transform.TransformDirection(Vector3.ClampMagnitude(motion,1))*speed*Time.unscaledDeltaTime*(Input.GetKey(KeyCode.LeftShift)?3:1);
                    speed=Mathf.Clamp(speed*Mathf.Exp(Input.mouseScrollDelta.y*.15f),.001f,100);
                }
                else
                {
                    ReleaseCursor();
                    if(Input.GetMouseButton(2))
                    {
                        var center=sourceBounds[selectedView].center;
                        view.transform.RotateAround(center,Vector3.up,Input.GetAxisRaw("Mouse X")*2);
                        view.transform.RotateAround(center,view.transform.right,-Input.GetAxisRaw("Mouse Y")*2);
                        SyncAngles();
                    }
                    view.transform.position+=view.transform.forward*Input.mouseScrollDelta.y*speed*.2f;
                }
                inputError=null;
            }
            catch(InvalidOperationException)
            {
                inputError="Desktop input is unavailable. Enable the legacy Input Manager (or Both) for this review player.";ReleaseCursor();
            }
        }

        private void SyncAngles(){var angles=view.transform.eulerAngles;yaw=angles.y;pitch=angles.x>180?angles.x-360:angles.x;}
        private static void ReleaseCursor(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        private void OnApplicationFocus(bool focused){if(!focused)ReleaseCursor();}
        private void OnDisable(){ReleaseCursor();}

        private void OnGUI()
        {
            if(ViewCount==0)return;
            var width=Mathf.Min(Screen.width-24,960);GUI.Box(new Rect(12,12,width,116),GUIContent.none);
            GUI.Label(new Rect(24,20,width-24,24),"CHOOGuard reconstruction review | "+identifiers[selectedView]);
            GUI.Label(new Rect(24,44,width-24,24),"Uncalibrated model-relative units | partial per-view surfaces | two-sided vertex colors | no training collision");
            GUI.Label(new Rect(24,68,width-24,24),"1 / 2 or arrows: view   R: first camera +Z   F: frame   RMB + WASD/QE: fly   MMB: orbit   wheel: dolly / speed");
            if(GUI.Button(new Rect(24,94,95,24),"Previous"))SelectView((selectedView+ViewCount-1)%ViewCount);
            if(GUI.Button(new Rect(127,94,95,24),"Next"))SelectView((selectedView+1)%ViewCount);
            if(GUI.Button(new Rect(230,94,120,24),"First camera"))ResetCamera();
            if(GUI.Button(new Rect(358,94,95,24),"Frame"))FrameSelected();
            if(inputError!=null)GUI.Label(new Rect(20,136,Screen.width-40,50),inputError);
        }

        private IEnumerator CaptureViews(string path)
        {
            // Explicit command-line output only; refuse a foreign directory or links.
            var directory=Path.GetFullPath(path);const string owner="chooguard.reconstruction-captures.v1";
            for(var current=new DirectoryInfo(directory);current!=null;current=current.Parent)
                if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Capture output cannot use links.");
            if(Directory.Exists(directory))foreach(var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
                if((entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Capture output contains a linked destination.");
            if(Directory.Exists(directory)&&(!File.Exists(Path.Combine(directory,"owner.txt"))||File.ReadAllText(Path.Combine(directory,"owner.txt"))!=owner))
                throw new InvalidOperationException("Capture output is not owned by this viewer.");
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"owner.txt"),owner);capturing=true;
            for(var i=0;i<ViewCount;i++)for(var angle=0;angle<3;angle++)
            {
                SelectView(i);ResetCamera();
                if(angle>0)FrameSelected();
                if(angle==2){view.transform.RotateAround(sourceBounds[i].center,Vector3.up,35);view.transform.RotateAround(sourceBounds[i].center,view.transform.right,15);SyncAngles();}
                yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();
                var destination=Path.Combine(directory,$"view-{i}-{angle}.png");
                if(File.Exists(destination)&&(File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Capture destination is a link.");
                ScreenCapture.CaptureScreenshot(destination);yield return new WaitForSecondsRealtime(.3f);
            }
            File.WriteAllText(Path.Combine(directory,"capture-complete.json"),JsonUtility.ToJson(new CaptureReceipt
                {views=identifiers,fbxSha256=sourceHash,angles=new[]{"source-origin-plus-z","framed-front","framed-oblique"},graphicsDevice=SystemInfo.graphicsDeviceType.ToString(),
                 scope="Native real-time uncalibrated observation review; no metric, collision, facility or VR acceptance"},true));
            capturing=false;SelectView(0);ResetCamera();
        }
        [Serializable] private sealed class CaptureReceipt {public string[] views;public string[] angles;public string fbxSha256;public string graphicsDevice;public string scope;}
    }
}
