using System.Collections.Generic;
using ChooGuard.App.Fps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChooGuard.EditorTools
{
    // 역사 건물만 담은 가벼운 씬을 만든다.
    //
    // 왜 "대합실만" 이 아닌가: 대합실은 별도 오브젝트가 아니다. 역사 셸은 메시 8개
    // (OfficialStation_-부산역_0~7)이고 대합실은 그 안의 한 구역이다 — 바닥이 _6, 벽이 _5 다.
    // 도려내려면 메시를 잘라야 하고, 그것은 PM 이 만든 모델의 파생물을 만드는 일이다.
    //
    // 그럴 필요가 없다. FpsStation 의 충돌 메시 1,979개 중 역사는 22개뿐이고 나머지는
    // 지하철(1,107+384)과 KTX(444)다. 열차를 빼는 것만으로 대부분이 사라진다.
    //
    // 원본을 고치지 않는다. FpsStation 은 읽기만 하고 저장하지 않는다.
    // 메시·재질 자산은 복제하지 않고 공유한다 — 원본 모델이 갱신되면 이 씬도 따라간다.
    public static class StationInteriorSceneBuilder
    {
        private const string SourceScene="Assets/ChooGuard/Scenes/FpsStation.unity";
        private const string OutputScene="Assets/ChooGuard/Scenes/StationInterior.unity";
        private const string StationRoot="공식 자료 부산역 역사";

        [MenuItem("ChooGuard/수직 슬라이스/역사 건물만 담은 씬 만들기")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {Debug.LogError("[역사씬] 재생 중에는 만들 수 없습니다.");return;}

            var source=EditorSceneManager.OpenScene(SourceScene,OpenSceneMode.Single);
            if(!source.IsValid()){Debug.LogError("[역사씬] 원본 씬을 열지 못했습니다 · "+SourceScene);return;}

            // 가져갈 것을 원본에서 찾는다. 하나라도 없으면 만들지 않는다 —
            // 반쯤 만들어진 씬을 남기면 다음 사람이 무엇이 빠졌는지 알 수 없다.
            GameObject station=null,playerRoot=null;
            var lights=new List<GameObject>();
            foreach(var root in source.GetRootGameObjects())
            {
                if(root==null)continue;
                if(station==null)station=FindByName(root.transform,StationRoot);
            }
            // 광원은 그 오브젝트만 가져온다. 루트를 통째로 가져오면 그 아래 세계가 전부 딸려온다 —
            // 실제로 렌더러가 2,336개(원본 2,322개보다 많다)로 나와서 발견했다(2026-09-27).
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light!=null&&light.type==LightType.Directional)lights.Add(light.gameObject);

            var responder=Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder!=null)playerRoot=responder.transform.root.gameObject;

            if(station==null)
            {
                // 못 찾으면 무엇이 있는지 보여준다. 이름만 말하고 끝내면 다음 사람이 다시 찾아야 한다.
                var names=new System.Text.StringBuilder();
                foreach(var root in source.GetRootGameObjects())
                {
                    if(root==null)continue;
                    names.Append("\n  ").Append(root.name);
                    for(int i=0;i<root.transform.childCount&&i<12;i++)
                        names.Append("\n    - ").Append(root.transform.GetChild(i).name);
                }
                Debug.LogError("[역사씬] 역사 루트를 찾지 못했습니다 · "+StationRoot+" · 씬 계층:"+names);
                return;
            }
            if(playerRoot==null){Debug.LogError("[역사씬] 플레이어를 찾지 못했습니다.");return;}
            if(lights.Count==0)Debug.LogWarning("[역사씬] 광원을 찾지 못했습니다. 새 씬이 어두울 수 있습니다.");

            // 원본의 하늘·환경광을 그대로 가져간다. 새 씬 기본값을 쓰면 색이 달라져
            // "같은 역사인데 다르게 보이는" 상태가 된다.
            var skybox=RenderSettings.skybox;
            var ambientMode=RenderSettings.ambientMode;
            var ambientSky=RenderSettings.ambientSkyColor;
            var ambientEquator=RenderSettings.ambientEquatorColor;
            var ambientGround=RenderSettings.ambientGroundColor;
            var ambientIntensity=RenderSettings.ambientIntensity;
            var fog=RenderSettings.fog;

            var target=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            int copied=0;
            copied+=Copy(station,target);
            copied+=Copy(playerRoot,target);
            foreach(var light in lights)
                if(light!=station&&light!=playerRoot)copied+=Copy(light,target);

            SceneManager.SetActiveScene(target);
            RenderSettings.skybox=skybox;
            RenderSettings.ambientMode=ambientMode;
            RenderSettings.ambientSkyColor=ambientSky;
            RenderSettings.ambientEquatorColor=ambientEquator;
            RenderSettings.ambientGroundColor=ambientGround;
            RenderSettings.ambientIntensity=ambientIntensity;
            RenderSettings.fog=fog;

            if(!EditorSceneManager.SaveScene(target,OutputScene))
            {Debug.LogError("[역사씬] 저장하지 못했습니다 · "+OutputScene);return;}

            int renderers=0;long triangles=0;
            foreach(var root in target.GetRootGameObjects())
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderers++;
                    var filter=renderer.GetComponent<MeshFilter>();
                    if(filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.isReadable)
                        triangles+=filter.sharedMesh.triangles.Length/3;
                }
            Debug.Log("[역사씬] 저장 · "+OutputScene+" · 최상위 "+copied+"개 · 렌더러 "+renderers
                      +" · 읽을 수 있는 삼각형 "+triangles.ToString("N0"));
            // 숫자만 믿지 않는다. 플레이어가 실제로 바닥 위에 서는지, 조명이 따라왔는지 확인한다 —
            // 오늘까지 "개수는 맞는데 화면이 다른" 일을 여러 번 겪었다.
            Physics.SyncTransforms();
            var feet=playerRoot.transform.position;
            if(Physics.Raycast(feet+Vector3.up*1.5f,Vector3.down,out var floor,20f,~0,QueryTriggerInteraction.Ignore))
                Debug.Log("[역사씬] 플레이어 발밑 · "+floor.collider.name+" · "
                          +floor.distance.ToString("F2")+"m 아래");
            else
                Debug.LogError("[역사씬] 플레이어 발밑에 바닥이 없습니다. 이 씬에서는 떨어집니다 · "
                               +feet.ToString("F2"));

            int directional=0;
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional)directional++;
            if(directional==0)Debug.LogError("[역사씬] 방향광이 없습니다. 화면이 어둡습니다.");
            else Debug.Log("[역사씬] 방향광 "+directional+"개 · 스카이박스 "
                           +(RenderSettings.skybox==null?"없음":RenderSettings.skybox.name)
                           +" · 환경광 "+RenderSettings.ambientMode);

            Debug.Log("[역사씬] 빌드 설정에는 넣지 않았습니다. 에디터에서 열어 Play 하면 됩니다.");
        }

        // 이름으로 계층을 훑는다. 조합 루트 아래에 들어가 있을 수 있어 최상위만 봐서는 못 찾는다.
        private static GameObject FindByName(Transform node,string name)
        {
            if(node==null)return null;
            if(node.name==name)return node.gameObject;
            for(int i=0;i<node.childCount;i++)
            {
                var found=FindByName(node.GetChild(i),name);
                if(found!=null)return found;
            }
            return null;
        }

        // 원본을 복제해 새 씬으로 옮긴다. 원본 씬은 건드리지 않는다.
        private static int Copy(GameObject source,Scene target)
        {
            if(source==null)return 0;
            var clone=Object.Instantiate(source);
            clone.name=source.name;
            clone.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            clone.transform.localScale=source.transform.localScale;
            SceneManager.MoveGameObjectToScene(clone,target);
            return 1;
        }
    }
}
