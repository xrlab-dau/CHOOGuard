using System;

namespace ChooGuard.Foundation.Simulation
{
    [Serializable] public sealed class CrowdPhaseReport
    {
        public double TotalMilliseconds,CopyIntentWindowMs,EligibilityMs,StartGeometryMs,DesiredMs,PersonRepulsionMs,WallRepulsionMs,
            ConstraintsMs,ProjectionMs,SweptGeometryMs,CommitMs;
        public long DirectVelocityBodies,CsmBodies,Substeps;
        public void Add(CrowdPhaseReport p)
        {
            if(p==null)return;
            TotalMilliseconds+=p.TotalMilliseconds;CopyIntentWindowMs+=p.CopyIntentWindowMs;EligibilityMs+=p.EligibilityMs;
            StartGeometryMs+=p.StartGeometryMs;DesiredMs+=p.DesiredMs;PersonRepulsionMs+=p.PersonRepulsionMs;WallRepulsionMs+=p.WallRepulsionMs;
            ConstraintsMs+=p.ConstraintsMs;ProjectionMs+=p.ProjectionMs;SweptGeometryMs+=p.SweptGeometryMs;CommitMs+=p.CommitMs;
            DirectVelocityBodies+=p.DirectVelocityBodies;CsmBodies+=p.CsmBodies;Substeps+=p.Substeps;
        }
    }
}
