using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ChooGuard.App.Fps.Shell;

namespace ChooGuard.App.Fps.TrainDeparture
{
    public interface IDepartureInput
    {
        bool FocusAvailable { get; }
        bool IsHolding { get; }
        float PhysicalProgress { get; }
        void StepInput(bool pressed,bool held,Vector2 delta,float wheel,float dt);
        void Release();
    }
    [DefaultExecutionOrder(200)]
    public sealed class TrainDepartureGame : MonoBehaviour
    {
        public FirstPersonResponder Player;
        public Camera MenuCamera;
        public DepartureGameView View;
        public Transform TrainRoot;
        public Vector3 ArrivalDirection = new Vector3(.347f,0,.938f);
        public float ArrivalDistance = 45, ArrivalSeconds = 9;
        public DepartureDoor[] Doors = Array.Empty<DepartureDoor>();
        public DeparturePortal ThirdCoachPortal;
        public BoxCollider CrewSpace;
        public Transform OwnBag, BagCarryAnchor;
        public Collider BagSupport;
        public Renderer Exterior, Airbag, Bogie, ClosedLamp;
        public AudioClip CrewReport, CaptainAnnouncement, DepartureRadio;
        public DepartureAnimationRig AnimationRig;
        public DepartureSignalEmote SignalEmote;
        public bool IsEmoting=>SignalEmote!=null&&SignalEmote.IsPlaying;
        public DepartureKeyInventory KeyInventory;
        public DepartureContextActions ContextActions;
        public DepartureItemSlots ItemSlots;
        public bool HasOwnBag => bagCarried;
        public DepartureManipulable LuggageHandle;
        public Transform LuggageStowTarget;
        public bool Started { get; private set; }
        public bool Completed => Flow.Stage == DepartureStage.Complete;
        public DepartureFlow Flow { get; private set; } = new DepartureFlow();
        public BerneDoorMechanism Mechanism { get; private set; } = new BerneDoorMechanism();
        public bool Receiving { get; private set; }
        public bool Transmitting { get; private set; }
        public bool InsideTrain { get; private set; }
        public float ArrivalProgress { get; private set; }
        public event Action<bool,bool> RadioChanged;
        public event Action<bool,bool,float> GripChanged;
        public event Action Restarted;
        Vector3 parkedTrain, startFeet, bagStart;
        Quaternion bagRotation;
        float startYaw,arrivalElapsed;
        bool bagCarried=true,radioPlayed,bagStowing;
        readonly DepartureRadioPlaybackGuard txPlayback = new DepartureRadioPlaybackGuard();
        AudioClip acceptedTxClip;
        float bagStowTime;Vector3 bagStowStart;Quaternion bagStowRotation;
        readonly RaycastHit[] observationHits = new RaycastHit[16];
        Rigidbody bagBody;
        AudioSource voice;
        IDepartureInput input;
        CharacterController body;
        float standingHeight,standingEye;
        bool crouched,resumeAfterFocus,requestFocusResume;
        void Awake()
        {
            if(Player==null||TrainRoot==null||View==null||MenuCamera==null) { Debug.LogError("Departure scene bindings are incomplete.",this);enabled=false;return; }
            parkedTrain=TrainRoot.position;startFeet=Player.transform.position;startYaw=Player.transform.eulerAngles.y;
            body=Player.GetComponent<CharacterController>();standingHeight=body.height;standingEye=Player.PlayerCamera.transform.localPosition.y;
            Player.SimpleControls=false;Player.SuppressInteractionInput=true;
            foreach(var component in GetComponents<MonoBehaviour>())if(component is IDepartureInput control)input=control;
            if(ContextActions==null)ContextActions=GetComponent<DepartureContextActions>();
            if(ContextActions==null)ContextActions=gameObject.AddComponent<DepartureContextActions>();
            ContextActions.Game=this;
            if(OwnBag!=null)
            {
                bagStart=OwnBag.position;bagRotation=OwnBag.rotation;bagBody=OwnBag.GetComponent<Rigidbody>();
                if(bagBody!=null)bagBody.isKinematic=true;
            }
            if(ItemSlots==null)ItemSlots=GetComponent<DepartureItemSlots>();
            if(ItemSlots==null)ItemSlots=gameObject.AddComponent<DepartureItemSlots>();
            ItemSlots.Game=this;ItemSlots.Initialize();
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.volume=.85f;
            IgnoreCarriedCollisions();
            foreach(var door in Doors)door.Capture();
            Flow.StageChanged+=OnStage;
            Player.Pause();AnimationRig?.SetPaused(true);Player.PlayerCamera.enabled=false;MenuCamera.enabled=true;
            View.Game=this;View.Build();View.ShowMain();
            var hud=GetComponent<DeparturePlayHud>();if(hud!=null){hud.Game=this;hud.Build();}
        }
        public void Begin()
        {
            if(!enabled||Started||!Player.Resume())return;
            Started=true;arrivalElapsed=0;ArrivalProgress=0;
            AnimationRig?.SetBagState(bagCarried,false);AnimationRig?.SetPaused(false);SignalEmote?.SetPaused(false);
            TrainRoot.position=parkedTrain-ArrivalDirection.normalized*ArrivalDistance;
            foreach(var door in Doors)door.SetOpen(0);
            MenuCamera.enabled=false;Player.PlayerCamera.enabled=true;View.HideMenu();
        }
        public void Resume()
        {
            if(!Started||Completed||!Player.Resume())return;
            voice.UnPause();AnimationRig?.SetPaused(false);SignalEmote?.SetPaused(false);View.HideMenu();
        }
        public void Restart()
        {
            ReleaseInput();voice.Stop();Started=false;Flow.Reset();Mechanism.Reset();InsideTrain=false;
            Receive(false);Transmit(false);radioPlayed=false;arrivalElapsed=0;
            TrainRoot.position=parkedTrain;foreach(var door in Doors)door.SetOpen(1);
            Player.Pause();Player.RestorePhysicalPose(startFeet,startYaw,0);Player.SuppressInteractionInput=true;
            bagCarried=true;bagStowing=false;bagStowTime=0;if(OwnBag!=null){OwnBag.gameObject.SetActive(true);if(bagBody!=null){bagBody.isKinematic=true;}OwnBag.SetPositionAndRotation(bagStart,bagRotation);}
            ThirdCoachPortal?.ResetCrossing();KeyInventory?.ResetItem();Restarted?.Invoke();
            SignalEmote?.ResetEmote();AnimationRig?.ResetRig();AnimationRig?.SetBagState(true,false);AnimationRig?.SetPaused(true);
            MenuCamera.enabled=true;Player.PlayerCamera.enabled=false;View.ShowMain();
        }
        public void ExitApplication(){ReleaseInput();SceneFlow.Quit();}
        void ReleaseInput(){ContextActions?.Cancel();ItemSlots?.CancelTransition();input?.Release();SignalEmote?.SetPaused(true);AnimationRig?.SetPaused(true);}
        void Receive(bool value){Receiving=value;RadioChanged?.Invoke(Receiving,Transmitting);}
        void Transmit(bool value){Transmitting=value;RadioChanged?.Invoke(Receiving,Transmitting);}
        void OnStage(DepartureStage stage)
        {
            radioPlayed=false;
            if(stage==DepartureStage.Complete)
            {
                ReleaseInput();Player.Pause();View.ShowComplete();
            }
        }
        void OnApplicationFocus(bool focused)
        {
            if(!focused)resumeAfterFocus=Started&&!Completed&&View!=null&&!View.MenuVisible;
            else if(resumeAfterFocus){requestFocusResume=true;resumeAfterFocus=false;}
        }
        void IgnoreCarriedCollisions()
        {
            if(body==null)return;
            if(OwnBag!=null&&OwnBag.gameObject.activeInHierarchy)
                foreach(var collider in OwnBag.GetComponentsInChildren<Collider>())if(collider.enabled)Physics.IgnoreCollision(body,collider,true);
            if(KeyInventory!=null&&KeyInventory.KeyCollider!=null&&KeyInventory.KeyCollider.enabled)Physics.IgnoreCollision(body,KeyInventory.KeyCollider,true);
            if(ContextActions!=null&&ContextActions.RadioColliders!=null)
                foreach(var collider in ContextActions.RadioColliders)if(collider!=null&&collider.enabled)Physics.IgnoreCollision(body,collider,true);
            // IgnoreCollision pairs are not scene-serialized. Keep the three authored query
            // meshes raycastable while excluding only their collision with this player body.
            if(ContextActions!=null)
            {
                IgnoreInspectionCollision(ContextActions.ExteriorContact);
                IgnoreInspectionCollision(ContextActions.AirbagContact);
                IgnoreInspectionCollision(ContextActions.BogieContact);
            }
        }
        void IgnoreInspectionCollision(Collider contact)
        {
            if(body.enabled&&contact!=null&&contact!=body&&contact.enabled&&contact.gameObject.activeInHierarchy)
                Physics.IgnoreCollision(body,contact,true);
        }
        void Update()
        {
            if(!enabled)return;
            if(requestFocusResume&&Player.HasFocus){requestFocusResume=false;Resume();}
            bool cancel=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame || Gamepad.current!=null&&Gamepad.current.buttonEast.wasPressedThisFrame;
            if(View.MenuVisible)
            {
                if(cancel)View.HandleCancel();
                if(View.MenuVisible){if(!Player.IsPaused)Player.Pause();ReleaseInput();voice.Pause();}
                return;
            }
            if(!Started||Completed)return;
            if(Player.IsPaused||!Player.ExternalInputMode&&!Player.HasFocus)
            {ReleaseInput();voice.Pause();View.ShowPause();return;}
            AnimationRig?.SetPaused(false);SignalEmote?.SetPaused(false);
            if(Player.ExternalInputMode)return;
            var keys=Keyboard.current;
            // Official InputSystem SimpleController_UsingState edge pattern (1.20.2,
            // 54b1581acb600c1f00998505af19a644795ef9b8, Unity Companion License).
            // Only this root samples hardware. A released F does not cancel the accepted action.
            if(keys!=null)
            {
                // The same official edge module; one hardware owner, no queued digit spam.
                if(keys.digit1Key.wasPressedThisFrame||keys.numpad1Key.wasPressedThisFrame)RequestSlot(1);
                else if(keys.digit2Key.wasPressedThisFrame||keys.numpad2Key.wasPressedThisFrame)RequestSlot(2);
                else if(keys.digit3Key.wasPressedThisFrame||keys.numpad3Key.wasPressedThisFrame)RequestSlot(3);
                else if(keys.digit4Key.wasPressedThisFrame||keys.numpad4Key.wasPressedThisFrame)RequestSlot(4);
                if(keys.fKey.wasPressedThisFrame)ContextActions.QueueInteraction();
            }
            StepWorld(Time.deltaTime,false,false,Vector2.zero,0,false,
                keys!=null&&keys.gKey.isPressed,keys!=null&&keys.leftCtrlKey.isPressed);
        }
        // Legacy arguments remain ABI-compatible for native adapters but never drive production mechanics.
        // External checks call ContextActions.QueueInteraction(), then StepWorld; G remains signal.
        public void QueueInteraction() { ContextActions?.QueueInteraction(); }
        public bool RequestSlot(int slot) => ItemSlots != null && ItemSlots.RequestSlot(slot);
        public bool IsClosedLampVisible() => LookingAt(ClosedLamp,2);
        void OnDisable() { ContextActions?.Cancel(); ItemSlots?.CancelTransition(); }
        // Native input and external integration checks share physical simulation; no next-step UI exists.
        public void StepWorld(float dt,bool pressed,bool held,Vector2 delta,float wheel,bool ptt,bool signal,bool crouch)
        {
            if(!Started||Completed||Player.IsPaused||float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0||dt>.25f){ContextActions?.Cancel();ItemSlots?.CancelTransition();return;}
            if(!IsEmoting&&(ContextActions==null||!ContextActions.IsBusy)&&(ItemSlots==null||!ItemSlots.IsSwitching))SetCrouch(crouch);
            KeyInventory?.Step(dt);
            ItemSlots?.Step(dt);
            IgnoreCarriedCollisions();
            UpdateBag(dt);
            AnimationRig?.SetBagState(bagCarried,bagStowing);
            AnimationRig?.SetKeyHeld(KeyInventory!=null&&KeyInventory.IsInHand);
            if(Flow.Stage==DepartureStage.WaitForArrival)
            {
                arrivalElapsed+=dt;ArrivalProgress=Mathf.Clamp01(arrivalElapsed/Mathf.Max(1,ArrivalSeconds));
                float t=1-Mathf.Pow(1-ArrivalProgress,3);
                TrainRoot.position=parkedTrain-ArrivalDirection.normalized*ArrivalDistance*(1-t);
                if(ArrivalProgress>=1){TrainRoot.position=parkedTrain;Flow.TryAction(DepartureAction.TrainStopped);}
                Physics.SyncTransforms();
                return;
            }
            if(Flow.Stage==DepartureStage.BoardForLuggage)
                foreach(var door in Doors)if(door.OpenOnArrival)door.SetOpen(Mathf.MoveTowards(door.OpenAmount,1,dt*.7f));
            if(ThirdCoachPortal!=null)
            {
                int crossing=ThirdCoachPortal.Sample(Player.transform.position);
                if(crossing!=0)
                {
                    InsideTrain=crossing>0;
                    Flow.TryAction(InsideTrain?DepartureAction.EnterThirdCoach:DepartureAction.ExitThirdCoach);
                }
            }
            ContextActions?.BeforeWorld(dt);
            if(Flow.Stage==DepartureStage.ReceiveCrewReport)PlayReceived(CrewReport,DepartureAction.CrewInspectionReport);
            AnimationRig?.SetBagState(bagCarried,bagStowing);
            AnimationRig?.SetKeyHeld(KeyInventory!=null&&KeyInventory.IsInHand);
            if(SignalEmote!=null&&SignalEmote.Step(signal,!InsideTrain,Flow.Stage==DepartureStage.SignalCaptain&&(ItemSlots==null||ItemSlots.SelectedSlot==4&&!ItemSlots.IsSwitching)&&(ContextActions==null||!ContextActions.IsBusy)&&(KeyInventory==null||!KeyInventory.OccupiesHand)&&(input==null||!input.IsHolding),dt))
                Flow.TryAction(DepartureAction.SignalCaptain);
            if(Flow.Stage==DepartureStage.ReceiveAnnouncement)PlayReceived(CaptainAnnouncement,DepartureAction.CaptainAnnouncement);
            GripChanged?.Invoke(ContextActions!=null&&ContextActions.FocusAvailable,ContextActions!=null&&ContextActions.IsBusy,ContextActions!=null?ContextActions.PhysicalProgress:-1);
            if((Flow.Stage==DepartureStage.CloseDoors||Flow.Stage==DepartureStage.ObserveClosedLamp)&&Mechanism.CloseCommand)
                foreach(var door in Doors)door.SetOpen(Mathf.MoveTowards(door.OpenAmount,0,dt*.55f));
            int closed=0;foreach(var door in Doors)if(door.IsClosed)closed++;
            Mechanism.ReadDoorFeedback(closed,Doors.Length==16?16:0); // This consist has exactly sixteen independent door groups.
            if(Flow.Stage==DepartureStage.CloseDoors&&Mechanism.ClosedLamp)Flow.TryAction(DepartureAction.AllDoorsClosed);
            if(Flow.Stage==DepartureStage.RetrieveKey&&Mechanism.Retrieved)Flow.TryAction(DepartureAction.RetrieveKey);
            if(Flow.Stage==DepartureStage.SendDepartureRadio)UpdatePtt(ContextActions!=null&&ContextActions.RadioPttHeld);
            ContextActions?.AfterWorld();
            Physics.SyncTransforms();
        }
        void PlayReceived(AudioClip clip,DepartureAction action)
        {
            if(clip==null)return;
            if(!radioPlayed){radioPlayed=true;voice.clip=clip;voice.Play();Receive(true);return;}
            if(voice.isPlaying)return;
            Receive(false);Flow.TryAction(action);
        }
        void UpdatePtt(bool held)
        {
            if(!held){if(Transmitting)CancelDepartureTransmission();return;}
            if(!radioPlayed)
            {
                if(DepartureRadio==null||voice==null||!voice.isActiveAndEnabled||AudioListener.pause||
                    DepartureRadio.loadState!=AudioDataLoadState.Loaded)
                {ContextActions?.Cancel();return;}
                acceptedTxClip=DepartureRadio;
                voice.clip=acceptedTxClip;voice.loop=false;voice.pitch=1;
                // Official AudioSource PCM position + AudioSettings DSP-clock modules, not game-time completion.
                if(!txPlayback.Begin(acceptedTxClip.samples,acceptedTxClip.frequency,AudioSettings.dspTime))
                {ContextActions?.Cancel();return;}
                radioPlayed=true;voice.Play();Transmit(true);return;
            }
            bool valid=voice!=null&&voice.isActiveAndEnabled&&acceptedTxClip!=null&&
                DepartureRadio==acceptedTxClip&&voice.clip==acceptedTxClip&&acceptedTxClip.loadState==AudioDataLoadState.Loaded&&
                !AudioListener.pause&&!voice.loop&&Mathf.Abs(voice.pitch-1)<.0001f;
            var result=txPlayback.Observe(valid,valid&&voice.isPlaying,valid?voice.timeSamples:0,AudioSettings.dspTime);
            if(result==DepartureRadioPlaybackStatus.Interrupted){ContextActions?.Cancel();return;}
            if(result==DepartureRadioPlaybackStatus.Completed){Transmit(false);ContextActions?.NotifyRadioVoiceFinished();}
        }
        public void CancelDepartureTransmission()
        {
            if(Transmitting){if(voice!=null)voice.Stop();Transmit(false);}
            txPlayback.Reset();acceptedTxClip=null;
            if(Flow.Stage==DepartureStage.SendDepartureRadio)radioPlayed=false;
        }
        bool LookingAt(Renderer target,float reach)
        {
            if(target==null||!target.enabled||!target.gameObject.activeInHierarchy)return false;
            var eye=Player.PlayerCamera.transform;var towards=target.bounds.center-eye.position;
            if(towards.magnitude>reach||Vector3.Dot(eye.forward,towards.normalized)<.97f)return false;
            // Reuse the reviewed OpenKCC nearest-solid/self-filter foundation for downward inspection too.
            var bag=OwnBag!=null&&OwnBag.gameObject.activeInHierarchy?OwnBag:null;
            var key=KeyInventory!=null&&!KeyInventory.IsSocketed?KeyInventory.KeyModel:null;
            if(!NearestSolidRay.TryCast(eye.position,towards.normalized,reach,~0,observationHits,Player.transform,bag,key,out var hit,out _))return false;
            var contactBounds=target.bounds;contactBounds.Expand(.025f);
            return contactBounds.Contains(hit.point);
        }
        public void BeginLuggageStow()
        {
            if(!Started||Player.IsPaused||!InsideTrain||!bagCarried||bagStowing||OwnBag==null||LuggageStowTarget==null||LuggageHandle==null||
                LuggageHandle.Amount<LuggageHandle.PressThreshold||KeyInventory!=null&&KeyInventory.OccupiesHand)return;
            if(Flow.Stage==DepartureStage.EnterCrewSpace)Flow.TryAction(DepartureAction.EnterCrewSpace);
            if(Flow.Stage!=DepartureStage.StowLuggage)return;
            bagStowing=true;bagStowTime=0;bagStowStart=OwnBag.position;bagStowRotation=OwnBag.rotation;
        }
        void UpdateBag(float dt)
        {
            if(OwnBag==null||BagCarryAnchor==null||!bagCarried)return;
            if(!bagStowing&&ItemSlots!=null&&(!ItemSlots.BagVisible||ItemSlots.IsSwitching))return;
            if(bagStowing)
            {
                bagStowTime=Mathf.Min(.5f,bagStowTime+dt);float t=Mathf.SmoothStep(0,1,bagStowTime/.5f);
                OwnBag.SetPositionAndRotation(Vector3.Lerp(bagStowStart,LuggageStowTarget.position,t),Quaternion.Slerp(bagStowRotation,LuggageStowTarget.rotation,t));
                if(bagStowTime>=.5f){bagCarried=false;bagStowing=false;OwnBag.gameObject.SetActive(false);Flow.TryAction(DepartureAction.PlaceOwnBag);}
                return;
            }
            OwnBag.SetPositionAndRotation(Vector3.Lerp(OwnBag.position,BagCarryAnchor.position,1-Mathf.Exp(-dt*16)),Quaternion.Slerp(OwnBag.rotation,BagCarryAnchor.rotation,1-Mathf.Exp(-dt*12)));
        }
        void SetCrouch(bool down)
        {
            if(down==crouched)return;
            if(!down)
            {
                var feet=Player.transform.position;var radius=body.radius*.9f;
                if(Physics.CheckCapsule(feet+Vector3.up*(body.height-radius),feet+Vector3.up*(standingHeight-radius),radius,~(1<<Player.gameObject.layer),QueryTriggerInteraction.Ignore))return;
            }
            crouched=down;body.height=down?1.05f:standingHeight;body.center=Vector3.up*(body.height*.5f);
            Player.PlayerCamera.transform.localPosition=Vector3.up*(down?.94f:standingEye);
        }
        static bool Contains(BoxCollider box,Vector3 point)
        {if(box==null)return false;var p=box.transform.InverseTransformPoint(point)-box.center;var h=box.size*.5f;return Mathf.Abs(p.x)<=h.x&&Mathf.Abs(p.y)<=h.y&&Mathf.Abs(p.z)<=h.z;}
    }
    [Serializable]
    public sealed class DepartureDoor
    {
        public Transform[] Pieces=Array.Empty<Transform>();
        public Vector3[] OpenPositions=Array.Empty<Vector3>(),ClosedPositions=Array.Empty<Vector3>();
        public float OpenAmount=1;
        public bool OpenOnArrival=true;
        public bool IsClosed
        {get{if(Pieces.Length==0||ClosedPositions.Length!=Pieces.Length)return false;for(int i=0;i<Pieces.Length;i++)if(Pieces[i]==null||Vector3.Distance(Pieces[i].localPosition,ClosedPositions[i])>.00003f)return false;return true;}}
        public void Capture(){if(OpenPositions.Length!=Pieces.Length){OpenPositions=new Vector3[Pieces.Length];for(int i=0;i<Pieces.Length;i++)OpenPositions[i]=Pieces[i].localPosition;}}
        public void SetOpen(float amount){if(Pieces.Length==0||OpenPositions.Length!=Pieces.Length||ClosedPositions.Length!=Pieces.Length)return;OpenAmount=Mathf.Clamp01(amount);for(int i=0;i<Pieces.Length;i++)if(Pieces[i]!=null)Pieces[i].localPosition=Vector3.Lerp(ClosedPositions[i],OpenPositions[i],OpenAmount);}
    }
}
