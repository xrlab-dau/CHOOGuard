using System.Collections;
using ChooGuard.Seeded;
using ChooGuard.Seeded.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.StationFireTutorial
{
    public sealed class DetailedCarryCollisionTests
    {
        GameObject root;
        Mouse testMouse,previousMouse;
        Keyboard testKeyboard,previousKeyboard;
        InputSettings previousInputSettings,temporarySettingsBackup,testInputSettings;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible;
        sealed class Host:IDetailedManipulationHost
        {
            public ChooGuard.Seeded.World State{get;set;}public SeededFpsController Player;
            public Camera View=>Player!=null?Player.View:null;public Transform Actor=>Player!=null?Player.transform:null;
            public Transform Root{get;set;}public bool Running=>true;public bool Captured=>Player!=null&&Player.Captured;
            public bool IsReplaying=>false;public StationWorldAdapter WorldSnapshot=>null;public string ResolveResponseSubject(string id)=>id;
        }
        void Fixture(float toolY,out Host host,out DetailedManipulationController controls,out Transform tool,string profile="small-tool",bool carried=true)
        {
            Time.timeScale=1;root=new GameObject("carry collision regression");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform,false);
            floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(8,1,8);
            var player=new GameObject("actor");player.transform.SetParent(root.transform,false);
            var motor=player.AddComponent<SeededFpsController>();motor.Pose(Vector3.up*.03f,0);motor.SetExternalInputMode(true);motor.Capture(true);
            var world=new ChooGuard.Seeded.World{Seed=318,StepSeconds=.05};world.Add(new Entity{Id="player",Kind="staff"});
            world.Add(new Entity{Id="tool",Kind="small-tool",Y=toolY,Z=.85});
            world.Entities["player"].Set("mech.carry.tool",carried?1:0);
            host=new Host{State=world,Player=motor,Root=root.transform};var mechanics=new MechanicalInteractions(world);mechanics.Attach("tool",profile);mechanics.SetInteractionCenter("tool",0,toolY,.85);
            controls=root.AddComponent<DetailedManipulationController>();controls.Initialize(host,mechanics,false);controls.SetExternalInputMode(true);controls.ShowHints=false;
            var prop=GameObject.CreatePrimitive(PrimitiveType.Cube);tool=prop.transform;tool.SetParent(root.transform,false);
            tool.SetPositionAndRotation(new Vector3(0,toolY,.85f),Quaternion.identity);tool.localScale=new Vector3(.7f,.8f,.2f);
            controls.BindEquipment("tool",tool,new[]{new DetailedPartPoseBinding{PartId="body",Part=tool,DrivePosition=false,DriveRotation=false}},new Vector3(0,-1.05f,.85f));
            motor.MovementConstraint=controls.ConstrainActorMovement;Physics.SyncTransforms();
        }
        [UnityTest] public IEnumerator CarryingBlocksAtWallAndAllowsBackingAway()
        {
            Fixture(.58f,out var host,out var controls,out var tool);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);
            wall.transform.position=new Vector3(0,1,1.7f);wall.transform.localScale=new Vector3(3,2,.4f);
            yield return new WaitForSeconds(.3f);Physics.SyncTransforms();
            for(int i=0;i<60;i++){host.Player.StepInput(Vector2.up,Vector2.zero,false,.02f);yield return null;}
            float stopped=host.Player.transform.position.z;
            Assert.That(stopped,Is.LessThan(.8f),"Held footprint must stop before the body reaches the wall; "+controls.CarryObservation);
            Assert.That(tool.GetComponent<Collider>().bounds.max.z,Is.LessThanOrEqualTo(wall.GetComponent<Collider>().bounds.min.z+.02f));
            for(int i=0;i<25;i++){host.Player.StepInput(Vector2.down,Vector2.zero,false,.02f);yield return null;}
            Assert.That(host.Player.transform.position.z,Is.LessThan(stopped-.2f),"Contact must permit a separating movement; "+controls.CarryObservation);
        }
        [UnityTest] public IEnumerator GroundedPropCanLiftAwayFromFloorContact()
        {
            Fixture(.415f,out var host,out var controls,out var tool);
            yield return new WaitForSeconds(.5f);
            Assert.That(tool.GetComponent<Collider>().bounds.min.y,Is.GreaterThan(.12f),"Floor contact must not lock the pickup lift; "+controls.CarryObservation);
        }
        [UnityTest]public IEnumerator PartialDragStopsAtRequestedTravelAndDoesNotTurnCamera()
        {
            Fixture(.58f,out var host,out var controls,out var tool,"extinguisher");
            var pin=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;pin.name="real pin fixture";pin.SetParent(tool,false);
            pin.localPosition=new Vector3(0,.6f,-.7f);pin.localScale=Vector3.one*.1f;
            var rest=pin.localPosition;
            controls.BindEquipment("tool",tool,new[]{new DetailedPartPoseBinding{PartId="body",Part=tool,DrivePosition=false,DriveRotation=false},
                new DetailedPartPoseBinding{PartId="pin",Part=pin,MinLocalPosition=rest,MaxLocalPosition=rest+Vector3.right*.1f,DriveRotation=false}},new Vector3(0,-1.05f,.85f));
            host.Player.PointerConsumed=()=>controls.ConsumesPointerMotion;
            yield return new WaitForSeconds(.3f);
            for(int i=0;i<5;i++)
            {
                var direction=pin.position-host.View.transform.position;
                float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
                host.Player.StepInput(Vector2.zero,new Vector2(Mathf.DeltaAngle(host.Player.YawDegrees,yaw),host.Player.PitchDegrees-pitch),false,.02f);
                controls.StepInput(default,.02f);yield return null;
            }
            Assert.That(controls.AimedPart?.PartId,Is.EqualTo("pin"));
            controls.StepInput(new DetailedInputFrame{GripPressed=true},.02f);controls.Tick(controls.CollectInputs());host.State.Tick++;
            float heading=host.Player.YawDegrees;
            // Five render samples before one mechanical tick must not multiply a tiny stroke.
            for(int i=0;i<5;i++){controls.StepInput(new DetailedInputFrame{PointerDeltaX=i==0?1:0},.01f);yield return null;}
            controls.Tick(controls.CollectInputs());host.State.Tick++;
            Assert.That(controls.Mechanics.Position("tool","pin"),Is.InRange(.006,.010),"A queued stroke is spent once per mechanical tick");
            for(int i=0;i<24;i++)
            {
                host.Player.StepInput(Vector2.zero,new Vector2(.09f,0),false,.02f);
                controls.StepInput(new DetailedInputFrame{PointerDeltaX=1},.02f);controls.Tick(controls.CollectInputs());host.State.Tick++;yield return null;
            }
            for(int i=0;i<15;i++){controls.StepInput(default,.02f);controls.Tick(controls.CollectInputs());host.State.Tick++;yield return null;}
            Assert.That(host.Player.YawDegrees,Is.EqualTo(heading).Within(.001f),"Mouse drag belongs to the held part");
            Assert.That(controls.Mechanics.Position("tool","pin"),Is.InRange(.17,.23),"A partial stroke must stay partial while held");
            controls.StepInput(new DetailedInputFrame{PointerDeltaX=100},.02f);controls.Tick(controls.CollectInputs());host.State.Tick++;
            for(int i=0;i<60;i++){controls.StepInput(default,.02f);controls.Tick(controls.CollectInputs());host.State.Tick++;yield return null;}
            Assert.That(controls.Mechanics.Position("tool","pin"),Is.GreaterThanOrEqualTo(.95));
        }
        static void BoxDimensions(Transform target,Vector3 size)
        {
            target.localScale=Vector3.one;target.GetComponent<BoxCollider>().size=size;
            var mesh=Object.Instantiate(target.GetComponent<MeshFilter>().sharedMesh);var points=mesh.vertices;
            for(int i=0;i<points.Length;i++)points[i]=Vector3.Scale(points[i],size);
            mesh.vertices=points;mesh.RecalculateBounds();mesh.RecalculateNormals();target.GetComponent<MeshFilter>().sharedMesh=mesh;
        }
        void Aim(Host host,Vector3 point)
        {
            var d=point-host.View.transform.position;
            float yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg;
            host.Player.StepInput(Vector2.zero,new Vector2(Mathf.DeltaAngle(host.Player.YawDegrees,yaw),host.Player.PitchDegrees-pitch),false,.02f);
        }
        void MechanicalFrame(Host host,DetailedManipulationController controls,DetailedInputFrame input)
        {controls.StepInput(input,.05f);controls.Tick(controls.CollectInputs());host.State.Tick++;}
        [UnityTest]public IEnumerator FOnHandlePicksWholeEquipmentAndGPlacesWithoutMouseGrip()
        {
            Fixture(.58f,out var host,out var controls,out var tool,"extinguisher",false);
            var handle=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;handle.SetParent(tool,false);
            handle.localPosition=new Vector3(0,.6f,0);handle.localScale=Vector3.one*.15f;
            controls.BindEquipment("tool",tool,new[]{new DetailedPartPoseBinding{PartId="body",Part=tool,DrivePosition=false,DriveRotation=false},
                new DetailedPartPoseBinding{PartId="lever",Part=handle,DrivePosition=false,DriveRotation=false}},new Vector3(0,-1.05f,.85f));
            for(int i=0;i<3;i++){Aim(host,handle.position);MechanicalFrame(host,controls,default);yield return null;}
            Physics.Raycast(host.View.ViewportPointToRay(new Vector3(.5f,.5f,0)),out var seen,2.4f,~(1<<2));
            Assert.That(controls.AimedPart?.PartId,Is.EqualTo("lever"),"nearest="+seen.collider?.name+" captured="+host.Captured+" view="+host.View.transform.position+" handle="+handle.position);
            MechanicalFrame(host,controls,new DetailedInputFrame{CarryPressed=true});
            Assert.That(host.State.Entities["player"].Get("mech.carry.tool"),Is.EqualTo(1),"F alone on a non-carryable handle picks the equipment body");
            yield return new WaitForSeconds(.5f);
            for(int i=0;i<3;i++){Aim(host,new Vector3(0,0,1.25f));MechanicalFrame(host,controls,default);yield return null;}
            MechanicalFrame(host,controls,new DetailedInputFrame{PlacePressed=true});
            Assert.That(host.State.Entities["player"].Get("mech.carry.tool"),Is.EqualTo(0),controls.PlacementFeedback);
        }
        [UnityTest]public IEnumerator FDoesNotPickEquipmentThroughWall()
        {
            Fixture(.58f,out var host,out var controls,out var tool,"small-tool",false);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);
            wall.transform.position=new Vector3(0,1,.4f);wall.transform.localScale=new Vector3(3,2,.1f);
            yield return null;Aim(host,tool.position);MechanicalFrame(host,controls,new DetailedInputFrame{CarryPressed=true});
            Assert.That(host.State.Entities["player"].Get("mech.carry.tool"),Is.EqualTo(0));
        }
        [UnityTest]public IEnumerator OneRadioClickTransmitsAfterMouseReleaseAndStopsExplicitly()
        {
            Fixture(.58f,out var host,out var controls,out var tool,"radio",false);
            BoxDimensions(tool,new Vector3(.085f,.16f,.042f));
            var ptt=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;ptt.SetParent(tool,false);
            ptt.localPosition=new Vector3(0,.04f,-.035f);ptt.localScale=new Vector3(.09f,.12f,.035f);
            controls.BindEquipment("tool",tool,new[]{new DetailedPartPoseBinding{PartId="body",Part=tool,DrivePosition=false,DriveRotation=false},
                new DetailedPartPoseBinding{PartId="ptt",Part=ptt,DrivePosition=false,DriveRotation=false,ClickLatched=true}},new Vector3(0,-.3f,.6f));
            for(int i=0;i<3;i++){Aim(host,ptt.position);MechanicalFrame(host,controls,default);yield return null;}
            Assert.That(controls.AimedPart?.PartId,Is.EqualTo("ptt"));
            MechanicalFrame(host,controls,new DetailedInputFrame{GripPressed=true});
            Assert.That(controls.IsPressLatched("tool"),Is.True);
            MechanicalFrame(host,controls,new DetailedInputFrame{GripReleased=true});
            for(int i=0;i<20;i++){MechanicalFrame(host,controls,default);yield return null;}
            Assert.That(host.State.Entities["tool"].Get("radio.transmitting"),Is.GreaterThan(0));
            controls.StopLatchedPress();controls.Tick(controls.CollectInputs());host.State.Tick++;
            for(int i=0;i<20;i++){MechanicalFrame(host,controls,default);yield return null;}
            Assert.That(host.State.Entities["tool"].Get("radio.transmitting"),Is.EqualTo(0));
        }
        [UnityTest]public IEnumerator WornEquipmentCanBeLookedAtAndStaysAtWaistWhenWalking()
        {
            Fixture(.58f,out var host,out var controls,out var tool);BoxDimensions(tool,new Vector3(.085f,.16f,.042f));
            host.State.Entities["player"].Set("mech.carry.tool",0);
            controls.SetEquipmentDock("tool",host.Player.transform,new Vector3(-.25f,1,.2f),Quaternion.identity,true,true);
            for(int i=0;i<12;i++)
            {
                var direction=tool.position-host.View.transform.position;
                float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
                host.Player.StepInput(Vector2.zero,new Vector2(Mathf.DeltaAngle(host.Player.YawDegrees,yaw),host.Player.PitchDegrees-pitch),false,.02f);
                controls.StepInput(default,.02f);yield return null;
            }
            Assert.That(controls.AimedPart?.OwnerId,Is.EqualTo("tool"),"Turning the head must not rotate the worn radio away from the crosshair");
            for(int i=0;i<15;i++){host.Player.StepInput(Vector2.up,Vector2.zero,false,.02f);yield return null;}
            Assert.That(tool.position.y-host.Player.transform.position.y,Is.EqualTo(1).Within(.03f));
            var horizontal=tool.position-host.Player.transform.position;horizontal.y=0;
            Assert.That(horizontal.magnitude,Is.InRange(.28f,.36f),"The radio stays mounted at the waist");
            for(int i=0;i<12;i++)
            {
                var direction=tool.position-host.View.transform.position;
                float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
                host.Player.StepInput(Vector2.zero,new Vector2(Mathf.DeltaAngle(host.Player.YawDegrees,yaw),host.Player.PitchDegrees-pitch),false,.02f);
                controls.StepInput(default,.02f);yield return null;
            }
            Assert.That(controls.AimedPart?.OwnerId,Is.EqualTo("tool"),"Worn equipment remains reachable after walking");
        }
        [UnityTest]public IEnumerator AimedNozzleCannotRotateThroughSolidWall()
        {
            Fixture(.58f,out var host,out var controls,out var tool,"extinguisher");BoxDimensions(tool,new Vector3(.7f,.8f,.2f));
            var nozzle=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;nozzle.name="physical nozzle fixture";nozzle.SetParent(tool,false);
            BoxDimensions(nozzle,new Vector3(.056f,.064f,.16f));nozzle.localPosition=new Vector3(.84f,.64f,-.16f);
            var outlet=new GameObject("outlet").transform;outlet.SetParent(nozzle,false);outlet.localPosition=Vector3.forward*.08f;
            controls.BindEquipment("tool",tool,new[]{new DetailedPartPoseBinding{PartId="body",Part=tool,DrivePosition=false,DriveRotation=false},
                new DetailedPartPoseBinding{PartId="nozzle",Part=nozzle,MinLocalPosition=nozzle.localPosition,MaxLocalPosition=nozzle.localPosition,
                    DrivePosition=false,DriveRotation=false,AimWithView=true,AimOrigin=outlet,AimAfterNormalized=0}},new Vector3(0,-1.05f,.85f));
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);wall.name="solid wall";
            wall.transform.position=new Vector3(.95f,1,.69f);wall.transform.localScale=new Vector3(.08f,2,3);
            yield return new WaitForSeconds(.3f);
            for(int i=0;i<10;i++){host.Player.StepInput(Vector2.zero,new Vector2(i<2?45:0,0),false,.02f);yield return null;}
            Physics.SyncTransforms();
            Assert.That(outlet.position.x,Is.LessThan(wall.GetComponent<Collider>().bounds.min.x+.002f),"Outlet cannot start beyond the wall");
            bool overlap=Physics.ComputePenetration(nozzle.GetComponent<Collider>(),nozzle.position,nozzle.rotation,
                wall.GetComponent<Collider>(),wall.transform.position,wall.transform.rotation,out _,out float depth);
            Assert.That(overlap?depth:0,Is.LessThanOrEqualTo(.002f),"Aimed physical nozzle must respect the wall");
        }
        void PinFixture(out Host host,out DetailedManipulationController controls,out Transform tool,out Transform pin)
        {
            Fixture(.58f,out host,out controls,out tool,"extinguisher");BoxDimensions(tool,new Vector3(.14f,.55f,.12f));
            pin=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;pin.name="small physical safety pin";pin.SetParent(tool,false);
            BoxDimensions(pin,new Vector3(.025f,.018f,.012f));pin.localPosition=new Vector3(.10f,.22f,-.09f);
            controls.BindEquipment("tool",tool,new[]{new DetailedPartPoseBinding{PartId="body",Part=tool,DrivePosition=false,DriveRotation=false},
                new DetailedPartPoseBinding{PartId="pin",Part=pin,MinLocalPosition=pin.localPosition,MaxLocalPosition=pin.localPosition+Vector3.up*.07f,DriveRotation=false}},new Vector3(0,-.76f,.62f));
            var boundControls=controls;host.Player.PointerConsumed=()=>boundControls.ConsumesPointerMotion;
        }
        void UseNormalDeviceInput(Host host,DetailedManipulationController controls)
        {
            previousMouse=Mouse.current;previousKeyboard=Keyboard.current;previousInputSettings=InputSystem.settings;
            // InputManager destroys a HideAndDontSave settings instance when it is replaced.
            // Keep its values before assigning fixture settings, so teardown never reinstalls
            // the destroyed instance. Persistent project settings retain their original identity.
            if(previousInputSettings.hideFlags==HideFlags.HideAndDontSave)
            {
                temporarySettingsBackup=Object.Instantiate(previousInputSettings);
                temporarySettingsBackup.hideFlags=HideFlags.HideAndDontSave;
            }
            previousCursorLock=Cursor.lockState;previousCursorVisible=Cursor.visible;
            // Batch PlayMode has no focused Game View. Default routing sends pointer/keyboard
            // events to the editor, so give only this fixture an unsaved player-input setting.
            testInputSettings=Object.Instantiate(previousInputSettings);testInputSettings.hideFlags=HideFlags.HideAndDontSave;
            testInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            testInputSettings.scrollDeltaBehavior=InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
            InputSystem.settings=testInputSettings;
            testMouse=InputSystem.AddDevice<Mouse>();testKeyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.onAfterUpdate+=KeepTestDevicesCurrent;KeepTestDevicesCurrent();
            controls.SetExternalInputMode(false);host.Player.SetExternalInputMode(false);host.Player.Capture(true);
        }
        void KeepTestDevicesCurrent()
        {
            if(testMouse!=null&&testMouse.added)testMouse.MakeCurrent();
            if(testKeyboard!=null&&testKeyboard.added)testKeyboard.MakeCurrent();
        }
        string DeviceObservation(Host host,DetailedManipulationController controls)
            =>controls.PartInputFeedback+"; captured="+host.Captured+"; external="+controls.ExternalInputMode+"/"+host.Player.ExternalInputMode+
                "; current="+(Mouse.current==testMouse)+"; enabled="+testMouse.enabled+"; button="+testMouse.leftButton.isPressed+
                "; pressedThisFrame="+testMouse.leftButton.wasPressedThisFrame+"; gameFocus="+Application.isFocused;
        IEnumerator PumpDeviceFrames(Host host,DetailedManipulationController controls,float seconds)
        {
            float until=Time.realtimeSinceStartup+seconds;
            do{yield return null;controls.Tick(controls.CollectInputs());host.State.Tick++;}while(Time.realtimeSinceStartup<until);
        }
        void QueueMouse(bool held,float wheel=0,Vector2 delta=default)
            =>InputSystem.QueueStateEvent(testMouse,new MouseState{scroll=new Vector2(0,wheel),delta=delta}.WithButton(MouseButton.Left,held));

        [UnityTest]public IEnumerator NormalizedMouseWheelMovesPinAndReleaseRegripPreservesPreparation()
        {
            PinFixture(out var host,out var controls,out var tool,out var pin);
            yield return new WaitForSeconds(.3f);
            for(int i=0;i<3;i++){Aim(host,pin.position);controls.StepInput(default,.02f);yield return null;}
            UseNormalDeviceInput(host,controls);
            QueueMouse(true);yield return PumpDeviceFrames(host,controls,.08f);
            Assert.That(controls.HeldPart?.PartId,Is.EqualTo("pin"),DeviceObservation(host,controls));
            var rest=pin.position;
            // One real normalized notch, followed by frames with zero scroll. The old /120
            // conversion made this 120 times too small; full-scene synthetic +120 hid that.
            QueueMouse(true,1);yield return PumpDeviceFrames(host,controls,.2f);
            double partial=controls.Mechanics.Position("tool","pin");
            Assert.That(partial,Is.InRange(.06,.10),controls.PartInputFeedback);
            Assert.That(pin.position.y-rest.y,Is.GreaterThan(.003f));
            QueueMouse(false);yield return PumpDeviceFrames(host,controls,.08f);
            Assert.That(controls.HeldPart,Is.Null);
            Assert.That(controls.Mechanics.Position("tool","pin"),Is.EqualTo(partial).Within(.002));
            QueueMouse(true);yield return PumpDeviceFrames(host,controls,.08f);
            Assert.That(controls.HeldPart?.PartId,Is.EqualTo("pin"),DeviceObservation(host,controls));
            for(int i=0;i<12;i++){QueueMouse(true,1);yield return PumpDeviceFrames(host,controls,.13f);}
            yield return PumpDeviceFrames(host,controls,.2f);
            Assert.That(controls.Mechanics.Position("tool","pin"),Is.GreaterThanOrEqualTo(.95));
            Assert.That(pin.position.y-rest.y,Is.GreaterThan(.06f));
            Assert.That(host.State.Entities["player"].Get("mech.carry.tool"),Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator NormalMouseVerticalDragUsesVisiblePartTravelWithoutMovingCamera()
        {
            PinFixture(out var host,out var controls,out var tool,out var pin);
            yield return new WaitForSeconds(.3f);
            for(int i=0;i<3;i++){Aim(host,pin.position);controls.StepInput(default,.02f);yield return null;}
            UseNormalDeviceInput(host,controls);
            QueueMouse(true);yield return PumpDeviceFrames(host,controls,.08f);
            Assert.That(controls.HeldPart?.PartId,Is.EqualTo("pin"),DeviceObservation(host,controls));
            float pitch=host.Player.PitchDegrees,yaw=host.Player.YawDegrees;
            QueueMouse(true,0,Vector2.up*30);yield return PumpDeviceFrames(host,controls,.4f);
            Assert.That(controls.Mechanics.Position("tool","pin"),Is.InRange(.20,.27),controls.PartInputFeedback);
            Assert.That(host.Player.PitchDegrees,Is.EqualTo(pitch).Within(.001));
            Assert.That(host.Player.YawDegrees,Is.EqualTo(yaw).Within(.001));
            QueueMouse(false);yield return PumpDeviceFrames(host,controls,.08f);
        }
        [UnityTest]public IEnumerator NearbyPinTargetIsReachableAndMenuBlockReleasesGripWithoutResettingEquipment()
        {
            PinFixture(out var host,out var controls,out var tool,out var pin);
            yield return new WaitForSeconds(.3f);
            for(int i=0;i<3;i++){Aim(host,pin.position+host.View.transform.right*.018f);controls.StepInput(default,.02f);yield return null;}
            Assert.That(controls.AimedPart?.PartId,Is.EqualTo("pin"),"A small nearby visible part needs a modest aiming tolerance");
            MechanicalFrame(host,controls,new DetailedInputFrame{GripPressed=true});
            MechanicalFrame(host,controls,new DetailedInputFrame{ScrollDelta=1});
            for(int i=0;i<12;i++)MechanicalFrame(host,controls,default);
            double before=controls.Mechanics.Position("tool","pin");
            Assert.That(before,Is.GreaterThan(.05));
            bool menu=true;controls.InputBlocked=()=>menu;
            MechanicalFrame(host,controls,new DetailedInputFrame{GripPressed=true,ScrollDelta=1});
            Assert.That(controls.HeldPart,Is.Null);Assert.That(controls.AimedPart,Is.Null);
            Assert.That(host.State.Entities["player"].Get("mech.grip.tool::pin"),Is.Zero);
            Assert.That(host.State.Entities["player"].Get("mech.carry.tool"),Is.EqualTo(1));
            Assert.That(controls.Mechanics.Position("tool","pin"),Is.EqualTo(before));
            menu=false;MechanicalFrame(host,controls,new DetailedInputFrame{ScrollDelta=1});
            Assert.That(controls.Mechanics.Position("tool","pin"),Is.EqualTo(before),"Closing a menu must not resume a released grip");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);
            wall.transform.position=host.View.transform.position+host.View.transform.forward*.25f;wall.transform.localScale=new Vector3(2,2,.08f);
            MechanicalFrame(host,controls,new DetailedInputFrame{GripPressed=true});
            Assert.That(controls.AimedPart,Is.Null,"Picking tolerance must not select a pin through an unrelated wall");
            Assert.That(controls.HeldPart,Is.Null);
        }
        [TearDown]public void Cleanup()
        {
            if(root!=null)Object.DestroyImmediate(root);
            InputSystem.onAfterUpdate-=KeepTestDevicesCurrent;
            if(testInputSettings!=null)
            {
                if(InputSystem.settings==testInputSettings)
                    InputSystem.settings=previousInputSettings!=null?previousInputSettings:temporarySettingsBackup;
                else Object.DestroyImmediate(testInputSettings);
                testInputSettings=null;
            }
            if(temporarySettingsBackup!=null&&InputSystem.settings!=temporarySettingsBackup)Object.DestroyImmediate(temporarySettingsBackup);
            previousInputSettings=null;temporarySettingsBackup=null;
            if(testMouse!=null)
            {
                Cursor.lockState=previousCursorLock;Cursor.visible=previousCursorVisible;
                InputSystem.RemoveDevice(testMouse);testMouse=null;
                if(previousMouse!=null&&previousMouse.added)previousMouse.MakeCurrent();
            }
            if(testKeyboard!=null){InputSystem.RemoveDevice(testKeyboard);testKeyboard=null;if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();}
            Time.timeScale=1;
        }
    }
}
