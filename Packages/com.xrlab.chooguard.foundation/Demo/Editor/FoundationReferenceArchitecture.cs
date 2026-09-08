using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using static ChooGuard.Foundation.Demo.Editor.FoundationBlenderAssets;

namespace ChooGuard.Foundation.Demo.Editor
{
    // Public-reference form study in the existing synthetic footprint; dimensions are NOT surveyed.
    // Run after the old mesh replacement and annex construction. All new solid boundaries are above 3.2m.
    public static class FoundationReferenceArchitecture
    {
        public static void Build(Transform root,Material[] materials,string generatedRoot)
        {
            Material M(string name)=>materials.Single(m=>m.name==name);
            var environment=root.Find("Environment");
            var architecture=Group("ReferenceArchitecture",environment,Vector3.zero);
            var interior=environment.Find("InteriorDetails");
            foreach(var old in interior.Cast<Transform>().Where(t=>t.name.StartsWith("FloorJoint")).ToArray())Object.DestroyImmediate(old.gameObject);
            foreach(var ceiling in environment.GetComponentsInChildren<MeshFilter>().Where(t=>t.name.Contains("Ceiling")&&!t.name.Contains("Light")).ToArray())
            {
                var concourse=ceiling.name=="CeilingConcourse";
                var position=ceiling.transform.position;position.y=concourse?7.4f:4.2f;ceiling.transform.position=position;
                ceiling.GetComponent<Renderer>().sharedMaterial=M("Ceiling");
            }
            // Close the new upper envelope with matching visual and raycast surfaces. Ground colliders are untouched.
            foreach(Transform wall in environment.Cast<Transform>().ToArray())
            {
                if(wall.GetComponent<BoxCollider>()==null||wall.localScale.y<3.1f||wall.name.Contains("Collision"))continue;
                var high=wall.name.StartsWith("Concourse")||wall.name.Contains("PassageWing")||wall.name=="WestUpperWall"||wall.name=="EastUpperWall";
                var top=high?7.34f:4.14f;
                var scale=wall.localScale;scale.y=top-3.2f;
                var at=wall.localPosition;at.y=3.2f+scale.y/2;
                Box(wall.name+"_UpperClosure",architecture,at,scale,high?M("Glass"):M("Wall"));
                if(!high)continue;
                var alongX=scale.x>scale.z;var length=alongX?scale.x:scale.z;
                for(var offset=-length/2+1;offset<length/2;offset+=2)
                {
                    var point=at+(alongX?Vector3.right:Vector3.forward)*offset;
                    Box(wall.name+"_Mullion_"+offset,architecture,point,alongX?new Vector3(.055f,scale.y,.36f):new Vector3(.36f,scale.y,.055f),M("White"),false);
                }
                foreach(var y in new[]{3.25f,5.2f,7.29f})
                {
                    at.y=y;
                    Box(wall.name+"_Transom_"+y,architecture,at,alongX?new Vector3(length,.075f,.36f):new Vector3(.36f,.075f,length),M("White"),false);
                }
            }
            Box("ConcourseToCorridorHeader",architecture,new Vector3(0,5.74f,8.15f),new Vector3(6,3.2f,.3f),M("Wall"));
            foreach(var z in new[]{-4f,1f,6f})
            {
                var truss=Instantiate("RoofTruss",architecture,materials);truss.localPosition=new Vector3(0,6.15f,z);
            }
            for(var x=-6;x<=6;x+=2)
            {
                var purlin=Instantiate("Purlin",architecture,materials);purlin.localPosition=new Vector3(x,7.15f,.5f);purlin.localScale=new Vector3(1,1,15);
            }
            foreach(var side in new[]{-1,1})foreach(var z in new[]{-2,6})
            {
                var extension=Instantiate("Pillar",architecture,materials);
                extension.name="ColumnExtension_"+side+"_"+z;
                extension.localPosition=new Vector3(side*6.6f,4.675f,z);extension.localScale=new Vector3(1,.921875f,1);
                var collider=extension.gameObject.AddComponent<BoxCollider>();collider.center=Vector3.zero;collider.size=new Vector3(.70f,3.2f,.70f);
            }
            var board=Instantiate("DepartureBoard",architecture,materials);board.localPosition=new Vector3(0,4.3f,7.76f);
            var boardCollider=board.gameObject.AddComponent<BoxCollider>();boardCollider.size=new Vector3(3.2f,1,.15f);
            Label(board,"CONCOURSE   /   PLATFORMS",new Vector3(0,.32f,-.12f),2.6f,Color.white);
            Label(board,"STATION INFORMATION",new Vector3(0,.03f,-.13f),2.4f,new Color(.82f,.91f,.6f));
            Label(board,"WEST     CENTRAL     EAST",new Vector3(0,-.29f,-.13f),2.4f,Color.white);
            foreach(var side in new[]{-1,1})
            {
                // Bay assets stay overhead; they do not invent navigable or visible exterior routes.
                var bay=Instantiate("ClerestoryBay",architecture,materials);
                bay.localPosition=new Vector3(side*8.15f,5.25f,3);bay.localRotation=Quaternion.Euler(0,90,0);bay.localScale=new Vector3(3,4,.9f);
                var closure=bay.gameObject.AddComponent<BoxCollider>();closure.size=new Vector3(1,1,.12f);
            }
            AddReferencedSurfaces(environment,materials);
            FoundationSurfaceMaterials.ApplyFloorScale(environment,generatedRoot,M("Floor"));
            ConfigureLights(root,materials);
        }

        private static void AddReferencedSurfaces(Transform environment,Material[] materials)
        {
            var surface=Group("ReferenceSurfaces",environment,Vector3.zero);
            var interior=environment.Find("InteriorDetails");
            foreach(var old in interior.Cast<Transform>().Where(t=>t.name.Contains("_Skirting")||t.name.Contains("_Band")||t.name.Contains("_Joint_")).ToArray())Object.DestroyImmediate(old.gameObject);
            foreach(var wall in environment.Cast<Transform>().ToArray())
            {
                if(wall.GetComponent<BoxCollider>()==null||wall.localScale.y<2.25f)continue;
                var alongX=wall.localScale.x>wall.localScale.z;
                var length=alongX?wall.localScale.x:wall.localScale.z;
                var thickness=alongX?wall.localScale.z:wall.localScale.x;
                var faces=wall.name.Contains("Partition")||Mathf.Abs(Mathf.Abs(wall.position.x)-8.15f)<.02f?2:1;
                var direction=alongX?(wall.position.z<0?Vector3.forward:Vector3.back):(wall.position.x<0?Vector3.right:Vector3.left);
                var count=Mathf.CeilToInt(length/4);var width=length/count;
                for(var face=0;face<faces;face++)for(var i=0;i<count;i++)
                {
                    var normal=face==0?direction:-direction;
                    var panel=Instantiate("WallPanel",surface,materials);
                    panel.localPosition=wall.localPosition+(alongX?Vector3.right:Vector3.forward)*(-length/2+width*(i+.5f))+normal*(thickness/2-.022f);
                    panel.localScale=new Vector3(width,wall.localScale.y-.06f,1);
                    panel.localRotation=Quaternion.Euler(0,alongX?(normal.z>0?180:0):(normal.x>0?-90:90),0);
                }
            }
            // Expose corrugated upper sheet faces toward the hall. The base ceiling remains the enclosure.
            for(var x=0;x<16;x++)
            {
                var roof=Instantiate("RoofPanel",surface,materials);roof.localPosition=new Vector3(-7.5f+x,7.335f,.5f);
                roof.localScale=new Vector3(1,1,15);roof.localRotation=Quaternion.Euler(0,0,180);
            }
            foreach(var ceiling in environment.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.Contains("Ceiling")&&!m.name.Contains("Light")&&m.name!="CeilingConcourse").ToArray())
            {
                var panel=Instantiate("CeilingPanel",surface,materials);
                panel.position=ceiling.transform.position+Vector3.down*.045f;
                panel.localScale=new Vector3(ceiling.transform.lossyScale.x/3.6f,1,ceiling.transform.lossyScale.z/3.6f);
                Object.DestroyImmediate(ceiling.GetComponent<Renderer>());Object.DestroyImmediate(ceiling);
            }
            var hall=interior.Find("HallSign");var oldBoard=hall.Find("Board");
            if(oldBoard!=null)Object.DestroyImmediate(oldBoard.gameObject);
            var hallBoard=Instantiate("AssemblySign",hall,materials);hallBoard.localScale=new Vector3(2.8f/1.7f,.42f/.45f,1);
            var title=hall.GetComponentInChildren<TextMesh>();title.anchor=TextAnchor.MiddleCenter;title.alignment=TextAlignment.Center;
            title.transform.localPosition=new Vector3(0,0,-.04f);title.characterSize=.009f;
            // An authored tactile strip, not a claim of approved accessibility routing or an incident solution.
            for(var i=0;i<44;i++)
            {
                var tile=Instantiate("TactileTile",surface,materials);tile.localPosition=new Vector3(-.75f,-.009f,-6.45f+i*.3f);
                tile.localScale=new Vector3(.3f,1,.3f);
            }
            var furniture=Group("ReferenceFurniture",environment,Vector3.zero);
            var counter=Instantiate("InformationIsland",furniture,materials);counter.localPosition=new Vector3(14.65f,0,-.6f);
            FoundationAssetPhysics.Apply(counter,counter,"InformationIsland");
        }

        private static void ConfigureLights(Transform root,Material[] materials)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.63f,.69f,.74f);
            RenderSettings.ambientEquatorColor=new Color(.46f,.48f,.50f);
            RenderSettings.ambientGroundColor=new Color(.30f,.31f,.32f);
            var sun=root.Find("Lighting/Sun").GetComponent<Light>();sun.intensity=.75f;sun.color=new Color(1,.98f,.93f);
            sun.shadows=LightShadows.Soft;sun.shadowStrength=.55f;sun.shadowBias=.04f;sun.shadowNormalBias=.2f;
            foreach(var fixture in root.Find("Environment").GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("CeilingLight_")||t.name=="Blender_CeilingLight"||t.name=="Blender_RecessedLight").ToArray())
            {
                // Nested FBX fixtures inherit their parent's position; create one actual light per assembly.
                if((fixture.name=="Blender_CeilingLight"||fixture.name=="Blender_RecessedLight")&&fixture.parent.name.StartsWith("CeilingLight_"))continue;
                var position=fixture.position;
                var rs=fixture.GetComponentsInChildren<Renderer>();var bound=rs[0].bounds;foreach(var r in rs)bound.Encapsulate(r.bounds);
                var mountingHeight=position.z<8&&Mathf.Abs(position.x)<8?6.15f:4.14f;
                position.y+=mountingHeight-bound.max.y;fixture.position=position;
                var source=Group("RoomFill",fixture,Vector3.down*.16f).gameObject.AddComponent<Light>();
                source.type=LightType.Point;source.range=11;source.intensity=1.25f;source.color=new Color(.97f,.98f,1);
                source.shadows=LightShadows.None;source.renderMode=LightRenderMode.Auto;
            }
            // One small realtime probe, refreshed once per scene load. No editor/environment bake.
            var probe=Group("ConcourseReflection",root.Find("Lighting"),new Vector3(0,2,.5f)).gameObject.AddComponent<ReflectionProbe>();
            probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=64;
            probe.size=new Vector3(18,10,17);probe.boxProjection=true;probe.hdr=false;probe.farClipPlane=40;
            probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=new Color(.57f,.64f,.70f);
            probe.enabled=false;probe.gameObject.AddComponent<DemoRealtimeReflection>();
        }

        private static void Label(Transform parent,string text,Vector3 position,float width,Color color)
        {
            var label=Group("InformationText",parent,position).gameObject.AddComponent<TextMesh>();
            label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=64;
            label.characterSize=.1f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;
            var renderer=label.GetComponent<MeshRenderer>();renderer.sharedMaterial=label.font.material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            if(renderer.bounds.size.x>0)label.transform.localScale=Vector3.one*(width/renderer.bounds.size.x);
        }
    }
}
