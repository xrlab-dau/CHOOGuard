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
        CharacterController body;
        float yaw,pitch,vertical;
        void Awake()
        {
            body=GetComponent<CharacterController>();body.height=1.72f;body.radius=.28f;body.center=Vector3.up*.86f;body.skinWidth=.025f;body.stepOffset=.28f;body.slopeLimit=36;
            if(View==null){var go=new GameObject("FPSCamera",typeof(Camera),typeof(AudioListener));go.transform.SetParent(transform,false);View=go.GetComponent<Camera>();}
            View.transform.localPosition=Vector3.up*1.6f;View.nearClipPlane=.05f;View.farClipPlane=1200;View.fieldOfView=70;yaw=transform.eulerAngles.y;
        }
        public void Capture(bool active){Captured=active;Cursor.lockState=active?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!active;}
        public void Pose(Vector3 feet,float heading)
        {
            if(body==null)Awake();body.enabled=false;transform.SetPositionAndRotation(feet,Quaternion.Euler(0,heading,0));body.enabled=true;yaw=heading;pitch=0;vertical=0;
        }
        void OnApplicationFocus(bool focused){if(!focused)Capture(false);}
        void Update()
        {
            var k=Keyboard.current;var m=Mouse.current;
            if(k!=null&&k.escapeKey.wasPressedThisFrame){Capture(false);return;}
            if(!Captured)return;
            Vector2 move=Vector2.zero;if(k!=null){if(k.wKey.isPressed)move.y++;if(k.sKey.isPressed)move.y--;if(k.dKey.isPressed)move.x++;if(k.aKey.isPressed)move.x--;}
            if(m!=null){var look=m.delta.ReadValue()*.09f;yaw+=Mathf.Clamp(look.x,-45,45);pitch=Mathf.Clamp(pitch-look.y,-85,85);}
            transform.rotation=Quaternion.Euler(0,yaw,0);View.transform.localRotation=Quaternion.Euler(pitch,0,0);
            float dt=Mathf.Min(Time.deltaTime,.1f);if(body.isGrounded&&vertical<0)vertical=-2;vertical=Mathf.Max(-30,vertical-9.81f*dt);
            var delta=(transform.right*move.x+transform.forward*move.y).normalized*(k!=null&&k.leftShiftKey.isPressed?SprintSpeed:WalkSpeed)*dt+Vector3.up*vertical*dt;
            body.Move(delta);
        }
    }
    public sealed class SemanticBinding : MonoBehaviour
    {
        public string EntityId;
    }
}
