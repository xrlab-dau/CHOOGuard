using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
        [SerializeField] private string shadingMode="observed-unlit";
        [SerializeField] private int selectedView;
        private float yaw, pitch, speed=.5f;
        private string inputError;
        private bool capturing;
        public bool IsCapturing=>capturing;
        public int ViewCount=>surfaces==null?0:surfaces.Length;
        public int SelectedView=>selectedView;
        public int ActiveViewCount
        {
            get {var count=0;if(surfaces!=null)foreach(var surface in surfaces)if(surface!=null&&surface.gameObject.activeSelf)count++;return count;}
        }

        public void Configure(Transform[] models,string[] names,Bounds[] bounds,Camera camera,string hash,string shading="observed-unlit")
        {
            if(capturing)throw new InvalidOperationException("Cannot change reconstruction bindings during capture.");
            if(models==null||names==null||bounds==null||models.Length==0||models.Length!=names.Length||models.Length!=bounds.Length||camera==null)
                throw new ArgumentException("Reconstruction view bindings are incomplete.");
            surfaces=models;identifiers=names;sourceBounds=bounds;view=camera;sourceHash=hash;shadingMode=shading;
            SelectView(0);ResetCamera();
        }

        public void SelectView(int index)
        {
            if(index<0||index>=ViewCount)throw new ArgumentOutOfRangeException(nameof(index));
            selectedView=index;for(var i=0;i<surfaces.Length;i++)surfaces[i].gameObject.SetActive(i==index);
        }

        public void CycleView(int direction)
        {
            if(ViewCount==0)return;
            SelectView(((selectedView+direction)%ViewCount+ViewCount)%ViewCount);
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
                if(Input.GetKeyDown(KeyCode.RightArrow))CycleView(1);
                if(Input.GetKeyDown(KeyCode.LeftArrow))CycleView(-1);
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
            GUI.Label(new Rect(24,44,width-24,24),shadingMode=="authored-lit"?
                "Photo-guided authored structural study | model-relative units | illustrative lighting | no training collision":
                "Uncalibrated model-relative units | partial per-view surfaces | two-sided vertex colors | no training collision");
            GUI.Label(new Rect(24,68,width-24,24),(ViewCount>1?"1 / 2 or arrows: view   ":"Single assembly   ")+"R: first camera +Z   F: frame   RMB + WASD/QE: fly   MMB: orbit   wheel: dolly / speed");
            var enabledBeforeControls=GUI.enabled;
            GUI.enabled=enabledBeforeControls&&!capturing&&ViewCount>1;
            if(GUI.Button(new Rect(24,94,95,24),"Previous"))CycleView(-1);
            if(GUI.Button(new Rect(127,94,95,24),"Next"))CycleView(1);
            GUI.enabled=enabledBeforeControls&&!capturing;
            if(GUI.Button(new Rect(230,94,120,24),"First camera"))ResetCamera();
            if(GUI.Button(new Rect(358,94,95,24),"Frame"))FrameSelected();
            GUI.enabled=enabledBeforeControls;
            if(inputError!=null)GUI.Label(new Rect(20,136,Screen.width-40,50),inputError);
        }

        private IEnumerator CaptureViews(string path)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Reconstruction capture requires a graphics device.");
            return CaptureViews(path,CapturePng);
        }

        private static byte[] CapturePng()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Reconstruction capture requires a graphics device.");
            // Called only after WaitForEndOfFrame. Encoding and persistence finish before receipt publication.
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            if(texture==null)throw new InvalidOperationException("Reconstruction screenshot capture failed.");
            try {return texture.EncodeToPNG();}
            finally {Destroy(texture);}
        }

        private IEnumerator CaptureViews(string path,Func<byte[]> capturePng)
        {
            if(capturing||ViewCount==0||view==null)throw new InvalidOperationException("Reconstruction viewer is not ready to capture.");
            var previousView=selectedView;var previousPosition=view.transform.position;var previousRotation=view.transform.rotation;
            var previousYaw=yaw;var previousPitch=pitch;var previousSpeed=speed;
            capturing=true;
            try
            {
                var writer=new ReconstructionCaptureWriter(path,identifiers,sourceHash,shadingMode,SystemInfo.graphicsDeviceType.ToString());
                for(var i=0;i<ViewCount;i++)for(var angle=0;angle<ReconstructionCaptureWriter.AngleCount;angle++)
                {
                    SelectView(i);ResetCamera();
                    if(angle>0)FrameSelected();
                    if(angle==2){view.transform.RotateAround(sourceBounds[i].center,Vector3.up,35);view.transform.RotateAround(sourceBounds[i].center,view.transform.right,15);SyncAngles();}
                    yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();
                    writer.WriteImage(i,angle,capturePng());
                }
                writer.Complete(receipt=>JsonUtility.ToJson(receipt,true));
            }
            finally
            {
                capturing=false;
                if(view!=null)
                {
                    SelectView(previousView);view.transform.SetPositionAndRotation(previousPosition,previousRotation);
                    yaw=previousYaw;pitch=previousPitch;speed=previousSpeed;
                }
                ReleaseCursor();
            }
        }
    }

    // Filesystem transaction for one capture run; deliberately independent of the screenshot API.
    public sealed class ReconstructionCaptureWriter
    {
        public const int AngleCount=3;
        private static readonly string[] Angles={"source-origin-plus-z","framed-front","framed-oblique"};
        private static readonly byte[] PngSignature={137,80,78,71,13,10,26,10};
        private readonly string directory;
        private readonly CaptureReceipt receipt;
        private bool failed,completed;

        public ReconstructionCaptureWriter(string path,string[] views,string sourceHash,string shadingMode,string graphicsDevice)
        {
            if(views==null||views.Length==0||sourceHash==null||!Regex.IsMatch(sourceHash,"\\A[0-9a-f]{64}\\z"))
                throw new ArgumentException("Capture requires view identities and the current FBX SHA256.");
            var unique=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in views)if(string.IsNullOrWhiteSpace(item)||!unique.Add(item))throw new ArgumentException("Capture view identities must be nonempty and unique.");
            directory=Path.GetFullPath(path);RejectDirectoryLinks();
            // A previous run, even one owned by this viewer, must never acquire a new receipt.
            if(Directory.Exists(directory)||File.Exists(directory))throw new InvalidOperationException("Capture output must be a new directory.");
            Directory.CreateDirectory(directory);
            WriteNew("owner.txt",Encoding.UTF8.GetBytes("chooguard.reconstruction-captures.v2"));
            receipt=new CaptureReceipt{views=(string[])views.Clone(),angles=(string[])Angles.Clone(),fbxSha256=sourceHash,
                shadingMode=shadingMode,graphicsDevice=graphicsDevice,images=new ImageReceipt[checked(views.Length*AngleCount)],
                scope=(shadingMode=="authored-lit"?"Native real-time photo-guided authored structural study":"Native real-time uncalibrated observation review")+"; no metric, collision, facility or VR acceptance"};
        }

        public void WriteImage(int viewIndex,int angleIndex,byte[] png)
        {
            RequireOpen();
            try
            {
                if(viewIndex<0||viewIndex>=receipt.views.Length||angleIndex<0||angleIndex>=AngleCount)throw new ArgumentOutOfRangeException("Capture view or angle is invalid.");
                var index=viewIndex*AngleCount+angleIndex;
                if(receipt.images[index]!=null)throw new InvalidOperationException("Capture view and angle were already written.");
                if(png==null||png.Length<=PngSignature.Length)throw new InvalidOperationException("Screenshot encoding returned no PNG.");
                for(var i=0;i<PngSignature.Length;i++)if(png[i]!=PngSignature[i])throw new InvalidOperationException("Screenshot encoding did not return a PNG.");
                var file=$"view-{viewIndex}-{angleIndex}.png";var digest=Hash(png);
                WriteNew(file,png);
                var item=new ImageReceipt{file=file,view=receipt.views[viewIndex],angle=Angles[angleIndex],bytes=png.LongLength,sha256=digest};
                VerifyImage(item);receipt.images[index]=item;
            }
            catch {failed=true;throw;}
        }

        public void Complete(Func<CaptureReceipt,string> serialize)
        {
            RequireOpen();failed=true;
            foreach(var item in receipt.images)
            {
                if(item==null)throw new InvalidOperationException("Capture is incomplete; no success receipt will be published.");
                VerifyImage(item);
            }
            var json=serialize(receipt);
            if(string.IsNullOrWhiteSpace(json))throw new InvalidOperationException("Capture receipt serialization failed.");
            WriteNew("capture-complete.json.pending",Encoding.UTF8.GetBytes(json));
            RejectDirectoryLinks();
            // The complete filename appears only after every image and the full JSON have been written.
            File.Move(Path.Combine(directory,"capture-complete.json.pending"),Path.Combine(directory,"capture-complete.json"));
            completed=true;
        }

        private void RequireOpen()
        {if(failed||completed)throw new InvalidOperationException("Capture has failed or completed; start a new output directory.");}

        private void WriteNew(string file,byte[] data)
        {
            RejectDirectoryLinks();
            using(var stream=new FileStream(Path.Combine(directory,file),FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {stream.Write(data,0,data.Length);stream.Flush(true);}
        }

        private void VerifyImage(ImageReceipt item)
        {
            RejectDirectoryLinks();var file=Path.Combine(directory,item.file);
            if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Capture image cannot be a link.");
            using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())
                if(stream.Length!=item.bytes||Hex(sha.ComputeHash(stream))!=item.sha256)throw new InvalidOperationException("Persisted capture does not match the captured PNG: "+item.file);
        }

        private void RejectDirectoryLinks()
        {
            for(var current=new DirectoryInfo(directory);current!=null;current=current.Parent)
                if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Capture output cannot use links.");
        }
        private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return Hex(sha.ComputeHash(bytes));}
        private static string Hex(byte[] digest)=>BitConverter.ToString(digest).Replace("-","").ToLowerInvariant();

        [Serializable] public sealed class CaptureReceipt
        {
            public string schemaVersion="unity-reconstruction-capture-2";
            public string[] views;public string[] angles;public string fbxSha256;public string shadingMode;public string graphicsDevice;public string scope;public ImageReceipt[] images;
        }
        [Serializable] public sealed class ImageReceipt
        {public string file;public string view;public string angle;public long bytes;public string sha256;}
    }
}
