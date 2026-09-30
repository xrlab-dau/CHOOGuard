using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Work;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.EditorTools
{
    /// <summary>
    /// 플레이어를 실제로 걷게 하면서 연속 프레임을 남긴다. 씬을 저장하지 않는다.
    /// </summary>
    /// <remarks>
    /// 정지 화면 몇 장으로는 "저기까지 갈 수 있는가" 를 알 수 없다. 그래서 한 걸음씩 옮기며
    /// 두 가지를 매번 확인한다 — 발밑에 바닥이 있는가(없으면 허공), 앞이 막혔는가(캡슐 검사).
    /// 막히면 막힌 자리에서 멈추고 그 사실을 기록한다. 통과시키지 않는다.
    ///
    /// 벽을 뚫고 지나가게 만들면 "12대를 다 돌았다" 는 거짓 결론이 나온다.
    /// </remarks>
    public static class TutorialWalkthrough
    {
        private const int Width=640,Height=360,Quality=75;
        private const float Step=0.9f;          // 한 프레임당 전진 거리
        private const float EyeHeight=1.62f;
        private const float BodyRadius=.30f;    // FirstPersonResponder 의 캡슐과 같은 크기대
        private const float BodyHeight=1.75f;
        // 상한은 측정 편의일 뿐 제품 제약이 아니다. 140 에서는 마지막 FE-002 가 8.6m 를 남기고
        // 끝나 "막혔다" 와 구별되지 않았다. 12대를 다 돌 수 있는지 보려면 넉넉해야 한다.
        private const int MaxFrames=260;
        // 정면이 막히면 시도해 보는 각도. 0 이 곧장 앞이고, 좌우를 번갈아 넓혀 간다.
        // 90°를 넘기면 목표에서 멀어지므로 거기까지만 본다.
        private static readonly float[] Detours={0,25,-25,50,-50,75,-75};

        [MenuItem("ChooGuard/수직 슬라이스/튜토리얼 맵 걸어서 순회")]
        public static void Run()
        {
            var root=Environment.GetEnvironmentVariable("CG_SHOT_DIR");
            if(string.IsNullOrEmpty(root)){Debug.LogError("[걷기] CG_SHOT_DIR 을 주세요.");return;}
            Directory.CreateDirectory(root);

            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[걷기] 씬을 열지 못했습니다.");return;}
            Physics.SyncTransforms();

            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null){Debug.LogError("[걷기] 플레이어를 찾지 못했습니다.");return;}
            // 플레이어 자신의 콜라이더는 빼고 검사한다. 켜 두면 자기 자신에 막힌다.
            var own=responder.GetComponentsInChildren<Collider>(true);
            var ownWas=new bool[own.Length];
            for(int i=0;i<own.Length;i++){ownWas[i]=own[i].enabled;own[i].enabled=false;}
            Physics.SyncTransforms();

            var log=new StringBuilder();
            int frame=0;
            try
            {
                var units=UnityEngine.Object.FindObjectsByType<FacilityInspectable>(FindObjectsSortMode.None)
                    .OrderBy(f=>f.SerialNumber,StringComparer.Ordinal).ToList();
                var start=responder.transform.position;
                var route=NearestFirst(start,units);
                Debug.Log("[걷기] 출발 "+start.ToString("F1")+" · 경유 "+route.Count+"곳");

                var position=start;
                foreach(var leg in route)
                {
                    // 소화기 앞 1.4m 를 목표로 삼는다. 소화기 자리 자체로 가면 몸이 겹친다.
                    var goal=leg.transform.position+leg.transform.forward*1.4f;
                    var outcome=Walk(ref position,goal,root,ref frame,log,leg.SerialNumber);
                    Debug.Log("[걷기] "+leg.SerialNumber+" · "+outcome
                              +" · 현재 "+position.ToString("F1")
                              +" · 목표까지 "+Vector3.Distance(position,goal).ToString("F1")+"m");
                    if(frame>=MaxFrames){Debug.Log("[걷기] 프레임 상한에서 멈춥니다.");break;}
                }
            }
            finally
            {
                for(int i=0;i<own.Length;i++)own[i].enabled=ownWas[i];
                Physics.SyncTransforms();
            }
            File.WriteAllText(Path.Combine(root,"walk.txt"),log.ToString(),new UTF8Encoding(false));
            Debug.Log("CG_WALK frames="+frame+" dir="+root);
        }

        // 가까운 것부터 잇는다. 최적 경로를 구하려는 것이 아니라 사람이 돌 법한 순서를 만드는 것이다.
        private static List<FacilityInspectable> NearestFirst(Vector3 from,List<FacilityInspectable> units)
        {
            var remaining=new List<FacilityInspectable>(units);
            var route=new List<FacilityInspectable>();
            var cursor=from;
            while(remaining.Count>0)
            {
                var next=remaining.OrderBy(u=>Vector3.SqrMagnitude(u.transform.position-cursor)).First();
                remaining.Remove(next);
                route.Add(next);
                cursor=next.transform.position;
            }
            return route;
        }

        /// <summary>한 구간을 걷는다. 막히거나 바닥이 없으면 그 자리에서 멈추고 이유를 돌려준다.</summary>
        private static string Walk(ref Vector3 position,Vector3 goal,string root,ref int frame,StringBuilder log,string label)
        {
            for(int guard=0;guard<200;guard++)
            {
                if(frame>=MaxFrames)return "프레임 상한";
                var flat=new Vector3(goal.x-position.x,0,goal.z-position.z);
                float remaining=flat.magnitude;
                if(remaining<.35f)return "도착";
                var direction=flat/remaining;
                float advance=Mathf.Min(Step,remaining);

                // 앞이 막혔는가. 몸통 캡슐로 본다 — 점으로 보면 벽을 스쳐 지나간다.
                //
                // 막히면 바로 포기하지 않고 좌우로 비껴 본다. 사람은 장애물을 돌아간다.
                // 회피 없이 재면 목표물 자신의 콜라이더에 부딪혀 "갈 수 없다" 고 잘못 보고한다 —
                // 실제로 FE-003 을 그렇게 결함으로 보고했다가 뒤집었다(2026-09-28).
                var bottom=position+Vector3.up*(BodyRadius+.05f);
                var top=position+Vector3.up*(BodyHeight-BodyRadius);
                Vector3 chosen=direction;string blocker=null;bool moved=false;
                foreach(float turn in Detours)
                {
                    var probe=Quaternion.Euler(0,turn,0)*direction;
                    if(Physics.CapsuleCast(bottom,top,BodyRadius,probe,out var hit,advance+.05f,~0,QueryTriggerInteraction.Ignore))
                    {
                        if(blocker==null)blocker=hit.collider.name;
                        continue;
                    }
                    chosen=probe;moved=true;break;
                }
                if(!moved)
                {
                    Capture(root,frame,position,direction,log,label,"막힘 "+blocker);
                    frame++;
                    return "막힘 · "+blocker;
                }
                direction=chosen;

                var next=position+direction*advance;
                // 발밑에 바닥이 있는가. 없으면 허공이므로 나아가지 않는다.
                if(!Physics.Raycast(next+Vector3.up*1.2f,Vector3.down,out var floor,3.5f,~0,QueryTriggerInteraction.Ignore))
                {
                    Capture(root,frame,position,direction,log,label,"바닥 없음");
                    frame++;
                    return "바닥 없음";
                }
                next.y=floor.point.y;
                position=next;
                Capture(root,frame,position,direction,log,label,"이동");
                frame++;
            }
            return "구간 상한";
        }

        private static void Capture(string root,int frame,Vector3 position,Vector3 facing,StringBuilder log,string label,string state)
        {
            var eye=position+Vector3.up*EyeHeight;
            var path=Path.Combine(root,string.Format(CultureInfo.InvariantCulture,"walk-{0:000}.jpg",frame));
            var holder=EditorUtility.CreateGameObjectWithHideFlags("CG walk",HideFlags.HideAndDontSave,typeof(Camera));
            var camera=holder.GetComponent<Camera>();
            var target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){antiAliasing=2};
            var previous=RenderTexture.active;
            var texture=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            try
            {
                var source=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .FirstOrDefault(c=>c.name=="FirstPersonCamera");
                if(source!=null)camera.CopyFrom(source);
                camera.fieldOfView=70;camera.nearClipPlane=.05f;camera.farClipPlane=600;
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(facing,Vector3.up));
                camera.targetTexture=target;
                camera.Render();
                RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,Width,Height),0,0);
                texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToJPG(Quality));
                log.Append(frame).Append(' ')
                   .Append(position.x.ToString("F2",CultureInfo.InvariantCulture)).Append(' ')
                   .Append(position.y.ToString("F2",CultureInfo.InvariantCulture)).Append(' ')
                   .Append(position.z.ToString("F2",CultureInfo.InvariantCulture)).Append(' ')
                   .Append(label).Append(' ').Append(state).Append('\n');
            }
            catch(Exception error){Debug.LogError("[걷기] 프레임 "+frame+" 실패 · "+error.Message);}
            finally
            {
                camera.targetTexture=null;
                RenderTexture.active=previous;
                UnityEngine.Object.DestroyImmediate(texture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }
    }
}
