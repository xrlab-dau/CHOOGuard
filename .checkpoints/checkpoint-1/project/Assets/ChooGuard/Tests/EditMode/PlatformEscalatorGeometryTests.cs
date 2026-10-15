using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ChooGuard.Circulation;
using ChooGuard.Editor.Twin;

namespace ChooGuard.Tests.EditMode
{
    public sealed class PlatformEscalatorGeometryTests
    {
        [Test]
        public void RemovingExactFacesPreservesAllOtherNativeChannelsAndSubmeshOrder()
        {
            var source=new Mesh { vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.forward,Vector3.one,Vector3.right+Vector3.forward},
                normals=Enumerable.Repeat(Vector3.up,6).ToArray(),tangents=Enumerable.Repeat(new Vector4(1,0,0,-1),6).ToArray(),
                uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one,Vector2.right*.5f,Vector2.up*.5f},
                colors=Enumerable.Repeat(Color.blue,6).ToArray(),subMeshCount=2};
            source.SetUVs(4,Enumerable.Range(0,6).Select(i=>new Vector4(i,i+1,i+2,i+3)).ToList());
            source.SetTriangles(new[]{0,1,2,1,3,2},0);source.SetTriangles(new[]{2,3,4,3,5,4},1);
            Mesh separated=null;
            try
            {
                separated=ExistingPlatformEscalatorSetup.RemoveExactTriangles(source,new[]{1,2});
                CollectionAssert.AreEqual(source.vertices,separated.vertices);CollectionAssert.AreEqual(source.normals,separated.normals);
                CollectionAssert.AreEqual(source.tangents,separated.tangents);CollectionAssert.AreEqual(source.uv,separated.uv);
                CollectionAssert.AreEqual(source.colors,separated.colors);
                var a=new System.Collections.Generic.List<Vector4>();var b=new System.Collections.Generic.List<Vector4>();source.GetUVs(4,a);separated.GetUVs(4,b);CollectionAssert.AreEqual(a,b);
                CollectionAssert.AreEqual(new[]{0,1,2},separated.GetTriangles(0));CollectionAssert.AreEqual(new[]{3,5,4},separated.GetTriangles(1));
                Assert.That(source.triangles.Length,Is.EqualTo(12));
                Assert.Throws<ArgumentException>(()=>ExistingPlatformEscalatorSetup.RemoveExactTriangles(source,new[]{0,0}));
                Assert.Throws<ArgumentException>(()=>ExistingPlatformEscalatorSetup.RemoveExactTriangles(source,new[]{4}));
            }
            finally { UnityEngine.Object.DestroyImmediate(source);if(separated!=null)UnityEngine.Object.DestroyImmediate(separated); }
        }

        [Test]
        public void NativeBoundaryClipPreservesUvAndNeverAddsAClosedCap()
        {
            var source=new Mesh { vertices=new[]{new Vector3(-1,0,0),new Vector3(1,0,0),new Vector3(1,0,1),new Vector3(-1,0,1)},
                normals=Enumerable.Repeat(Vector3.up,4).ToArray(),uv=new[]{new Vector2(-1,0),new Vector2(1,0),new Vector2(1,1),new Vector2(-1,1)},
                triangles=new[]{0,2,1,0,3,2} };
            var result=new Mesh();var frame=new GameObject("Native boundary frame");var run=new GameObject("Observed source run");
            try
            {
                PlatformEscalatorRun.ClipMesh(source,frame.transform,run.transform,Vector3.right,0,.5f,result);
                Assert.That(result.triangles.Length,Is.GreaterThan(0));
                foreach(var p in result.vertices){Assert.That(p.x,Is.InRange(-.00001f,.50001f));Assert.That(p.y,Is.Zero);}
                for(int i=0;i<result.vertexCount;i++)Assert.That(Vector2.Distance(result.uv[i],new Vector2(result.vertices[i].x,result.vertices[i].z)),Is.LessThan(.00001f));
                foreach(var n in result.normals)Assert.That(n,Is.EqualTo(Vector3.up));
                float area=0;var v=result.vertices;var t=result.triangles;
                for(int i=0;i<t.Length;i+=3)area+=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).magnitude*.5f;
                Assert.That(area,Is.EqualTo(.5f).Within(.00001f));
                Assert.That(source.vertexCount,Is.EqualTo(4));Assert.That(source.triangles.Length,Is.EqualTo(6));
            }
            finally{UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(result);UnityEngine.Object.DestroyImmediate(frame);UnityEngine.Object.DestroyImmediate(run);}
        }

        [Test]
        public void SourcePathCollapseKeepsCurvesAndNeverClosesHighToLowAcrossSpace()
        {
            var source=new[]{new Vector3(0,7,0),new Vector3(0,7,1),new Vector3(0,6.8f,1.4f),new Vector3(0,6.6f,1.8f),new Vector3(0,6.4f,2.2f),new Vector3(0,6.3f,2.6f),new Vector3(0,6.3f,3)};
            var path=ExistingPlatformEscalatorSetup.CollapseCollinear(source);
            Assert.That(path.Length,Is.EqualTo(5));Assert.That(path[0],Is.EqualTo(source[0]));Assert.That(path.Last(),Is.EqualTo(source.Last()));
            Assert.That(path,Does.Contain(source[1]));Assert.That(path,Does.Contain(source[4]));Assert.That(path,Does.Contain(source[5]));
            Assert.That(PlatformEscalatorRun.SampleObservedOrigin(source,2.5f),Is.EqualTo((source[2]+source[3])*.5f));
            Assert.Throws<ArgumentException>(()=>PlatformEscalatorRun.SampleObservedOrigin(source,float.NaN));
        }
    }
}
