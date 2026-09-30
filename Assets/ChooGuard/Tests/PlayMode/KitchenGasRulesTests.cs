using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.PlayMode
{
    // 주방·가스 설비의 소비자 관점 규칙: 식용유 화재는 물에 커지고 분말에 되살아나며 K급만 끈다, 가스가 새는 한 불이 꺼지지 않는다,
    // 중간밸브는 한 번 잠그면 그 뜻을 듣는 쪽에 한 번만 알린다.
    public sealed class KitchenGasRulesTests
    {
        private EmergencyArt art;
        private GameObject parent;

        [SetUp]
        public void SetUp()
        {
            art = ScriptableObject.CreateInstance<EmergencyArt>();
            parent = new GameObject("시험 주방");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(art);
            HazardRegistry.Clear();
        }

        private FireHazard OilFire(float intensity = .6f) => new FireHazard("oil", Vector3.zero, "시험", "튀김기", intensity, art, parent.transform) { Oil = true };

        [Test]
        public void WaterOnBurningOilFlaresTheFireAndSplashesOnce()
        {
            var fire = OilFire();
            int splashes = 0;
            fire.Splashed += _ => splashes++;

            fire.SuppressWith(ExtinguishAgent.Water, 1f, 1f);
            fire.SuppressWith(ExtinguishAgent.Water, 1f, 1f);

            Assert.That(fire.Intensity, Is.GreaterThan(.6f), "끓는 기름에 물을 뿌리면 불이 커져야 한다");
            Assert.That(splashes, Is.EqualTo(1), "기름이 튀는 일은 처음 물이 닿을 때 한 번");
        }

        [Test]
        public void DryPowderKnocksAnOilFireDownButNeverPutsItOut()
        {
            var fire = OilFire();

            for (int i = 0; i < 600; i++) fire.SuppressWith(ExtinguishAgent.Powder, 1f, .1f);

            Assert.That(fire.Extinguished, Is.False, "분말만으로는 뜨거운 기름 불이 꺼지지 않고 되살아난다");
            Assert.That(fire.Intensity, Is.LessThan(.6f).And.GreaterThan(0f));
        }

        [Test]
        public void WetChemicalPutsAnOilFireOut()
        {
            var fire = OilFire();

            for (int i = 0; i < 600 && !fire.Extinguished; i++) fire.SuppressWith(ExtinguishAgent.WetChemical, 1f, .1f);

            Assert.That(fire.Extinguished, Is.True, "K급 소화기는 식용유 불을 끈다");
            Assert.That(fire.Cooled, Is.True);
        }

        [Test]
        public void AnOrdinaryFireIgnoresWhatOilFiresDo()
        {
            var powder = new FireHazard("p", Vector3.zero, "시험", "가방", .3f, art, parent.transform);
            var water = new FireHazard("w", new Vector3(10, 0, 0), "시험", "가방", .3f, art, parent.transform);

            for (int i = 0; i < 100; i++) { powder.SuppressWith(ExtinguishAgent.Powder, 1f, .1f); water.SuppressWith(ExtinguishAgent.Water, 1f, .1f); }

            Assert.That(powder.Extinguished, Is.True);
            Assert.That(water.Extinguished, Is.True, "기름이 아닌 불에 물은 그대로 듣는다");
        }

        [Test]
        public void ALeakingGasFeedKeepsTheFireFromGoingOutUntilItIsCut()
        {
            var fire = new FireHazard("gas", Vector3.zero, "시험", "가스레인지", .5f, art, parent.transform);
            fire.SetFeed("가스", Agency.Fire, 20f);

            for (int i = 0; i < 300; i++) fire.SuppressWith(ExtinguishAgent.WetChemical, 1f, .1f);
            Assert.That(fire.Extinguished, Is.False, "가스가 새는 한 불은 꺼지지 않는다");

            Assert.That(fire.CutFeed("역무원 · 가스 중간밸브"), Is.True);
            for (int i = 0; i < 300 && !fire.Extinguished; i++) fire.SuppressWith(ExtinguishAgent.WetChemical, 1f, .1f);
            Assert.That(fire.Extinguished, Is.True, "밸브를 잠근 뒤에는 꺼진다");
        }

        [Test]
        public void ClosingAGasValveTurnsTheHandleAcrossThePipeAndTellsListenersOnce()
        {
            var go = new GameObject("밸브");
            go.transform.SetParent(parent.transform);
            var equipment = go.AddComponent<StationEquipment>();
            equipment.Assign("shop-시험/valve-1", GasValvePoint.Kind, "가스 중간밸브", "hall2f", "shop=shop-시험;role=intermediate;feeds=shop-시험/range-1");
            var lever = new GameObject("Lever");
            lever.transform.SetParent(go.transform, false);
            var valve = go.AddComponent<GasValvePoint>();
            valve.Bind();
            int closings = 0;
            string who = null;
            valve.Closing += (_, by) => { closings++; who = by; };

            Assert.That(valve.InteractionPrompt, Is.EqualTo("가스 중간밸브 잠그기"));
            valve.Close("역무원");
            valve.Close("점포 직원");

            Assert.That(closings, Is.EqualTo(1));
            Assert.That(who, Is.EqualTo("역무원"), "처음 잠근 사람만 기록한다");
            Assert.That(valve.Closed, Is.True);
            Assert.That(equipment.State, Is.EqualTo("잠김"));
            Assert.That(valve.InteractionPrompt, Is.Empty, "잠근 밸브에는 더 할 동작이 없다");
        }
    }
}
