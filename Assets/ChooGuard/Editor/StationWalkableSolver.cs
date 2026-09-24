using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ChooGuard.App.Fps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.EditorTools
{
    // 소화기 배치를 실제 씬 콜라이더 기준으로 다시 푼다.
    //
    // 기존 좌표(extinguisher-placement-v3.json)는 격자와 보행거리로만 산정했고 실제 메시를
    // 보지 않았다. 그 결과 12개 중 3개가 사람이 설 수 없는 자리에 놓였고, 그중 하나는
    // 아래 8m 까지 바닥이 없는 건물 밖이었다(2026-09-25 생성기 검증).
    //
    // 씬을 고치지 않는다. 읽고 계산해서 JSON 과 붙여넣을 C# 블록을 남긴다 —
    // 배치를 바꾸는 것은 사람이 검토한 뒤에 할 일이다.
    //
    // 주장하지 않는 것: 법정 적합. NFTC 101 의 보행거리 항목(소형 20m)만 계산에 쓰고,
    // 구획 면적·소화 능력단위 산정은 하지 않는다. 그것까지 했다고 적으면 거짓이 된다.
    public static class StationWalkableSolver
    {
        private const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
        private const string OutDir=".planning/2026-09-25-placement-resolve";
        private const string OutFile=OutDir+"/extinguisher-placement-v4.json";

        private const float Cell=.5f;              // 격자 한 칸
        private const float PersonRadius=.3f;      // 사람 반지름 — 플레이어 .28 보다 조금 넉넉히
        private const float PersonHeight=1.7f;
        private const float StepLimit=.35f;        // 이웃 칸으로 걸어갈 수 있는 단차 상한
        private const float ReachMetres=20f;       // NFTC 101 소형 보행거리
        private const int WallSampleStride=3;      // 벽면 후보를 1.5m 간격으로만 본다
        private const float MountOffset=.35f;      // 벽에서 설비 중심까지
        private const float PlateHeight=1.65f;     // 판독면 대략 높이(거치 1.10 + .55)
        private const float WallSearch=12f;        // 칸에서 벽면을 찾는 수평 광선 거리. 대합실이 개방적이라 2.2m 로는 벽에 닿는 표본이 382 개 중 4 개뿐이었다.
        // 개수는 기존 배치(v3)와 같은 12 개로 맞춘다. 보행거리만으로는 5 개면 100% 덮이지만,
        // v3 의 12 개는 구획 요건까지 반영한 수치이고 나는 그 계산을 하지 않는다. 개수를 줄이면
        // 내가 검증하지 않은 요건을 내 판단으로 완화하는 것이 된다 — 커버는 채우되 개수는 유지한다.
        private const int TargetCount=12;
        // 상호작용은 3m 상한 안에서 가장 가까운 콜라이더를 고른다. 두 유닛이 그 안에 들어오면
        // 어느 것을 겨눈 것인지 모호해진다. 여유를 둬서 3.2m 미만은 아예 고르지 않는다.
        private const float MinGap=3.2f;
        private const float SampleRadius=70f;      // 시작점 기준 표본 반경. 역사 외부까지 재면 격자가 터진다.

        [MenuItem("ChooGuard/수직 슬라이스/소화기 배치 재산정 (실제 메시)")]
        public static void SolveMenu(){Solve();}

        public static void Solve()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[재산정] 씬을 열지 못했습니다 · "+ScenePath);return;}

            var responder=Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null){Debug.LogError("[재산정] 플레이어를 찾지 못했습니다. 보행 영역의 시작점이 필요합니다.");return;}

            // 플레이어와 기존 튜토리얼 산출물은 지형이 아니다. 끄고 잰다 —
            // 켜둔 채로 재면 이미 놓인 소화기가 벽처럼 잡혀 다음 배치가 그걸 피해 간다.
            var hidden=DisableNonTerrainColliders(responder.transform);
            try
            {
                Physics.SyncTransforms();
                var grid=Sample(responder.transform.position);
                if(grid==null)return;
                Report(grid);
            }
            finally
            {
                foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=pair.Value;
            }
        }

        // ── 격자 ────────────────────────────────────────────────────────
        private sealed class Grid
        {
            public int Width,Depth;
            public float MinX,MinZ;
            public bool[] Walkable;       // 바닥이 있고 사람이 들어간다
            public bool[] Reachable;      // 시작점에서 걸어서 닿는다
            public float[] Height;
            public int Index(int x,int z)=>z*Width+x;
            public Vector3 Center(int x,int z)=>new Vector3(MinX+(x+.5f)*Cell,Height[Index(x,z)],MinZ+(z+.5f)*Cell);
        }

        private static Grid Sample(Vector3 seed)
        {
            var bounds=new Bounds();bool any=false;
            foreach(var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if(!collider.enabled||collider.isTrigger)continue;
                if(!any){bounds=collider.bounds;any=true;}else bounds.Encapsulate(collider.bounds);
            }
            if(!any){Debug.LogError("[재산정] 콜라이더가 하나도 없습니다.");return null;}

            // 역사 외부 모델까지 경계에 들어오면 격자가 899×908(81만 칸)이 된다. 시작점 주변으로
            // 자른다 — 튜토리얼이 일어나는 곳은 대합실이고, 걸어 닿지 않는 곳은 어차피 버려진다.
            var limit=new Bounds(new Vector3(seed.x,bounds.center.y,seed.z),
                                 new Vector3(SampleRadius*2f,bounds.size.y,SampleRadius*2f));
            var min=Vector3.Max(bounds.min,limit.min);
            var max=Vector3.Min(bounds.max,limit.max);
            bounds.SetMinMax(min,max);

            var grid=new Grid
            {
                MinX=bounds.min.x,MinZ=bounds.min.z,
                Width=Mathf.CeilToInt(bounds.size.x/Cell),
                Depth=Mathf.CeilToInt(bounds.size.z/Cell),
            };
            if(grid.Width<=0||grid.Depth<=0||(long)grid.Width*grid.Depth>400000)
            {
                Debug.LogError("[재산정] 격자가 비었거나 너무 큽니다 · "+grid.Width+"×"+grid.Depth);
                return null;
            }
            int cells=grid.Width*grid.Depth;
            grid.Walkable=new bool[cells];grid.Reachable=new bool[cells];grid.Height=new float[cells];

            // 바닥은 시작점이 선 층에서만 찾는다. 건물 맨 위에서 아래로 쏘면 지붕을 먼저 맞고,
            // 그 결과 "보행 가능 영역" 이 지붕 위가 된다 — 실제로 산출 좌표의 y 가 전부 13~16m 로
            // 나왔다(대합실 바닥은 7.0m). 한 층 높이 안에서만 훑는다(2026-09-25).
            float top=seed.y+2.2f,span=5f;
            int walkable=0;
            for(int z=0;z<grid.Depth;z++)
            for(int x=0;x<grid.Width;x++)
            {
                var probe=new Vector3(grid.MinX+(x+.5f)*Cell,top,grid.MinZ+(z+.5f)*Cell);
                if(!Physics.Raycast(probe,Vector3.down,out var floor,span,~0,QueryTriggerInteraction.Ignore))continue;
                int i=grid.Index(x,z);
                grid.Height[i]=floor.point.y;
                if(Physics.CheckCapsule(floor.point+Vector3.up*(PersonRadius+.05f),
                                        floor.point+Vector3.up*(PersonHeight-PersonRadius),
                                        PersonRadius,~0,QueryTriggerInteraction.Ignore))continue;
                grid.Walkable[i]=true;walkable++;
            }

            int reachable=Flood(grid,seed);
            Debug.Log("[재산정] 격자 "+grid.Width+"×"+grid.Depth+" ("+Cell.ToString("0.0",CultureInfo.InvariantCulture)
                      +"m) · 보행 가능 "+walkable+" · 시작점에서 닿는 곳 "+reachable);
            if(reachable<10){Debug.LogError("[재산정] 시작점에서 닿는 칸이 너무 적습니다. 시작 좌표를 확인하세요.");return null;}
            return grid;
        }

        // 시작점에서 걸어 닿는 곳만 남긴다. 떨어진 섬을 덮어봐야 아무도 못 간다.
        private static int Flood(Grid grid,Vector3 seed)
        {
            int sx=Mathf.Clamp(Mathf.FloorToInt((seed.x-grid.MinX)/Cell),0,grid.Width-1);
            int sz=Mathf.Clamp(Mathf.FloorToInt((seed.z-grid.MinZ)/Cell),0,grid.Depth-1);
            int start=NearestWalkable(grid,sx,sz);
            if(start<0)return 0;

            var queue=new Queue<int>();queue.Enqueue(start);grid.Reachable[start]=true;
            int count=1;
            while(queue.Count>0)
            {
                int i=queue.Dequeue();int x=i%grid.Width,z=i/grid.Width;
                foreach(var n in Neighbours(grid,x,z))
                {
                    if(grid.Reachable[n]||!grid.Walkable[n])continue;
                    if(Mathf.Abs(grid.Height[n]-grid.Height[i])>StepLimit)continue;
                    grid.Reachable[n]=true;count++;queue.Enqueue(n);
                }
            }
            return count;
        }

        private static int NearestWalkable(Grid grid,int sx,int sz)
        {
            for(int r=0;r<12;r++)
            for(int dz=-r;dz<=r;dz++)
            for(int dx=-r;dx<=r;dx++)
            {
                int x=sx+dx,z=sz+dz;
                if(x<0||z<0||x>=grid.Width||z>=grid.Depth)continue;
                int i=grid.Index(x,z);
                if(grid.Walkable[i])return i;
            }
            return -1;
        }

        private static IEnumerable<int> Neighbours(Grid grid,int x,int z)
        {
            if(x>0)yield return grid.Index(x-1,z);
            if(x<grid.Width-1)yield return grid.Index(x+1,z);
            if(z>0)yield return grid.Index(x,z-1);
            if(z<grid.Depth-1)yield return grid.Index(x,z+1);
        }

        // ── 후보와 커버 ─────────────────────────────────────────────────
        private sealed class Candidate
        {
            public Vector3 Mount;      // 바닥 접지점
            public Vector2Int Normal;  // 벽에서 방 쪽
            public Vector3 Stand;      // 점검하려고 서는 자리
            public int Cell;
            public List<int> Covers=new List<int>();
        }

        // 벽은 격자 이웃으로 찾지 않는다. 대합실은 열린 메자닌이라 보행 영역의 경계가 대부분
        // 벽이 아니라 난간 너머 허공이었다 — 이웃 기반으로는 146 개 표본이 전부 허공으로 잡혔다.
        // 대신 칸에서 수평으로 광선을 쏴 진짜 벽면을 직접 찾는다. 법선이 서 있어야 벽이다.
        private static List<Candidate> Candidates(Grid grid)
        {
            var list=new List<Candidate>();
            int sampled=0,hitSomething=0,notVertical=0,noStand=0,blocked=0,unreachableStand=0;
            var seen=new HashSet<Vector3Int>();
            for(int z=0;z<grid.Depth;z+=WallSampleStride)
            for(int x=0;x<grid.Width;x+=WallSampleStride)
            {
                int i=grid.Index(x,z);
                if(!grid.Reachable[i])continue;
                sampled++;
                var cellCentre=grid.Center(x,z);
                foreach(var dir in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                {
                    if(!Physics.Raycast(cellCentre+Vector3.up*1.1f,dir,out var wall,WallSearch,~0,
                                        QueryTriggerInteraction.Ignore))continue;
                    if(wall.distance<MountOffset+.15f)continue;   // 너무 붙어 서면 점검할 공간이 없다
                    hitSomething++;
                    if(Mathf.Abs(wall.normal.y)>.4f){notVertical++;continue;}   // 바닥·천장은 벽이 아니다

                    var mount=new Vector3(wall.point.x,cellCentre.y,wall.point.z)-dir*MountOffset;
                    var stand=new Vector3(mount.x,cellCentre.y,mount.z)-dir*.75f;
                    if(!StandClear(stand)){noStand++;continue;}
                    // 설 수 있는 것과 걸어서 갈 수 있는 것은 다르다. 서는 자리가 보행 영역 밖이면
                    // 그 설비는 점검하러 갈 수가 없다 — 개방도 0 인 유닛이 둘 나와서 발견했다.
                    if(!ReachableAt(grid,stand)){unreachableStand++;continue;}

                    var eye=new Vector3(stand.x,stand.y+1.6f,stand.z);
                    var plate=new Vector3(mount.x,mount.y+PlateHeight,mount.z);
                    if(Physics.Raycast(eye,(plate-eye).normalized,Vector3.Distance(eye,plate)-.1f,~0,
                                       QueryTriggerInteraction.Ignore)){blocked++;continue;}

                    // 여러 칸에서 같은 벽면을 찾으면 거의 같은 자리가 여러 번 나온다. 1m 격자로 뭉친다.
                    var key=new Vector3Int(Mathf.RoundToInt(mount.x),Mathf.RoundToInt(mount.y),Mathf.RoundToInt(mount.z));
                    if(!seen.Add(key))break;
                    list.Add(new Candidate{Mount=mount,
                                           Normal=new Vector2Int(Mathf.RoundToInt(-dir.x),Mathf.RoundToInt(-dir.z)),
                                           Stand=stand,Cell=i});
                    break;   // 한 칸에서 벽 하나면 충분하다
                }
            }
            Debug.Log("[재산정] 후보 걸러짐 · 표본 "+sampled+" · 벽면 닿음 "+hitSomething
                      +" · 법선이 눕음(바닥·천장) "+notVertical+" · 설 자리 없음 "+noStand
                      +" · 서는 자리가 보행 영역 밖 "+unreachableStand+" · 시야 가림 "+blocked+" · 통과 "+list.Count);
            return list;
        }

        private static bool ReachableAt(Grid grid,Vector3 point)
        {
            int x=Mathf.FloorToInt((point.x-grid.MinX)/Cell);
            int z=Mathf.FloorToInt((point.z-grid.MinZ)/Cell);
            if(x<0||z<0||x>=grid.Width||z>=grid.Depth)return false;
            return grid.Reachable[grid.Index(x,z)];
        }

        private static bool StandClear(Vector3 point)
        {
            if(!Physics.Raycast(point+Vector3.up*2.5f,Vector3.down,out var floor,8f,~0,QueryTriggerInteraction.Ignore))
                return false;
            return !Physics.CheckCapsule(floor.point+Vector3.up*(PersonRadius+.05f),
                                         floor.point+Vector3.up*(PersonHeight-PersonRadius),
                                         PersonRadius,~0,QueryTriggerInteraction.Ignore);
        }

        // 후보에서 보행거리 20m 안에 들어오는 칸을 모은다. 직선거리가 아니라 걸어서 잰다.
        private static void FillCoverage(Grid grid,Candidate candidate)
        {
            int limit=Mathf.CeilToInt(ReachMetres/Cell);
            var dist=new Dictionary<int,int>{{candidate.Cell,0}};
            var queue=new Queue<int>();queue.Enqueue(candidate.Cell);
            while(queue.Count>0)
            {
                int i=queue.Dequeue();int d=dist[i];
                candidate.Covers.Add(i);
                if(d>=limit)continue;
                int x=i%grid.Width,z=i/grid.Width;
                foreach(var n in Neighbours(grid,x,z))
                {
                    if(dist.ContainsKey(n)||!grid.Reachable[n])continue;
                    if(Mathf.Abs(grid.Height[n]-grid.Height[i])>StepLimit)continue;
                    dist[n]=d+1;queue.Enqueue(n);
                }
            }
        }

        private static void Report(Grid grid)
        {
            var candidates=Candidates(grid);
            Debug.Log("[재산정] 벽면 후보 "+candidates.Count+"개 (설 자리·시야 검사 통과)");
            if(candidates.Count==0){Debug.LogError("[재산정] 후보가 없습니다.");return;}
            foreach(var c in candidates)FillCoverage(grid,c);

            var uncovered=new HashSet<int>();
            for(int i=0;i<grid.Reachable.Length;i++)if(grid.Reachable[i])uncovered.Add(i);
            int target=uncovered.Count;

            var chosen=new List<Candidate>();
            while(uncovered.Count>0&&chosen.Count<40)
            {
                Candidate best=null;int bestGain=0;
                foreach(var c in candidates)
                {
                    if(chosen.Contains(c))continue;
                    // 커버 단계에서도 간격을 지킨다. 여기서 놓치면 1.87m 짜리 쌍이 그대로 남는다.
                    bool tooClose=false;
                    foreach(var taken in chosen)
                        if(Vector3.Distance(c.Mount,taken.Mount)<MinGap){tooClose=true;break;}
                    if(tooClose)continue;
                    int gain=0;
                    foreach(var cell in c.Covers)if(uncovered.Contains(cell))gain++;
                    if(gain>bestGain){bestGain=gain;best=c;}
                }
                if(best==null||bestGain==0)break;
                chosen.Add(best);
                foreach(var cell in best.Covers)uncovered.Remove(cell);
            }

            // 커버를 채운 뒤 남은 자리는 서로 멀리 떨어지게 고른다. 가까이 몰리면 상호작용
            // 상한(3m) 안에 두 유닛이 들어와 어느 것을 겨눈 것인지 모호해진다 — v3 에서 실제로
            // 2.00m 경고가 났다.
            while(chosen.Count<TargetCount)
            {
                Candidate best=null;float bestGap=-1f;
                foreach(var c in candidates)
                {
                    if(chosen.Contains(c))continue;
                    float nearest=float.PositiveInfinity;
                    foreach(var taken in chosen)nearest=Mathf.Min(nearest,Vector3.Distance(c.Mount,taken.Mount));
                    if(nearest<MinGap)continue;   // 너무 붙으면 고르지 않는다
                    if(nearest>bestGap){bestGap=nearest;best=c;}
                }
                if(best==null)break;
                chosen.Add(best);
            }
            if(chosen.Count<TargetCount)
                Debug.LogWarning("[재산정] 간격 "+MinGap.ToString("0.0",CultureInfo.InvariantCulture)+"m 를 지키면서 고를 수 있는 자리가 "+chosen.Count+"개뿐입니다 (목표 "+TargetCount+"개). "+"개수를 채우려면 간격을 포기해야 하므로 채우지 않았습니다.");

            float minGap=float.PositiveInfinity;
            for(int a=0;a<chosen.Count;a++)for(int b=a+1;b<chosen.Count;b++)
                minGap=Mathf.Min(minGap,Vector3.Distance(chosen[a].Mount,chosen[b].Mount));
            Debug.Log("[재산정] 유닛 간 최소 간격 "+minGap.ToString("0.00",CultureInfo.InvariantCulture)+"m");

            // 튜토리얼 대상은 개방도로 고른다. 기하학적으로 유효해도 어두운 벽감에 묻히면
            // 첫 화면에서 실루엣만 보인다 — 실제로 그런 자리가 뽑혀 프레임으로 확인했다(2026-09-25).
            int bestOpen=-1,bestOpenIndex=-1;
            for(int c=0;c<chosen.Count;c++)
            {
                int open=0;
                for(int cell=0;cell<grid.Reachable.Length;cell++)
                {
                    if(!grid.Reachable[cell])continue;
                    int cx=cell%grid.Width,cz=cell/grid.Width;
                    if(Vector3.Distance(grid.Center(cx,cz),chosen[c].Stand)<=6f)open++;
                }
                Debug.Log("[재산정] 유닛 "+c+" 개방도 "+open);
                if(open>bestOpen){bestOpen=open;bestOpenIndex=c;}
            }
            Debug.Log("[재산정] 튜토리얼 대상 권장 · 유닛 "+bestOpenIndex+" 개방도 "+bestOpen);

            float covered=target==0?0f:(target-uncovered.Count)*100f/target;
            Debug.Log("[재산정] 선택 "+chosen.Count+"개 · 보행 영역 커버 "+covered.ToString("0.0",CultureInfo.InvariantCulture)
                      +"% · 못 덮은 칸 "+uncovered.Count+"/"+target);

            Write(grid,chosen,target,uncovered.Count,candidates.Count);
        }

        private static void Write(Grid grid,List<Candidate> chosen,int reachableCells,int uncovered,int candidateCount)
        {
            Directory.CreateDirectory(OutDir);
            var inv=CultureInfo.InvariantCulture;
            var json=new StringBuilder();
            json.Append("{\n  \"schema\": \"chooguard.extinguisher-placement.v3\",\n");
            json.Append("  \"computedAt\": \""+System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ",inv)+"\",\n");
            json.Append("  \"scene\": \""+ScenePath+"\",\n");
            json.Append("  \"method\": \"씬 콜라이더를 "+Cell.ToString("0.0",inv)+"m 격자로 훑어 바닥과 사람 여유를 확인하고, "
                        +"플레이어 시작점에서 걸어 닿는 영역만 남긴 뒤, 벽면 후보 중 설 자리와 판독면 시야가 확보된 것만 "
                        +"골라 보행거리 "+ReachMetres.ToString("0",inv)+"m 그리디 커버로 선택했다.\",\n");
            json.Append("  \"limits\": \"법정 적합을 주장하지 않는다. NFTC 101 의 보행거리 항목만 계산에 썼고 구획 면적·"
                        +"소화 능력단위 산정은 하지 않았다. 격자 해상도와 단차 상한이 결과를 좌우한다.\",\n");
            json.Append("  \"grid\": { \"cell\": "+Cell.ToString("0.0",inv)+", \"stepLimit\": "+StepLimit.ToString("0.00",inv)
                        +", \"personRadius\": "+PersonRadius.ToString("0.00",inv)
                        +", \"personHeight\": "+PersonHeight.ToString("0.0",inv)+" },\n");
            json.Append("  \"coverage\": { \"reachableCells\": "+reachableCells+", \"uncoveredCells\": "+uncovered
                        +", \"wallCandidates\": "+candidateCount+" },\n");
            json.Append("  \"placements\": [\n");
            for(int i=0;i<chosen.Count;i++)
            {
                var c=chosen[i];
                json.Append("    { \"index\": "+i+", \"serial\": \"BSN-CONC-FE-"+(i+1).ToString("000")+"\", ");
                json.Append("\"position\": { \"x\": "+c.Mount.x.ToString("0.00",inv)+", \"y\": "+c.Mount.y.ToString("0.00",inv)
                            +", \"z\": "+c.Mount.z.ToString("0.00",inv)+" }, ");
                json.Append("\"normal\": { \"x\": "+c.Normal.x+", \"z\": "+c.Normal.y+" }, ");
                json.Append("\"standingSpot\": { \"x\": "+c.Stand.x.ToString("0.00",inv)+", \"z\": "+c.Stand.z.ToString("0.00",inv)+" } }");
                json.Append(i<chosen.Count-1?",\n":"\n");
            }
            json.Append("  ]\n}\n");
            File.WriteAllText(OutFile,json.ToString(),new UTF8Encoding(false));
            Debug.Log("[재산정] 기록 · "+OutFile);

            // 붙여넣을 C# 블록. 배치를 바꾸는 것은 사람이 검토한 뒤에 할 일이므로 여기서 씬을 고치지 않는다.
            var code=new StringBuilder("[재산정] PlacementsV4 후보:\n");
            for(int i=0;i<chosen.Count;i++)
            {
                var c=chosen[i];
                code.Append("            Unit("+i.ToString().PadLeft(2)+","
                            +c.Mount.x.ToString("0.00",inv)+"f,"+c.Mount.y.ToString("0.00",inv)+"f,"
                            +c.Mount.z.ToString("0.00",inv)+"f, "+c.Normal.x+","+c.Normal.y+", false,false,false),\n");
            }
            Debug.Log(code.ToString());
        }

        private static Dictionary<Collider,bool> DisableNonTerrainColliders(Transform player)
        {
            var hidden=new Dictionary<Collider,bool>();
            foreach(var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if(collider==null)continue;
                var root=collider.transform.root;
                bool isPlayer=player!=null&&(collider.transform==player||collider.transform.IsChildOf(player));
                bool isTutorial=root!=null&&(root.name.StartsWith("소화기 · ")||root.name.StartsWith("튜토리얼 · "));
                if(!isPlayer&&!isTutorial)continue;
                hidden[collider]=collider.enabled;
                collider.enabled=false;
            }
            return hidden;
        }
    }
}
