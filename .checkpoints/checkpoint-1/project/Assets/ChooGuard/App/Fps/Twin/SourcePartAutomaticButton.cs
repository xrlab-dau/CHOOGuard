using ChooGuard.App.Fps.Facilities;
using UnityEngine;
namespace ChooGuard.App.Fps.Twin
{
    /// <summary>Operate the real named source push plate only when one actual door controller was explicitly reconciled.</summary>
    [DisallowMultipleComponent]
    public sealed class SourcePartAutomaticButton : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsHoldInteraction, IFpsObservable
    {
        public StationDoor Door;
        public string Label = "자동문 누름 버튼";
        public MeshFilter Plate;
        public Vector3 PressDirectionLocal = Vector3.right;
        public float Travel = .002f;
        public const float PressSeconds=.25f;
        public int AcceptedPresses { get; private set; }
        float held=-1, depression;
        bool sent;
        Mesh deform, source;
        MeshFilter capturedPlate;
        Vector3[] original,vertices, originalNormals;
        Vector4[] originalTangents;
        bool[] front;
        Vector3 capturedAxis;
        float appliedDepression = float.NaN;
        public string DisplayName=>Label;
        public string StateText=>Door==null?"문 구동 연결 미확인":Door.StateText;
        public string InteractionPrompt=>"자동문 열림 요청";
        public bool CanInteract(FirstPersonResponder responder,out string reason)
        {
            reason=Door==null?"실제 연결 문을 확인하지 못했습니다":!Door.Usable?Door.Sealed:Door.Kind!=StationDoor.DoorKind.Sliding?"자동문 구동부가 아닙니다":null;
            if(reason==null&&!EnsurePlate())reason="누름판이 작동하지 않습니다";
            return reason==null;
        }
        public bool TryInteract(FirstPersonResponder responder,out string feedback)
        {
            if(!CanInteract(responder,out feedback))return false;
            if(!Door.RequestButtonOpen(out feedback))return false;
            AcceptedPresses++; depression=1;return true;
        }
        void Awake() { EnsurePlate(); }
        bool EnsurePlate()
        {
            if(!float.IsFinite(Travel)||Travel<=0||Travel>.02f||
                !float.IsFinite(PressDirectionLocal.x)||!float.IsFinite(PressDirectionLocal.y)||!float.IsFinite(PressDirectionLocal.z)||
                PressDirectionLocal.sqrMagnitude<1e-8f||Plate==null||Plate.sharedMesh==null||!Plate.sharedMesh.isReadable)return false;
            bool current=deform!=null&&capturedPlate==Plate&&Plate.sharedMesh==deform;
            var candidate=current?original:Plate.sharedMesh.vertices;
            if(candidate.Length==0)return false;
            var axis=PressDirectionLocal.normalized;float low=float.PositiveInfinity,high=float.NegativeInfinity;
            foreach(var p in candidate)
            {
                float d=Vector3.Dot(p,axis);
                if(!float.IsFinite(d))return false;
                low=Mathf.Min(low,d);high=Mathf.Max(high,d);
            }
            float worldThickness=Plate.transform.TransformVector(axis*(high-low)).magnitude;
            if(high-low<1e-6f||!float.IsFinite(worldThickness)||Travel>=worldThickness*.8f)return false;
            if(current&&(capturedAxis-axis).sqrMagnitude<1e-10f)return true;
            ReleasePlate();
            source=Plate.sharedMesh;capturedPlate=Plate;capturedAxis=axis;
            original=source.vertices;originalNormals=source.normals;originalTangents=source.tangents;
            deform=Instantiate(source);deform.name=source.name+"_push_plate_runtime";Plate.sharedMesh=deform;
            original=deform.vertices;vertices=(Vector3[])original.Clone();front=new bool[original.Length];
            // The source plate is an extruded body. Its mounting/back plane remains fixed.
            for(int i=0;i<front.Length;i++)front[i]=Vector3.Dot(original[i],axis)<low+(high-low)*.2f;
            appliedDepression=0;return true;
        }
        void Update()
        {
            if(held<0)depression=Mathf.MoveTowards(depression,0,Time.deltaTime/.15f);
            if(deform==null||Mathf.Approximately(appliedDepression,depression))return;
            if(!EnsurePlate()){held=-1;sent=false;depression=0;ReleasePlate();return;}
            var direction=Plate.transform.TransformVector(PressDirectionLocal).normalized;
            var localTravel=Plate.transform.InverseTransformVector(direction*(Travel*depression));
            for(int i=0;i<vertices.Length;i++)vertices[i]=original[i]+(front[i]?localTravel:Vector3.zero);
            deform.vertices=vertices;
            if(depression<=0)
            {
                deform.normals=originalNormals;deform.tangents=originalTangents;
            }
            else
            {
                deform.RecalculateNormals();
                if(deform.uv.Length==vertices.Length)deform.RecalculateTangents();
            }
            deform.RecalculateBounds();appliedDepression=depression;
        }
        void ReleasePlate()
        {
            if(capturedPlate!=null&&capturedPlate.sharedMesh==deform&&deform!=null)capturedPlate.sharedMesh=source;
            if(deform!=null)Destroy(deform);
            deform=null;source=null;capturedPlate=null;original=null;vertices=null;front=null;
            originalNormals=null;originalTangents=null;appliedDepression=float.NaN;
        }
        void OnDisable(){held=-1;sent=false;depression=0;ReleasePlate();}
        void OnDestroy(){ReleasePlate();}
        public HoldStyle HoldStyle=>HoldStyle.Press;
        public bool Holdable(FirstPersonResponder responder)=>responder!=null&&!responder.IsPaused&&CanInteract(responder,out _);
        public void BeginHold(FirstPersonResponder responder){held=0;sent=false;}
        public void Hold(FirstPersonResponder responder,Vector2 mouse,float wheel,float deltaSeconds)
        {
            if(held<0||sent||deltaSeconds<=0)return;held+=deltaSeconds;depression=Mathf.Clamp01(held/PressSeconds);
            if(held>=PressSeconds){sent=true;TryInteract(responder,out var feedback);responder?.ShowFeedback(feedback);}
        }
        public string EndHold(FirstPersonResponder responder){bool partial=!sent&&held>0;held=-1;return partial?"버튼을 끝까지 누르지 않았습니다":null;}
        public float HoldProgress=>held<0?-1:Mathf.Clamp01(held/PressSeconds);
        public string Observe(FirstPersonResponder responder)=>DisplayName+" · "+StateText;
    }
}
