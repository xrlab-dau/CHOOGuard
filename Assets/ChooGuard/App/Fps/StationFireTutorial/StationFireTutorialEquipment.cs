using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.Seeded.Unity;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    public sealed partial class StationFireTutorialSession
    {
        Transform safetyPin, nozzle, lever;
        StationFireVisuals visuals;
        NavMeshObstacle carriedEquipmentObstacle;
        const string FireId="fire", ExtinguisherId="extinguisher";

        void InitializeEquipment()
        {
            if(sourceExtinguisher==null)throw new InvalidOperationException("기존 맵 소화기 연결이 없습니다");
            var model=sourceExtinguisher.gameObject;
            var basePart=OriginalPart("04");basePart.SetParent(sourceExtinguisher.parent,true);
            sourceExtinguisher.SetParent(transform,true);ExtinguisherBody=sourceExtinguisher;
            var tank=OriginalPart("00");AddMeshTarget(tank);
            safetyPin=RigPivot("안전핀 인출",OriginalPart("01"),new Vector3(.00148158f,.92680984f,-.04624668f));
            lever=RigPivot("레버 힌지",OriginalPart("06"),new Vector3(.03940342f,.90208480f,-.04624668f));
            nozzle=RigPivot("노즐 잡는 곳",OriginalPart("13"),new Vector3(.15771957f,.33583586f,-.04215112f));
            AddPartBox(safetyPin);AddPartBox(lever);AddPartBox(nozzle);
            MechanicalOwner(ExtinguisherId,"extinguisher",ExtinguisherBody,"extinguisher");
            State.Entities[ExtinguisherId].Set("mech.discharge-enabled",0);State.Entities[ExtinguisherId].Set("mech.require-nozzle-ready",1);
            var tip=RigAnchor("NozzleExit",new Vector3(.15771957f,.24482343f,-.04215112f),nozzle);
            var outlet=RigAnchor("HoseOutlet",new Vector3(.09097712f,.87371926f,-.04321293f),ExtinguisherBody);
            tip.rotation=Quaternion.LookRotation((tip.position-nozzle.position).normalized,ExtinguisherBody.forward);
            var pinBinding=Pose("pin",safetyPin);pinBinding.MaxLocalPosition+=Vector3.back*.10618116f;
            var leverBinding=Pose("lever",lever);leverBinding.MaxLocalRotation=lever.localRotation*Quaternion.AngleAxis(-15,Vector3.back);
            var nozzleBinding=Pose("nozzle",nozzle);
            nozzleBinding.MaxLocalPosition=nozzle.parent.InverseTransformPoint(tank.position+ExtinguisherBody.TransformVector(new Vector3(-.25f,.85f,.35f)));
            var partToTip=Quaternion.Inverse(nozzle.rotation)*tip.rotation;
            nozzleBinding.MaxLocalRotation=Quaternion.Inverse(nozzle.parent.rotation)*layoutRotation*Quaternion.Inverse(partToTip);
            nozzleBinding.AimOrigin=tip;nozzleBinding.AimWithView=true;
            // Show the pin-ring side: the original lever extends towards local -X.
            // The opposite yaw places its real grip volume in front of the pin.
            var carryRotation=Quaternion.Euler(0,65,0);
            // Anchor the actual visible grip, respecting this original model's scale and pivot.
            // A root offset tuned for another extinguisher can leave this pin below the normal view.
            var pinFromRoot=Vector3.Scale(ExtinguisherBody.InverseTransformPoint(safetyPin.position),ExtinguisherBody.lossyScale);
            var carryPosition=new Vector3(.20f,-.20f,.65f)-carryRotation*pinFromRoot;
            Controls.BindEquipment(ExtinguisherId,ExtinguisherBody,new[]{Pose("body",ExtinguisherBody,false),pinBinding,nozzleBinding,leverBinding},
                carryPosition,carryRotation);
            Record("bound-original-extinguisher-without-changing-appearance:"+sourceExtinguisher.name);
            // Agents must give the carried object room as well as the player's body.
            // This is local avoidance only: never carve the player's own navigation route.
            carriedEquipmentObstacle=ExtinguisherBody.gameObject.AddComponent<NavMeshObstacle>();
            carriedEquipmentObstacle.shape=NavMeshObstacleShape.Box;carriedEquipmentObstacle.carving=false;
            carriedEquipmentObstacle.enabled=false;
            var charSurface=InitializeBenchFire();
            visuals=gameObject.AddComponent<StationFireVisuals>();
            visuals.CharSurface=charSurface;
            visuals.Flame=StationFireVisuals.Particles(FireTarget,"화염",new Color(1,.3f,.025f,.8f),.3f,.85f,.6f,22);
            visuals.Flame.transform.localRotation=Quaternion.Euler(-90,0,0);
            visuals.Smoke=StationFireVisuals.Particles(FireTarget,"잔류 연기",new Color(.3f,.3f,.32f,.18f),.32f,2.8f,.3f,20);
            visuals.Smoke.transform.localRotation=Quaternion.Euler(-90,0,0);
            visuals.Powder=StationFireVisuals.Particles(tip,"분말 방사",new Color(.93f,.91f,.82f,.45f),.1f,.75f,6,10);
            var powderCollision=visuals.Powder.collision;powderCollision.enabled=true;
            powderCollision.type=ParticleSystemCollisionType.World;powderCollision.mode=ParticleSystemCollisionMode.Collision3D;
            powderCollision.quality=ParticleSystemCollisionQuality.Medium;powderCollision.enableDynamicColliders=true;
            powderCollision.dampen=1;powderCollision.bounce=0;powderCollision.lifetimeLoss=1;powderCollision.radiusScale=.2f;
            visuals.HoseOutlet=outlet;visuals.NozzleRear=nozzle;visuals.NozzleExit=tip;visuals.StowedHose=OriginalPart("03");
            var hose=new GameObject("전개 호스").AddComponent<LineRenderer>();hose.transform.SetParent(transform,false);
            hose.useWorldSpace=true;hose.widthMultiplier=.013f;hose.numCapVertices=5;hose.numCornerVertices=4;
            hose.material=Material("호스 고무",new Color(.025f,.027f,.03f),.1f);visuals.Hose=hose;
            visuals.FireSound=FireTarget.gameObject.AddComponent<AudioSource>();visuals.FireSound.clip=FireLoop;visuals.FireSound.loop=true;
            visuals.FireSound.spatialBlend=1;visuals.FireSound.minDistance=1;visuals.FireSound.maxDistance=12;if(FireLoop!=null)visuals.FireSound.Play();
            visuals.SpraySound=tip.gameObject.AddComponent<AudioSource>();visuals.SpraySound.clip=SprayLoop!=null?SprayLoop:AirNoise();
            visuals.SpraySound.loop=true;visuals.SpraySound.spatialBlend=.7f;visuals.SpraySound.maxDistance=10;
            ProjectEquipment();
        }
        Transform OriginalPart(string suffix)
        {
            var part=sourceExtinguisher.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="cc0_korean_fire_extinguisher_01_body_editable_connected_component_"+suffix);
            if(part==null)throw new InvalidOperationException("기존 소화기 원본 부품이 없습니다: "+suffix);return part;
        }
        Transform RigPivot(string name,Transform mesh,Vector3 rootLocal)
        {
            var pivot=new GameObject(name).transform;pivot.SetParent(sourceExtinguisher,false);pivot.localPosition=rootLocal;
            mesh.SetParent(pivot,true);return pivot;
        }
        Transform RigAnchor(string name,Vector3 rootLocal,Transform parent)
        {
            var marker=new GameObject(name).transform;marker.SetParent(sourceExtinguisher,false);marker.localPosition=rootLocal;marker.SetParent(parent,true);return marker;
        }
        void MechanicalOwner(string id,string profile,Transform root,string kind)
        {
            var center=RenderBounds(root).center;var owner=AddEntity(id,kind,center,kind,"training-model");owner.Set("charge",1);owner.Set("power",1);
            Mechanics.Attach(id,profile);Mechanics.SetInteractionCenter(id,center.x,center.y,center.z);
        }
        static DetailedPartPoseBinding Pose(string id,Transform part,bool drive=true)=>new DetailedPartPoseBinding{
            PartId=id,Part=part,MinLocalPosition=part.localPosition,MaxLocalPosition=part.localPosition,
            MinLocalRotation=part.localRotation,MaxLocalRotation=part.localRotation,DrivePosition=drive,DriveRotation=drive};
        static Transform FindPart(Transform parent,string name)
        {
            var part=parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
            if(part==null)throw new InvalidOperationException("실제 장비 부품 누락: "+name);return part;
        }
        static Bounds RenderBounds(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true)
                .Where(r=>r is MeshRenderer||r is SkinnedMeshRenderer).ToArray();
            if(renderers.Length==0)throw new InvalidOperationException("물체의 실제 모델이 없습니다: "+root.name);
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);return bounds;
        }
        static void AddMeshTarget(Transform part)
        {
            var filter=part.GetComponent<MeshFilter>();
            if(filter==null||filter.sharedMesh==null)throw new InvalidOperationException("원본 부품 메시 누락: "+part.name);
            if(part.GetComponent<Collider>()==null){var collider=part.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}
        }
        static void AddPartBox(Transform part)
        {
            if(part.GetComponent<Collider>()!=null)return;
            var local=MeshBounds(part);
            var scale=part.lossyScale;
            var minimum=new Vector3(.006f/Mathf.Max(.000001f,Mathf.Abs(scale.x)),.006f/Mathf.Max(.000001f,Mathf.Abs(scale.y)),.006f/Mathf.Max(.000001f,Mathf.Abs(scale.z)));
            var box=part.gameObject.AddComponent<BoxCollider>();box.center=local.center;box.size=Vector3.Max(local.size,minimum);
        }
        static Bounds MeshBounds(Transform part)
        {
            var local=new Bounds();bool any=false;
            foreach(var filter in part.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.sharedMesh==null)continue;
                var bounds=filter.sharedMesh.bounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var point=part.InverseTransformPoint(filter.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))));
                    if(!any){local=new Bounds(point,Vector3.zero);any=true;}else local.Encapsulate(point);
                }
            }
            if(!any)throw new InvalidOperationException("부품의 실제 메시 범위가 없습니다: "+part.name);
            return local;
        }
        static Vector3 Grounded(Vector3 navPoint)
        {
            var surface=Physics.RaycastAll(navPoint+Vector3.up*.35f,Vector3.down,.9f,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.65f).OrderBy(h=>h.distance).FirstOrDefault();
            if(surface.collider==null)throw new InvalidOperationException("교육 물체를 지지하는 실제 바닥이 없습니다");
            return surface.point+Vector3.up*.015f;
        }
        static void Shape(Transform parent,string name,PrimitiveType primitive,Vector3 position,Vector3 scale,Material material)
        {var go=GameObject.CreatePrimitive(primitive);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;}
        static Material Material(string name,Color color,float metallic)
        {
            var value=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};value.color=color;value.SetFloat("_Metallic",metallic);value.SetFloat("_Smoothness",.35f);return value;
        }
        static AudioClip AirNoise()
        {
            const int count=24000;var samples=new float[count];var random=new System.Random(318);float previous=0;
            for(int i=0;i<count;i++){float raw=(float)(random.NextDouble()*2-1);previous=Mathf.Lerp(previous,raw,.6f);samples[i]=previous*.45f;}
            var clip=AudioClip.Create("로컬 합성 분말 분사음",count,1,24000,false);clip.SetData(samples,0);return clip;
        }
        void ReleaseEquipmentPressure()
        {
            Mechanics?.ReleaseActorGrips("player");
            ProjectEquipment();
        }
        void ProjectEquipment()
        {
            if(visuals==null||State==null)return;
            // Keep parts steady while preparing them; once the ready lever is gripped,
            // turn the carried equipment with the torso for aiming. Existing sweeps
            // still refuse rotation through a person or furnishing.
            Controls.SetCarryHeadingFollowsActor(ExtinguisherId,Controls.HeldPart?.OwnerId==ExtinguisherId&&
                Controls.HeldPart.PartId=="lever"&&Mechanics.Position(ExtinguisherId,"nozzle")>=.95);
            if(carriedEquipmentObstacle!=null)
            {
                bool carrying=State.Entities["player"].Get("mech.carry."+ExtinguisherId)>0;
                if(carrying){var bounds=MeshBounds(ExtinguisherBody);carriedEquipmentObstacle.center=bounds.center;carriedEquipmentObstacle.size=bounds.size;}
                carriedEquipmentObstacle.enabled=carrying;
            }
            bool outOfClip=Mechanics.Position(ExtinguisherId,"nozzle")>=.95;
            float flow=(float)State.Entities[ExtinguisherId].Get("mech.flow");
            visuals.Agent=(float)State.Entities[FireId].Get("mech.agent-deposit");
            visuals.Project((float)State.Entities[FireId].Get("fire"),flow,outOfClip,!Captured);
        }
    }
}
