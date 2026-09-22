using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace ChooGuard.App.Fps
{
    [DisallowMultipleComponent,RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonResponder : MonoBehaviour
    {
        public Camera PlayerCamera;
        public float BodyHeight=1.72f,EyeHeight=1.60f,BodyRadius=.28f;
        public float WalkSpeed=1.8f,SprintSpeed=3.4f,LookDegreesPerPixel=.09f;
        public float InteractionDistance=2.5f;
        public LayerMask InteractionMask=~0;
        public bool EnableJump;
        public float JumpHeight=.35f;
        public bool IsPaused { get; private set; }=true;
        public bool ExternalInputMode { get; private set; }
        public bool HasFocus { get; private set; }
        public bool HasTarget=>targetBehaviour!=null;
        public string CurrentPrompt { get; private set; }="";
        public string LastFeedback { get; private set; }="";
        public Collider CurrentTargetCollider { get; private set; }
        public CollisionFlags LastCollisionFlags { get; private set; }
        public int SuccessfulInteractions { get; private set; }
        public float PitchDegrees=>pitch;
        public float YawDegrees=>yaw;
        public event Action<bool> PauseChanged;
        public event Action<string> FeedbackChanged;
        private CharacterController body;
        private MonoBehaviour targetBehaviour;
        private IFpsInteraction target;
        private float yaw,pitch,verticalVelocity;
        private bool interactionHeld,jumpHeld,suppressCaptureUntilRelease;
        private int firstCaptureFrame;
        private const float MaxStepSeconds=.05f,MaxFrameSeconds=.5f,Gravity=-9.81f;
        private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        private void Awake()
        {
            body=GetComponent<CharacterController>();
            body.height=Mathf.Clamp(BodyHeight,1.4f,2.1f);body.radius=Mathf.Clamp(BodyRadius,.2f,.4f);body.center=Vector3.up*(body.height*.5f);body.skinWidth=.025f;body.stepOffset=.28f;body.slopeLimit=45;body.minMoveDistance=0;
            if(PlayerCamera==null){var cameraObject=new GameObject("FirstPersonCamera",typeof(Camera));cameraObject.transform.SetParent(transform,false);PlayerCamera=cameraObject.GetComponent<Camera>();}
            if(!PlayerCamera.transform.IsChildOf(transform))PlayerCamera.transform.SetParent(transform,false);
            PlayerCamera.transform.localPosition=new Vector3(0,Mathf.Clamp(EyeHeight,1.2f,body.height-.05f),0);PlayerCamera.nearClipPlane=.05f;
            yaw=transform.eulerAngles.y;pitch=0;PlayerCamera.transform.localRotation=Quaternion.identity;
        }
        private void OnEnable(){HasFocus=UnityEngine.Application.isFocused;IsPaused=true;firstCaptureFrame=Time.frameCount+1;UnlockPointer();}
        private void OnDisable(){Pause();UnlockPointer();}
        private void OnApplicationFocus(bool focused){HasFocus=focused;if(!focused){suppressCaptureUntilRelease=true;Pause();}}
        private void OnApplicationPause(bool paused){if(paused){suppressCaptureUntilRelease=true;Pause();}}
        private static void UnlockPointer(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        public void Pause()
        {
            bool changed=!IsPaused;IsPaused=true;ClearTarget();UnlockPointer();if(changed)PauseChanged?.Invoke(true);
        }
        public bool Resume(bool capturePointer=true)
        {
            if(!ExternalInputMode&&(!HasFocus||!capturePointer))return false;
            interactionHeld=ExternalInputMode?false:Keyboard.current!=null&&Keyboard.current.eKey.isPressed;
            jumpHeld=ExternalInputMode?false:Keyboard.current!=null&&Keyboard.current.spaceKey.isPressed;
            IsPaused=false;if(capturePointer&&!ExternalInputMode){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            PauseChanged?.Invoke(false);return true;
        }
        // Explicit root verification adapter. It never synthesizes OS keyboard/mouse events.
        public void SetExternalInputMode(bool enabled){Pause();ExternalInputMode=enabled;}
        private void Update()
        {
            var keyboard=Keyboard.current;var mouse=Mouse.current;
            if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame){Pause();return;}
            if(ExternalInputMode){if(!IsPaused)RefreshInteraction();return;}
            if(!HasFocus){if(!IsPaused)Pause();return;}
            if(IsPaused)
            {
                if(suppressCaptureUntilRelease){if(mouse==null||!mouse.leftButton.isPressed)suppressCaptureUntilRelease=false;return;}
                if(Time.frameCount>=firstCaptureFrame&&mouse!=null&&mouse.leftButton.wasPressedThisFrame&&(EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject()))Resume();return;
            }
            if(Cursor.lockState!=CursorLockMode.Locked){Pause();return;}
            Vector2 movement=Vector2.zero;
            if(keyboard!=null){if(keyboard.wKey.isPressed)movement.y++;if(keyboard.sKey.isPressed)movement.y--;if(keyboard.dKey.isPressed)movement.x++;if(keyboard.aKey.isPressed)movement.x--;}
            if(Time.deltaTime>MaxFrameSeconds){ShowFeedback("긴 화면 지연으로 일시 정지되었습니다. 클릭하여 계속하세요");Pause();return;}
            Simulate(movement,mouse==null?Vector2.zero:mouse.delta.ReadValue()*LookDegreesPerPixel,keyboard!=null&&(keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed),keyboard!=null&&keyboard.eKey.isPressed,keyboard!=null&&keyboard.spaceKey.isPressed,Time.deltaTime);
        }
        public bool StepInput(Vector2 movement,Vector2 lookDegrees,bool sprint,bool interactHeld,bool jumpPressed,float deltaSeconds)
        {
            if(!ExternalInputMode||IsPaused)return false;return Simulate(movement,lookDegrees,sprint,interactHeld,jumpPressed,deltaSeconds);
        }
        private bool Simulate(Vector2 movement,Vector2 look,bool sprint,bool interact,bool jump,float deltaSeconds)
        {
            if(IsPaused||body==null||!body.enabled||!Finite(deltaSeconds)||deltaSeconds<=0||deltaSeconds>MaxFrameSeconds||!Finite(movement.x)||!Finite(movement.y)||!Finite(look.x)||!Finite(look.y))return false;
            if((transform.lossyScale-Vector3.one).sqrMagnitude>.0001f){ShowFeedback("플레이어의 미터 단위 크기 설정을 확인하세요");Pause();return false;}
            int steps=Mathf.CeilToInt(deltaSeconds/MaxStepSeconds);float dt=deltaSeconds/steps;yaw=Mathf.Repeat(yaw+Mathf.Clamp(look.x,-45,45),360);pitch=Mathf.Clamp(pitch-Mathf.Clamp(look.y,-45,45),-85,85);
            transform.rotation=Quaternion.Euler(0,yaw,0);PlayerCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            movement=Vector2.ClampMagnitude(movement,1);float speed=Mathf.Clamp(sprint?SprintSpeed:WalkSpeed,0,5);
            bool jumpEdge=jump&&!jumpHeld;jumpHeld=jump;
            LastCollisionFlags=CollisionFlags.None;
            for(int step=0;step<steps;step++)
            {
                if(body.isGrounded&&verticalVelocity<0)verticalVelocity=-2;
                if(step==0&&EnableJump&&jumpEdge&&body.isGrounded)verticalVelocity=Mathf.Sqrt(2*-Gravity*Mathf.Clamp(JumpHeight,0,.8f));
                verticalVelocity=Mathf.Max(-30,verticalVelocity+Gravity*dt);
                var motion=(transform.right*movement.x+transform.forward*movement.y)*speed*dt+Vector3.up*(verticalVelocity*dt);
                var flags=body.Move(motion);LastCollisionFlags|=flags;
                if((flags&CollisionFlags.Above)!=0&&verticalVelocity>0)verticalVelocity=0;if((flags&CollisionFlags.Below)!=0&&verticalVelocity<0)verticalVelocity=-2;
            }
            RefreshInteraction();bool edge=interact&&!interactionHeld;interactionHeld=interact;if(edge)TryInteract();return true;
        }
        public void RefreshInteraction()
        {
            ClearTarget();if(IsPaused||PlayerCamera==null)return;
            if(!Physics.Raycast(PlayerCamera.transform.position,PlayerCamera.transform.forward,out var hit,Mathf.Clamp(InteractionDistance,.25f,3),InteractionMask,QueryTriggerInteraction.Ignore))return;
            // Only the nearest real solid collider is considered. A wall cannot be skipped.
            if(hit.collider.transform.IsChildOf(transform))return;
            foreach(var behaviour in hit.collider.GetComponentsInParent<MonoBehaviour>())if(behaviour is IFpsInteraction candidate)
            {
                targetBehaviour=behaviour;target=candidate;CurrentTargetCollider=hit.collider;
                CurrentPrompt=candidate.CanInteract(this,out var reason)?"E · "+candidate.InteractionPrompt:reason??"지금은 사용할 수 없습니다";return;
            }
        }
        public bool TryInteract()
        {
            if(IsPaused)return false;RefreshInteraction();if(targetBehaviour==null||target==null)return false;
            if(!target.CanInteract(this,out var reason)){if(!string.IsNullOrEmpty(reason))ShowFeedback(reason);return false;}
            bool performed=target.TryInteract(this,out var feedback);if(performed)SuccessfulInteractions++;if(!string.IsNullOrEmpty(feedback))ShowFeedback(feedback);RefreshInteraction();return performed;
        }
        public void ShowFeedback(string message){LastFeedback=message??"";FeedbackChanged?.Invoke(LastFeedback);}
        private void ClearTarget(){target=null;targetBehaviour=null;CurrentTargetCollider=null;CurrentPrompt="";}
    }
}
