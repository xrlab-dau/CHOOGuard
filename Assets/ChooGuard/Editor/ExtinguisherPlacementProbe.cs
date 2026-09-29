using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
// 씬 경로·navmesh 경로·맞이방 기준점은 ChooGuard.Editor 의 빌더가 이미 선언한다. 여기서 새로 정하지 않는다.
using ChooGuard.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
namespace ChooGuard.EditorTools
{
    /// <summary>
    /// 씬에 구워진 소화기 12대가 실제로 바닥 위에 있고 맞이방에서 걸어서 닿는지 측정한다(#242).
    /// 아무것도 바꾸지 않는다 — 열고 재고 JSON 을 쓴다.
    /// </summary>
    /// <remarks>
    /// 왜 씬을 읽는가: FireExtinguisherSliceBuilder 에는 단일 배치(FE-003)만 있고 12대 좌표가 없다.
    /// 좌표의 정본은 FpsStation.unity 다.
    ///
    /// 왜 navmesh 를 직접 올리는가: 씬의 m_NavMeshData 는 0 이고 보행 영역은 실행 중
    /// StationWorld 가 NavMesh.AddNavMeshData 로 올린다. 올리지 않고 재면 12대 전부 도달 불가로 나온다.
    ///
    /// 기준점·navmesh 경로는 StationNavigationBuilder 의 것을 그대로 쓴다. 여기서 새로 정하면
    /// 같은 세계를 두 기준으로 재게 된다.
    /// </remarks>
    public static class ExtinguisherPlacementProbe
    {
        private const string OutPath=".planning/2026-09-28-extinguisher-replacement/measure-a.json";
        private const string UnitPrefix="소화기 · ";

        // #242 의 PM 측정과 대조하려면 같은 값을 써야 한다. 설 자리 반경 1.6m, 바닥 탐색 6m.
        private const float StandRadius=1.6f;
        private const float FloorDepth=6f;

        [MenuItem("ChooGuard/Emergency/소화기 배치 측정 (#242)")]
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene(EmergencySceneBuilder.StationScenePath,OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[소화기측정] 씬을 열지 못했습니다 · "+EmergencySceneBuilder.StationScenePath);return;}

            var data=AssetDatabase.LoadAssetAtPath<NavMeshData>(StationNavigationBuilder.NavMeshPath);
            if(data==null)
            {
                Debug.LogError("[소화기측정] navmesh 자산이 없습니다 · "+StationNavigationBuilder.NavMeshPath
                               +" · 없이 재면 12대 전부 도달 불가로 나오므로 멈춥니다.");
                return;
            }
            var instance=NavMesh.AddNavMeshData(data);
            if(!instance.valid){Debug.LogError("[소화기측정] navmesh 를 올리지 못했습니다.");return;}

            var report=new StringBuilder();
            report.Append("{\n  \"schema\": \"chooguard.extinguisher-probe.v1\",\n");
            report.Append("  \"measuredAt\": \"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ",CultureInfo.InvariantCulture)).Append("\",\n");
            report.Append("  \"scene\": \"").Append(EmergencySceneBuilder.StationScenePath).Append("\",\n");
            report.Append("  \"navmesh\": \"").Append(StationNavigationBuilder.NavMeshPath).Append("\",\n");
            report.Append("  \"standRadius\": ").Append(F(StandRadius)).Append(", \"floorDepth\": ").Append(F(FloorDepth)).Append(",\n");
            try
            {
                var references=Concourse();
                report.Append("  \"references\": [");
                for(int i=0;i<references.Count;i++)
                {
                    if(i>0)report.Append(", ");
                    report.Append("{\"id\": \"").Append(references[i].Key).Append("\", \"position\": ").Append(V(references[i].Value)).Append('}');
                }
                report.Append("],\n");
                if(references.Count==0)
                {
                    Debug.LogError("[소화기측정] 맞이방 기준점이 보행 영역에 없습니다. 측정을 멈춥니다 — "
                                   +"기준점이 틀리면 12대 판정이 전부 뒤집힙니다.");
                    return;
                }

                var units=Units(scene);
                if(units.Count==0){Debug.LogError("[소화기측정] '"+UnitPrefix+"' 로 시작하는 유닛이 없습니다.");return;}
                Debug.Log("[소화기측정] 유닛 "+units.Count+"개 · 기준점 "+references.Count+"개");

                // 판정마다 따로 센다. 앞서 else 하나로 묶었다가 PARTIAL_PATH 가
                // noStandingSpot 으로 집계되어 총계가 틀렸다(2026-09-28).
                int reachable=0,noFloor=0,island=0,noStand=0,partialPath=0;
                report.Append("  \"units\": [\n");
                for(int i=0;i<units.Count;i++)
                {
                    var line=Measure(units[i],references,out var verdict);
                    if(verdict=="REACHABLE")reachable++;
                    else if(verdict=="NO_FLOOR")noFloor++;
                    else if(verdict=="ISLAND")island++;
                    else if(verdict=="PARTIAL_PATH")partialPath++;
                    else if(verdict=="NO_STANDING_SPOT")noStand++;
                    else Debug.LogError("[소화기측정] 집계하지 않은 판정 · "+verdict);
                    report.Append("    ").Append(line).Append(i<units.Count-1?",\n":"\n");
                }
                report.Append("  ],\n  \"totals\": {\"units\": ").Append(units.Count)
                      .Append(", \"reachable\": ").Append(reachable)
                      .Append(", \"noFloor\": ").Append(noFloor)
                      .Append(", \"island\": ").Append(island)
                      .Append(", \"partialPath\": ").Append(partialPath)
                      .Append(", \"noStandingSpot\": ").Append(noStand).Append("}\n}\n");

                Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
                File.WriteAllText(OutPath,report.ToString(),new UTF8Encoding(false));
                Debug.Log("CG_FE_PROBE units="+units.Count+" reachable="+reachable+" noFloor="+noFloor
                          +" island="+island+" partial="+partialPath+" noStand="+noStand+" out="+OutPath);
            }
            finally
            {
                // 올린 navmesh 는 반드시 내린다. 남겨 두면 다음 에디터 동작이 이 데이터를 본다.
                if(instance.valid)instance.Remove();
            }
        }

        /// <summary>
        /// 12대의 설 자리와 기준점들을 서로 오갈 수 있는지로 묶어 연결 성분을 보고한다.
        /// </summary>
        /// <remarks>
        /// 왜 필요한가: 2026-09-28 측정에서 PM 의 7/5 분할이 재현되지 않았다(내 결과는 2/9).
        /// 어긋나는 것이 정확히 다섯 개여서, 내 기준점이 PM 의 것과 다른 성분에 있을 가능성이 크다.
        /// 성분 구조를 보면 어느 쪽이 실제 맞이방인지 판단할 수 있다. 판정을 고치기 전에 이것을 먼저 본다.
        /// </remarks>
        [MenuItem("ChooGuard/Emergency/소화기 보행 성분 진단 (#242)")]
        public static void Components()
        {
            var scene=EditorSceneManager.OpenScene(EmergencySceneBuilder.StationScenePath,OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[성분진단] 씬을 열지 못했습니다.");return;}
            var data=AssetDatabase.LoadAssetAtPath<NavMeshData>(StationNavigationBuilder.NavMeshPath);
            if(data==null){Debug.LogError("[성분진단] navmesh 자산이 없습니다.");return;}
            var instance=NavMesh.AddNavMeshData(data);
            if(!instance.valid){Debug.LogError("[성분진단] navmesh 를 올리지 못했습니다.");return;}
            try
            {
                var names=new List<string>();
                var spots=new List<Vector3>();
                // 기준점 후보를 넓게 본다. HallCentre 와 그 주변, 그리고 각 zone 중심을 함께 넣어
                // 어느 점이 큰 성분에 드는지 비교한다.
                foreach(var probe in Probes())
                    if(NavMesh.SamplePosition(probe.Value,out var hit,4f,NavMesh.AllAreas))
                    {names.Add(probe.Key);spots.Add(hit.position);}
                    else Debug.Log("[성분진단] 보행 영역 밖 · "+probe.Key+" "+probe.Value.ToString("F1"));

                foreach(var unit in Units(scene))
                {
                    var body=Body(unit);
                    var origin=Physics.Raycast(body,Vector3.down,out var floor,FloorDepth,~0,QueryTriggerInteraction.Ignore)
                        ? new Vector3(body.x,floor.point.y,body.z) : body;
                    if(NavMesh.SamplePosition(origin,out var hit,StandRadius,NavMesh.AllAreas))
                    {names.Add(unit.name.Substring(UnitPrefix.Length));spots.Add(hit.position);}
                    else Debug.Log("[성분진단] 설 자리 없음 · "+unit.name);
                }

                // 유니온-파인드. 양방향 어느 쪽이든 완전한 경로가 있으면 같은 성분으로 본다.
                var parent=new int[spots.Count];
                for(int i=0;i<parent.Length;i++)parent[i]=i;
                int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
                var path=new NavMeshPath();
                for(int i=0;i<spots.Count;i++)
                    for(int j=i+1;j<spots.Count;j++)
                    {
                        if(Find(i)==Find(j))continue;
                        bool linked=NavMesh.CalculatePath(spots[i],spots[j],NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                        if(!linked)linked=NavMesh.CalculatePath(spots[j],spots[i],NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                        if(linked)parent[Find(i)]=Find(j);
                    }

                var groups=new Dictionary<int,List<string>>();
                for(int i=0;i<spots.Count;i++)
                {
                    int root=Find(i);
                    if(!groups.TryGetValue(root,out var list)){list=new List<string>();groups[root]=list;}
                    list.Add(names[i]+" "+spots[i].ToString("F1"));
                }
                Debug.Log("CG_FE_COMPONENTS groups="+groups.Count+" nodes="+spots.Count);
                int index=0;
                foreach(var group in groups.Values)
                    Debug.Log("[성분진단] 성분 "+(++index)+" · "+group.Count+"개 · "+string.Join(" | ",group));
            }
            finally { if(instance.valid)instance.Remove(); }
        }

        /// <summary>
        /// 유닛 하나를 깊게 들여다본다. 아래·위에 무엇이 있는지, 주변 어디에 진짜 바닥과 설 자리가
        /// 있는지, 그 자리가 벽에 붙어 있는지 — 옮길 후보를 고르기 위한 재료다(#242, FE-005).
        /// </summary>
        /// <remarks>
        /// 바꾸지 않는다. 후보를 늘어놓기만 한다. 어디로 옮길지는 사람이 본 뒤에 정한다.
        /// </remarks>
        [MenuItem("ChooGuard/Emergency/소화기 한 대 정밀 진단 (#242)")]
        public static void Inspect()
        {
            const string Serial="BSN-CONC-FE-005";
            var scene=EditorSceneManager.OpenScene(EmergencySceneBuilder.StationScenePath,OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[정밀진단] 씬을 열지 못했습니다.");return;}
            var data=AssetDatabase.LoadAssetAtPath<NavMeshData>(StationNavigationBuilder.NavMeshPath);
            if(data==null){Debug.LogError("[정밀진단] navmesh 자산이 없습니다.");return;}
            var instance=NavMesh.AddNavMeshData(data);
            if(!instance.valid){Debug.LogError("[정밀진단] navmesh 를 올리지 못했습니다.");return;}
            try
            {
                GameObject unit=null;
                foreach(var candidate in Units(scene))
                    if(candidate.name.EndsWith(Serial,StringComparison.Ordinal)){unit=candidate;break;}
                if(unit==null){Debug.LogError("[정밀진단] 유닛을 찾지 못했습니다 · "+Serial);return;}

                var body=Body(unit);
                Debug.Log("[정밀진단] "+Serial+" · 루트 "+unit.transform.position.ToString("F2")
                          +" · 본체 "+body.ToString("F2"));

                // 유닛 자신의 콜라이더를 끄고 본다. 켜 두면 자기 자신에 맞는다.
                var colliders=unit.GetComponentsInChildren<Collider>(true);
                var wasEnabled=new bool[colliders.Length];
                for(int i=0;i<colliders.Length;i++){wasEnabled[i]=colliders[i].enabled;colliders[i].enabled=false;}
                Physics.SyncTransforms();
                try
                {
                    // 이 기둥의 수직 구성을 전부 본다. 본체에서 아래로만 쏘면 위에 있는 바닥을 놓친다 —
                    // FE-005 가 실제로 슬래브 아래 빈 공간에 있었고 그래서 '바닥 없음' 으로 나왔다.
                    // 각 면마다 그 높이에 보행 영역이 있는지도 함께 잰다. 서 있을 수 있어야 바닥이다.
                    var column=Physics.RaycastAll(new Vector3(body.x,body.y+30f,body.z),Vector3.down,90f,~0,QueryTriggerInteraction.Ignore);
                    Array.Sort(column,(a,b)=>a.distance.CompareTo(b.distance));
                    if(column.Length==0)Debug.Log("[정밀진단] 이 기둥에는 아무 면도 없습니다.");
                    foreach(var hit in column)
                    {
                        bool walkable=NavMesh.SamplePosition(hit.point,out var spot,1.2f,NavMesh.AllAreas)
                                      &&Mathf.Abs(spot.position.y-hit.point.y)<.6f;
                        Debug.Log("[정밀진단] 면 y "+hit.point.y.ToString("F2")
                                  +" · "+hit.collider.name
                                  +" · 본체 대비 "+(hit.point.y-body.y).ToString("+0.00;-0.00")+"m"
                                  +" · 보행 "+(walkable?"가능 "+spot.position.ToString("F2"):"불가"));
                    }

                    // 주변을 훑어 진짜 바닥과 설 자리가 함께 있는 곳을 찾는다.
                    // 소화기는 벽에 붙으므로 벽이 있는 방향도 같이 잰다.
                    // 2층 대역(y 6.5~8.0)에 있는 자리만 찾는다. 높이로 거르지 않으면 바깥 지면(y -0.8)이
                    // 가까운 순서로 전부 잡혀 '후보가 많다' 는 잘못된 인상을 준다 — 실제로 그렇게 나왔다.
                    // 위에서 아래로 쏘아 슬래브 윗면을 잡는다. 본체 높이에서 쏘면 그 위의 바닥을 놓친다.
                    Debug.Log("[정밀진단] --- 2층 대역(y 6.5~8.0) 후보 ---");
                    const float BandLow=6.5f,BandHigh=8f;
                    int found=0;
                    for(float radius=1f;radius<=30f&&found<12;radius+=1f)
                        for(int step=0;step<24&&found<12;step++)
                        {
                            float angle=step*Mathf.PI*2f/24f;
                            var probe=new Vector3(body.x+Mathf.Cos(angle)*radius,BandHigh+4f,body.z+Mathf.Sin(angle)*radius);
                            if(!Physics.Raycast(probe,Vector3.down,out var floor,20f,~0,QueryTriggerInteraction.Ignore))continue;
                            if(floor.point.y<BandLow||floor.point.y>BandHigh)continue;
                            if(!NavMesh.SamplePosition(floor.point,out var stand,1.2f,NavMesh.AllAreas))continue;
                            if(Mathf.Abs(stand.position.y-floor.point.y)>.6f)continue;
                            // 벽 찾기: 설 자리에서 가슴 높이로 바깥을 향해 쏜다.
                            // 벽 안에서 출발하면 Unity 가 그 레이를 무시하므로 설 자리에서 쏜다.
                            string wall=null;float wallDistance=float.NaN;
                            for(int w=0;w<12&&wall==null;w++)
                            {
                                var direction=new Vector3(Mathf.Cos(w*Mathf.PI*2f/12f),0,Mathf.Sin(w*Mathf.PI*2f/12f));
                                if(Physics.Raycast(stand.position+Vector3.up*1.1f,direction,out var side,1.6f,~0,QueryTriggerInteraction.Ignore))
                                {wall=side.collider.name;wallDistance=side.distance;}
                            }
                            // 도달 가능성까지 재야 후보다. 재지 않으면 '바닥 없음' 을 '섬' 으로 바꾸는 이동이 된다.
                            int reach=0;var leg=new NavMeshPath();
                            foreach(var reference in Concourse())
                                if(NavMesh.CalculatePath(reference.Value,stand.position,NavMesh.AllAreas,leg)
                                   &&leg.status==NavMeshPathStatus.PathComplete)reach++;
                            found++;
                            Debug.Log("[정밀진단] 후보 "+found+" · 반경 "+radius.ToString("F0")+"m · 바닥 "
                                      +floor.collider.name+" y "+floor.point.y.ToString("F2")
                                      +" · 설 자리 "+stand.position.ToString("F2")
                                      +" · 벽 "+(wall==null?"없음":wall+" "+wallDistance.ToString("F2")+"m")
                                      +" · 맞이방에서 도달 "+reach+"/3");
                        }
                    if(found==0)Debug.Log("[정밀진단] 반경 30m 안에 2층 대역 바닥+설 자리가 함께 있는 곳이 없습니다.");
                }
                finally
                {
                    for(int i=0;i<colliders.Length;i++)colliders[i].enabled=wasEnabled[i];
                    Physics.SyncTransforms();
                }
            }
            finally { if(instance.valid)instance.Remove(); }
        }

        /// <summary>
        /// FE-005 를 실제 바닥 위 벽면으로 옮긴다(#242). **씬을 저장한다** — 다른 메서드와 달리 읽기 전용이 아니다.
        /// </summary>
        /// <remarks>
        /// 왜 이 자리인가: 원 좌표 `(60, 6.7, -65.57)` 에는 2층 면이 없다. 그 기둥의 면은 y 11.15(3층 슬래브 윗면)와
        /// y -0.89(바깥 지면)뿐이고 소화기는 그 사이 빈 공간에 매달려 있었다. v3 산정(`index 4`, roomSqm 45)이
        /// 실재하지 않는 층을 바닥으로 잡은 결과다.
        ///
        /// 목표 자리는 반경 5m 후보 중 **이웃 다섯(FE-003·006·009·010·011)과 같은 보행 성분에 드는 유일한 것**이다.
        /// 벽이 더 가까운 후보가 둘 더 있었으나 각각 고립돼 있어, 옮겨도 갈 수 없는 소화기가 된다.
        ///
        /// 이 수정은 #242 를 닫지 않는다. 커버리지는 다시 풀지 않았고 y 6.7 로 산정된 나머지 셋
        /// (FE-001·007·008)은 그대로다.
        /// </remarks>
        [MenuItem("ChooGuard/Emergency/FE-005 를 바닥 위로 옮긴다 (#242)")]
        public static void Apply()
        {
            const string Serial="BSN-CONC-FE-005";
            // 성분 진단에서 이웃과 같은 성분으로 확인된 설 자리.
            var chosen=new Vector3(55.12f,7.06f,-64.20f);
            var scene=EditorSceneManager.OpenScene(EmergencySceneBuilder.StationScenePath,OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[FE005이동] 씬을 열지 못했습니다.");return;}
            var data=AssetDatabase.LoadAssetAtPath<NavMeshData>(StationNavigationBuilder.NavMeshPath);
            if(data==null){Debug.LogError("[FE005이동] navmesh 자산이 없습니다.");return;}
            var instance=NavMesh.AddNavMeshData(data);
            if(!instance.valid){Debug.LogError("[FE005이동] navmesh 를 올리지 못했습니다.");return;}
            try
            {
                GameObject unit=null;
                foreach(var candidate in Units(scene))
                    if(candidate.name.EndsWith(Serial,StringComparison.Ordinal)){unit=candidate;break;}
                if(unit==null){Debug.LogError("[FE005이동] 유닛을 찾지 못했습니다 · "+Serial);return;}

                if(!NavMesh.SamplePosition(chosen,out var stand,1f,NavMesh.AllAreas))
                {Debug.LogError("[FE005이동] 목표 설 자리가 보행 영역에 없습니다 · "+chosen.ToString("F2"));return;}

                // 벽을 직접 잰다. 방향을 추정해 두면 벽이 아닌 곳에 붙일 수 있다.
                // 설 자리에서 쏜다 — 벽 안에서 시작한 레이는 Unity 가 무시한다.
                var eye=stand.position+Vector3.up*1.1f;
                RaycastHit wall=default;bool hasWall=false;float best=float.MaxValue;
                for(int w=0;w<36;w++)
                {
                    var direction=new Vector3(Mathf.Cos(w*Mathf.PI*2f/36f),0,Mathf.Sin(w*Mathf.PI*2f/36f));
                    if(!Physics.Raycast(eye,direction,out var hit,2f,~0,QueryTriggerInteraction.Ignore))continue;
                    if(hit.distance>=best)continue;
                    best=hit.distance;wall=hit;hasWall=true;
                }
                if(!hasWall){Debug.LogError("[FE005이동] 2m 안에 벽이 없습니다. 소화기는 벽에 붙는 물건이므로 멈춥니다.");return;}

                // 벽면에서 살짝 떼어 놓는다. 벽에 박히면 조준선이 벽을 먼저 문다.
                var normal=new Vector3(wall.normal.x,0,wall.normal.z);
                if(normal.sqrMagnitude<.0001f){Debug.LogError("[FE005이동] 벽 법선이 수평이 아닙니다 · "+wall.normal.ToString("F2"));return;}
                normal.Normalize();
                var against=new Vector3(wall.point.x,0,wall.point.z)+normal*.08f;

                // 바닥 높이를 그 자리에서 다시 잰다. 목표 y 를 그대로 쓰지 않는다.
                //
                // 시작 높이가 중요하다. 처음에 y 12 에서 쏘았더니 3층 슬래브 윗면(y 11.15)을 먼저 맞아
                // 소화기를 2층 바닥 위 4.15m 공중에 올려 놓았다. 2층 대역 바로 위에서 쏜다.
                const float BandLow=6.5f,BandHigh=8f;
                if(!Physics.Raycast(new Vector3(against.x,BandHigh+.5f,against.z),Vector3.down,out var floor,4f,~0,QueryTriggerInteraction.Ignore))
                {Debug.LogError("[FE005이동] 붙일 자리 아래에 바닥이 없습니다 · "+against.ToString("F2"));return;}
                if(floor.point.y<BandLow||floor.point.y>BandHigh)
                {Debug.LogError("[FE005이동] 잡힌 바닥이 2층 대역 밖입니다 · y "+floor.point.y.ToString("F2")
                                +" (허용 "+BandLow+"~"+BandHigh+") · "+floor.collider.name);return;}

                var before=unit.transform.position;
                var beforeRotation=unit.transform.rotation;
                var target=new Vector3(against.x,floor.point.y,against.z);
                // 판독면이 보이도록 벽을 등지고 선다 — 기존 규약대로 로컬 +Z 가 사람 쪽을 향한다.
                var facing=Quaternion.LookRotation(normal,Vector3.up);
                Undo.RecordObject(unit.transform,"FE-005 이동");
                unit.transform.SetPositionAndRotation(target,facing);
                Physics.SyncTransforms();

                Debug.Log("[FE005이동] 벽 "+wall.collider.name+" "+best.ToString("F2")+"m · 법선 "+normal.ToString("F2"));
                Debug.Log("[FE005이동] 위치 "+before.ToString("F3")+" → "+target.ToString("F3")
                          +" · 이동 "+Vector3.Distance(before,target).ToString("F2")+"m");
                Debug.Log("[FE005이동] 회전 "+beforeRotation.eulerAngles.ToString("F1")+" → "+facing.eulerAngles.ToString("F1"));

                // 저장 전에 스스로 확인한다. 실패하면 되돌리고 저장하지 않는다.
                //
                // 처음 쓴 검사는 "6m 안에 바닥이 있으면 통과" 였고, 그래서 2층 바닥 위 4.15m 에 뜬 소화기를
                // '바닥 있음' 으로 통과시켰다. 좋은 경우와 나쁜 경우 모두에서 통과하는 단언이었다.
                // 이제 **발밑에 닿아 있는지**를 본다 — 루트 바로 위에서 쏘아 그만큼만 떨어져 있어야 한다.
                const float Contact=.05f;
                var root=unit.transform.position;
                bool grounded=Physics.Raycast(root+Vector3.up*.5f,Vector3.down,out var check,1f,~0,QueryTriggerInteraction.Ignore)
                              &&Mathf.Abs(check.distance-.5f)<Contact;
                bool canStand=NavMesh.SamplePosition(root,out var spot,StandRadius,NavMesh.AllAreas)
                              &&Mathf.Abs(spot.position.y-root.y)<.5f;
                if(!grounded||!canStand)
                {
                    unit.transform.SetPositionAndRotation(before,beforeRotation);
                    Debug.LogError("[FE005이동] 접지 "+(grounded?"됨":"안 됨")
                                   +" · 같은 높이 설 자리 "+(canStand?"있음":"없음")
                                   +" — 되돌리고 저장하지 않습니다.");
                    return;
                }
                Debug.Log("[FE005이동] 확인 · 접지 "+check.collider.name+" 틈 "
                          +Mathf.Abs(check.distance-.5f).ToString("F3")+"m"
                          +" · 설 자리 "+Vector3.Distance(root,spot.position).ToString("F2")+"m"
                          +" (높이차 "+(spot.position.y-root.y).ToString("+0.00;-0.00")+"m)");

                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)){Debug.LogError("[FE005이동] 씬을 저장하지 못했습니다.");return;}
                Debug.Log("CG_FE005_MOVED from="+before.ToString("F3")+" to="+target.ToString("F3"));
            }
            finally { if(instance.valid)instance.Remove(); }
        }

        /// <summary>
        /// FE-005 를 선택하고 씬 뷰를 그 앞에 세운다. 눈으로 볼 때 쓰는 도우미다 — 씬을 바꾸지 않는다.
        /// </summary>
        /// <remarks>
        /// 렌더러가 2천 개가 넘어 손으로 찾기 어렵다. 배치모드 검사는 "바닥 위에 있다" 까지만 말하고
        /// "벽에 박히지 않았다"·"판독면이 보인다" 는 말하지 못한다. 그것은 사람이 본다.
        /// </remarks>
        [MenuItem("ChooGuard/Emergency/FE-005 를 화면에 잡는다 (#242)")]
        public static void Frame()
        {
            const string Serial="BSN-CONC-FE-005";
            var scene=EditorSceneManager.OpenScene(EmergencySceneBuilder.StationScenePath,OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[FE005보기] 씬을 열지 못했습니다.");return;}
            GameObject unit=null;
            foreach(var candidate in Units(scene))
                if(candidate.name.EndsWith(Serial,StringComparison.Ordinal)){unit=candidate;break;}
            if(unit==null){Debug.LogError("[FE005보기] 유닛을 찾지 못했습니다 · "+Serial);return;}

            Selection.activeGameObject=unit;
            EditorGUIUtility.PingObject(unit);
            var view=SceneView.lastActiveSceneView;
            if(view!=null)
            {
                // 사람이 설 자리에서 보는 각도로 세운다. 위에서 내려다보면 벽에 박혔는지 안 보인다.
                var body=Body(unit);
                view.LookAt(body,Quaternion.LookRotation(unit.transform.forward*-1f+Vector3.down*.15f),2.5f);
                view.Repaint();
            }
            Debug.Log("[FE005보기] 선택했습니다 · 루트 "+unit.transform.position.ToString("F3")
                      +" · 회전 "+unit.transform.rotation.eulerAngles.ToString("F1")
                      +"\n볼 것: (1) 벽에 박히지 않았는가 (2) 바닥에 떠 있거나 묻히지 않았는가 "
                      +"(3) 앞에 서면 고유번호·제원표·지시압력계가 읽히는가 (4) 앞에 설 공간이 있는가");
        }

        // 기준점 후보. 어느 것이 큰 성분에 드는지 비교하기 위한 것이므로 넓게 잡는다.
        private static List<KeyValuePair<string,Vector3>> Probes()=>new List<KeyValuePair<string,Vector3>>
        {
            new KeyValuePair<string,Vector3>("*hallCentre",StationNavigationBuilder.HallCentre),
            new KeyValuePair<string,Vector3>("*hall2f-mid",new Vector3(65,7,17)),
            new KeyValuePair<string,Vector3>("*hall2f-west",new Vector3(24,7,-24)),
            new KeyValuePair<string,Vector3>("*hall2f-east",new Vector3(106,7,58)),
            new KeyValuePair<string,Vector3>("*southgate",new Vector3(44,7,-50)),
            new KeyValuePair<string,Vector3>("*main2f",new Vector3(-22,7,-5)),
            new KeyValuePair<string,Vector3>("*northdeck",new Vector3(85,7,82)),
            // FE-005 를 옮길 후보 두 곳(#242). 어느 성분에 드는지 보려고 넣었다 —
            // 이웃한 FE-011·006 과 같은 성분이면 '이웃과 같은 처지' 가 되고, 혼자면 옮길 값이 없다.
            new KeyValuePair<string,Vector3>("?cand2",new Vector3(55.12f,7.06f,-64.20f)),
            new KeyValuePair<string,Vector3>("?cand4",new Vector3(55.04f,7.06f,-67.06f)),
            new KeyValuePair<string,Vector3>("?cand11",new Vector3(59.58f,7.06f,-58.35f)),
        };

        private static Vector3 Body(GameObject unit)
        {
            var bounds=new Bounds(unit.transform.position,Vector3.zero);
            bool any=false;
            foreach(var renderer in unit.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer==null)continue;
                if(!any){bounds=renderer.bounds;any=true;}
                else bounds.Encapsulate(renderer.bounds);
            }
            return any?bounds.center:unit.transform.position;
        }

        /// <summary>
        /// 맞이방 기준점. StationNavigationBuilder.HallCentre 를 그대로 쓰고, 그 좌우로 두 점을 더 얻는다.
        /// 한 점만 쓰면 그 점이 속한 성분만 검사하게 되어 '섬' 판정이 기준점 선택에 좌우된다.
        /// </summary>
        private static List<KeyValuePair<string,Vector3>> Concourse()
        {
            var found=new List<KeyValuePair<string,Vector3>>();
            var centre=StationNavigationBuilder.HallCentre;
            // hall2f 는 station-points.json 이 x 18~112 로 선언한다. 중심에서 ±22m 면 그 안에 든다.
            var probes=new[]
            {
                new KeyValuePair<string,Vector3>("hallCentre",centre),
                new KeyValuePair<string,Vector3>("hallWest",centre+new Vector3(-22,0,0)),
                new KeyValuePair<string,Vector3>("hallEast",centre+new Vector3(22,0,0)),
            };
            foreach(var probe in probes)
            {
                if(NavMesh.SamplePosition(probe.Value,out var hit,4f,NavMesh.AllAreas))
                    found.Add(new KeyValuePair<string,Vector3>(probe.Key,hit.position));
                else
                    Debug.LogWarning("[소화기측정] 기준점 후보가 보행 영역에 없습니다 · "+probe.Key+" "+probe.Value.ToString("F1"));
            }
            return found;
        }

        private static List<GameObject> Units(Scene scene)
        {
            var units=new List<GameObject>();
            foreach(var root in scene.GetRootGameObjects())
                if(root!=null)Collect(root.transform,units);
            units.Sort((a,b)=>string.Compare(a.name,b.name,StringComparison.Ordinal));
            return units;
        }

        private static void Collect(Transform node,List<GameObject> into)
        {
            if(node.name.StartsWith(UnitPrefix,StringComparison.Ordinal))into.Add(node.gameObject);
            for(int i=0;i<node.childCount;i++)Collect(node.GetChild(i),into);
        }

        private static string Measure(GameObject unit,List<KeyValuePair<string,Vector3>> references,out string verdict)
        {
            var serial=unit.name.Substring(UnitPrefix.Length);
            var root=unit.transform.position;
            // 본체는 렌더러 경계로 잡는다. 자식 구성이 바뀌어도 따라간다.
            var bounds=new Bounds(root,Vector3.zero);
            bool any=false;
            foreach(var renderer in unit.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer==null)continue;
                if(!any){bounds=renderer.bounds;any=true;}
                else bounds.Encapsulate(renderer.bounds);
            }
            var body=any?bounds.center:root;

            // 유닛의 콜라이더를 잠시 끈다. Unity 는 콜라이더 안에서 시작한 레이를 무시하므로,
            // 끄지 않으면 소화기 자신에 맞거나 아무것도 못 맞힌다(2026-09-25 에 146/146 을 이렇게 틀렸다).
            var colliders=unit.GetComponentsInChildren<Collider>(true);
            var wasEnabled=new bool[colliders.Length];
            for(int i=0;i<colliders.Length;i++){wasEnabled[i]=colliders[i].enabled;colliders[i].enabled=false;}
            Physics.SyncTransforms();
            string floorName=null;float floorDrop=float.NaN,floorY=float.NaN;bool hasFloor;
            try
            {
                hasFloor=Physics.Raycast(body,Vector3.down,out var hit,FloorDepth,~0,QueryTriggerInteraction.Ignore);
                if(hasFloor){floorName=hit.collider.name;floorDrop=hit.distance;floorY=hit.point.y;}
            }
            finally
            {
                for(int i=0;i<colliders.Length;i++)colliders[i].enabled=wasEnabled[i];
                Physics.SyncTransforms();
            }

            // 설 자리는 바닥 높이에서 찾는다. 본체 높이(1.1m 위)에서 찾으면 위층을 물 수 있다.
            var origin=new Vector3(body.x,hasFloor?floorY:root.y,body.z);
            bool hasStand=NavMesh.SamplePosition(origin,out var stand,StandRadius,NavMesh.AllAreas);
            var standPos=hasStand?stand.position:Vector3.zero;
            float standDistance=hasStand?Vector3.Distance(origin,standPos):float.NaN;

            // 도달 가능성.
            //
            // PathPartial 과 PathInvalid 를 구분해야 한다. 이 navmesh 에서는 긴 경로가 한 번의
            // CalculatePath 로 완성되지 않는다 — StationWorld.Via() 가 "splitting it keeps every
            // navmesh path short enough to be found in full" 이라며 이미 우회하고 있다.
            // 둘을 뭉개면 '멀다' 를 '끊겼다' 로 잘못 보고한다(2026-09-28 에 그렇게 2/9 를 냈다).
            //
            // 그래서 부분 경로는 끝점이 목표에 얼마나 가까운지도 남긴다. 그 거리가 작으면
            // 실제로는 이어져 있고 탐색 예산이 모자란 것이다.
            int complete=0,partial=0,invalid=0,mostLegs=1;
            float shortest=float.PositiveInfinity,nearestPartialGap=float.PositiveInfinity;
            var path=new NavMeshPath();
            if(hasStand)
                foreach(var reference in references)
                {
                    if(!NavMesh.CalculatePath(reference.Value,standPos,NavMesh.AllAreas,path))
                    {invalid++;continue;}
                    if(path.status==NavMeshPathStatus.PathInvalid){invalid++;continue;}
                    float length=0f;
                    for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);
                    if(path.status==NavMeshPathStatus.PathComplete)
                    {
                        complete++;
                        if(length<shortest)shortest=length;
                        continue;
                    }
                    partial++;
                    var end=path.corners.Length>0?path.corners[path.corners.Length-1]:reference.Value;
                    float gap=Vector3.Distance(end,standPos);
                    if(gap<nearestPartialGap)nearestPartialGap=gap;
                    // 부분 경로를 이어서 다시 건다. 게임이 StationWorld.Via() 로 하는 것과 같은 일이다.
                    // 이어 붙여 닿으면 '멀다' 이고, 끝점이 더 나아가지 못하면 그때가 '막혔다' 이다.
                    if(WalkInLegs(end,standPos,length,out var total,out var legs))
                    {
                        complete++;partial--;
                        if(total<shortest)shortest=total;
                        if(legs>mostLegs)mostLegs=legs;
                    }
                }

            if(!hasFloor)verdict="NO_FLOOR";
            else if(!hasStand)verdict="NO_STANDING_SPOT";
            else if(complete>0)verdict="REACHABLE";
            else if(partial>0)verdict="PARTIAL_PATH";
            else verdict="ISLAND";

            var line=new StringBuilder();
            line.Append("{\"serial\": \"").Append(Escape(serial)).Append("\", \"verdict\": \"").Append(verdict).Append("\", ");
            line.Append("\"root\": ").Append(V(root)).Append(", \"body\": ").Append(V(body)).Append(", ");
            line.Append("\"floor\": ").Append(hasFloor
                ? "{\"name\": \""+Escape(floorName)+"\", \"drop\": "+F(floorDrop)+", \"y\": "+F(floorY)+"}"
                : "null").Append(", ");
            line.Append("\"standingSpot\": ").Append(hasStand
                ? "{\"position\": "+V(standPos)+", \"distance\": "+F(standDistance)+"}"
                : "null").Append(", ");
            line.Append("\"reachableFrom\": ").Append(complete);
            line.Append(", \"partialFrom\": ").Append(partial);
            line.Append(", \"invalidFrom\": ").Append(invalid);
            line.Append(", \"referenceCount\": ").Append(references.Count);
            line.Append(", \"shortestWalk\": ").Append(F(shortest));
            line.Append(", \"nearestPartialGap\": ").Append(F(nearestPartialGap));
            // 구간 수가 1보다 크면 한 번의 CalculatePath 로는 못 푸는 거리라는 뜻이다.
            line.Append(", \"legs\": ").Append(mostLegs).Append('}');
            // 한 줄씩 콘솔에도 남긴다. JSON 만 쓰면 무엇이 틀렸는지 로그에서 안 보인다.
            Debug.Log("[소화기측정] "+serial+" · "+verdict
                      +" · 바닥 "+(hasFloor?floorName+" "+floorDrop.ToString("F2")+"m 아래":"없음")
                      +" · 설 자리 "+(hasStand?standDistance.ToString("F2")+"m":"없음")
                      +" · 완전 "+complete+" 부분 "+partial+" 없음 "+invalid+"/"+references.Count
                      +(float.IsInfinity(shortest)?"":" · 최단 "+shortest.ToString("F1")+"m")
                      +(float.IsInfinity(nearestPartialGap)?"":" · 부분경로 끝까지 "+nearestPartialGap.ToString("F1")+"m 남음"));
            return line.ToString();
        }

        /// <summary>
        /// 부분 경로를 이어 붙여 목표까지 가 본다. 이 navmesh 는 긴 경로를 한 번에 풀지 못하므로
        /// (StationWorld.Via 가 같은 이유로 경로를 쪼갠다), 한 번의 CalculatePath 결과만 보고
        /// '끊겼다' 고 판정하면 멀기만 한 자리를 섬으로 잘못 보고한다.
        /// </summary>
        /// <remarks>
        /// 끝점이 더 나아가지 못하면(<see cref="Stall"/> 미만) 그때가 실제로 막힌 것이다.
        /// 구간 수를 제한하는 이유는 진동하는 경우에도 끝나게 하기 위해서다.
        /// </remarks>
        private static bool WalkInLegs(Vector3 from,Vector3 to,float travelled,out float total,out int legs)
        {
            const int MaxLegs=24;
            const float Stall=.5f;
            total=travelled;legs=1;
            var path=new NavMeshPath();
            var cursor=from;
            for(;legs<MaxLegs;legs++)
            {
                if(!NavMesh.CalculatePath(cursor,to,NavMesh.AllAreas,path))return false;
                if(path.status==NavMeshPathStatus.PathInvalid)return false;
                float length=0f;
                for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);
                total+=length;
                if(path.status==NavMeshPathStatus.PathComplete)return true;
                var end=path.corners.Length>0?path.corners[path.corners.Length-1]:cursor;
                if(Vector3.Distance(end,cursor)<Stall)return false;
                cursor=end;
            }
            return false;
        }

        private static string V(Vector3 v)=>"["+F(v.x)+", "+F(v.y)+", "+F(v.z)+"]";
        private static string F(float f)=>float.IsNaN(f)||float.IsInfinity(f)?"null":f.ToString("0.###",CultureInfo.InvariantCulture);
        private static string Escape(string s)=>s==null?"":s.Replace("\\","\\\\").Replace("\"","\\\"");
    }
}
