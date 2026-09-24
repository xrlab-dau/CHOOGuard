using ChooGuard.App.Fps;
using ChooGuard.App.Fps.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.EditorTools
{
    // 승객 한 명을 대합실에 놓고 목적지를 준다. 본편 첫 조각의 시험 대상이다.
    //
    // 자리를 계산 하나로 정하지 않는다. 오늘 하루에 네 번, 좌표를 계산으로 고정했다가 실제
    // 구조물과 부딪혔다. 여기서는 출발지와 목적지를 후보로 놓고 **실제로 경로가 나오는지**
    // 물어본 뒤 쓴다. 한 쌍도 못 찾으면 만들지 않고 크게 알린다.
    public static class PassengerBuilder
    {
        private const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
        private const string RootName="승객 · 시험";
        private const string MaterialDir="Assets/ChooGuard/Art/FireSafety/Materials";
        private const string MaterialPath=MaterialDir+"/tutorial_passenger.mat";

        [MenuItem("ChooGuard/수직 슬라이스/승객 배치 (시험용 1명)")]
        public static void Build()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[승객] 씬을 열지 못했습니다 · "+ScenePath);return;}
            var responder=Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null){Debug.LogError("[승객] 플레이어가 없습니다.");return;}
            var navigation=Object.FindFirstObjectByType<StationNavigation>();
            if(navigation==null||navigation.NavData==null)
            {Debug.LogError("[승객] navmesh 가 없습니다. 먼저 '역사 보행 navmesh 굽기' 를 실행하세요.");return;}

            foreach(var root in scene.GetRootGameObjects())
                if(root!=null&&root.name==RootName)Undo.DestroyObjectImmediate(root);

            var origin=responder.transform.position;
            // 플레이어 앞 4m 를 출발지로, 그보다 먼 곳을 목적지로 삼는다. 둘 다 경로가 나와야 쓴다.
            if(!FindSpot(origin,4f,out var start))
            {Debug.LogError("[승객] 출발지를 찾지 못했습니다.");return;}

            Vector3 goal=Vector3.zero;bool found=false;string tried="";int noSpot=0,tooClose=0,noPath=0;
            foreach(var distance in new[]{22f,18f,14f,10f})
            {
                for(int degrees=0;degrees<360&&!found;degrees+=30)
                {
                    var direction=Quaternion.Euler(0,degrees,0)*Vector3.forward;
                    if(!FindSpot(origin+direction*distance,0f,out var candidate)){noSpot++;continue;}
                    if(Vector3.Distance(candidate,start)<6f){tooClose++;continue;}
                    if(!navigation.TryPlan(start,candidate,out var corners,out var reason))
                    {tried+="\n  "+distance.ToString("0")+"m "+degrees+"도 · "+reason;continue;}
                    goal=candidate;found=true;
                    Debug.Log("[승객] 경로 확인 · "+start.ToString("F2")+" -> "+goal.ToString("F2")
                              +" · 꺾임 "+corners.Length+"개 · 거리 "
                              +Vector3.Distance(start,goal).ToString("F1")+"m");
                }
                if(found)break;
            }
            if(!found){Debug.LogError("[승객] 경로가 나오는 목적지를 찾지 못했습니다 · 설 자리 없음 "+noSpot+" · 너무 가까움 "+tooClose+" · 경로 실패 "+noPath+" 시도:"+tried);return;}

            var host=new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(host,"승객 루트 생성");

            var marker=new GameObject("목적지");
            Undo.RegisterCreatedObjectUndo(marker,"목적지 생성");
            marker.transform.SetParent(host.transform,false);
            marker.transform.position=goal;

            var body=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name="승객";
            Undo.RegisterCreatedObjectUndo(body,"승객 생성");
            body.transform.SetParent(host.transform,false);
            body.transform.position=start+Vector3.up*.85f;
            body.transform.localScale=new Vector3(.36f,.85f,.36f);
            // 콜라이더를 지운다. 이 조각의 목적은 "걷는가" 이지 밀치기가 아니다.
            // 몸을 붙이면 플레이어를 밀거나 설비 조준을 가로챈다 — 기술자 대역과 같은 이유다.
            var collider=body.GetComponent<Collider>();
            if(collider!=null)Undo.DestroyObjectImmediate(collider);
            var renderer=body.GetComponent<MeshRenderer>();
            if(renderer!=null)renderer.sharedMaterial=PassengerMaterial();

            var agent=Undo.AddComponent<PassengerAgent>(body);
            agent.Navigation=navigation;
            agent.StartDestination=marker.transform;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[승객] 배치 완료 · 출발 "+start.ToString("F2")+" · 목적지 "+goal.ToString("F2"));
        }

        // 바닥을 찾아 그 위에 놓는다. 사람이 들어갈 자리가 아니면 쓰지 않는다.
        private static bool FindSpot(Vector3 near,float forward,out Vector3 spot)
        {
            spot=Vector3.zero;
            var probe=near+Vector3.forward*forward;
            if(!Physics.Raycast(probe+Vector3.up*2.5f,Vector3.down,out var floor,8f,~0,QueryTriggerInteraction.Ignore))
                return false;
            if(Physics.CheckCapsule(floor.point+Vector3.up*.35f,floor.point+Vector3.up*1.4f,.3f,~0,
                                    QueryTriggerInteraction.Ignore))return false;
            spot=floor.point;
            return true;
        }

        private static Material PassengerMaterial()
        {
            var existing=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(existing!=null)return existing;
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null){Debug.LogError("[승객] URP/Lit 셰이더를 찾지 못했습니다.");return null;}
            var material=new Material(shader){name="tutorial_passenger"};
            material.SetColor("_BaseColor",new Color(.72f,.45f,.28f));   // 기술자(남색)와 구분되는 색
            material.SetFloat("_Metallic",0f);
            material.SetFloat("_Smoothness",.15f);
            AssetDatabase.CreateAsset(material,MaterialPath);
            Debug.Log("[승객] 재질 생성 · "+MaterialPath);
            return material;
        }
    }
}
