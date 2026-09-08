using System;
using NUnit.Framework;
namespace ChooGuard.Foundation.Demo.Tests
{
    public sealed class DemoExerciseTests
    {
        [Test]
        public void OrderedStepsRejectSkippingAndDuplicatesAndOwnTheirInput()
        {
            var definition = new DemoDrill { id = "test", title = "Synthetic", steps = new[] { "anchor-02", "rally-west", "assembly-register" } };
            var exercise = new DemoExercise(definition, "anchor-01");
            definition.steps[0] = "changed";
            Assert.That(exercise.Current, Is.EqualTo("anchor-01"));
            Assert.That(exercise.TryAdvance("anchor-02"), Is.False);
            Assert.That(exercise.TryAdvance("anchor-01"), Is.True);
            Assert.That(exercise.TryAdvance("anchor-01"), Is.False);
            Assert.That(exercise.Current, Is.EqualTo("anchor-02"));
            Assert.That(exercise.TryAdvance("anchor-02"), Is.True);
            Assert.That(exercise.TryAdvance("rally-west"), Is.True);
            Assert.That(exercise.ReadyForAssembly, Is.False);
            Assert.That(exercise.TryAdvance("assembly-register"), Is.True);
            Assert.That(exercise.ReadyForAssembly, Is.True);
            Assert.That(exercise.TryAdvance("assembly-register"), Is.False);
        }
        [Test]
        public void SelectedRoleOnlyOccursOnceAndCrowdStepsRemainOrdered()
        {
            var exercise = new DemoExercise(new DemoDrill { id="test", title="test", steps=new[]{"anchor-01","anchor-02","rally-west","assembly-register"} }, "anchor-02");
            Assert.That(exercise.Steps, Is.EqualTo(new[]{"anchor-02","anchor-01","rally-west","assembly-register"}));
        }
        [Test]
        public void InvalidDrillIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new DemoExercise(new DemoDrill { id="bad", steps=new[]{"x","x"} }, "anchor-01"));
            Assert.Throws<ArgumentException>(() => new DemoExercise(new DemoDrill { id="bad", steps=new[]{""} }, "anchor-01"));
        }
    }
}
