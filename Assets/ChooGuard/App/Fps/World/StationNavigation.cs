using System;
using System.IO;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using UnityEngine;
namespace ChooGuard.App.Fps.World
{
    // 역사 보행 경로 질의. 베이크된 navmesh 를 읽어 두 점 사이의 경로를 돌려준다.
    //
    // 길찾기를 직접 구현하지 않는다 — DotRecast 는 이미 이 저장소의 의존성이고 구 RTS 계층이
    // 같은 방식으로 쓴다(MvpTeamNavigation). 그 코드를 그대로 쓰지 않는 이유는 두 가지다.
    // 층 높이를 y≈5.05 로 하드코딩하고 있고(대합실 바닥은 7.00), ChooGuard.App.Mvp 네임스페이스라
    // FPS 계층이 RTS 계층에 의존하게 된다 — 튜토리얼이 비상 계층을 참조하지 않는 것과 같은 규율이다.
    //
    // 실패를 삼키지 않는다. 경로를 못 찾으면 왜 못 찾았는지를 돌려준다 — 호출하는 쪽이
    // "못 감" 과 "막힘" 을 구분해 보고해야 하기 때문이다.
    [DisallowMultipleComponent]
    public sealed class StationNavigation : MonoBehaviour
    {
        public TextAsset NavData;
        // 베이크한 기하의 지문. 씬이 바뀌었는데 오래된 navmesh 를 쓰고 있는지 사람이 알아볼 수 있게 남긴다.
        public string GeometryDigest="";
        // 끝점을 navmesh 위로 끌어당길 때 허용하는 반경. 선 자리가 폴리곤 경계 밖일 수 있다.
        public Vector3 SnapExtent=new Vector3(1f,2f,1f);

        public string LastReason { get; private set; }="navigation_not_loaded";
        public bool Loaded=>query!=null;
        private DtNavMeshQuery query;
        private const int MaxPolygons=256,MaxCorners=256;

        public void Reload(){query=null;LastReason="navigation_not_loaded";}

        private static RcVec3f Rc(Vector3 p)=>new RcVec3f(p.x,p.y,p.z);
        private static Vector3 Unity(RcVec3f p)=>new Vector3(p.X,p.Y,p.Z);

        public bool TryPlan(Vector3 start,Vector3 end,out Vector3[] corners,out string reason)
        {
            corners=Array.Empty<Vector3>();
            if(NavData==null){reason=LastReason="navigation_not_baked";return false;}
            try
            {
                if(query==null)
                {
                    using(var reader=new BinaryReader(new MemoryStream(NavData.bytes)))
                    {
                        var data=new DtMeshDataReader().Read(reader,6);
                        var mesh=new DtNavMesh();
                        if(!mesh.Init(data,6,0).Succeeded())throw new InvalidDataException("navmesh_init_failed");
                        query=new DtNavMeshQuery(mesh);
                    }
                }
                var filter=new DtQueryDefaultFilter();
                var extent=Rc(SnapExtent);
                var a=query.FindNearestPoly(Rc(start),extent,filter,out var startRef,out var startPoint,out var _);
                var b=query.FindNearestPoly(Rc(end),extent,filter,out var endRef,out var endPoint,out var _);
                if(!a.Succeeded()||startRef==0){reason=LastReason="start_outside_walkable_surface";return false;}
                if(!b.Succeeded()||endRef==0){reason=LastReason="target_outside_walkable_surface";return false;}

                Span<long> polys=new long[MaxPolygons];
                if(!query.FindPath(startRef,endRef,startPoint,endPoint,filter,polys,out int polyCount,MaxPolygons)
                        .Succeeded()||polyCount==0)
                {reason=LastReason="no_path_between_points";return false;}

                Span<DtStraightPath> straight=new DtStraightPath[MaxCorners];
                if(!query.FindStraightPath(startPoint,endPoint,polys,polyCount,straight,out int cornerCount,
                                           MaxCorners,0).Succeeded()||cornerCount==0)
                {reason=LastReason="straight_path_failed";return false;}

                // 마지막 폴리곤이 목표 폴리곤이 아니면 부분 경로다 — 따라가도 도착하지 못한다.
                if(polys[polyCount-1]!=endRef)
                {
                    // 폴리곤 수를 실어 보낸다. 1 이면 시작 폴리곤이 고립된 것이고, 여럿이면
                    // 중간에서 끊긴 것이다 — 원인이 전혀 다르므로 구분해야 한다.
                    reason=LastReason="partial_path_target_unreachable(polys="+polyCount+")";
                    return false;
                }

                corners=new Vector3[cornerCount];
                for(int i=0;i<cornerCount;i++)corners[i]=Unity(straight[i].pos);
                reason=LastReason="";
                return true;
            }
            catch(Exception error)
            {
                // 조용히 실패하지 않는다. 읽기·초기화 실패는 데이터 문제이므로 크게 남긴다.
                Debug.LogError("[길찾기] navmesh 를 읽지 못했습니다 · "+error.Message,this);
                reason=LastReason="navigation_data_unreadable";
                return false;
            }
        }
    }
}
