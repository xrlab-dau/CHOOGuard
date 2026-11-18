using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    public sealed partial class StationFireTutorialSession
    {
        LineRenderer routeLine;
        readonly List<Vector3> visibleRoute=new List<Vector3>();
        public IReadOnlyList<Vector3> VisibleRoute=>visibleRoute;
        public Vector3 VisibleAimPoint {get;private set;}
        Transform responseMarker;
        public static Mesh GroundRingMesh(float radius)
        {
            const int count=64;var v=new Vector3[count*2];var tris=new int[count*6];
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));v[i*2]=d*(radius-.045f);v[i*2+1]=d*radius;
                int j=(i+1)%count;int n=i*6;tris[n]=i*2;tris[n+1]=j*2;tris[n+2]=i*2+1;tris[n+3]=i*2+1;tris[n+4]=j*2;tris[n+5]=j*2+1;
            }
            var mesh=new Mesh{name="설치 허용 구역 안내"};mesh.vertices=v;mesh.triangles=tris;mesh.RecalculateNormals();return mesh;
        }
        Transform GroundRing(string name,Vector3 point,float radius,Color color)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);go.transform.position=Grounded(point)+Vector3.up*.025f;
            go.GetComponent<MeshFilter>().sharedMesh=GroundRingMesh(radius);
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=color;material.SetInt("_Cull",0);
            go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        void InitializeGroundGuide()
        {
            responseMarker=GroundRing("안전한 분사 위치 안내",SuppressionPoint,.6f,new Color(.95f,.7f,.12f));
            var line=new GameObject("현재 목표까지 보이는 보행 안내선");line.transform.SetParent(transform,false);routeLine=line.AddComponent<LineRenderer>();
            routeLine.useWorldSpace=true;routeLine.widthMultiplier=.065f;routeLine.numCornerVertices=4;
            routeLine.material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(.95f,.75f,.2f)};
        }
        void ShowVisibleRoute(Vector3 destination,bool show)
        {
            visibleRoute.Clear();routeLine.enabled=false;if(!show||!Captured)return;
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(Player.transform.position,destination,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)return;
            foreach(var point in path.corners)visibleRoute.Add(point+Vector3.up*.045f);
            routeLine.positionCount=visibleRoute.Count;routeLine.SetPositions(visibleRoute.ToArray());routeLine.enabled=visibleRoute.Count>1;
        }
        Vector3 CurrentExtinguisherApproach()
        {
            var away=Player.transform.position-ExtinguisherBody.position;away.y=0;
            if(away.sqrMagnitude<.1f)away=-(layoutRotation*Vector3.forward);
            var desired=ExtinguisherBody.position+away.normalized*.9f;desired.y=Player.transform.position.y;
            return NavMesh.SamplePosition(desired,out var hit,.8f,NavMesh.AllAreas)?hit.position:ExtinguisherPickupPoint;
        }
    }
}
