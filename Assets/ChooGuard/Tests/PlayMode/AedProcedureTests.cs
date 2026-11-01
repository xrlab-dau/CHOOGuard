using System.Collections.Generic;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// The AED's own course as the device runs it (<see cref="AedProcedure"/>), in the rescuer's order of operations fixed on
    /// 2026-10-31: lid open (power), a pad taken out and stuck on, the same again, hands off calling "물러나세요", the analysis,
    /// "제세동이 필요합니다" and the button — or "전기충격이 필요없습니다" — then compressions until it asks everyone off again two
    /// minutes later. No scene, no player: the two facts the device reads from the patient are stubbed.
    /// </summary>
    public sealed class AedProcedureTests
    {
        private AedProcedure device;
        private readonly List<string> voice = new List<string>();
        private readonly List<string> log = new List<string>();
        private bool shockable, handsOn;

        [SetUp]
        public void Setup()
        {
            shockable = true;
            handsOn = false;
            voice.Clear();
            log.Clear();
            device = new AedProcedure { Shockable = () => shockable, HandsOn = () => handsOn };
            device.Voice += voice.Add;
            device.Logged += log.Add;
        }

        private void Run(float seconds, float step = .1f)
        {
            for (float t = 0; t < seconds; t += step) device.Tick(step);
        }

        private string LastSaid => voice[voice.Count - 1];

        /// <summary>Lid open, both pads taken out one at a time and stuck on.</summary>
        private void PadsOn()
        {
            Assert.That(device.OpenLid(), Is.True);
            Assert.That(device.TakePad(), Is.True);
            Assert.That(device.AttachPad(), Is.True);
            Assert.That(device.TakePad(), Is.True);
            Assert.That(device.AttachPad(), Is.True);
        }

        [Test]
        public void ShutItWantsTheLidOpenedAndNothingElse()
        {
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.Off));
            Assert.That(device.Prompt, Is.EqualTo("뚜껑 열기"));
            Assert.That(device.TakePad(), Is.False);
            Assert.That(device.AttachPad(), Is.False);
            Assert.That(device.StandClear(), Is.False);
            Assert.That(device.PressShock(), Is.False);
            Run(10f);
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.Off));
            Assert.That(voice, Is.Empty);
        }

        [Test]
        public void EachPadIsTakenOutOfTheLidThenStuckOnOneAtATime()
        {
            Assert.That(device.OpenLid(), Is.True);
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.PadsWanted));
            Assert.That(device.Prompt, Is.Empty, "the case itself wants nothing: the pads are the targets");
            Assert.That(device.Refusal, Does.Contain("패드를 집으십시오"));
            Assert.That(device.AttachPad(), Is.False, "nothing in hand to stick on");
            Assert.That(device.TakePad(), Is.True);
            Assert.That(device.PadInHand, Is.True);
            Assert.That(device.TakePad(), Is.False, "one pad in hand at a time");
            Assert.That(device.Refusal, Does.Contain("환자 가슴에 붙이십시오"));
            Assert.That(device.ReturnPad(), Is.True, "it can go back into the lid");
            Assert.That(device.PadInHand, Is.False);
            Assert.That(device.Pads, Is.EqualTo(0));
            Assert.That(device.TakePad(), Is.True);
            Assert.That(device.AttachPad(), Is.True);
            Assert.That(device.Pads, Is.EqualTo(1));
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.PadsWanted), "one pad is not enough");
            Assert.That(device.TakePad(), Is.True);
            Assert.That(device.AttachPad(), Is.True);
            Assert.That(device.Pads, Is.EqualTo(2));
            Assert.That(device.PadsTaken, Is.EqualTo(2));
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.ClearWanted), "both pads on: it asks everyone off and waits for the call");
            Assert.That(LastSaid, Does.Contain("물러나십시오"));
            Assert.That(device.TakePad(), Is.False);
            Assert.That(log, Has.Some.EqualTo("AED 뚜껑 열고 전원 켬"));
            Assert.That(log, Has.Some.EqualTo("AED 패드 2장 부착"));
        }

        [Test]
        public void TheShockablePathRunsClearCallAnalysisChargeShockThenCompressionsAndAsksForClearAgain()
        {
            PadsOn();
            Run(30f);
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.ClearWanted), "no analysis until the rescuer calls everyone clear");
            Assert.That(device.Analyses, Is.EqualTo(0));
            Assert.That(device.Refusal, Does.Contain("물러나세요"));

            Assert.That(device.StandClear(), Is.True);
            Assert.That(device.ClearCalls, Is.EqualTo(1));
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.Analysing));
            Assert.That(log, Has.Some.Contains("\"물러나세요\" 외침"));
            Run(AedProcedure.AnalysisSeconds - .5f);
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.Analysing));
            Run(1f);
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.ShockAdvised));
            Assert.That(LastSaid, Does.StartWith("제세동이 필요합니다"));
            Assert.That(device.LastAdvice, Is.True);
            Assert.That(device.Analyses, Is.EqualTo(1));
            Assert.That(device.Charged, Is.False);
            Assert.That(device.PressShock(), Is.False, "the button does nothing before it is charged");
            Assert.That(device.Shocks, Is.EqualTo(0));

            Run(AedProcedure.ChargeSeconds + .2f);
            Assert.That(device.Charged, Is.True);
            Assert.That(device.Prompt, Is.EqualTo("충격 버튼 누르기"));
            Assert.That(LastSaid, Does.Contain("버튼"));
            Assert.That(device.PressShock(), Is.True);
            Assert.That(device.Shocks, Is.EqualTo(1));
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.Compressions));
            Assert.That(LastSaid, Does.Contain("가슴압박을 계속"));
            Assert.That(device.Refusal, Does.Contain("재분석"));

            Run(AedProcedure.CompressionCycleSeconds + .5f, .5f);
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.ClearWanted), "after the two-minute cycle it asks everyone off again");
            Assert.That(LastSaid, Does.Contain("가슴압박을 멈추십시오"));
            Assert.That(device.StandClear(), Is.True);
            Assert.That(device.ClearCalls, Is.EqualTo(2));
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.Analysing));
            Assert.That(log, Has.Some.EqualTo("AED 분석 1회 · 심장충격 필요"));
            Assert.That(log, Has.Some.EqualTo("AED 심장충격 1회"));
        }

        [Test]
        public void HandsOnThePatientHoldTheAnalysisAndBlockTheShock()
        {
            PadsOn();
            Assert.That(device.StandClear(), Is.True);
            handsOn = true;
            Run(AedProcedure.AnalysisSeconds * 2);
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.Analysing), "no analysis while someone touches the patient");
            Assert.That(voice, Has.Some.Contains("분석이 중단"));
            handsOn = false;
            Run(AedProcedure.AnalysisSeconds + .2f);
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.ShockAdvised));
            Run(AedProcedure.ChargeSeconds + .2f);
            handsOn = true;
            Assert.That(device.PressShock(), Is.False, "no shock with hands on the patient");
            Assert.That(device.Shocks, Is.EqualTo(0));
            Assert.That(LastSaid, Does.Contain("떨어지십시오"));
            handsOn = false;
            Assert.That(device.PressShock(), Is.True);
        }

        [Test]
        public void ABreathingPatientGetsNoShockAdviceAndGoesStraightToCompressions()
        {
            shockable = false;
            PadsOn();
            Assert.That(device.StandClear(), Is.True);
            Run(AedProcedure.AnalysisSeconds + .2f);
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.Compressions));
            Assert.That(LastSaid, Does.StartWith("전기충격이 필요없습니다"));
            Assert.That(LastSaid, Does.Contain("가슴압박을 계속"));
            Assert.That(device.LastAdvice, Is.False);
            Assert.That(device.Shocks, Is.EqualTo(0));
            Assert.That(log, Has.Some.Contains("심장충격 불필요"));
            Assert.That(device.PressShock(), Is.False, "there is no shock to give");
        }

        [Test]
        public void LiftingItPowersDownTakesThePadsBackAndKeepsTheCounts()
        {
            PadsOn();
            Assert.That(device.StandClear(), Is.True);
            Run(AedProcedure.AnalysisSeconds + AedProcedure.ChargeSeconds + .5f);
            Assert.That(device.PressShock(), Is.True);
            device.PowerOff();
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.Off));
            Assert.That(device.Pads, Is.EqualTo(0));
            Assert.That(device.PadInHand, Is.False);
            Assert.That(device.Shocks, Is.EqualTo(1), "what was done stays counted");
            Assert.That(device.ClearCalls, Is.EqualTo(1));
            Assert.That(log, Has.Some.EqualTo("AED 전원 끔"));
            Assert.That(device.OpenLid(), Is.True);
            Assert.That(device.Pads, Is.EqualTo(0), "the pads must go on again");
        }

        [Test]
        public void HandedOverItGoesQuiet()
        {
            PadsOn();
            device.HandOver();
            int said = voice.Count;
            Run(30f);
            Assert.That(device.Current, Is.EqualTo(AedProcedure.Stage.HandedOver));
            Assert.That(voice.Count, Is.EqualTo(said));
            Assert.That(device.Prompt, Is.Empty);
            Assert.That(device.StandClear(), Is.False);
            Assert.That(device.Refusal, Does.Contain("구급대"));
        }
    }
}
