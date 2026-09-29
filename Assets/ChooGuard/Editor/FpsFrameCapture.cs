using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.EditorTools
{
    /// <summary>
    /// 씬을 열어 정해진 시점에서 프레임을 PNG 로 남긴다. 저장소에는 아무것도 쓰지 않는다 —
    /// 출력 폴더는 CG_SHOT_DIR 로 바깥에서 준다.
    /// </summary>
    /// <remarks>
    /// 왜 필요한가: 자동 시험은 "컴포넌트가 있다" 까지만 말하고 "보인다" 는 말하지 못한다.
    /// 2026-09-24 에 PlayMode 75개가 통과하는 동안 점검표가 아예 그려지지 않고 있었다.
    ///
    /// 왜 이 방식인가: 배치모드에서는 WaitForEndOfFrame 이 반환되지 않아 프레임 끝을 기다리는
    /// 하네스가 멈춘다(2026-09-25 에 40분 멈췄다). 대신 카메라를 RenderTexture 에 직접
    /// Render() 한다 — EmergencySceneBuilder.RenderPerspective 가 쓰는 것과 같은 방식이고,
    /// 그쪽은 private 이라 호출할 수 없어 여기에 다시 적는다.
    /// </remarks>
    public static class FpsFrameCapture
    {
        private const int Width=1600,Height=900;

        [MenuItem("ChooGuard/Emergency/현재 상태를 프레임으로 남긴다")]
        public static void Run()
        {
            // 출력 경로는 환경변수로 받는다. 기본값을 저장소 안에 두면 실수로 커밋된다.
            var outputRoot=Environment.GetEnvironmentVariable("CG_SHOT_DIR");
            if(string.IsNullOrEmpty(outputRoot))
            {
                Debug.LogError("[프레임] CG_SHOT_DIR 환경변수로 출력 폴더를 주세요. "
                               +"저장소 안에 기본값을 두지 않습니다.");
                return;
            }
            Directory.CreateDirectory(outputRoot);

            var scenePath=Environment.GetEnvironmentVariable("CG_SHOT_SCENE");
            if(string.IsNullOrEmpty(scenePath))scenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
            var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[프레임] 씬을 열지 못했습니다 · "+scenePath);return;}
            Debug.Log("[프레임] 씬 "+scenePath+" · 최상위 "+scene.rootCount+"개");

            // 플레이어가 서 있는 곳에서 본다. 임의의 좌표에서 찍으면 플레이어가 보는 화면이 아니다.
            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null){Debug.LogError("[프레임] 플레이어를 찾지 못했습니다.");return;}
            var eye=responder.transform.position+Vector3.up*1.6f;
            var forward=responder.transform.forward;
            Debug.Log("[프레임] 플레이어 "+responder.transform.position.ToString("F2")
                      +" · 정면 "+forward.ToString("F2"));

            int taken=0;
            taken+=Shot(outputRoot,"01-player-forward",eye,Quaternion.LookRotation(forward,Vector3.up));
            taken+=Shot(outputRoot,"02-player-right",eye,Quaternion.LookRotation(Quaternion.Euler(0,90,0)*forward,Vector3.up));
            taken+=Shot(outputRoot,"03-player-back",eye,Quaternion.LookRotation(-forward,Vector3.up));
            taken+=Shot(outputRoot,"04-player-left",eye,Quaternion.LookRotation(Quaternion.Euler(0,-90,0)*forward,Vector3.up));

            // 소화기가 있으면 그 앞에서도 한 장. 튜토리얼이 보는 대상이다.
            var target=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t=>t.name.StartsWith("소화기 · ",StringComparison.Ordinal));
            if(target!=null)
            {
                var body=target.position+Vector3.up*1.1f;
                var stand=body-target.forward*1.8f+Vector3.up*.5f;
                taken+=Shot(outputRoot,"05-"+target.name.Replace("소화기 · ",""),stand,
                            Quaternion.LookRotation((body-stand).normalized,Vector3.up));
                Debug.Log("[프레임] 소화기 "+target.name+" "+target.position.ToString("F2"));
            }
            else Debug.Log("[프레임] 소화기 유닛을 찾지 못했습니다.");

            Debug.Log("CG_SHOTS taken="+taken+" dir="+outputRoot);
        }

        /// <summary>한 장 찍는다. 화면이 통째로 비면 그것도 알리도록 밝기와 색 수를 함께 잰다.</summary>
        private static int Shot(string root,string name,Vector3 position,Quaternion rotation)
        {
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
                camera.fieldOfView=70;camera.nearClipPlane=.05f;camera.farClipPlane=600;
                camera.transform.SetPositionAndRotation(position,rotation);
                camera.targetTexture=target;
                camera.Render();
                RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,Width,Height),0,0);
                texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());

                // 까만 화면을 '찍었다' 고 보고하지 않기 위해 평균 밝기와 색 수를 남긴다.
                // 2026-09-24 에 "렌더러가 켜져 있다" 를 근거로 통과시켰다가 화면이 비어 있었다.
                var pixels=texture.GetPixels32();
                double sum=0;var seen=new System.Collections.Generic.HashSet<int>();
                for(int i=0;i<pixels.Length;i+=37)
                {
                    var pixel=pixels[i];
                    sum+=(pixel.r+pixel.g+pixel.b)/3.0;
                    seen.Add((pixel.r>>3)<<10|(pixel.g>>3)<<5|(pixel.b>>3));
                }
                int samples=(pixels.Length+36)/37;
                Debug.Log("[프레임] "+name+" · 평균밝기 "+(sum/samples).ToString("F1",CultureInfo.InvariantCulture)
                          +"/255 · 색 "+seen.Count+"종 · "+position.ToString("F1"));
                return 1;
            }
            catch(Exception error){Debug.LogError("[프레임] "+name+" 실패 · "+error.Message);return 0;}
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
