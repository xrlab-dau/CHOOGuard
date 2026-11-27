#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using static ChooGuard.App.Fps.Emergency.PracticeLabel;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// The per-scenario order lessons (docs/practice-lessons-plan.md). They are built without a session: they only hear signals
    /// and keep the score, so each rule can be checked on its own.
    /// </summary>
    public sealed class PracticeOrderLessonTests
    {
        static string[] Keys(PracticeOrderLesson lesson) => lesson.Mistakes.Select(mistake => mistake.Key).ToArray();

        [Test]
        public void EveryScenarioWithoutABuiltInLessonHasAnOrderLesson()
        {
            foreach (var scenario in PracticeCatalog.Groups.SelectMany(group => group.Scenarios))
            {
                if (scenario.Lesson != PracticeLesson.None)
                {
                    Assert.That(scenario.OrderLesson, Is.Null, scenario.Title + " already has a built-in lesson.");
                    continue;
                }
                Assert.That(scenario.OrderLesson, Is.Not.Null, scenario.Title + " has no order lesson.");
                var lesson = scenario.OrderLesson(null);
                Assert.That(lesson.StepNumbers, Is.Not.Empty, scenario.Title);
                Assert.That(lesson.OrderScore, Is.EqualTo(PracticeOrderLesson.FullScore), scenario.Title);
            }
        }

        [Test]
        public void TheRadioLabelsTheLessonsListenForAreTheGameOwn()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var teams = (string[])typeof(IncidentDirector).GetField("TeamRequests", flags).GetValue(null);
            var measures = (string[])typeof(IncidentDirector).GetField("MeasureRequests", flags).GetValue(null);
            Assert.That(teams, Is.SupersetOf(new[] { Fire119, Ems119, Police112, RailwayPolice, FacilityTeam, ElectricTeam, GasCompany, ElevatorTeam }));
            Assert.That(measures, Is.SupersetOf(new[] { TrainHold, EscalatorHalt, ReceiverReset, ShutterRestore }));
            var scopeName = typeof(IncidentDirector).GetMethod("ScopeName", flags);
            Assert.That(new[] { PaScope.ClearAround, PaScope.EvacuateArea, PaScope.EvacuateStation, PaScope.Inform }.Select(scope => (string)scopeName.Invoke(null, new object[] { scope })),
                Is.EqualTo(new[] { ClearAround, EvacuateArea, EvacuateStation, Inform }));
        }

        [Test]
        public void EscalatorCareBeforeTheStopButtonAndARestartAreEachDeductedOnce()
        {
            var lesson = new EscalatorAccidentLesson(null).Start();
            lesson.Signal(PracticeSignal.Interact, CheckPatient);
            lesson.Signal(PracticeSignal.Interact, EscalatorStop);
            lesson.Signal(PracticeSignal.Interact, EscalatorRestart);
            lesson.Signal(PracticeSignal.Interact, EscalatorRestart);
            Assert.That(Keys(lesson), Is.EquivalentTo(new[] { "care-before-stop", "restart-before-inspection" }));
            Assert.That(lesson.Mistakes.Single(mistake => mistake.Key == "restart-before-inspection").Count, Is.EqualTo(2));
            Assert.That(lesson.OrderScore, Is.EqualTo(80));
        }

        [Test]
        public void EscalatorInTheDocumentedOrderKeepsFullScore()
        {
            var lesson = new EscalatorAccidentLesson(null).Start();
            lesson.Signal(PracticeSignal.Interact, EscalatorStop);
            lesson.Signal(PracticeSignal.Measure, EscalatorHalt);
            lesson.Signal(PracticeSignal.Request, Ems119);
            lesson.Signal(PracticeSignal.Interact, CheckPatient);
            lesson.Signal(PracticeSignal.Handover, null);
            Assert.That(lesson.Mistakes, Is.Empty);
            Assert.That(lesson.StepNumbers.All(lesson.IsDone), Is.True);
        }

        [Test]
        public void PatientReportedBeforeAnyCheckIsDeducted()
        {
            var early = new CollapseLesson(null).Start();
            early.Signal(PracticeSignal.Report, "쓰러짐");
            early.Signal(PracticeSignal.Interact, CheckPatient);
            Assert.That(Keys(early), Is.EqualTo(new[] { "call-before-check" }));

            var inOrder = new CollapseLesson(null).Start();
            inOrder.Signal(PracticeSignal.Interact, CheckPatient);
            inOrder.Signal(PracticeSignal.Request, Ems119);
            Assert.That(inOrder.Mistakes, Is.Empty);
        }

        [Test]
        public void LeftBagFireAlarmAndEvacuationBeforeReportAreDeducted()
        {
            var lesson = new LeftBagLesson(null).Start();
            lesson.Signal(PracticeSignal.Interact, CallPoint);
            lesson.Signal(PracticeSignal.Broadcast, EvacuateArea);
            lesson.Signal(PracticeSignal.Report, "의심 물체");
            Assert.That(Keys(lesson), Is.EquivalentTo(new[] { "fire-alarm", "evacuate-before-report" }));

            var inOrder = new LeftBagLesson(null).Start();
            inOrder.Signal(PracticeSignal.Report, "의심 물체");
            inOrder.Signal(PracticeSignal.Interact, Cordon);
            inOrder.Signal(PracticeSignal.Broadcast, EvacuateArea);
            Assert.That(inOrder.Mistakes, Is.Empty);
        }

        [Test]
        public void ARequiredStepMissingAtHandoverIsDeductedAndTheScoreThenFreezes()
        {
            var lesson = new FallingObjectLesson(null).Start();
            lesson.Signal(PracticeSignal.Report, "낙하물");
            lesson.Signal(PracticeSignal.Handover, null);
            Assert.That(Keys(lesson), Is.EqualTo(new[] { "no-cordon" }));
            Assert.That(lesson.HandedOver, Is.True);
            lesson.Signal(PracticeSignal.Interact, Cordon);
            Assert.That(lesson.IsDone("3"), Is.False, "Nothing is recorded after the handover.");
            Assert.That(lesson.OrderScore, Is.EqualTo(90));
        }

        [Test]
        public void PowerOutageMovingPeopleBeforeTheCalmAnnouncementIsDeducted()
        {
            var lesson = new PowerOutageLesson(null).Start();
            lesson.Signal(PracticeSignal.Broadcast, EvacuateStation);
            lesson.Signal(PracticeSignal.Broadcast, Inform);
            Assert.That(Keys(lesson), Is.EqualTo(new[] { "move-before-calm" }));
        }

        [Test]
        public void CommonLessonNeedsAReportBeforeHandover()
        {
            var lesson = new CommonOrderLesson(null, "출입문 끼임").Start();
            lesson.Signal(PracticeSignal.Measure, TrainHold);
            lesson.Signal(PracticeSignal.Handover, null);
            Assert.That(Keys(lesson), Is.EqualTo(new[] { "no-report" }));
            Assert.That(lesson.ScoreReport(), Does.Contain("●3 열차 출발 보류").And.Contain("○1 역무실 보고"));
        }
    }
}
#endif
