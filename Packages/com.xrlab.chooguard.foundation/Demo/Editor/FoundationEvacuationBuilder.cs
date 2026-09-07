using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ChooGuard.Foundation.Demo.Editor.FoundationBlenderAssets;
namespace ChooGuard.Foundation.Demo.Editor
{
    public static class FoundationEvacuationBuilder
    {
        public static void Build(Transform root,Material[] m,TextAsset data)
        {
            var env=root.Find("Environment");
            // Door openings retain solid jambs; each side room joins a rear bypass and assembly room.
            foreach(var sign in new[]{-1,1})
            {
                var side=sign<0?"West":"East";
                UnityEngine.Object.DestroyImmediate(env.Find("Concourse"+side+"Wall").gameObject);
                Box("Concourse"+side+"Wall",env,new Vector3(sign*8.15f,1.6f,-2.75f),new Vector3(.3f,3.2f,8.5f),m[1]);
                Box(side+"UpperWall",env,new Vector3(sign*8.15f,1.6f,6.25f),new Vector3(.3f,3.2f,3.5f),m[1]);
                // Remove old decorative strips which would otherwise cross the new doors.
                foreach(var t in env.Find("InteriorDetails").Cast<Transform>().Where(t=>t.name.StartsWith("Concourse"+side+"Wall_")||t.name.StartsWith("AssemblySideWall_"+sign+"_")).ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
                UnityEngine.Object.DestroyImmediate(env.Find("AssemblySideWall_"+sign).gameObject);
                Box(side+"AssemblyLowerWall",env,new Vector3(sign*6.15f,1.6f,14.5f),new Vector3(.3f,3.2f,1),m[1]);
                Box(side+"AssemblyUpperWall",env,new Vector3(sign*6.15f,1.6f,20),new Vector3(.3f,3.2f,4),m[1]);
                Box(side+"WaitingFloor",env,new Vector3(sign*12,-.15f,3),new Vector3(8,.3f,8),m[0]);
                Box(side+"BypassFloor",env,new Vector3(sign*13,-.15f,12.5f),new Vector3(6,.3f,11),m[0]);
                Box(side+"LinkFloor",env,new Vector3(sign*8,-.15f,16.5f),new Vector3(4,.3f,3),m[0]);
                Box(side+"OuterWall",env,new Vector3(sign*16.15f,1.6f,8.5f),new Vector3(.3f,3.2f,19),m[1]);
                Box(side+"SouthWall",env,new Vector3(sign*12,1.6f,-1.15f),new Vector3(8,3.2f,.3f),m[1]);
                Box(side+"NorthWall",env,new Vector3(sign*11,1.6f,18.15f),new Vector3(10,3.2f,.3f),m[1]);
                Box(side+"WaitingNorthClosure",env,new Vector3(sign*9,1.6f,7.15f),new Vector3(2.3f,3.2f,.3f),m[1]);
                Box(side+"BypassInner",env,new Vector3(sign*9.85f,1.6f,11),new Vector3(.3f,3.2f,8),m[1]);
                Box(side+"LinkLower",env,new Vector3(sign*8,1.6f,14.85f),new Vector3(4,3.2f,.3f),m[1]);
                Box(side+"WaitingCeiling",env,new Vector3(sign*12,3.30f,3),new Vector3(8,.12f,8),m[1],false);
                Box(side+"BypassCeiling",env,new Vector3(sign*13,3.30f,12.5f),new Vector3(6,.12f,11),m[1],false);
                Box(side+"LinkCeiling",env,new Vector3(sign*8,3.30f,16.5f),new Vector3(4,.12f,3),m[1],false);
                for(var z=2;z<=17;z+=5){var light=Instantiate("CeilingLight",env,m);light.localPosition=new Vector3(sign*13,3.16f,z);}
                var door=Instantiate("DoorFrame",env,m);door.localPosition=new Vector3(sign*8.1f,0,3);door.localRotation=Quaternion.Euler(0,90,0);
                var inner=Instantiate("DoorFrame",env,m);inner.localPosition=new Vector3(sign*6.1f,0,16.5f);inner.localRotation=Quaternion.Euler(0,90,0);
                // Partition makes a real corner; collision and graph tests cover both bypasses.
                Box(side+"WaitingPartition",env,new Vector3(sign*12,1.15f,5.8f),new Vector3(3.6f,2.3f,.18f),m[3]);
                for(var i=0;i<2;i++)
                {
                    var bench=Instantiate("Bench",env,m);bench.localPosition=new Vector3(sign*13,0,.4f+i*3);
                    Box(side+"BenchCollision_"+i,env,bench.localPosition+Vector3.up*.6f,new Vector3(2.4f,1.2f,.75f),m[0]).GetComponent<Renderer>().enabled=false;
                    var luggage=Instantiate("Luggage",env,m);luggage.localPosition=new Vector3(sign*15,0,2+i*4);
                    Box(side+"LuggageCollision_"+i,env,luggage.localPosition+Vector3.up*.4f,new Vector3(.55f,.8f,.35f),m[0]).GetComponent<Renderer>().enabled=false;
                }
                var signModel=Instantiate("AssemblySign",env,m);signModel.localPosition=new Vector3(sign*12,2.65f,7);Label(signModel,"BYPASS  "+side.ToUpper(),new Vector3(0,.01f,-.08f),.065f);
            }
            var shared=Group("SharedObjects",root,Vector3.zero);
            var extra=new[]{
                Target(shared,"route-console","우회 안내 확인","RouteConsole",new Vector3(5.5f,1.1f,-3),Quaternion.Euler(0,90,0),m),
                Target(shared,"rally-west","서측 인솔 시작","RallyPoint",new Vector3(-10.5f,1.1f,2.5f),Quaternion.Euler(0,-90,0),m,true),
                Target(shared,"rally-east","동측 인솔 시작","RallyPoint",new Vector3(10.5f,1.1f,2.5f),Quaternion.Euler(0,90,0),m,true),
                Target(shared,"west-check","서측 경로 확인","DirectionSign",new Vector3(-12,1.1f,14),Quaternion.identity,m),
                Target(shared,"east-check","동측 경로 확인","DirectionSign",new Vector3(12,1.1f,14),Quaternion.identity,m),
                Target(shared,"assembly-register","집결 인원 확인","AssemblyRegister",new Vector3(0,1.1f,19.6f),Quaternion.identity,m)};
            var crowd=Group("Evacuees",root,Vector3.zero);
            var actors=new DemoEvacuee[6];
            for(var i=0;i<6;i++)
            {
                var side=i<3?-1:1;var local=i%3;
                var at=new Vector3(side*(11.5f+local*.7f),.1f,4.7f);
                var actor=Group("Evacuee_"+i,crowd,at);actor.gameObject.layer=2;
                var body=Instantiate("Evacuee",actor,m);
                foreach(var limb in new[]{"LeftArm","RightArm","LeftLeg","RightLeg"})
                {
                    var pivot=new Vector3(limb.StartsWith("Left")?-.26f:.26f,limb.EndsWith("Arm")?1.32f:.88f,0);
                    if(limb.EndsWith("Leg"))pivot.x*=.5f;
                    var joint=Group(limb+"_Joint",body,pivot);
                    foreach(var part in body.Cast<Transform>().Where(x=>x.name.StartsWith(limb+"_")&&x!=joint).ToArray())
                    {part.SetParent(joint,false);part.localPosition=-pivot;}
                }
                var trigger=actor.gameObject.AddComponent<CapsuleCollider>();trigger.radius=.26f;trigger.height=1.75f;trigger.center=Vector3.up*.87f;trigger.isTrigger=true;
                actors[i]=actor.gameObject.AddComponent<DemoEvacuee>();
                actors[i].Configure(side<0?"west":"east",i,at,new Vector3(-1.25f+local*1.25f,.1f,16.5f+(i/3)*1.25f));
            }
            var graph=Group("WalkGraph",root,Vector3.zero).gameObject.AddComponent<DemoWalkGraph>();
            // Ignore actor colliders during static route construction; runtime interactions still use first-hit rays.
            root.GetComponentInChildren<DemoPlayerController>().gameObject.layer=2;
            var guide=Group("WorldNavigation",root,Vector3.zero);
            var line=Group("FloorRoute",guide,Vector3.zero).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial=m[7];line.widthMultiplier=.10f;line.useWorldSpace=true;line.numCornerVertices=4;line.numCapVertices=4;
            var marker=Group("Destination",guide,Vector3.zero);
            var label=Label(marker,"",Vector3.zero,.13f);
            var ring=Instantiate("AssemblySign",marker,m);ring.localScale=Vector3.one*.35f;ring.localPosition=Vector3.up*.2f;
            var guidance=guide.gameObject.AddComponent<DemoWorldGuide>();guidance.Configure(line,marker,label);guidance.ConfigureFontShader(Shader.Find("CHOOguard/WorldLabel"));
            if(data==null)throw new FileNotFoundException("Imported evacuation-drills.json missing.");
            root.GetComponentInChildren<DemoGameController>().ConfigureExercise(data,extra,actors,graph,guidance);
        }
        private static DemoInteractable Target(Transform parent,string id,string label,string asset,Vector3 at,Quaternion rotation,Material[] m,bool floorPivot=false)
        {
            var target=Group(id,parent,at);target.localRotation=rotation;
            var model=Instantiate(asset,target,m);if(floorPivot)model.localPosition=Vector3.down*1.1f;
            var collider=target.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(.8f,.7f,.3f);
            var interaction=target.gameObject.AddComponent<DemoInteractable>();interaction.Configure(id,label,0);
            interaction.ConfigureFeedback(model.GetComponentsInChildren<Renderer>().Where(x=>x.name.StartsWith("Status_")).ToArray());
            return interaction;
        }
        public static TextMesh Label(Transform parent,string text,Vector3 at,float height)
        {
            var t=Group("WorldLabel",parent,at);var label=t.gameObject.AddComponent<TextMesh>();label.text=text;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=64;label.characterSize=height*10/64;
            label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=Color.white;
            t.GetComponent<Renderer>().sharedMaterial=label.font.material;return label;
        }
    }
}
