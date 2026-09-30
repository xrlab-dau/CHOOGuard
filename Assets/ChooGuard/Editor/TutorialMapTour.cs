using System;
using System.Collections.Generic;
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
    /// 튜토리얼 맵을 한 바퀴 돌며 화면을 남긴다. 씬을 저장하지 않는다 — 열고 찍기만 한다.
    /// </summary>
    /// <remarks>
    /// 왜 12대를 전부 찍는가: 생성기는 12대 모두 "설 자리 있음" 으로 검증했지만 사람이 걸어가 본 적이 없다.
    /// 벽에 박혔거나 어두워서 안 보이는 자리는 좌표로는 드러나지 않는다.
    /// </remarks>
    public static class TutorialMapTour
    {
        private const int Width=1600,Height=900;

        [MenuItem("ChooGuard/수직 슬라이스/튜토리얼 맵 한 바퀴")]
        public static void Run()
        {
            var root=Environment.GetEnvironmentVariable("CG_SHOT_DIR");
            if(string.IsNullOrEmpty(root)){Debug.LogError("[맵투어] CG_SHOT_DIR 을 주세요.");return;}
            Directory.CreateDirectory(root);

            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[맵투어] 씬을 열지 못했습니다.");return;}

            int index=0;
            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder!=null)
            {
                var eye=responder.transform.position+Vector3.up*1.6f;
                var forward=responder.transform.forward;
                Debug.Log("[맵투어] 플레이어 시작 "+responder.transform.position.ToString("F2"));
                Shot(root,ref index,"spawn-forward",eye,Quaternion.LookRotation(forward,Vector3.up),70);
                Shot(root,ref index,"spawn-right",eye,Quaternion.LookRotation(Quaternion.Euler(0,90,0)*forward,Vector3.up),70);
                Shot(root,ref index,"spawn-back",eye,Quaternion.LookRotation(-forward,Vector3.up),70);
                Shot(root,ref index,"spawn-left",eye,Quaternion.LookRotation(Quaternion.Euler(0,-90,0)*forward,Vector3.up),70);
            }
            else Debug.LogWarning("[맵투어] 플레이어를 찾지 못했습니다.");

            // 소화기 12대를 번호순으로. 각각 정면 1.8m, 눈높이에서 본다.
            var units=UnityEngine.Object.FindObjectsByType<FacilityInspectable>(FindObjectsSortMode.None)
                .OrderBy(f=>f.SerialNumber,StringComparer.Ordinal).ToList();
            Debug.Log("[맵투어] 소화기 "+units.Count+"대");
            foreach(var unit in units)
            {
                var body=unit.transform.position+Vector3.up*1.1f;
                var eye=body+unit.transform.forward*1.8f+Vector3.up*.5f;
                var name=(unit.SerialNumber??unit.name).Replace("BSN-CONC-","");
                Shot(root,ref index,"fe-"+name,eye,Quaternion.LookRotation((body-eye).normalized,Vector3.up),60,
                     "부식 "+unit.Corroded+" · 기한경과 "+unit.ExpiryPassed);
            }

            // 감사 단말. 절차의 마지막이 여기로 돌아온다.
            var terminal=UnityEngine.Object.FindFirstObjectByType<AuditTerminal>();
            if(terminal!=null)
            {
                var face=terminal.transform.position+Vector3.up*1.3f;
                var eye=face+terminal.transform.forward*2f+Vector3.up*.3f;
                Shot(root,ref index,"audit-terminal",eye,Quaternion.LookRotation((face-eye).normalized,Vector3.up),60);
            }
            else Debug.LogWarning("[맵투어] 감사 단말을 찾지 못했습니다.");

            // 대합실 전체를 위에서. 12대가 어떤 범위에 흩어져 있는지 한 장으로 본다.
            if(units.Count>0)
            {
                var bounds=new Bounds(units[0].transform.position,Vector3.zero);
                foreach(var unit in units)bounds.Encapsulate(unit.transform.position);
                var centre=bounds.center;
                Debug.Log("[맵투어] 소화기 분포 중심 "+centre.ToString("F1")+" · 크기 "+bounds.size.ToString("F1"));
                Shot(root,ref index,"overview-top",centre+Vector3.up*34f,
                     Quaternion.LookRotation(Vector3.down,Vector3.forward),60);
                var corner=centre+new Vector3(-26,20,-26);
                Shot(root,ref index,"overview-angle",corner,
                     Quaternion.LookRotation((centre-corner).normalized,Vector3.up),60);
            }

            Debug.Log("CG_TOUR_SHOTS taken="+index+" dir="+root);
        }

        /// <summary>
        /// 감사 단말이 통로를 막는 문제를 재기 위한 진단. 바꾸지 않는다.
        /// </summary>
        /// <remarks>
        /// 걸어서 순회했더니 시작 지점에서 3걸음 만에 이 단말에 막혔다(FE-001 방향).
        /// 옮기려면 먼저 무엇이 얼마나 튀어나와 있는지 알아야 한다 — 렌더러 크기와 콜라이더 크기는 다를 수 있다.
        /// </remarks>
        [MenuItem("ChooGuard/수직 슬라이스/감사 단말 진단")]
        public static void InspectTerminal()
        {
            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[단말진단] 씬을 열지 못했습니다.");return;}
            Physics.SyncTransforms();

            var terminal=UnityEngine.Object.FindFirstObjectByType<AuditTerminal>();
            if(terminal==null){Debug.LogError("[단말진단] 감사 단말이 없습니다.");return;}
            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();

            Debug.Log("[단말진단] 이름 "+terminal.name
                      +" · 위치 "+terminal.transform.position.ToString("F3")
                      +" · 회전 "+terminal.transform.rotation.eulerAngles.ToString("F1")
                      +" · 크기 "+terminal.transform.lossyScale.ToString("F2"));
            if(responder!=null)
                Debug.Log("[단말진단] 플레이어 "+responder.transform.position.ToString("F2")
                          +" · 단말까지 "+Vector3.Distance(responder.transform.position,terminal.transform.position).ToString("F2")+"m");

            foreach(var renderer in terminal.GetComponentsInChildren<Renderer>(true))
                Debug.Log("[단말진단] 렌더러 "+renderer.name+" · 중심 "+renderer.bounds.center.ToString("F2")
                          +" · 크기 "+renderer.bounds.size.ToString("F2"));
            foreach(var collider in terminal.GetComponentsInChildren<Collider>(true))
                Debug.Log("[단말진단] 콜라이더 "+collider.name+" · "+collider.GetType().Name
                          +" · 트리거 "+collider.isTrigger
                          +" · 중심 "+collider.bounds.center.ToString("F2")
                          +" · 크기 "+collider.bounds.size.ToString("F2"));

            // 막힌 지점에서 단말이 어느 쪽으로 튀어나왔는지. 걸었을 때 (20.79, 7.00, -39.58) 에서 걸렸다.
            var blockedAt=new Vector3(20.79f,7f,-39.58f);
            Debug.Log("[단말진단] 막힌 지점 "+blockedAt.ToString("F2")
                      +" · 단말까지 "+Vector3.Distance(blockedAt,terminal.transform.position).ToString("F2")+"m");

            // 단말 주변으로 벽까지 거리를 잰다. 어디로 붙일 수 있는지 보려는 것이다.
            //
            // 높이를 틀리면 허공을 잰다 — 처음에 단말 위치보다 1.2m 위에서 쏘아 8방향 중 6방향이
            // "8m 안에 없음" 으로 나왔고, 그것을 단말이 허공에 떠 있다는 증거로 읽을 뻔했다.
            // 단말 자신의 높이에서 쏜다.
            var eye=terminal.transform.position;
            var own=terminal.GetComponentsInChildren<Collider>(true);
            var was=new bool[own.Length];
            for(int i=0;i<own.Length;i++){was[i]=own[i].enabled;own[i].enabled=false;}
            Physics.SyncTransforms();
            try
            {
                for(int step=0;step<12;step++)
                {
                    float angle=step*Mathf.PI*2f/12f;
                    var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    string what=Physics.Raycast(eye,direction,out var hit,8f,~0,QueryTriggerInteraction.Ignore)
                        ? hit.collider.name+" "+hit.distance.ToString("F2")+"m" : "8m 안에 없음";
                    Debug.Log("[단말진단] 방향 "+(step*30)+"° · "+what);
                }
                // 단말 뒤에 벽이 있는가. 얇은 패널이므로 벽에 붙어 있어야 정상이다.
                var back=-terminal.transform.forward;
                Debug.Log("[단말진단] 뒤쪽("+back.ToString("F2")+") · "
                          +(Physics.Raycast(eye,back,out var behind,4f,~0,QueryTriggerInteraction.Ignore)
                            ?behind.collider.name+" "+behind.distance.ToString("F2")+"m"
                            :"4m 안에 아무것도 없음 — 허공에 떠 있다"));
                // 발밑에 무엇이 있는가. 기둥이나 카운터 위에 얹힌 것인지 본다.
                Debug.Log("[단말진단] 아래 · "
                          +(Physics.Raycast(eye,Vector3.down,out var below,4f,~0,QueryTriggerInteraction.Ignore)
                            ?below.collider.name+" "+below.distance.ToString("F2")+"m 아래 (y "+below.point.y.ToString("F2")+")"
                            :"4m 안에 바닥 없음"));
            }
            finally
            {
                for(int i=0;i<own.Length;i++)own[i].enabled=was[i];
                Physics.SyncTransforms();
            }
        }

        /// <summary>
        /// 허공에 떠 있는 감사 단말을 뒤쪽 벽에 붙인다. **씬을 저장한다.**
        /// </summary>
        /// <remarks>
        /// 진단 결과: 두께 7 cm 벽걸이 패널이 가장 가까운 벽에서 3.01 m 떨어진 허공, 바닥 위 1.32 m 에
        /// 떠 있었다. 플레이어 시작점에서 2.63 m 라 출발하자마자 통로를 막았다.
        ///
        /// 벽까지 거리를 그 자리에서 다시 재서 그만큼만 민다. 거리를 상수로 박으면 씬이 바뀔 때 조용히 틀린다.
        /// 저장 전에 세 가지를 확인하고 하나라도 어긋나면 되돌린다 — 벽에 닿았는가, 앞에 설 바닥이 있는가,
        /// 막히던 자리가 트였는가.
        /// </remarks>
        [MenuItem("ChooGuard/수직 슬라이스/감사 단말을 벽에 붙인다")]
        public static void FixTerminal()
        {
            const float Gap=.01f;
            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[단말이동] 씬을 열지 못했습니다.");return;}
            Physics.SyncTransforms();

            var terminal=UnityEngine.Object.FindFirstObjectByType<AuditTerminal>();
            if(terminal==null){Debug.LogError("[단말이동] 감사 단말이 없습니다.");return;}

            var before=terminal.transform.position;
            var back=-terminal.transform.forward;back.y=0;
            if(back.sqrMagnitude<.0001f){Debug.LogError("[단말이동] 패널이 수평을 향하지 않습니다.");return;}
            back.Normalize();

            // 자신의 콜라이더를 끄고 벽까지 잰다. 켜 두면 자기 자신에 맞는다.
            var own=terminal.GetComponentsInChildren<Collider>(true);
            var was=new bool[own.Length];
            for(int i=0;i<own.Length;i++){was[i]=own[i].enabled;own[i].enabled=false;}
            Physics.SyncTransforms();

            float halfDepth=.035f;
            foreach(var collider in terminal.GetComponentsInChildren<Collider>(true))
                halfDepth=Mathf.Max(.02f,Vector3.Scale(collider.bounds.size,new Vector3(Mathf.Abs(back.x),0,Mathf.Abs(back.z))).magnitude*.5f);

            bool ok;RaycastHit wall;
            try{ ok=Physics.Raycast(before,back,out wall,6f,~0,QueryTriggerInteraction.Ignore); }
            finally
            {
                for(int i=0;i<own.Length;i++)own[i].enabled=was[i];
                Physics.SyncTransforms();
            }
            if(!ok){Debug.LogError("[단말이동] 뒤쪽 6m 안에 벽이 없습니다. 붙일 곳을 모르므로 멈춥니다.");return;}

            float push=wall.distance-halfDepth-Gap;
            if(push<=.05f){Debug.Log("[단말이동] 이미 벽에 붙어 있습니다 ("+wall.distance.ToString("F2")+"m). 바꾸지 않습니다.");return;}
            var target=before+back*push;

            Undo.RecordObject(terminal.transform,"감사 단말 이동");
            terminal.transform.position=target;
            Physics.SyncTransforms();
            Debug.Log("[단말이동] 벽 "+wall.collider.name+" "+wall.distance.ToString("F2")+"m · 패널 반두께 "
                      +halfDepth.ToString("F3")+"m · 밀어낸 거리 "+push.ToString("F2")+"m");
            Debug.Log("[단말이동] 위치 "+before.ToString("F3")+" → "+target.ToString("F3"));

            // 확인 ① 벽에 닿았는가. 콜라이더를 다시 끄고 재야 자기 자신에 맞지 않는다.
            for(int i=0;i<own.Length;i++)own[i].enabled=false;
            Physics.SyncTransforms();
            bool touching=Physics.Raycast(target,back,out var check,1f,~0,QueryTriggerInteraction.Ignore)
                          &&check.distance<halfDepth+Gap+.03f;
            // 확인 ② 앞에 설 바닥이 있는가. 읽으려면 사람이 앞에 서야 한다.
            var front=target-back*1.1f;
            bool standable=Physics.Raycast(front+Vector3.up*.6f,Vector3.down,out var floor,3f,~0,QueryTriggerInteraction.Ignore)
                           &&Mathf.Abs(floor.point.y-(target.y-1.32f))<.4f;
            // 확인 ③ 막히던 자리가 트였는가. 걸었을 때 (20.79, 7.00, -39.58) 에서 걸렸다.
            var was_blocked=new Vector3(20.79f,7f,-39.58f);
            bool clear=!Physics.CheckCapsule(was_blocked+Vector3.up*.35f,was_blocked+Vector3.up*1.45f,.30f,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<own.Length;i++)own[i].enabled=was[i];
            Physics.SyncTransforms();

            Debug.Log("[단말이동] 확인 · 벽 접촉 "+touching+" · 앞에 설 바닥 "+standable+" · 막히던 자리 트임 "+clear);
            if(!touching||!standable||!clear)
            {
                terminal.transform.position=before;
                Physics.SyncTransforms();
                Debug.LogError("[단말이동] 확인에 실패해 되돌렸습니다. 저장하지 않습니다.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)){Debug.LogError("[단말이동] 저장하지 못했습니다.");return;}
            Debug.Log("CG_TERMINAL_MOVED from="+before.ToString("F3")+" to="+target.ToString("F3"));
        }

        /// <summary>
        /// 관측 지점 콜라이더가 실제로 사람을 막는지 잰다. 바꾸지 않는다.
        /// </summary>
        /// <remarks>
        /// 걸어서 순회했을 때 FE-003 이 `관측 · 본체 외관` 에 막혔는데, 그 검사는 CapsuleCast 를
        /// 모든 레이어(~0)에 걸었다. 실제 플레이어는 CharacterController 라 레이어 충돌 행렬을 따르므로
        /// **내 판정이 과했을 수 있다.** 레이어와 행렬을 먼저 확인한다.
        ///
        /// 트리거로 바꾸는 선택지는 이미 배제됐다 — 조준 레이캐스트가 QueryTriggerInteraction.Ignore 라
        /// 트리거가 되면 응시 판정이 통째로 깨진다(FirstPersonResponder.cs:114).
        /// </remarks>
        [MenuItem("ChooGuard/수직 슬라이스/관측 콜라이더 진단")]
        public static void InspectObservationColliders()
        {
            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[관측진단] 씬을 열지 못했습니다.");return;}
            Physics.SyncTransforms();

            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            int playerLayer=responder!=null?responder.gameObject.layer:0;
            Debug.Log("[관측진단] 플레이어 레이어 "+playerLayer+" ("+LayerMask.LayerToName(playerLayer)+")");
            var controller=responder!=null?responder.GetComponent<CharacterController>():null;
            Debug.Log("[관측진단] CharacterController "+(controller!=null
                ?"반지름 "+controller.radius.ToString("F2")+" 높이 "+controller.height.ToString("F2")
                :"없음"));

            var seen=new HashSet<string>();
            foreach(var unit in UnityEngine.Object.FindObjectsByType<FacilityInspectable>(FindObjectsSortMode.None)
                        .OrderBy(f=>f.SerialNumber,StringComparer.Ordinal))
            {
                foreach(var collider in unit.GetComponentsInChildren<Collider>(true))
                {
                    int layer=collider.gameObject.layer;
                    bool blocks=!Physics.GetIgnoreLayerCollision(playerLayer,layer)&&!collider.isTrigger;
                    string key=collider.name+"|"+layer+"|"+collider.isTrigger;
                    if(seen.Add(key))
                        Debug.Log("[관측진단] 형태 · "+collider.name+" · "+collider.GetType().Name
                                  +" · 레이어 "+layer+"("+LayerMask.LayerToName(layer)+")"
                                  +" · 트리거 "+collider.isTrigger
                                  +" · 사람을 막는가 "+blocks);
                    if(unit.SerialNumber!=null&&unit.SerialNumber.EndsWith("003",StringComparison.Ordinal))
                        Debug.Log("[관측진단] FE-003 · "+collider.name
                                  +" · 중심 "+collider.bounds.center.ToString("F2")
                                  +" · 크기 "+collider.bounds.size.ToString("F2")
                                  +" · 막는가 "+blocks);
                }
                if(unit.SerialNumber!=null&&unit.SerialNumber.EndsWith("003",StringComparison.Ordinal))
                    Debug.Log("[관측진단] FE-003 본체 "+unit.transform.position.ToString("F2")
                              +" · 정면 "+unit.transform.forward.ToString("F2"));
            }

            // 막힌 자리에서 CharacterController 와 같은 조건으로 다시 검사한다.
            var blockedAt=new Vector3(50.7f,7f,-62.9f);
            int mask=~0;
            for(int layer=0;layer<32;layer++)
                if(Physics.GetIgnoreLayerCollision(playerLayer,layer))mask&=~(1<<layer);
            bool hitAll=Physics.CheckCapsule(blockedAt+Vector3.up*.35f,blockedAt+Vector3.up*1.45f,.30f,~0,QueryTriggerInteraction.Ignore);
            bool hitReal=Physics.CheckCapsule(blockedAt+Vector3.up*.35f,blockedAt+Vector3.up*1.45f,.30f,mask,QueryTriggerInteraction.Ignore);
            Debug.Log("[관측진단] 막힌 자리 "+blockedAt.ToString("F1")
                      +" · 모든 레이어로 검사 "+hitAll
                      +" · 플레이어가 실제로 부딪히는 레이어로만 검사 "+hitReal);
        }

        /// <summary>
        /// 점검표 부착 지점을 소화기 몸통으로 올린다. **씬을 저장한다.**
        /// </summary>
        /// <remarks>
        /// 원인: 관측 지점은 Point() 에서 메시 경계 비율로 잡는데
        /// (worldY = bounds.min.y + bounds.size.y * 비율), 점검표 앵커만 루트에서 raw 미터로
        /// 0.26 m 에 놓였다. 소화기는 bounds.center 가 루트 위 1.10 m 에 오도록 올려져 있으므로
        /// 0.26 m 는 소화기 아랫단보다 한참 아래 — 발치 허공이다.
        ///
        /// 같은 규약으로 되돌린다. 비율 0.22 는 본체 관측(0.42)보다 아래, 제원표·고유번호(0.55)를
        /// 가리지 않는 위치다. 생성기 주석이 밝힌 원래 의도가 그것이다.
        /// </remarks>
        [MenuItem("ChooGuard/수직 슬라이스/점검표 부착 지점을 몸통으로 올린다")]
        public static void FixTagAnchors()
        {
            const float Fraction=.22f;
            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[점검표] 씬을 열지 못했습니다.");return;}

            int moved=0,skipped=0;
            foreach(var unit in UnityEngine.Object.FindObjectsByType<FacilityInspectable>(FindObjectsSortMode.None)
                        .OrderBy(f=>f.SerialNumber,StringComparer.Ordinal))
            {
                var anchor=unit.TagAnchor;
                if(anchor==null){Debug.LogWarning("[점검표] 앵커 없음 · "+unit.SerialNumber);skipped++;continue;}

                // 소화기 자신의 렌더러만 모은다. 앵커 아래에 붙은 점검표는 빼야 경계가 오염되지 않는다.
                bool any=false;var bounds=new Bounds(unit.transform.position,Vector3.zero);
                foreach(var renderer in unit.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer==null||renderer.transform.IsChildOf(anchor))continue;
                    if(!any){bounds=renderer.bounds;any=true;}else bounds.Encapsulate(renderer.bounds);
                }
                if(!any){Debug.LogWarning("[점검표] 렌더러 없음 · "+unit.SerialNumber);skipped++;continue;}

                float worldY=bounds.min.y+bounds.size.y*Fraction;
                float localY=worldY-unit.transform.position.y;
                var before=anchor.localPosition;
                if(Mathf.Abs(before.y-localY)<.005f){skipped++;continue;}

                Undo.RecordObject(anchor,"점검표 부착 지점 이동");
                anchor.localPosition=new Vector3(before.x,localY,before.z);
                moved++;
                Debug.Log("[점검표] "+unit.SerialNumber
                          +" · 메시 "+bounds.min.y.ToString("F2")+"~"+bounds.max.y.ToString("F2")
                          +" (높이 "+bounds.size.y.ToString("F2")+"m)"
                          +" · 로컬 y "+before.y.ToString("F3")+" → "+localY.ToString("F3")
                          +" · 월드 y "+(unit.transform.position.y+before.y).ToString("F2")+" → "+worldY.ToString("F2"));
            }

            // 확인: 앵커가 소화기 경계 안에 들어왔는가. 밖이면 여전히 허공이다.
            int outside=0;
            foreach(var unit in UnityEngine.Object.FindObjectsByType<FacilityInspectable>(FindObjectsSortMode.None))
            {
                var anchor=unit.TagAnchor;if(anchor==null)continue;
                bool any=false;var bounds=new Bounds(unit.transform.position,Vector3.zero);
                foreach(var renderer in unit.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer==null||renderer.transform.IsChildOf(anchor))continue;
                    if(!any){bounds=renderer.bounds;any=true;}else bounds.Encapsulate(renderer.bounds);
                }
                if(!any)continue;
                float y=anchor.position.y;
                if(y<bounds.min.y||y>bounds.max.y)
                {
                    outside++;
                    Debug.LogError("[점검표] 아직 경계 밖 · "+unit.SerialNumber+" · 앵커 y "+y.ToString("F2")
                                   +" · 메시 "+bounds.min.y.ToString("F2")+"~"+bounds.max.y.ToString("F2"));
                }
            }
            Debug.Log("[점검표] 옮김 "+moved+" · 건너뜀 "+skipped+" · 경계 밖 "+outside);
            if(outside>0){Debug.LogError("[점검표] 경계 밖이 남아 저장하지 않습니다.");return;}
            if(moved==0){Debug.Log("[점검표] 바꿀 것이 없어 저장하지 않습니다.");return;}

            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)){Debug.LogError("[점검표] 저장하지 못했습니다.");return;}
            Debug.Log("CG_TAG_ANCHORS moved="+moved);
        }

        /// <summary>
        /// 플레이어 눈에서 각도별로 무엇이 조준되는지 지도로 뽑는다. 바꾸지 않는다.
        /// </summary>
        /// <remarks>
        /// 실제 플레이에서 "고유번호를 확인하세요" 가 사라지지 않는다는 보고를 받았다.
        /// 조준은 가장 가까운 콜라이더 하나만 잡고 FpsGazeTracker 는 그 콜라이더에만 체류를 쌓으므로,
        /// 겨냥이 `관측 · 고유번호` 가 아니라 `관측 · 본체 외관` 에 붙으면 영원히 진행되지 않는다.
        ///
        /// 내 구동기는 tracker.Accumulate 로 응시를 주입해 이 경로를 통째로 건너뛰었다.
        /// 그래서 배치모드로는 9단계가 다 통과했는데 사람은 1단계에서 막힌다.
        /// </remarks>
        [MenuItem("ChooGuard/수직 슬라이스/조준 지도 (#튜토리얼 1단계)")]
        public static void AimMap()
        {
            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[조준지도] 씬을 열지 못했습니다.");return;}
            Physics.SyncTransforms();

            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null){Debug.LogError("[조준지도] 플레이어가 없습니다.");return;}
            var session=UnityEngine.Object.FindFirstObjectByType<TutorialSession>();
            var target=session!=null?session.Target:null;
            if(target==null){Debug.LogError("[조준지도] 대상 소화기가 없습니다.");return;}

            // 조준 조건을 responder 와 똑같이 맞춘다. 다르게 재면 아무 의미가 없다.
            float distance=Mathf.Clamp(responder.InteractionDistance,.25f,3f);
            int mask=responder.InteractionMask;
            Debug.Log("[조준지도] 상호작용 거리 "+distance.ToString("F2")+"m · 마스크 "+mask
                      +" · 대상 "+target.SerialNumber+" "+target.transform.position.ToString("F2"));
            foreach(var point in target.Points)
                if(point.Surface!=null)
                    Debug.Log("[조준지도] 관측 지점 "+point.Id+" · 중심 "+point.Surface.bounds.center.ToString("F2")
                              +" · 크기 "+point.Surface.bounds.size.ToString("F3")
                              +" · 체류 "+point.RequiredDwellSeconds.ToString("F1")+"초");

            // 플레이어를 소화기 정면 1.4m 눈높이에 세우고, 시야 좌우·상하 각도를 훑는다.
            var eye=target.transform.position+target.transform.forward*1.4f+Vector3.up*1.62f;
            var toTarget=(target.transform.position+Vector3.up*1.13f-eye).normalized;
            var counts=new Dictionary<string,int>();
            int total=0;
            var sb=new System.Text.StringBuilder();
            for(int py=8;py>=-8;py--)
            {
                sb.Append('\n');
                for(int px=-12;px<=12;px++)
                {
                    var direction=Quaternion.Euler(py*-2f,px*2f,0)*toTarget;
                    total++;
                    char mark='.';
                    if(Physics.Raycast(eye,direction,out var hit,distance,mask,QueryTriggerInteraction.Ignore))
                    {
                        var name=hit.collider.name;
                        counts.TryGetValue(name,out int had);counts[name]=had+1;
                        if(name.EndsWith("고유번호",StringComparison.Ordinal))mark='S';
                        else if(name.EndsWith("제원표",StringComparison.Ordinal))mark='P';
                        else if(name.EndsWith("지시압력계",StringComparison.Ordinal))mark='G';
                        else if(name.EndsWith("본체 외관",StringComparison.Ordinal))mark='B';
                        else mark='#';
                    }
                    sb.Append(mark);
                }
            }
            Debug.Log("[조준지도] 눈 "+eye.ToString("F2")+" · 좌우 ±24° 상하 ±16° · 2° 간격"
                      +"\nS=고유번호 P=제원표 G=지시압력계 B=본체 #=기타 .=아무것도 없음"+sb);
            foreach(var pair in counts.OrderByDescending(p=>p.Value))
                Debug.Log("[조준지도] "+pair.Key+" · "+pair.Value+"칸 / "+total
                          +" ("+(100f*pair.Value/total).ToString("F1")+"%)");
        }

        /// <summary>
        /// 콜라이더가 없어 통과되는 큰 면을 찾는다. 바꾸지 않는다.
        /// </summary>
        /// <remarks>
        /// "회색벽이 다 뚫린다" 는 보고를 받았다. 씬에는 MeshCollider 1,986개에 MeshRenderer 2,329개라
        /// 343개가 콜라이더 없이 그려지기만 한다. 그중 벽 크기인 것을 골라낸다.
        ///
        /// 내 보행 도구가 이것을 못 잡은 이유: 12대 순회 경로가 지나간 곳에 그 면이 없었다.
        /// 지나간 길만 검사하면 지나지 않은 벽은 영원히 확인되지 않는다.
        /// </remarks>
        [MenuItem("ChooGuard/수직 슬라이스/콜라이더 없는 큰 면 찾기")]
        public static void FindMissingColliders()
        {
            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[콜라이더] 씬을 열지 못했습니다.");return;}

            // 사람 키 대역에 걸치고 벽만 한 면만 본다. 바닥 타일·간판까지 세면 목록이 쓸모없어진다.
            const float MinWidth=2f,MinHeight=1.5f;
            const float LowY=6.5f,HighY=12f;   // 2층 대합실이 사는 높이대
            var found=new List<string>();
            int checkedCount=0,noCollider=0;
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if(renderer==null)continue;
                checkedCount++;
                var bounds=renderer.bounds;
                if(bounds.max.y<LowY||bounds.min.y>HighY)continue;
                float horizontal=Mathf.Max(bounds.size.x,bounds.size.z);
                if(horizontal<MinWidth||bounds.size.y<MinHeight)continue;
                // 부모에 콜라이더가 있다는 이유로 제외하면 안 된다 — 작은 콜라이더 하나 때문에
                // 거대한 벽 렌더러가 통째로 빠진다. 실제로 그렇게 1개만 나왔다(2026-09-28).
                // 대신 그 면을 직접 찔러 본다: 얇은 축 방향으로 밖에서 쏘아 자기 자신이 맞는지 본다.
                var extents=bounds.extents;
                var thin=extents.x<extents.z?Vector3.right:Vector3.forward;
                float back=Mathf.Min(extents.x,extents.z)+.6f;
                bool solid=false;
                foreach(var sign in new[]{1f,-1f})
                {
                    var from=bounds.center+thin*(back*sign);
                    if(!Physics.Raycast(from,-thin*sign,out var probe,back*2f,~0,QueryTriggerInteraction.Ignore))continue;
                    // 자기 자신이거나 같은 오브젝트에 달린 콜라이더면 막히는 것이다.
                    if(probe.collider.gameObject==renderer.gameObject
                       ||probe.collider.transform.IsChildOf(renderer.transform)
                       ||renderer.transform.IsChildOf(probe.collider.transform)){solid=true;break;}
                }
                if(solid)continue;
                noCollider++;
                found.Add(renderer.name+" · 중심 "+bounds.center.ToString("F1")
                          +" · 크기 "+bounds.size.ToString("F1"));
            }
            Debug.Log("[콜라이더] 렌더러 "+checkedCount+"개 검사 · 사람 키 대역의 벽 크기 면 중 "
                      +"콜라이더 없는 것 "+noCollider+"개");
            foreach(var line in found.Take(40))Debug.Log("[콜라이더] 뚫림 · "+line);
            if(found.Count>40)Debug.Log("[콜라이더] … 외 "+(found.Count-40)+"개");

            // 시작 지점 주변을 실제로 찔러 본다. 목록에 없어도 통과되면 다른 원인이다.
            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null)return;
            var eye=responder.transform.position+Vector3.up*1.2f;
            Debug.Log("[콜라이더] 시작 지점 "+responder.transform.position.ToString("F2")+" 에서 사방 30m");
            for(int step=0;step<12;step++)
            {
                float angle=step*Mathf.PI*2f/12f;
                var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                // 렌더러에는 맞는데 콜라이더에는 안 맞는 방향을 찾는다 — 그것이 '보이는데 통과되는' 벽이다.
                bool solid=Physics.Raycast(eye,direction,out var hit,30f,~0,QueryTriggerInteraction.Ignore);
                Debug.Log("[콜라이더] "+(step*30)+"° · "
                          +(solid?"막힘 "+hit.collider.name+" "+hit.distance.ToString("F1")+"m":"30m 안에 막는 것 없음"));
            }
        }

        /// <summary>
        /// 시작 지점에서 사방으로 걸어 나가며 어디까지 가는지 잰다. 대합실을 벗어나면 그 방향이 뚫린 것이다.
        /// </summary>
        /// <remarks>
        /// 렌더러를 훑는 대량 스캔을 두 번 했는데 둘 다 틀렸다. 처음에는 부모 콜라이더가 있다고 제외해
        /// 1개만 나왔고, 두 번째는 속 빈 셸의 경계 중심이 건물 안 허공이라 표면을 못 맞혔다 —
        /// 12방향 광선에서 막혔던 `부산역_5` 가 "뚫림" 으로 나오는 모순이 그 증거다.
        ///
        /// 그래서 플레이어와 같은 물리로 직접 걷는다. 캡슐과 바닥 검사는 감사 단말과 소화기 본체를
        /// 정확히 잡아낸 경로이므로 믿을 수 있다. 회피는 넣지 않는다 — 여기서는 '직선으로 벽을 통과하는가' 가 질문이다.
        /// </remarks>
        [MenuItem("ChooGuard/수직 슬라이스/시작 지점에서 사방으로 걸어 나가기")]
        public static void EscapeTest()
        {
            const float Step=.45f,Radius=.28f,Height=1.72f,MaxDistance=70f;
            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[탈출검사] 씬을 열지 못했습니다.");return;}
            Physics.SyncTransforms();

            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null){Debug.LogError("[탈출검사] 플레이어가 없습니다.");return;}
            var own=responder.GetComponentsInChildren<Collider>(true);
            var was=new bool[own.Length];
            for(int i=0;i<own.Length;i++){was[i]=own[i].enabled;own[i].enabled=false;}
            Physics.SyncTransforms();
            var start=responder.transform.position;
            Debug.Log("[탈출검사] 출발 "+start.ToString("F2")+" · 36방향 · 최대 "+MaxDistance+"m");
            int escaped=0;
            try
            {
                for(int step=0;step<36;step++)
                {
                    float angle=step*Mathf.PI*2f/36f;
                    var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    var position=start;
                    string stopped=null;float travelled=0;
                    while(travelled<MaxDistance)
                    {
                        var bottom=position+Vector3.up*(Radius+.05f);
                        var top=position+Vector3.up*(Height-Radius);
                        if(Physics.CapsuleCast(bottom,top,Radius,direction,out var hit,Step+.05f,~0,QueryTriggerInteraction.Ignore))
                        {stopped="막힘 "+hit.collider.name;break;}
                        var next=position+direction*Step;
                        if(!Physics.Raycast(next+Vector3.up*1.2f,Vector3.down,out var floor,3.5f,~0,QueryTriggerInteraction.Ignore))
                        {stopped="바닥 끊김";break;}
                        // 2층 바닥(y≈7.0)을 처음 벗어나는 지점이 곧 가장자리다. 큰 낙차만 보면 놓친다 —
                        // 실제로는 경사면을 따라 한 걸음에 0.35m 미만씩 내려가 '떨어짐' 으로 잡히지 않았다.
                        if(position.y>6.6f&&floor.point.y<=6.6f)
                            Debug.Log("[탈출검사] "+(step*10)+"° · **가장자리** "+position.ToString("F2")
                                      +" → y "+floor.point.y.ToString("F2")+" · 바닥 "+floor.collider.name);
                        if(Mathf.Abs(floor.point.y-position.y)>.35f)
                            Debug.Log("[탈출검사] "+(step*10)+"° · 층 바뀜 "+position.y.ToString("F2")
                                      +" → "+floor.point.y.ToString("F2")
                                      +" · 지점 "+position.ToString("F2")
                                      +" · 바닥 "+floor.collider.name);
                        next.y=floor.point.y;position=next;travelled+=Step;
                    }
                    bool far=travelled>=MaxDistance;
                    if(far)escaped++;
                    Debug.Log("[탈출검사] "+(step*10)+"° · "+travelled.ToString("F1")+"m"
                              +" · "+(stopped??"끝까지 감 — 막는 것 없음")
                              +" · 끝 "+position.ToString("F1")
                              +(far?"  ← 대합실 밖까지 나갔다":""));
                }
            }
            finally
            {
                for(int i=0;i<own.Length;i++)own[i].enabled=was[i];
                Physics.SyncTransforms();
            }
            Debug.Log("CG_ESCAPE far="+escaped+"/36");
        }

        /// <summary>
        /// 대합실이 열려 있는 구간에 보이지 않는 차단막을 세운다. **씬을 저장한다.**
        /// </summary>
        /// <remarks>
        /// 벽이 뚫린 것이 아니라 가장자리가 열려 있었다. 거기서 역사 셸 바깥면으로 올라타면
        /// 경사(slopeLimit 45° 안)를 따라 지면까지 걸어 내려간다 — 한 걸음 낙차가 0.35 m 미만이라
        /// 떨어짐으로도 잡히지 않는다. 밖으로 나가면 사방이 셸 뒷면이라 "회색벽이 다 뚫린다" 로 보인다.
        ///
        /// 이것은 **임시 차단막**이다. 실제 역사라면 그 자리에 난간이나 유리벽이 있어야 하고
        /// 그것은 모델링 범위다. 여기서는 튜토리얼이 2층을 벗어나지 않게만 막는다.
        ///
        /// 틈의 폭은 재서 정한다. 넉넉히 세워 놓고 "막혔으니 됐다" 고 하면 정상 동선까지 막을 수 있어,
        /// 세운 뒤 12대 순회를 다시 돌려 그대로 12/12 인지 확인해야 한다.
        /// </remarks>
        [MenuItem("ChooGuard/수직 슬라이스/대합실 열린 구간 막기")]
        public static void FixConcourseEdge()
        {
            const string BarrierName="튜토리얼 경계 차단";
            const float Step=.45f,Radius=.28f,Height=1.72f,FloorY=6.6f;
            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[경계차단] 씬을 열지 못했습니다.");return;}
            Physics.SyncTransforms();

            foreach(var existing in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if(existing!=null&&existing.name==BarrierName)
                {Debug.Log("[경계차단] 이미 있습니다 · "+existing.position.ToString("F2")+". 바꾸지 않습니다.");return;}

            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null){Debug.LogError("[경계차단] 플레이어가 없습니다.");return;}
            var own=responder.GetComponentsInChildren<Collider>(true);
            var was=new bool[own.Length];
            for(int i=0;i<own.Length;i++){was[i]=own[i].enabled;own[i].enabled=false;}
            Physics.SyncTransforms();

            // 열린 방향을 1° 간격으로 훑어 가장자리 점을 모은다. 폭을 눈대중으로 잡지 않는다.
            var edges=new List<Vector3>();
            var start=responder.transform.position;
            try
            {
                for(int degrees=40;degrees<=100;degrees++)
                {
                    float angle=degrees*Mathf.Deg2Rad;
                    var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    var position=start;
                    for(float travelled=0;travelled<25f;travelled+=Step)
                    {
                        var bottom=position+Vector3.up*(Radius+.05f);
                        var top=position+Vector3.up*(Height-Radius);
                        if(Physics.CapsuleCast(bottom,top,Radius,direction,out _,Step+.05f,~0,QueryTriggerInteraction.Ignore))break;
                        var next=position+direction*Step;
                        if(!Physics.Raycast(next+Vector3.up*1.2f,Vector3.down,out var floor,3.5f,~0,QueryTriggerInteraction.Ignore))break;
                        if(floor.point.y<=FloorY){edges.Add(position);break;}
                        next.y=floor.point.y;position=next;
                    }
                }
            }
            finally
            {
                for(int i=0;i<own.Length;i++)own[i].enabled=was[i];
                Physics.SyncTransforms();
            }
            if(edges.Count==0){Debug.Log("[경계차단] 열린 구간을 찾지 못했습니다. 세우지 않습니다.");return;}

            var bounds=new Bounds(edges[0],Vector3.zero);
            foreach(var point in edges)bounds.Encapsulate(point);
            Debug.Log("[경계차단] 가장자리 표본 "+edges.Count+"개 · 범위 "+bounds.min.ToString("F2")
                      +" ~ "+bounds.max.ToString("F2")+" · 폭 "+bounds.size.ToString("F2"));

            // 표본을 감싸는 얇은 판. 양옆으로 1m 씩 여유를 준다 — 표본 사이로 빠져나가지 않게.
            var centre=bounds.center;centre.y=start.y+1.4f;
            float spanX=bounds.size.x+2f,spanZ=bounds.size.z+2f;
            var barrier=new GameObject(BarrierName);
            Undo.RegisterCreatedObjectUndo(barrier,"경계 차단 생성");
            barrier.transform.position=centre;
            var box=barrier.AddComponent<BoxCollider>();
            box.size=new Vector3(Mathf.Max(spanX,.4f),3.2f,Mathf.Max(spanZ,.4f));
            Physics.SyncTransforms();
            Debug.Log("[경계차단] 세움 · 중심 "+centre.ToString("F2")+" · 크기 "+box.size.ToString("F2"));

            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)){Debug.LogError("[경계차단] 저장하지 못했습니다.");return;}
            Debug.Log("CG_EDGE_BARRIER centre="+centre.ToString("F2")+" size="+box.size.ToString("F2"));
        }

        /// <summary>
        /// 승객 NPC 에 몸통 콜라이더를 붙여 플레이어가 통과하지 못하게 한다. **씬을 저장한다.**
        /// </summary>
        /// <remarks>
        /// 승객은 transform.position 을 직접 옮겨 움직이고 콜라이더가 없었다 — 첫 조각에서 이동만
        /// 검증하고 충돌을 미뤘고, 체크리스트에 "의도한 것" 으로 적어 두었다. 직접 플레이해 보니
        /// 사람이 통과하는 승객은 "저기 사람이 있다" 는 감각을 깨뜨린다는 보고를 받았다.
        ///
        /// Rigidbody 를 함께 붙이는 이유: 콜라이더만 두고 transform 으로 옮기면 Unity 가 정적 콜라이더가
        /// 움직인 것으로 보고 충돌 정보를 매번 다시 만든다. isKinematic 으로 두면 '움직이는 콜라이더' 로 다뤄진다.
        ///
        /// 한계: 플레이어는 승객을 통과하지 못하지만, 승객은 여전히 플레이어 쪽으로 걸어와 겹칠 수 있다.
        /// 승객이 사람을 피하는 처리는 이 수정에 없다.
        /// </remarks>
        [MenuItem("ChooGuard/수직 슬라이스/승객에 몸통 콜라이더 붙이기")]
        public static void FixPassengerColliders()
        {
            var scene=EditorSceneManager.OpenScene("Assets/ChooGuard/Scenes/FpsStation.unity",OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[승객충돌] 씬을 열지 못했습니다.");return;}
            Physics.SyncTransforms();

            int added=0,already=0;
            foreach(var passenger in UnityEngine.Object.FindObjectsByType<ChooGuard.App.Fps.World.PassengerAgent>(FindObjectsSortMode.None))
            {
                if(passenger==null)continue;
                // 이미 있으면 발밑이 바닥에 닿았는지만 본다. 붙였다는 사실은 위치가 맞다는 뜻이 아니다 —
                // 처음에 원점을 발밑으로 가정해 center 를 키의 절반만큼 올렸는데 원점이 몸 중심이라
                // 콜라이더가 바닥 위 0.85m 에 떠 있었다(2026-09-28).
                var existing=passenger.GetComponentInChildren<CapsuleCollider>(true);
                if(existing!=null)
                {
                    float floorY=FloorUnder(passenger.transform.position,passenger.transform);
                    float gap=existing.bounds.min.y-floorY;
                    if(Mathf.Abs(gap)<=.08f)
                    {
                        already++;
                        Debug.Log("[승객충돌] 이미 제자리 · "+passenger.name
                                  +" · 콜라이더 "+existing.bounds.min.y.ToString("F2")+"~"+existing.bounds.max.y.ToString("F2")
                                  +" · 바닥 "+floorY.ToString("F2"));
                        continue;
                    }
                    Undo.RecordObject(existing,"승객 콜라이더 높이 보정");
                    float before=existing.center.y;
                    existing.center=new Vector3(existing.center.x,
                        existing.center.y-gap,existing.center.z);
                    Physics.SyncTransforms();
                    added++;
                    Debug.Log("[승객충돌] 높이 보정 · "+passenger.name
                              +" · center.y "+before.ToString("F3")+" → "+existing.center.y.ToString("F3")
                              +" · 콜라이더 "+existing.bounds.min.y.ToString("F2")+"~"+existing.bounds.max.y.ToString("F2")
                              +" · 바닥 "+floorY.ToString("F2"));
                    continue;
                }
                if(passenger.GetComponentInChildren<Collider>(true)!=null)
                {
                    already++;
                    Debug.Log("[승객충돌] 캡슐이 아닌 콜라이더가 있어 건너뜁니다 · "+passenger.name);
                    continue;
                }
                // 크기는 렌더러 실측에서 잡는다. 하드코딩하면 모델이 바뀔 때 조용히 어긋난다.
                bool any=false;var bounds=new Bounds(passenger.transform.position,Vector3.zero);
                foreach(var renderer in passenger.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer==null)continue;
                    if(!any){bounds=renderer.bounds;any=true;}else bounds.Encapsulate(renderer.bounds);
                }
                float height=any?Mathf.Clamp(bounds.size.y,1.2f,2.1f):1.7f;
                float radius=any?Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z)*.5f,.18f,.35f):.24f;

                Undo.RecordObject(passenger.gameObject,"승객 콜라이더 추가");
                var capsule=Undo.AddComponent<CapsuleCollider>(passenger.gameObject);
                capsule.height=height;capsule.radius=radius;capsule.direction=1;   // Y 축
                // 발이 바닥에 닿는 기준이므로 중심을 키의 절반 위로 올린다.
                capsule.center=new Vector3(0,height*.5f,0);
                var body=Undo.AddComponent<Rigidbody>(passenger.gameObject);
                body.isKinematic=true;body.useGravity=false;
                added++;
                Debug.Log("[승객충돌] 붙임 · "+passenger.name+" · 키 "+height.ToString("F2")
                          +"m · 반지름 "+radius.ToString("F2")+"m · 위치 "+passenger.transform.position.ToString("F2"));
            }
            Physics.SyncTransforms();
            Debug.Log("[승객충돌] 추가 "+added+" · 이미 있던 것 "+already);
            if(added==0){Debug.Log("[승객충돌] 바꿀 것이 없어 저장하지 않습니다.");return;}

            // 확인: **바닥에 선 사람**이 승객 자리에서 막히는가.
            // 앞서 승객 원점 기준으로 검사했는데, 그 범위가 내가 콜라이더를 올려 둔 자리와 같아
            // "거기 뒀으니 거기서 막힌다" 는 순환 논리였다. 바닥을 기준으로 잡아야 독립적인 검사다.
            int blocking=0,checkedCount=0;
            foreach(var passenger in UnityEngine.Object.FindObjectsByType<ChooGuard.App.Fps.World.PassengerAgent>(FindObjectsSortMode.None))
            {
                if(passenger==null)continue;
                checkedCount++;
                var at=passenger.transform.position;
                float floorY=FloorUnder(at,passenger.transform);
                var feet=new Vector3(at.x,floorY,at.z);
                if(Physics.CheckCapsule(feet+Vector3.up*.33f,feet+Vector3.up*1.44f,.28f,~0,QueryTriggerInteraction.Ignore))blocking++;
                else Debug.LogError("[승객충돌] 바닥에 선 사람을 막지 못합니다 · "+passenger.name+" 발밑 "+feet.ToString("F2"));
            }
            Debug.Log("[승객충돌] 확인 · "+blocking+"/"+checkedCount+" 이 사람 캡슐을 막는다");
            if(blocking<checkedCount){Debug.LogError("[승객충돌] 막지 않는 승객이 있어 저장하지 않습니다.");return;}

            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)){Debug.LogError("[승객충돌] 저장하지 못했습니다.");return;}
            Debug.Log("CG_PASSENGER_COLLIDERS added="+added);
        }

        /// <summary>발밑 바닥 높이. 못 찾으면 그 자리의 높이를 그대로 돌려준다.</summary>
        /// <remarks>
        /// owner 의 콜라이더는 빼고 잰다. 빼지 않으면 방금 붙인 승객 캡슐의 윗면을 바닥으로 읽는다 —
        /// 실제로 그렇게 "발밑 8.57" 이 나와 멀쩡한 보정을 실패로 판정했다(2026-09-28).
        /// </remarks>
        private static float FloorUnder(Vector3 at,Transform owner=null)
        {
            Collider[] own=owner!=null?owner.GetComponentsInChildren<Collider>(true):System.Array.Empty<Collider>();
            var was=new bool[own.Length];
            for(int i=0;i<own.Length;i++){was[i]=own[i].enabled;own[i].enabled=false;}
            if(own.Length>0)Physics.SyncTransforms();
            try
            {
                return Physics.Raycast(at+Vector3.up*1.2f,Vector3.down,out var hit,6f,~0,QueryTriggerInteraction.Ignore)
                    ?hit.point.y:at.y;
            }
            finally
            {
                for(int i=0;i<own.Length;i++)own[i].enabled=was[i];
                if(own.Length>0)Physics.SyncTransforms();
            }
        }

        private static void Shot(string root,ref int index,string name,Vector3 position,Quaternion rotation,float fov,string note=null)
        {
            var label=string.Format(CultureInfo.InvariantCulture,"tour-{0:00}-{1}",index++,name);
            var path=Path.Combine(root,"shot-"+label+".png");
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
                camera.fieldOfView=fov;camera.nearClipPlane=.05f;camera.farClipPlane=600;
                camera.transform.SetPositionAndRotation(position,rotation);
                camera.targetTexture=target;
                camera.Render();
                RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,Width,Height),0,0);
                texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());

                // 까만 화면을 '찍었다' 고 말하지 않기 위해 밝기와 색 수를 남긴다.
                var pixels=texture.GetPixels32();
                double sum=0;var seen=new HashSet<int>();
                for(int i=0;i<pixels.Length;i+=37)
                {
                    var pixel=pixels[i];
                    sum+=(pixel.r+pixel.g+pixel.b)/3.0;
                    seen.Add((pixel.r>>3)<<10|(pixel.g>>3)<<5|(pixel.b>>3));
                }
                int samples=(pixels.Length+36)/37;
                Debug.Log("[맵투어] "+label+" · 밝기 "+(sum/samples).ToString("F1",CultureInfo.InvariantCulture)
                          +" · 색 "+seen.Count+"종 · "+position.ToString("F1")
                          +(note==null?"":" · "+note));
            }
            catch(Exception error){Debug.LogError("[맵투어] "+label+" 실패 · "+error.Message);}
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
