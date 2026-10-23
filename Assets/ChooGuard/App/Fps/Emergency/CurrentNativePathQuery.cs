using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;

// Deliberately limited to the installed Unity6.3 API that exposes the query's
// actual node budget/status/corridor. No project-wide warning policy changes.
#pragma warning disable CS0618
namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Positive-only default-world query for one synchronous construction phase.
    /// It owns no NavMeshData/link and makes no claim of exclusive Data ownership.
    /// Dispose before any historical reference registration or world mutation.
    /// </summary>
    internal sealed class CurrentNativePathQuery : IDisposable
    {
        internal const int NodePool=65535;
        NavMeshQuery query;
        NativeArray<float> costs;
        bool alive;

        internal readonly struct Step
        {
            internal readonly Vector3 From,To;
            internal readonly bool OffMesh;
            internal Step(Vector3 from,Vector3 to,bool offMesh){From=from;To=to;OffMesh=offMesh;}
        }

        internal CurrentNativePathQuery()
        {
            var world=NavMeshWorld.GetDefaultWorld();
            if(!world.IsValid())throw new InvalidOperationException("Explicit native query has no valid default world");
            query=new NavMeshQuery(world,Allocator.Persistent,NodePool);alive=true;
            try
            {
                costs=new NativeArray<float>(32,Allocator.Persistent);
                for(int area=0;area<32;area++)
                {
                    float value=NavMesh.GetAreaCost(area);
                    if(!Finite(value)||value<1f)throw new InvalidOperationException("Explicit native query cannot preserve invalid area cost: "+area);
                    costs[area]=value;
                }
            }
            catch {Dispose();throw;}
        }

        // A successful status is not clearance. Only a complete exact endpoint
        // polygon sequence produces proposals for the existing short-path gates.
        internal bool TryCorridor(Vector3 from,Vector3 to,int agent,int mask,out Step[] steps,out string evidence)
        {
            steps=Array.Empty<Step>();evidence="pool="+NodePool+"; source="+Exact(from)+"; target="+Exact(to)+"; agent="+agent+"; mask="+mask;
            if(!alive)throw new ObjectDisposedException(nameof(CurrentNativePathQuery));
            if(!Finite(from)||!Finite(to))return Reject(ref evidence,"nonfinite-endpoint");
            var extents=new Vector3(.6f,.35f,.6f);
            var start=query.MapLocation(from,extents,agent,mask);
            var end=query.MapLocation(to,extents,agent,mask);
            if(!Location(start,from,agent)||!Location(end,to,agent))return Reject(ref evidence,"invalid-or-different-endpoint-mapping");
            evidence+="; mappedSource="+Exact(start.position)+"; mappedTarget="+Exact(end.position)+
                "; ephemeralPolygonHashes="+start.polygon.GetHashCode()+"/"+end.polygon.GetHashCode();
            var status=query.BeginFindPath(start,end,mask,costs);
            evidence+="; begin="+status;
            if(status!=PathQueryStatus.InProgress)return Reject(ref evidence,"begin-not-exact-InProgress");
            long iterations=0;int updates=0;
            do
            {
                status=query.UpdateFindPath(4096,out int performed);iterations+=performed;updates++;
                if(performed<0||(status==PathQueryStatus.InProgress&&performed==0))
                    return Reject(ref evidence,"invalid-or-stalled-update; status="+status);
            }
            while(status==PathQueryStatus.InProgress);
            evidence+="; terminal="+status+"; iterations="+iterations+"; updates="+updates;
            if(status==(PathQueryStatus.Success|PathQueryStatus.PartialResult))return Incomplete(ref evidence,"terminal-partial-corridor");
            if(status!=PathQueryStatus.Success)return Reject(ref evidence,"terminal-not-exact-Success");
            status=query.EndFindPath(out int size); // Exactly once.
            evidence+="; end="+status+"; polygonCount="+size;
            if(size<=0||size>NodePool)return Reject(ref evidence,"invalid-end-size");
            if(status==(PathQueryStatus.Success|PathQueryStatus.PartialResult))return Incomplete(ref evidence,"end-partial-corridor");
            if(status!=PathQueryStatus.Success)return Reject(ref evidence,"end-not-exact-Success");
            using(var polygons=new NativeArray<PolygonId>(size,Allocator.Persistent))
            {
                int copied=query.GetPathResult(new NativeSlice<PolygonId>(polygons));
                evidence+="; copied="+copied;
                if(copied!=size||!polygons[0].Equals(start.polygon)||!polygons[size-1].Equals(end.polygon))
                    return Reject(ref evidence,"truncated-or-not-exact-target-corridor");
                for(int i=0;i<size;i++)
                    if(!query.IsValid(polygons[i])||query.GetAgentTypeIdForPolygon(polygons[i])!=agent)
                        return Reject(ref evidence,"invalid-or-stale-polygon="+i);
                if(query.GetPolygonType(polygons[0])!=NavMeshPolyTypes.Ground||query.GetPolygonType(polygons[size-1])!=NavMeshPolyTypes.Ground)
                    return Reject(ref evidence,"endpoint-not-ground");
                var result=new List<Step>();result.Add(new Step(from,start.position,false));
                int at=0;
                while(at+1<size)
                {
                    if(!query.IsValid(polygons[at]))return Reject(ref evidence,"stale-consumed-polygon="+at);
                    var kind=query.GetPolygonType(polygons[at+1]);
                    if(kind==NavMeshPolyTypes.Ground)
                    {
                        if(!Portal(polygons[at],polygons[at+1],true,out var midpoint)||
                            !Projected(midpoint,polygons[at],agent,out var left)||!Projected(midpoint,polygons[at+1],agent,out var right))
                            return Reject(ref evidence,"unresolved-ground-portal="+at);
                        result.Add(new Step(result[result.Count-1].To,left,false));
                        result.Add(new Step(left,right,false));at++;
                    }
                    else if(kind==NavMeshPolyTypes.OffMeshConnection)
                    {
                        if(at+2>=size||query.GetPolygonType(polygons[at+2])!=NavMeshPolyTypes.Ground||
                            !Portal(polygons[at],polygons[at+1],false,out var entry)||
                            !Portal(polygons[at+1],polygons[at+2],false,out var exit)||
                            !Projected(entry,polygons[at],agent,out var entryFloor)||!Projected(exit,polygons[at+2],agent,out var exitFloor))
                            return Reject(ref evidence,"unresolved-offmesh-triple="+at);
                        result.Add(new Step(result[result.Count-1].To,entryFloor,false));
                        // The caller must resolve THIS ordered native seam to one
                        // exact existing directed typed owner arc, never adjacency.
                        result.Add(new Step(entry,exit,true));
                        result.Add(new Step(exit,exitFloor,false));at+=2;
                    }
                    else return Reject(ref evidence,"unknown-polygon-kind="+at);
                }
                if(!query.IsValid(start)||!query.IsValid(end))return Reject(ref evidence,"stale-final-endpoint");
                result.Add(new Step(result[result.Count-1].To,to,false));
                steps=result.ToArray();evidence+="; completeExactTarget=true; intermediateProposals="+steps.Length+
                    "; defaultWorldCorridorNotDataOwnership=true; shortTypedSurfaceGuardsStillRequired=true";
                return true;
            }
        }

        bool Location(NavMeshLocation location,Vector3 proposed,int agent)=>query.IsValid(location)&&
            query.GetAgentTypeIdForPolygon(location.polygon)==agent&&Finite(location.position)&&
            Vector3.Distance(location.position,proposed)<=.6f&&Mathf.Abs(location.position.y-proposed.y)<=.35f;
        bool Projected(Vector3 proposed,PolygonId polygon,int agent,out Vector3 position)
        {
            var location=query.CreateLocation(proposed,polygon);position=location.position;
            return location.polygon.Equals(polygon)&&Location(location,proposed,agent);
        }
        bool Portal(PolygonId a,PolygonId b,bool ground,out Vector3 midpoint)
        {
            midpoint=default;
            if(!query.IsValid(a)||!query.IsValid(b)||!query.GetPortalPoints(a,b,out var left,out var right)||!Finite(left)||!Finite(right))return false;
            if(ground&&left.Equals(right))return false;
            midpoint=(left+right)*.5f;return Finite(midpoint);
        }
        // Only this typed expected outcome is ordinary non-admission. Invalid,
        // stale, malformed or API-error states must abort the whole construction.
        static bool Incomplete(ref string evidence,string reason){evidence+="; outcome=EXPECTED_INCOMPLETE_CORRIDOR; rejected="+reason;return false;}
        static bool Reject(ref string evidence,string reason)
        {evidence+="; outcome=HARD_NATIVE_QUERY_FAILURE; rejected="+reason;throw new InvalidOperationException(evidence);}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        static string Exact(Vector3 p)=>"["+p.x.ToString("R",CultureInfo.InvariantCulture)+","+p.y.ToString("R",CultureInfo.InvariantCulture)+","+p.z.ToString("R",CultureInfo.InvariantCulture)+"]";
        public void Dispose()
        {
            if(!alive)return;alive=false;
            if(costs.IsCreated)costs.Dispose();
            query.Dispose();
        }
    }
}
#pragma warning restore CS0618
