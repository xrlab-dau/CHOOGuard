using System;
using System.Linq;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;

namespace ChooGuard.Foundation.Tests
{
    public sealed class CrowdWallOptimizationTests
    {
        [Test] public void PrunedWallsRetainExactLargeRosterTrajectoryAndSweptMinimum()
        {
            var bodies=Enumerable.Range(0,120).Select(i=>new CrowdAgent { Id="body-"+i,ContactSpaceId="world",RegionId="region-"+(i%13),FrameId="world",SurfaceId="floor",
                Position=new CrowdVector((i%12)*1.4,(i/12)*1.4),RadiusM=i<100 ? .41 : .3,HeightM=1.8,FootElevationM=(i%3)*4,PreferredSpeedMS=1.2,Pinned=i%10==0 }).ToArray();
            var definition=new CrowdDefinition { ProfileId="wall-pruning-regression",Parameters=new CrowdParameters { MaxAgents=120 },Spaces=new[] {new CrowdSpace {Id="world"}},
                Walls=Enumerable.Range(0,1200).Select(i=>new CrowdWall {Id="wall-"+i,ContactSpaceId="world",A=new CrowdVector(50+i,0),B=new CrowdVector(50+i,20)}).ToArray() };
            var reference=new CrowdMotionModel(definition,bodies,91) { Optimization=CrowdMotionModel.WallOptimization.None };
            var candidate=new CrowdMotionModel(definition,bodies,91);
            var intents=bodies.Select(b=>new CrowdIntent {AgentId=b.Id,Mode=CrowdIntentMode.DesiredVelocity,DesiredVelocity=new CrowdVector(.2,.05)}).ToArray();
            for (var i=0;i<4;i++)
            {
                Assert.That(reference.TryAdvance(.05,intents,out var full),Is.True,full.Failure);
                Assert.That(candidate.TryAdvance(.05,intents,out var pruned),Is.True,pruned.Failure);
                Assert.That(candidate.ExportCheckpoint(),Is.EqualTo(reference.ExportCheckpoint()));
                Assert.That(pruned.MinimumSweptGapM,Is.EqualTo(full.MinimumSweptGapM));
                Assert.That(candidate.WallWork.ConstraintRowsPruned,Is.GreaterThan(0));
            }
        }
        [Test] public void WindowExcludingZeroFallsBackWithoutChangingTheResult()
        {
            var body=new CrowdAgent {Id="body",ContactSpaceId="world",RegionId="room",FrameId="world",SurfaceId="floor",Position=new CrowdVector(),RadiusM=.3,HeightM=1.8,PreferredSpeedMS=1.2};
            var definition=new CrowdDefinition {ProfileId="window-fallback",Spaces=new[] {new CrowdSpace {Id="world"}},Walls=new[] {new CrowdWall {Id="far",ContactSpaceId="world",A=new CrowdVector(50,-1),B=new CrowdVector(50,1)}}};
            var full=new CrowdMotionModel(definition,new[]{body},91) {Optimization=CrowdMotionModel.WallOptimization.None};
            var pruned=new CrowdMotionModel(definition,new[]{body},91);
            var windows=new[]{new CrowdMovementWindow {AgentId="body",MinX=definition.Parameters.GeometryToleranceM/2,MaxX=1,MinY=-1,MaxY=1}};
            var intents=new[]{new CrowdIntent {AgentId="body",Mode=CrowdIntentMode.DesiredVelocity,DesiredVelocity=new CrowdVector(.1,0)}};
            var expected=full.TryAdvance(.01,intents,out var a,windows); var actual=pruned.TryAdvance(.01,intents,out var b,windows);
            Assert.That(actual,Is.EqualTo(expected)); Assert.That(pruned.ExportCheckpoint(),Is.EqualTo(full.ExportCheckpoint()));
            Assert.That(pruned.WallWork.NonzeroWindowFallbacks,Is.GreaterThan(0));
        }
    }
}
