using UnityEngine;
using UnityEngine.InputSystem;

namespace ChooGuard.Seeded.Unity
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SeededFpsController : MonoBehaviour
    {
        public Camera View;
        public bool Captured;
        public float WalkSpeed=1.8f,SprintSpeed=3.4f;
        public bool ExternalInputMode { get; private set; }
        public float PitchDegrees => pitch;
        public float YawDegrees => yaw;
        public CollisionFlags LastCollisionFlags { get; private set; }
        public System.Func<Vector3,Vector3> MovementConstraint;
        public System.Func<bool> PointerConsumed;
        CharacterController body;
        float yaw,pitch,vertical;
        void Awake()
        {
            body=GetComponent<CharacterController>();body.height=1.72f;body.radius=.28f;body.center=Vector3.up*.86f;body.skinWidth=.025f;body.stepOffset=.28f;body.slopeLimit=36;
            if(View==null){var go=new GameObject("FPSCamera",typeof(Camera),typeof(AudioListener));go.transform.SetParent(transform,false);View=go.GetComponent<Camera>();}
            View.transform.localPosition=Vector3.up*1.6f;View.nearClipPlane=.05f;View.farClipPlane=1200;View.fieldOfView=70;yaw=transform.eulerAngles.y;
        }
        public void Capture(bool active)
        {
            Captured=active;
            if(ExternalInputMode)return;
            Cursor.lockState=active?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!active;
        }
        /// <summary>Verification input uses the same camera and CharacterController path, without OS events.</summary>
        public void SetExternalInputMode(bool enabled)
        {
            Capture(false); ExternalInputMode=enabled;
        }
        public void Pose(Vector3 feet,float heading)
        {
            if(body==null)Awake();body.enabled=false;transform.SetPositionAndRotation(feet,Quaternion.Euler(0,heading,0));body.enabled=true;yaw=heading;pitch=0;vertical=0;
        }
        void OnApplicationFocus(bool focused){if(!focused&&!ExternalInputMode)Capture(false);}
        void Update()
        {
            if(ExternalInputMode)return;
            var k=Keyboard.current;var m=Mouse.current;
            if(k!=null&&k.escapeKey.wasPressedThisFrame){Capture(false);return;}
            if(!Captured)return;
            Vector2 move=Vector2.zero;if(k!=null){if(k.wKey.isPressed)move.y++;if(k.sKey.isPressed)move.y--;if(k.dKey.isPressed)move.x++;if(k.aKey.isPressed)move.x--;}
            Simulate(move,m!=null?m.delta.ReadValue()*.09f:Vector2.zero,k!=null&&k.leftShiftKey.isPressed,Mathf.Min(Time.deltaTime,.1f));
        }
        public bool StepInput(Vector2 movement,Vector2 lookDegrees,bool sprint,float deltaSeconds)
        {
            return ExternalInputMode&&Captured&&Simulate(movement,lookDegrees,sprint,deltaSeconds);
        }
        bool Simulate(Vector2 movement,Vector2 lookDegrees,bool sprint,float deltaSeconds)
        {
            if(!Captured||body==null||!body.enabled||View==null||!Finite(deltaSeconds)||deltaSeconds<=0||deltaSeconds>.1f||!Finite(movement.x)||!Finite(movement.y)||!Finite(lookDegrees.x)||!Finite(lookDegrees.y))return false;
            if(PointerConsumed!=null&&PointerConsumed())lookDegrees=Vector2.zero;
            yaw+=Mathf.Clamp(lookDegrees.x,-45,45);pitch=Mathf.Clamp(pitch-lookDegrees.y,-85,85);
            transform.rotation=Quaternion.Euler(0,yaw,0);View.transform.localRotation=Quaternion.Euler(pitch,0,0);
            if(body.isGrounded&&vertical<0)vertical=-2;vertical=Mathf.Max(-30,vertical-9.81f*deltaSeconds);
            var planar=(transform.right*movement.x+transform.forward*movement.y).normalized*(sprint?SprintSpeed:WalkSpeed)*deltaSeconds;
            if(MovementConstraint!=null&&planar.sqrMagnitude>0)planar=MovementConstraint(planar);
            if(!Finite(planar.x)||!Finite(planar.y)||!Finite(planar.z))return false;
            var delta=planar+Vector3.up*vertical*deltaSeconds;
            LastCollisionFlags=body.Move(delta);return true;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
    public sealed class SemanticBinding : MonoBehaviour
    {
        public string EntityId;
    }
}
