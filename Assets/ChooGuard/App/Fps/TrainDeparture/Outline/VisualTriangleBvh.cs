// Adapted from Godot TriangleMesh::_create_bvh (triangle_mesh.cpp:36-85), intersect_ray
// (:294-395), AABB::find_intersects_ray (aabb.cpp:121-181), pin ed1daf0bf001b61586d9930840f2f1394092c079.
// Copyright (c) 2014-present Godot Engine contributors; 2007-2014 Juan Linietsky, Ariel Manzur.
// MIT; full notice in ThirdParty/Godot-LICENSE.txt.
// Changes: original indexed vertices (NO source snapping), leaves of <=8 triangles, managed
// arrays/stack instead of pointers/alloca, bounded world-t ray query. Godot sort_array.h
// median_of_3_index/partitioner/introselect/nth_element port (57-75,168-245,309-313);
// managed Array.Sort fallback replaces heap/insertion fallback only at depth limit/tiny ranges.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChooGuard.App.Fps.TrainDeparture
{
    [Serializable] public struct VisualTriangleRef { public int Submesh, Offset; }
    [Serializable] public struct VisualBvhNode
    {
        public Vector3 Min, Max;
        public int Left, Right, First, Count;
    }
    [Serializable] public sealed class VisualTriangleBvh
    {
        public VisualBvhNode[] Nodes = Array.Empty<VisualBvhNode>();
        public VisualTriangleRef[] Triangles = Array.Empty<VisualTriangleRef>();
        public int Root = -1, Depth;
        struct BuildFace { public VisualTriangleRef Triangle; public Vector3 Min, Max, Center; }
        sealed class AxisOrder : IComparer<BuildFace>
        {
            public int Axis;
            public int Compare(BuildFace a, BuildFace b) => a.Center[Axis].CompareTo(b.Center[Axis]);
        }
        public static VisualTriangleBvh Build(Vector3[] vertices, VisualSubmesh[] submeshes)
        {
            int total = 0;
            foreach (var sub in submeshes) total = checked(total + sub.Indices.Length / 3);
            var result = new VisualTriangleBvh();
            if (total == 0) return result;
            var faces = new BuildFace[total]; int f = 0;
            for (int s = 0; s < submeshes.Length; s++)
            {
                var indices = submeshes[s].Indices;
                if (indices.Length % 3 != 0) throw new ArgumentException("Non-triangle index count");
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var a = vertices[indices[i]]; var b = vertices[indices[i+1]]; var c = vertices[indices[i+2]];
                    Vector3 min = Vector3.Min(a, Vector3.Min(b,c)), max = Vector3.Max(a, Vector3.Max(b,c));
                    faces[f++] = new BuildFace { Triangle = new VisualTriangleRef { Submesh=s, Offset=i }, Min=min, Max=max, Center=(min+max)*.5f };
                }
            }
            var nodes = new List<VisualBvhNode>(Math.Max(1,total/2));
            var comparers = new[] {new AxisOrder {Axis=0},new AxisOrder {Axis=1},new AxisOrder {Axis=2}};
            result.Root = BuildNode(faces, 0, total, 1, nodes, comparers, ref result.Depth);
            result.Nodes = nodes.ToArray(); result.Triangles = new VisualTriangleRef[total];
            for (int i = 0; i < total; i++) result.Triangles[i] = faces[i].Triangle;
            return result;
        }
        static int BuildNode(BuildFace[] faces, int from, int count, int depth, List<VisualBvhNode> nodes, AxisOrder[] comparers, ref int maxDepth)
        {
            maxDepth = Math.Max(depth,maxDepth);
            Vector3 min=faces[from].Min, max=faces[from].Max;
            for (int i=1;i<count;i++) {min=Vector3.Min(min,faces[from+i].Min); max=Vector3.Max(max,faces[from+i].Max);}
            var node = new VisualBvhNode {Min=min,Max=max,Left=-1,Right=-1,First=from,Count=count};
            if (count>8)
            {
                Vector3 size=max-min; int axis=size.x>size.y ? (size.x>size.z?0:2) : (size.y>size.z?1:2);
                // Godot longest-axis median subdivision with production introselect partition.
                SelectMedian(faces,from,count,axis,comparers[axis]);
                node.Left=BuildNode(faces,from,count/2,depth+1,nodes,comparers,ref maxDepth);
                node.Right=BuildNode(faces,from+count/2,count-count/2,depth+1,nodes,comparers,ref maxDepth);
                node.Count=0;
            }
            int index=nodes.Count;nodes.Add(node);return index;
        }
        static void SelectMedian(BuildFace[] faces,int first,int count,int axis,AxisOrder comparer)
        {
            int last=first+count,nth=first+count/2,depth=0;
            for(int n=count;n>1;n>>=1)depth++;
            depth*=2;
            while(last-first>3)
            {
                if(depth--==0){Array.Sort(faces,first,last-first,comparer);return;}
                int a=first,b=first+(last-first)/2,c=last-1;
                float av=faces[a].Center[axis],bv=faces[b].Center[axis],cv=faces[c].Center[axis];
                int pivot=av<bv?(bv<cv?b:av<cv?c:a):(av<cv?a:bv<cv?c:b);
                float key=faces[pivot].Center[axis];int left=first,right=last;
                while(true)
                {
                    while(faces[left].Center[axis]<key)left++;
                    right--;
                    while(key<faces[right].Center[axis])right--;
                    if(left>=right)break;
                    var temp=faces[left];faces[left]=faces[right];faces[right]=temp;left++;
                }
                if(left<=nth)first=left;else last=left;
            }
            Array.Sort(faces,first,last-first,comparer);
        }
        // Adapted Godot slab rejection. Direction remains unnormalised so maxDistance is world t.
        public static bool Intersects(Vector3 min, Vector3 max, Vector3 origin, Vector3 direction, float maxDistance)
        {
            float tmin=-1e20f,tmax=1e20f;
            for (int i=0;i<3;i++)
            {
                if(direction[i]==0) {if(origin[i]<min[i] || origin[i]>max[i])return false;}
                else
                {
                    float t1=(min[i]-origin[i])/direction[i],t2=(max[i]-origin[i])/direction[i];
                    if(t1>t2){float temp=t1;t1=t2;t2=temp;}
                    if(t1>=tmin)tmin=t1;
                    if(t2<tmax){if(t2<0)return false;tmax=t2;}
                    if(tmin>tmax)return false;
                }
            }
            return tmin<=maxDistance;
        }
        public int Collect(Vector3 origin,Vector3 direction,float maxDistance,List<VisualTriangleRef> output,int[] stack)
        {
            output.Clear(); if(Root<0)return 0;
            if(stack.Length<Depth+1)throw new ArgumentException("BVH traversal stack too small");
            int top=0,visited=0;stack[top++]=Root;
            // Godot explicit child traversal with caller-owned stack; leaves return source triangle IDs for alpha/cull admission.
            while(top>0)
            {
                var node=Nodes[stack[--top]];visited++;
                if(!Intersects(node.Min,node.Max,origin,direction,maxDistance))continue;
                if(node.Count>0)for(int i=0;i<node.Count;i++)output.Add(Triangles[node.First+i]);
                else {stack[top++]=node.Right;stack[top++]=node.Left;}
            }
            return visited;
        }
        public long PayloadBytes => Nodes.LongLength*40 + Triangles.LongLength*8;
    }
}
