using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Tests.EditMode
{
    public sealed class ObservedCeilingGeometryTests
    {
        const string Native="Assets/ChooGuard/Art/StationInterior/Kit/1F/";
        const string Corrected="Assets/ChooGuard/Art/StationInterior/ArchitectureCorrections/ObservedLowCeiling20261009/";
        [Test]
        public void SavedCorrectionPreservesEveryHigherCeilingFaceAndEveryOriginalFixtureFrame()
        {
            var panels=AssetDatabase.LoadAssetAtPath<Mesh>(Native+"Kit_Ceiling_Kit_Ceiling600.asset");
            var higher=AssetDatabase.LoadAssetAtPath<Mesh>(Corrected+"RetainedHigherPanels.asset");
            CollectionAssert.AreEqual(panels.triangles.Skip(33),higher.triangles);
            CollectionAssert.AreEqual(panels.vertices,higher.vertices);CollectionAssert.AreEqual(panels.normals,higher.normals);
            CollectionAssert.AreEqual(panels.uv,higher.uv);CollectionAssert.AreEqual(panels.tangents,higher.tangents);
            var grid=AssetDatabase.LoadAssetAtPath<Mesh>(Native+"Kit_CeilingGrid.asset");
            var retained=AssetDatabase.LoadAssetAtPath<Mesh>(Corrected+"RetainedFixtureFramesAndHigherGrid.asset");
            var expected=grid.triangles.Skip(234*3).Take(144*3).Concat(grid.triangles.Skip(404*3));
            CollectionAssert.AreEqual(expected,retained.triangles);
            CollectionAssert.AreEqual(grid.vertices,retained.vertices);CollectionAssert.AreEqual(grid.normals,retained.normals);
            CollectionAssert.AreEqual(grid.uv,retained.uv);CollectionAssert.AreEqual(grid.tangents,retained.tangents);
        }
        static double ProjectedArea(Mesh mesh,int[] indices)
        {
            double result=0;var v=mesh.vertices;
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=v[indices[i]];var b=v[indices[i+1]];var c=v[indices[i+2]];
                result+=System.Math.Abs(((double)b.x-a.x)*(c.z-a.z)-((double)b.z-a.z)*(c.x-a.x))*.5;
            }
            return result;
        }
        [Test]
        public void ReliefKeepsTheOriginalCeilingFootprintAndRaisesOnlyTheObservedRecesses()
        {
            var native=AssetDatabase.LoadAssetAtPath<Mesh>(Native+"Kit_Ceiling_Kit_Ceiling600.asset");
            var actual=AssetDatabase.LoadAssetAtPath<Mesh>(Corrected+"ObservedPressedMetalRelief.asset");
            Assert.That(ProjectedArea(actual,actual.triangles),Is.EqualTo(ProjectedArea(native,native.triangles.Take(33).ToArray())).Within(.0003));
            Assert.That(actual.vertexCount,Is.EqualTo(26281));Assert.That(actual.uv.Length,Is.EqualTo(actual.vertexCount));
            Assert.That(actual.vertices.All(v=>v.y>=2.30999f&&v.y<=2.32201f),Is.True);
            Assert.That(actual.vertices.Any(v=>v.y>2.32f),Is.True,"The fitted pressed circles must be actual geometry");
            Assert.That(actual.normals.All(n=>n.y<0&&Mathf.Abs(n.magnitude-1)<.002f),Is.True);
        }
    }
}
