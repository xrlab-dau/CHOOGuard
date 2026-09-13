using System;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;

public sealed class CrowdPhaseTests
{
    [Test]
    public void ComposedClockMatchesFullSnapshotNormalizationAndRejectsEmptyReusedOrRestoredTokens()
    {
        var normal=new CrowdMotionModel(new CrowdDefinition {ProfileId="clock-fixture",Spaces=new[]{new CrowdSpace {Id="floor"}}},
            new[]{new CrowdAgent {Id="a",ContactSpaceId="floor",RegionId="room",FrameId="world",SurfaceId="floor",Position=new CrowdVector(1,0)}});
        var measured=normal.Fork();var token=measured.BeginComposedTick();
        Assert.Throws<InvalidOperationException>(()=>token.Complete());
        Assert.Throws<InvalidOperationException>(()=>measured.BeginComposedTick());
        foreach(var seconds in new[]{.013,.017,.02})
        {
            Assert.That(normal.TryAdvance(seconds,null,out var a),Is.True,a.Failure);
            Assert.That(measured.TryAdvance(seconds,null,out var b),Is.True,b.Failure);
            Assert.That(BitConverter.DoubleToInt64Bits(b.MinimumSweptGapM),Is.EqualTo(BitConverter.DoubleToInt64Bits(a.MinimumSweptGapM)));
        }
        var expected=normal.ExportSnapshot();expected.Tick=1;normal=CrowdMotionModel.FromSnapshot(expected);
        token.Complete();Assert.That(measured.ExportCheckpoint(),Is.EqualTo(normal.ExportCheckpoint()));
        Assert.Throws<InvalidOperationException>(()=>token.Complete());
        var before=measured.ExportCheckpoint();var stale=measured.BeginComposedTick();
        Assert.That(measured.TryAdvance(.01,null,out _),Is.True);
        Assert.That(measured.TryRestore(CrowdMotionModel.FromCheckpoint(before).ExportSnapshot(),out var failure),Is.True,failure);
        Assert.Throws<InvalidOperationException>(()=>stale.Complete());
        Assert.That(measured.ExportCheckpoint(),Is.EqualTo(before));
    }
    [Test]
    public void DiagnosticPhasesKeepExactCheckpointAndSweptMinimumGap()
    {
        var normal=new CrowdMotionModel(new CrowdDefinition {ProfileId="phase-fixture",Spaces=new[]{new CrowdSpace {Id="floor"}}},
            new[]{new CrowdAgent {Id="a",ContactSpaceId="floor",RegionId="room",FrameId="world",SurfaceId="floor",
                Position=new CrowdVector(-1,0),Goal=new CrowdVector(1,0)},new CrowdAgent {Id="b",ContactSpaceId="floor",RegionId="room",FrameId="world",SurfaceId="floor",
                Position=new CrowdVector(1,0),Goal=new CrowdVector(-1,0)}});
        var measured=normal.Fork(); measured.ProfilePhases=true;
        for(var i=0;i<10;i++)
        {
            Assert.That(normal.TryAdvance(.05,null,out var a),Is.True,a.Failure);
            Assert.That(measured.TryAdvance(.05,null,out var b),Is.True,b.Failure);
            Assert.That(measured.ExportCheckpoint(),Is.EqualTo(normal.ExportCheckpoint()));
            Assert.That(BitConverter.DoubleToInt64Bits(b.MinimumSweptGapM),Is.EqualTo(BitConverter.DoubleToInt64Bits(a.MinimumSweptGapM)));
            Assert.That(measured.Phases.TotalMilliseconds,Is.GreaterThan(0));
            Assert.That(measured.Phases.CsmBodies,Is.GreaterThan(0));
        }
    }
}
