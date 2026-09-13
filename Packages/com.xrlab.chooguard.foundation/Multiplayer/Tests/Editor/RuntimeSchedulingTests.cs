using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;

public sealed class RuntimeSchedulingTests
{
    [Test]
    public void DueProbeCommandWaitsForActualSubmissionAndRetainsOrder()
    {
        var steps = new[] { new ProbeStep { AtSeconds = 1 }, new ProbeStep { AtSeconds = 2 } };
        var cursor = new ProbeStepCursor(); var calls = 0;
        cursor.Drain(steps, 3, _ => { calls++; return false; });
        Assert.That(cursor.Next, Is.Zero);
        cursor.Drain(steps, 3, _ => { calls++; return true; });
        Assert.That(cursor.Next, Is.EqualTo(2)); Assert.That(calls, Is.EqualTo(3));
        cursor.Drain(steps, 4, _ => { calls++; return true; });
        Assert.That(calls, Is.EqualTo(3));
    }

    [Test]
    public void CatchupYieldsAfterWallBudgetWithoutDiscardingDueSimulationTime()
    {
        Assert.That(ServerCatchupBudget.CanContinue(.2f, 0, 0), Is.True);
        Assert.That(ServerCatchupBudget.CanContinue(.2f, 1, 150), Is.False);
        Assert.That(ServerCatchupBudget.CanContinue(.2f, 4, 2), Is.False);
        Assert.That(ServerCatchupBudget.CanContinue(.049f, 0, 0), Is.False);
        Assert.That(ServerCatchupBudget.CanContinue(.05f, 1, 49), Is.True);
    }
}
