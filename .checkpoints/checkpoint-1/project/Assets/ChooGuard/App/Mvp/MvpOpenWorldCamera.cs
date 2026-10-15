using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
namespace ChooGuard.App.Mvp
{
    [DefaultExecutionOrder(-1000)]
    public sealed class MvpOpenWorldCamera : MonoBehaviour
    {
        public Transform WorldRoot;
        public MvpWorkspace Workspace;
        public Camera Camera;
        public MvpCameraSurfaceProfile Surface;
        public Vector3 FocusLocal=new Vector3(0,3,0);
        public float Zoom=260;
        [Range(30,75)] public float FieldOfView=50;
        [Min(.05f)] public float FocusSeconds=.5f;
        [Min(.01f)] public float FollowHalfLife=.16f;
        public float MinimumZoom=8,MaximumZoom=3000,GroundClearance=3;
        private float yaw=70,pitch=55;
        private Vector3 focusGoal,followOffset;
        private float zoomGoal;
        private bool transitioning,capturedAtFrameStart;
        private int capturedFrame=-1;
        public Transform FollowedTarget { get; private set; }
        public bool IsFollowing => FollowedTarget!=null;
        public float ViewDistance => Mathf.Max(2,Zoom/Mathf.Tan(FieldOfView*.5f*Mathf.Deg2Rad));
        public void Focus(Vector3 local,float framing)
        {
            EndFollow();focusGoal=local;zoomGoal=framing>0?Mathf.Clamp(framing,MinimumZoom,MaximumZoom):Zoom;transitioning=true;
        }
        public void BeginFollow(Transform target,float initialFraming=0)
        {
            if(target==null){EndFollow();return;}
            FollowedTarget=target;followOffset=Vector3.zero;
            focusGoal=WorldRoot!=null?WorldRoot.InverseTransformPoint(target.position):FocusLocal;
            zoomGoal=initialFraming>0?Mathf.Clamp(initialFraming,MinimumZoom,MaximumZoom):Zoom;transitioning=true;
        }
        public void EndFollow(){FollowedTarget=null;followOffset=Vector3.zero;transitioning=false;}
        private bool CaptureNow()
        {
            if(Workspace!=null&&Workspace.Router!=null&&Workspace.Router.ActiveModal!=null)return true;
            var es=EventSystem.current;if(es==null)return false;
            if(es.IsPointerOverGameObject())return true;
            var selected=es.currentSelectedGameObject;
            return selected!=null&&(selected.GetComponentInParent<InputField>()!=null||selected.GetComponentInParent<TMP_InputField>()!=null);
        }
        public bool IsInputCaptured => CaptureNow()||(capturedFrame==Time.frameCount&&capturedAtFrameStart);
        private void Update(){capturedFrame=Time.frameCount;capturedAtFrameStart=CaptureNow();}
        public bool TryCancelFollow(){if(IsInputCaptured||!IsFollowing)return false;EndFollow();return true;}
        private void OnEnable(){if(Camera==null)Camera=GetComponent<Camera>();Apply();}
        public void Pan(Vector2 localAxes)
        {
            if(IsInputCaptured||WorldRoot==null||localAxes.sqrMagnitude==0)return;
            EndFollow();var rotation=Quaternion.Euler(0,yaw,0);
            FocusLocal+=rotation*new Vector3(localAxes.x,0,localAxes.y);Apply();
        }
        public bool TryGetSurfacePoint(Vector2 screen,out Vector3 point)
        {
            point=default;if(Camera==null||WorldRoot==null)return false;
            var ray=Camera.ScreenPointToRay(screen);float distance=Camera.farClipPlane;bool found=false;
            if(Physics.Raycast(ray,out var hit,distance,Camera.cullingMask,QueryTriggerInteraction.Ignore)){point=hit.point;distance=hit.distance;found=true;}
            if(Surface!=null&&Surface.RaycastFloors(ray,distance,out var floor)){point=floor;distance=Vector3.Distance(ray.origin,floor);found=true;}
            if(Surface!=null&&Surface.Raycast(ray,distance,out var terrain)){point=terrain;return true;}
            if(found)return true;
            // Serialized visible floor triangles and DEM are camera-only; use a plane only outside known surfaces.
            var plane=new Plane(WorldRoot.up,WorldRoot.TransformPoint(new Vector3(0,FocusLocal.y,0)));
            if(!plane.Raycast(ray,out float enter))return false;point=ray.GetPoint(enter);return true;
        }
        public bool ZoomAtScreenPoint(Vector2 screen,float scrollDelta)
        {
            if(IsInputCaptured||Camera==null||WorldRoot==null||Mathf.Approximately(scrollDelta,0))return false;
            if(!TryGetSurfacePoint(screen,out var anchor))return false;
            Zoom=Mathf.Clamp(Zoom*Mathf.Exp(-scrollDelta*.0015f),MinimumZoom,MaximumZoom);transitioning=false;Apply();
            // Keep the picked world point at the same pixel, including terrain clearance correction.
            var plane=new Plane(WorldRoot.up,anchor);
            for(int i=0;i<4;i++)
            {
                var ray=Camera.ScreenPointToRay(screen);if(!plane.Raycast(ray,out float enter))break;
                FocusLocal+=WorldRoot.InverseTransformVector(anchor-ray.GetPoint(enter));Apply();
            }
            if(IsFollowing)followOffset=FocusLocal-WorldRoot.InverseTransformPoint(FollowedTarget.position);
            return true;
        }
        private void LateUpdate()
        {
            if(Camera==null||WorldRoot==null)return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            if(IsFollowing)focusGoal=WorldRoot.InverseTransformPoint(FollowedTarget.position)+followOffset;
            if(transitioning||IsFollowing)
            {
                float rate=transitioning?6/Mathf.Max(.05f,FocusSeconds):Mathf.Log(2)/Mathf.Max(.01f,FollowHalfLife);
                float t=1-Mathf.Exp(-rate*dt);FocusLocal=Vector3.Lerp(FocusLocal,focusGoal,t);
                if(transitioning){Zoom=Mathf.Lerp(Zoom,zoomGoal,t);if(Vector3.Distance(FocusLocal,focusGoal)<.02f&&Mathf.Abs(Zoom-zoomGoal)<.02f)transitioning=false;}
            }
            Apply();
            if(IsInputCaptured)return;
            var keyboard=Keyboard.current;var mouse=Mouse.current;var move=Vector2.zero;
            if(keyboard!=null)
            {
                if(keyboard.escapeKey.wasPressedThisFrame)TryCancelFollow();
                if(keyboard.wKey.isPressed||keyboard.upArrowKey.isPressed)move.y++;
                if(keyboard.sKey.isPressed||keyboard.downArrowKey.isPressed)move.y--;
                if(keyboard.dKey.isPressed||keyboard.rightArrowKey.isPressed)move.x++;
                if(keyboard.aKey.isPressed||keyboard.leftArrowKey.isPressed)move.x--;
                if(keyboard.qKey.isPressed)yaw-=60*dt;if(keyboard.eKey.isPressed)yaw+=60*dt;
                float keyZoom=(keyboard.pageUpKey.isPressed?1:0)-(keyboard.pageDownKey.isPressed?1:0);
                if(keyZoom!=0)ZoomAtScreenPoint(Camera.pixelRect.center,keyZoom*600*dt);
            }
            Pan(move*Zoom*dt);
            if(mouse!=null)
            {
                ZoomAtScreenPoint(mouse.position.ReadValue(),mouse.scroll.ReadValue().y);
                if(mouse.middleButton.isPressed)Pan(-mouse.delta.ReadValue()*Zoom*.002f);
            }
            Apply();
        }
        private void Apply()
        {
            if(Camera==null||WorldRoot==null)return;
            Camera.orthographic=false;Camera.fieldOfView=FieldOfView;Camera.nearClipPlane=.1f;
            float distance=ViewDistance;Camera.farClipPlane=Mathf.Max(1000,distance+Zoom*4+500);
            var rotation=WorldRoot.rotation*Quaternion.Euler(pitch,yaw,0);
            var eye=WorldRoot.TransformPoint(FocusLocal)-rotation*Vector3.forward*distance;
            if(Surface!=null&&Surface.TryHeight(eye,out float ground))eye.y=Mathf.Max(eye.y,ground+GroundClearance);
            Camera.transform.SetPositionAndRotation(eye,rotation);
        }
    }
}
