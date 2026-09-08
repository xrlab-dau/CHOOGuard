using System.Collections.Generic;
using UnityEngine;
namespace ChooGuard.Foundation.Demo
{
    // Small CPU walk grid. No NavMesh package, baking, or unsafe straight-line chase through walls.
    public sealed class DemoWalkGraph : MonoBehaviour
    {
        public const float Spacing = .75f;
        private readonly Dictionary<Vector2Int, Vector3> nodes = new Dictionary<Vector2Int, Vector3>();
        private readonly Dictionary<Vector2Int, List<Vector2Int>> edges = new Dictionary<Vector2Int, List<Vector2Int>>();
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private Bounds[] restrictedAreas=new Bounds[0];
        public int Version {get;private set;}
        public void SetRestrictedAreas(Bounds[] areas){restrictedAreas=(Bounds[])areas.Clone();Rebuild();}
        private bool Restricted(Vector3 p)
        {
            foreach(var area in restrictedAreas)
                if(p.x>=area.min.x-.3f&&p.x<=area.max.x+.3f&&p.z>=area.min.z-.3f&&p.z<=area.max.z+.3f)return true;
            return false;
        }
        public int NodeCount { get { return nodes.Count; } }
        public void Rebuild()
        {
            nodes.Clear(); edges.Clear(); Version++; Physics.SyncTransforms();
            for (var x=-23; x<=23; x++) for (var z=-9; z<=30; z++)
            {
                var p = new Vector3(x * Spacing, .1f, z * Spacing);
                if(Restricted(p))continue;
                if (!Physics.Raycast(p + Vector3.up * .2f, Vector3.down, .5f, 1, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.CheckCapsule(p + Vector3.up * .32f, p + Vector3.up * 1.38f, .30f, 1, QueryTriggerInteraction.Ignore)) continue;
                nodes[new Vector2Int(x,z)] = p;
            }
            foreach (var pair in nodes)
            {
                var neighbors = new List<Vector2Int>();
                foreach (var d in Directions)
                {
                    var key = pair.Key + d;
                    if (nodes.ContainsKey(key) && Clear(pair.Value,nodes[key])) neighbors.Add(key);
                }
                edges[pair.Key] = neighbors;
            }
        }
        public bool Clear(Vector3 start, Vector3 end)
        {
            if(Physics.CheckCapsule(start+Vector3.up*.32f,start+Vector3.up*1.38f,.29f,1,QueryTriggerInteraction.Ignore))return false;
            var delta=end-start; delta.y=0;
            if(delta.sqrMagnitude>.0001f && Physics.CapsuleCast(start+Vector3.up*.32f,
                start+Vector3.up*1.38f,.29f,delta.normalized,delta.magnitude,1,QueryTriggerInteraction.Ignore))return false;
            if(Physics.CheckCapsule(end+Vector3.up*.32f,end+Vector3.up*1.38f,.29f,1,QueryTriggerInteraction.Ignore))return false;
            var samples=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.25f));
            for(var i=0;i<=samples;i++)
            {
                var p=Vector3.Lerp(start,end,(float)i/samples);p.y=.3f;
                if(Restricted(p))return false;
                RaycastHit floor;
                if(!Physics.Raycast(p,Vector3.down,out floor,.5f,1,QueryTriggerInteraction.Ignore)||floor.normal.y<.95f)return false;
            }
            return true;
        }
        private Vector2Int? Nearest(Vector3 p)
        {
            Vector2Int? best=null; var distance=float.MaxValue;
            foreach(var n in nodes)
            {
                var delta=n.Value-p;delta.y=0;var sq=delta.sqrMagnitude;
                if(sq<distance && sq<16 && Clear(new Vector3(p.x,.1f,p.z),n.Value)) {best=n.Key;distance=sq;}
            }
            return best;
        }
        public Vector3[] Route(Vector3 from, Vector3 to)
        {
            if(nodes.Count==0) Rebuild();
            var a=Nearest(from);var b=Nearest(to);
            if(!a.HasValue||!b.HasValue)return new Vector3[0];
            var queue=new Queue<Vector2Int>();var came=new Dictionary<Vector2Int,Vector2Int>();
            queue.Enqueue(a.Value);came[a.Value]=a.Value;
            while(queue.Count>0)
            {
                var at=queue.Dequeue();if(at==b.Value)break;
                foreach(var next in edges[at])if(!came.ContainsKey(next)){came[next]=at;queue.Enqueue(next);}
            }
            if(!came.ContainsKey(b.Value))return new Vector3[0];
            var route=new List<Vector3>();var cursor=b.Value;
            while(cursor!=a.Value){route.Add(nodes[cursor]);cursor=came[cursor];}
            route.Add(nodes[a.Value]);route.Reverse();
            // Compress collinear nodes, retaining every corner.
            for(var i=route.Count-2;i>0;i--)
                if(Vector3.Dot((route[i]-route[i-1]).normalized,(route[i+1]-route[i]).normalized)>.999f)route.RemoveAt(i);
            return route.ToArray();
        }
    }
}
