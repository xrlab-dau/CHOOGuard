#if UNITY_INCLUDE_TESTS
using System;
using ChooGuard.Circulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.EditMode
{
    public sealed class PlatformEscalatorSourceAbsenceTests
    {
        GameObject root;Mesh mesh;
        PlatformEscalatorRun Run(int count)
        {
            root=new GameObject("Source absence contract");root.SetActive(false);
            var run=root.AddComponent<PlatformEscalatorRun>();run.Treads=new Transform[count];run.ObservedOrigins=new Vector3[count];
            run.SourcePitch=.4f;run.ExposureAxis=Vector3.forward;run.ExposureMinimum=-2;run.ExposureMaximum=2;return run;
        }
        Transform Tread(PlatformEscalatorRun run,int index)
        {
            if(mesh==null){mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.forward};mesh.triangles=new[]{0,1,2};mesh.RecalculateBounds();}
            var tread=new GameObject("Exact source tread "+index,typeof(MeshFilter),typeof(MeshRenderer));tread.transform.SetParent(root.transform,false);
            tread.GetComponent<MeshFilter>().sharedMesh=mesh;run.Treads[index]=tread.transform;return tread.transform;
        }
        [TearDown]public void Cleanup(){if(root!=null)UnityEngine.Object.DestroyImmediate(root);if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);}
        [Test]public void AllNullRunWithoutRenderGeometryIsUnavailableAndNeverPreparedOrAnimated()
        {
            var run=Run(41);
            for(int i=0;i<7;i++){var ride=new GameObject("Retained trigger "+i,typeof(BoxCollider));ride.transform.SetParent(root.transform,false);ride.GetComponent<BoxCollider>().isTrigger=true;}
            run.Prepare();Assert.That(run.SourceAvailable,Is.False);Assert.That(run.IsBound,Is.False);Assert.That(run.AnimatedOriginalPartCount,Is.Zero);
            run.ApplyMotion();run.ApplyDistance(3.25);Assert.That(run.IsBound,Is.False);Assert.That(run.CurrentPhase,Is.Zero);Assert.That(root.GetComponentsInChildren<MeshFilter>(true),Is.Empty);
        }
        [Test]public void PartialNullSourceStillRejectsInsteadOfBecomingUnavailable()
        {var run=Run(3);Tread(run,0);Tread(run,2);Assert.Throws<InvalidOperationException>(()=>run.Prepare());Assert.That(run.IsBound,Is.False);}
        [Test]public void NullReferencesWithExistingRenderGeometryStillReject()
        {var run=Run(2);Tread(run,0);run.Treads[0]=null;Assert.Throws<InvalidOperationException>(()=>run.Prepare());Assert.That(run.IsBound,Is.False);}
        [Test]public void HealthySourceBindsAndPreservesOriginalMeshAndRestoration()
        {
            var run=Run(2);var first=Tread(run,0);Tread(run,1);run.ObservedOrigins[1]=new Vector3(0,-.2f,.35f);run.Treads[1].localPosition=run.ObservedOrigins[1];
            run.Prepare();Assert.That(run.SourceAvailable,Is.True);Assert.That(run.IsBound,Is.True);Assert.That(run.AnimatedOriginalPartCount,Is.EqualTo(2));
            run.ApplyDistance(0);Assert.That(first.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));run.Restore();Assert.That(run.IsBound,Is.False);
        }
    }
}
#endif
