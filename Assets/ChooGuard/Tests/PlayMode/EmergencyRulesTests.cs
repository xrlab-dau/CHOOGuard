using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Tests.PlayMode
{
    // 본게임 비상대응의 소비자 관점 규칙: 소화기가 듣는 단계, 조준 품질, 의자 한 칸에 한 사람.
    public sealed class EmergencyRulesTests
    {
        private EmergencyArt art;
        private GameObject parent;

        [SetUp]
        public void SetUp()
        {
            art = ScriptableObject.CreateInstance<EmergencyArt>();
            parent = new GameObject("시험 사건");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(art);
            HazardRegistry.Clear();
        }

        [Test]
        public void HandExtinguisherBarelyHelpsOnceTheFireHasLeftItsFirstStage()
        {
            var early = new FireHazard("early", new Vector3(0, 0, 0), "시험", .5f, art, parent.transform);
            var grown = new FireHazard("grown", new Vector3(20, 0, 0), "시험", 1.2f, art, parent.transform);

            early.Suppress(1f, 1f);
            grown.Suppress(1f, 1f);

            float earlyDrop = .5f - early.Intensity;
            float grownDrop = 1.2f - grown.Intensity;
            Assert.That(earlyDrop, Is.GreaterThan(grownDrop * 5), "초기 단계를 넘은 불은 소화기로 거의 줄지 않아야 한다");
            Assert.That(early.Extinguished, Is.False);
        }

        [Test]
        public void FireIsOutAndNoLongerAHazardOnceIntensityReachesZero()
        {
            var fire = new FireHazard("small", Vector3.zero, "시험", .05f, art, parent.transform);

            fire.Suppress(1f, 2f);

            Assert.That(fire.Extinguished, Is.True);
            Assert.That(fire.Active, Is.False, "꺼진 불을 계속 위험으로 보면 승객이 끝없이 재판단한다");
            Assert.That(fire.DangerRadius, Is.EqualTo(0));
        }

        [Test]
        public void SmokeStaysOnItsStoreyAndRisesOnlyNarrowlyToTheOneAbove()
        {
            // 승강장(열차 안) 불의 연기가 선로 위 2층 맞이방 사람까지 쓰러뜨리던 문제: 연기는 층을 따라 퍼진다.
            var fire = new FireHazard("smoke", Vector3.zero, "시험", .5f, art, parent.transform);
            fire.SpreadSmoke(20f - fire.SmokeRadius);

            Assert.That(fire.InSmoke(new Vector3(10, 0, 0)), Is.True, "같은 층 10 m");
            Assert.That(fire.InSmoke(new Vector3(0, -7, 0)), Is.False, "아래층으로는 내려가지 않는다");
            Assert.That(fire.InSmoke(new Vector3(0, 7.2f, 0)), Is.False, "선로 위 맞이방(약 7 m 위)까지는 닿지 않는다");
            Assert.That(fire.InSmoke(new Vector3(5, 5.2f, 0)), Is.True, "뚫린 위층 가까이는 올라간다");
            Assert.That(fire.InSmoke(new Vector3(10, 5.2f, 0)), Is.False, "위층에서는 좁게만 퍼진다");
        }

        [Test]
        public void AimQualityRewardsTheBaseOfTheFireFromAFewMetres()
        {
            var fire = new FireHazard("aim", Vector3.zero, "시험", .5f, art, parent.transform);
            var view = new GameObject("시점").transform;
            view.SetParent(parent.transform, false);

            view.position = new Vector3(0, 1.6f, -3f);
            view.LookAt(fire.Position + Vector3.up * .3f);
            float atBase = StaffHands.AimQuality(view, fire);

            view.LookAt(fire.Position + Vector3.up * 3.2f);
            float atSmoke = StaffHands.AimQuality(view, fire);

            view.position = new Vector3(0, 1.6f, -9f);
            view.LookAt(fire.Position + Vector3.up * .3f);
            float tooFar = StaffHands.AimQuality(view, fire);

            view.position = new Vector3(0, 1.6f, -3f);
            view.rotation = Quaternion.LookRotation(Vector3.right);
            float lookingAway = StaffHands.AimQuality(view, fire);

            Assert.That(atBase, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(atSmoke, Is.LessThan(atBase));
            Assert.That(tooFar, Is.EqualTo(0f));
            Assert.That(lookingAway, Is.EqualTo(0f));
        }

        [Test]
        public void OneBenchSlotSeatsOnePersonEvenThoughBothSidesAreListed()
        {
            art.WorldNavMesh = new NavMeshData();
            art.StationData = new TextAsset(@"{ ""version"": 2, ""points"": [
                { ""id"": ""exit-a"", ""kind"": ""Exit"", ""label"": ""출구"", ""position"": { ""x"": 0, ""y"": 0, ""z"": 0 } },
                { ""id"": ""seat-000-0a"", ""kind"": ""Seat"", ""label"": ""의자"", ""slot"": ""seat-000-0"", ""position"": { ""x"": 1, ""y"": 0, ""z"": 0.75 }, ""yaw"": 0, ""anchor"": { ""x"": 1, ""y"": 0, ""z"": 0 } },
                { ""id"": ""seat-000-0b"", ""kind"": ""Seat"", ""label"": ""의자"", ""slot"": ""seat-000-0"", ""position"": { ""x"": 1, ""y"": 0, ""z"": -0.75 }, ""yaw"": 180, ""anchor"": { ""x"": 1, ""y"": 0, ""z"": 0 } },
                { ""id"": ""seat-000-1a"", ""kind"": ""Seat"", ""label"": ""의자"", ""slot"": ""seat-000-1"", ""position"": { ""x"": 1.7, ""y"": 0, ""z"": 0.75 }, ""yaw"": 0, ""anchor"": { ""x"": 1.7, ""y"": 0, ""z"": 0 } }
            ] }");
            using (var world = new StationWorld(art, 7))
            {
                var first = world.ReserveSeat(Vector3.zero);
                var second = world.ReserveSeat(Vector3.zero);
                var third = world.ReserveSeat(Vector3.zero);

                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.Not.Null);
                Assert.That(second.Slot, Is.Not.EqualTo(first.Slot), "의자 한 칸의 앞뒤에 두 사람을 앉히면 몸이 겹친다");
                Assert.That(third, Is.Null, "두 칸뿐인 의자에 세 번째 사람은 앉을 수 없다");

                world.ReleaseSeat(first);
                Assert.That(world.ReserveSeat(Vector3.zero)?.Slot, Is.EqualTo(first.Slot));
            }
        }
    }
}
