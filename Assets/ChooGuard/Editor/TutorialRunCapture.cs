using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Work;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.EditorTools
{
    /// <summary>
    /// 정비 튜토리얼 한 바퀴를 끝까지 돌리면서 단계마다 화면을 남긴다. 씬을 저장하지 않는다.
    /// </summary>
    /// <remarks>
    /// 무엇을 증명하고 무엇을 증명하지 않는가:
    /// - 증명한다 — 절차가 순서대로 진행되고, 각 단계에서 월드가 어떻게 보이는지.
    /// - 증명하지 않는다 — 사람이 마우스로 조준해 되는지. 시선은 아래처럼 주입한다.
    ///
    /// 시선은 tracker.Accumulate(point.Surface, seconds) 로 넣는다. 내가 만든 우회가 아니라
    /// Tests/PlayMode/FpsProcedureTests 가 쓰는 것과 같은 경로이고, 그 파일 주석이 밝히듯
    /// "시선을 합성하지 않고 관측 시간만 먹인다. Update 와 같은 경로다".
    /// 에디터 모드에서는 Update 가 돌지 않으므로 이 경로 말고는 응시를 만들 수 없다.
    /// </remarks>
    public static class TutorialRunCapture
    {
        private const int Width=1600,Height=900;
        private const float Dwell=1.5f;
        private const int MaxTurns=40;

        [MenuItem("ChooGuard/수직 슬라이스/튜토리얼 한 바퀴 돌리며 화면 남기기")]
        public static void Run()
        {
            var outputRoot=Environment.GetEnvironmentVariable("CG_SHOT_DIR");
            if(string.IsNullOrEmpty(outputRoot)){Debug.LogError("[튜토리얼] CG_SHOT_DIR 을 주세요.");return;}
            Directory.CreateDirectory(outputRoot);

            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[튜토리얼] 씬을 열지 못했습니다.");return;}

            var session=UnityEngine.Object.FindFirstObjectByType<TutorialSession>();
            if(session==null){Debug.LogError("[튜토리얼] TutorialSession 이 씬에 없습니다.");return;}
            var tracker=session.GazeTracker??UnityEngine.Object.FindFirstObjectByType<FpsGazeTracker>();
            if(tracker==null){Debug.LogError("[튜토리얼] FpsGazeTracker 가 없습니다.");return;}

            // 이월을 끄고 시작한다. 이전 회차가 남아 있으면 '처음부터' 가 아니다.
            session.ApplyCarryoverOnStart=false;
            session.WriteCarryover=false;
            session.Bind();
            if(!session.EnsureLoaded()){Debug.LogError("[튜토리얼] 절차를 불러오지 못했습니다.");return;}
            Debug.Log("[튜토리얼] 절차 "+session.Runner.Title+" · 유닛 "+session.UnitCount+"개 · 대상 "
                      +(session.Target!=null?session.Target.SerialNumber:"없음"));

            int shot=0;
            for(int turn=0;turn<MaxTurns;turn++)
            {
                var decision=session.Peek();
                var step=decision.Step;
                var id=step!=null?step.Id:"(없음)";
                Debug.Log("[튜토리얼] "+turn+" · 단계 "+id
                          +" · 가능 "+decision.Allowed
                          +" · 가드 "+decision.Guard
                          +(string.IsNullOrEmpty(decision.Reason)?"":" · 사유 "+decision.Reason));

                Capture(outputRoot,string.Format(CultureInfo.InvariantCulture,"step-{0:00}-{1}",shot++,id),session);

                if(session.Finished){Debug.Log("[튜토리얼] 절차 완료.");break;}
                if(step==null){Debug.Log("[튜토리얼] 더 진행할 단계가 없습니다.");break;}

                if(decision.Allowed)
                {
                    if(!session.Advance()){Debug.LogWarning("[튜토리얼] Advance 가 거절됐습니다 · "+session.LastReason);break;}
                    continue;
                }

                // 막혔다면 그 단계가 요구하는 것을 사람 대신 해 준다. 무엇을 했는지 반드시 남긴다.
                if(!Satisfy(session,tracker,id))
                {
                    Debug.LogWarning("[튜토리얼] 이 단계를 만족시키는 방법을 모릅니다 · "+id+" · "+decision.Reason);
                    break;
                }
            }

            Debug.Log("[튜토리얼] 판정 "+(session.Target!=null?session.Target.Verdict.ToString():"?")
                      +" · 역할경계 위반 "+session.RoleBoundaryViolations
                      +" · 오판정 "+session.MisjudgementCount);
            foreach(var finding in session.AuditFindings)Debug.Log("[튜토리얼] 감사 · "+finding);
            Debug.Log("CG_TUTORIAL_SHOTS taken="+shot+" dir="+outputRoot);
        }

        /// <summary>막힌 단계가 요구하는 조건을 만든다. 무엇을 했는지 로그로 남긴다.</summary>
        private static bool Satisfy(TutorialSession session,FpsGazeTracker tracker,string stepId)
        {
            var facility=session.Target;
            if(facility==null)return false;
            switch(stepId)
            {
                case "read-serial":return Gaze(tracker,facility,"serial");
                case "read-spec-plate":return Gaze(tracker,facility,"spec-plate");
                case "check-gauge":return Gaze(tracker,facility,"gauge");
                case "check-body":return Gaze(tracker,facility,"body");
                case "record-verdict":
                {
                    // 월드의 진짜 상태를 보고 고른다. 부식이면 부적합이다.
                    var verdict=facility.Corroded||facility.MechanicallyDefective||facility.PressureOutOfRange
                        ?InspectionVerdict.UNFIT:InspectionVerdict.FIT;
                    bool ok=session.SelectVerdict(verdict);
                    Debug.Log("[튜토리얼] 판정 선택 "+verdict+" (부식 "+facility.Corroded+") · "+ok);
                    return ok;
                }
                case "witness-replacement":
                {
                    var dispatch=session.Dispatch;
                    if(dispatch==null)return false;
                    // 벽시계를 쓰지 않는 설계다. 시간을 밀어 준다.
                    for(int i=0;i<200&&!dispatch.Completed;i++)dispatch.Tick(1f);
                    bool ok=session.WitnessRepairResult();
                    Debug.Log("[튜토리얼] 기술자 "+dispatch.StageLabel+" · 결과 확인 "+ok);
                    return ok;
                }
                default:return false;
            }
        }

        private static bool Gaze(FpsGazeTracker tracker,FacilityInspectable facility,string pointId)
        {
            var point=facility.Point(pointId);
            if(point==null||point.Surface==null){Debug.LogWarning("[튜토리얼] 관측 지점 없음 · "+pointId);return false;}
            tracker.Accumulate(point.Surface,Dwell);
            Debug.Log("[튜토리얼] 응시 주입 "+pointId+" "+Dwell.ToString("F1",CultureInfo.InvariantCulture)+"초");
            return true;
        }

        /// <summary>대상 소화기를 사람이 보는 각도에서 찍는다.</summary>
        private static void Capture(string root,string name,TutorialSession session)
        {
            var facility=session.Target;
            if(facility==null)return;
            var body=facility.transform.position+Vector3.up*1.1f;
            // 소화기 정면에서 1.6m 물러나 눈높이로 본다. 판독면이 보이는지 확인하려는 것이다.
            var eye=body+facility.transform.forward*1.6f+Vector3.up*.45f;
            var path=Path.Combine(root,"shot-"+name+".png");
            var holder=EditorUtility.CreateGameObjectWithHideFlags("CG shot",HideFlags.HideAndDontSave,typeof(Camera));
            var camera=holder.GetComponent<Camera>();
            var target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){antiAliasing=4};
            var previous=RenderTexture.active;
            var texture=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            try
            {
                var source=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .FirstOrDefault(c=>c.name=="FirstPersonCamera");
                if(source!=null)camera.CopyFrom(source);
                camera.fieldOfView=60;camera.nearClipPlane=.05f;camera.farClipPlane=600;
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation((body-eye).normalized,Vector3.up));
                camera.targetTexture=target;
                camera.Render();
                RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,Width,Height),0,0);
                texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            catch(Exception error){Debug.LogError("[튜토리얼] 캡처 실패 "+name+" · "+error.Message);}
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
