using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.PlayMode
{
    // 전기 설비의 소비자 관점 규칙: 통전된 불은 전원을 끊어야 꺼지고, 전기 담당은 끊은 뒤 점검하며, 분전반의 차단기는 자기 층 기계만 끊고, 메인은 전부 끊고, 탄 분전반은 되살아나지 않는다.
    public sealed class ElectricPlazaTests
    {
        private EmergencyArt art;
        private GameObject parent;
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            art = ScriptableObject.CreateInstance<EmergencyArt>();
            parent = new GameObject("시험 사건");
            EquipmentRegistry.Clear();
            ElectricNetwork.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in made) if (item != null) Object.DestroyImmediate(item);
            made.Clear();
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(art);
            HazardRegistry.Clear();
            EquipmentRegistry.Clear();
            ElectricNetwork.Clear();
        }

        private FireHazard LiveFire(float intensity = .5f)
        {
            var fire = new FireHazard("live", Vector3.zero, "시험 자판기", "자판기", intensity, art, parent.transform) { Electric = true, WaterIsDangerous = true };
            fire.SetFeed("전원", Agency.Facility, 25f);
            return fire;
        }

        [Test]
        public void ALiveElectricalFireIsBeatenDownToEmbersButOnlyGoesOutOnceTheFeedIsCut()
        {
            var fire = LiveFire();

            fire.Suppress(1f, 60f);
            Assert.That(fire.Extinguished, Is.False, "전기가 통하는 동안 불은 다시 붙는다");
            Assert.That(fire.Intensity, Is.EqualTo(FireHazard.LiveEmbers).Within(.001f));

            Assert.That(fire.CutFeed("역무원"), Is.True);
            Assert.That(fire.CutBy, Is.EqualTo("역무원"));
            fire.Suppress(1f, 5f);
            Assert.That(fire.Extinguished, Is.True, "차단한 불은 소화기로 꺼진다");
        }

        [Test]
        public void AFireThatIsAlreadyBelowTheEmbersNeverGrowsBackWhenSprayed()
        {
            var fire = LiveFire(.05f);

            fire.Suppress(1f, 30f);

            Assert.That(fire.Intensity, Is.EqualTo(.05f).Within(.001f), "불씨보다 작은 불을 분사가 키우면 안 된다");
            Assert.That(fire.Extinguished, Is.False);
        }

        [Test]
        public void WaterOnALiveElectricalFireRaisesTheShockSignalOnlyOnceInAWhile()
        {
            var fire = LiveFire();
            int shocks = 0;
            fire.WetWhileLive += _ => shocks++;

            fire.Suppress(1f, .02f, true);
            fire.Suppress(1f, .02f, true);
            Assert.That(shocks, Is.EqualTo(1), "호스를 든 채 매 프레임 감전 신호를 내면 안 된다");

            fire.CutFeed("역무원");
            var after = 0;
            fire.WetWhileLive += _ => after++;
            fire.Suppress(1f, .02f, true);
            Assert.That(after, Is.EqualTo(0), "전원이 끊긴 뒤의 방수는 감전이 아니다");
        }

        [Test]
        public void TheElectricianCutsTheFeedThenInspectsAndNobodyElseIsSentToDoIt()
        {
            var fire = LiveFire();

            Assert.That(fire.Involves(Agency.Facility), Is.True);
            Assert.That(fire.Dispatch, Does.Contain(Agency.Facility));
            Assert.That(fire.WorkSeconds(Agency.Fire), Is.EqualTo(0), "소방대는 전원을 끊으러 오지 않는다");
            Assert.That(fire.Resolve(Agency.Fire), Is.Null);
            Assert.That(fire.Active, Is.True, "소방대가 부르는 조치가 불을 없애면 안 된다");

            Assert.That(fire.WorkSeconds(Agency.Facility), Is.EqualTo(25));
            Assert.That(fire.Resolve(Agency.Facility), Does.Contain("차단"));
            Assert.That(fire.Feed, Is.Null);
            Assert.That(fire.CutBy, Is.EqualTo("전기 담당"));
            Assert.That(fire.Involves(Agency.Facility), Is.False, "끊은 뒤에는 시설 담당을 다시 부르지 않는다");
            Assert.That(fire.Active, Is.True, "전원을 끊어도 불은 그대로 타고 있다");

            Assert.That(fire.WorkSeconds(Agency.Facility), Is.GreaterThan(0), "끊은 뒤 점검이 남아 있다");
            Assert.That(fire.Resolve(Agency.Facility), Does.Contain("확인"));
            Assert.That(fire.Inspected, Is.True);
            Assert.That(fire.WorkSeconds(Agency.Facility), Is.EqualTo(0));
            Assert.That(fire.Resolve(Agency.Facility), Is.Null, "점검은 한 번이다");
        }

        [Test]
        public void ACoolingFireOfAnotherKindIsNotAffectedByTheFeedRules()
        {
            var plain = new FireHazard("bag", Vector3.zero, "시험", "가방", .5f, art, parent.transform);

            plain.Suppress(1f, 60f);

            Assert.That(plain.Extinguished, Is.True);
            Assert.That(plain.Involves(Agency.Facility), Is.False);
            Assert.That(plain.Dispatch.ToList(), Is.EqualTo(new List<Agency> { Agency.Fire }));
        }

        // ── 분전반과 기계 ──

        private StationEquipment Board(string id, Vector3 at)
        {
            var go = new GameObject(id);
            made.Add(go);
            go.transform.position = at;
            var equipment = go.AddComponent<StationEquipment>();
            equipment.Assign(id, "distribution_board", "분전반 LP-" + id, "hall2f");
            var unit = go.AddComponent<ElectricBoardUnit>();
            unit.Switches = Enumerable.Range(0, BreakerDeckLayout.Slots).Select(s =>
            {
                var breaker = new GameObject("Breaker" + s);
                breaker.transform.SetParent(go.transform, false);
                return breaker.AddComponent<BreakerSwitch>();
            }).ToArray();
            return equipment;
        }

        private StationEquipment Machine(string id, Vector3 at, string kind = "vending_machine")
        {
            var go = new GameObject(id);
            made.Add(go);
            go.transform.position = at;
            var equipment = go.AddComponent<StationEquipment>();
            equipment.Assign(id, kind, kind == "vending_machine" ? "음료 자동판매기" : "휴대폰 충전 키오스크", "hall2f");
            go.AddComponent<ElectricLoad>();
            return equipment;
        }

        [Test]
        public void AMachineHangsOnTheNearestBoardOfItsOwnFloorAndTheLastTwoBranchesStayForLighting()
        {
            var near = Board("a", new Vector3(0, 7, 0));
            var far = Board("b", new Vector3(30, 7, 0));
            var otherFloor = Board("c", new Vector3(1, 0, 0));
            var machines = Enumerable.Range(0, 7).Select(i => Machine("m" + i, new Vector3(2 + i * .1f, 7, 0))).ToList();

            ElectricNetwork.Build();

            Assert.That(ElectricNetwork.CircuitOf(machines[0]).Board.Equipment, Is.SameAs(near), "같은 층에서 가장 가까운 분전반");
            Assert.That(machines.Take(5).All(m => ElectricNetwork.CircuitOf(m).Board.Equipment == near), Is.True);
            Assert.That(ElectricNetwork.CircuitOf(machines[5]).Board.Equipment, Is.SameAs(far), "가지가 남지 않으면 다음 분전반, 다른 층 분전반은 아니다");
            Assert.That(ElectricNetwork.BoardOf(near).Branches.Count(c => c.Load != null), Is.EqualTo(ElectricNetwork.Branches - 2), "마지막 두 가지는 조명·콘센트 몫");
            Assert.That(ElectricNetwork.BoardOf(near).Branches.Where(c => c.Load == null).All(c => !c.Operable), Is.True, "조명·콘센트 회로는 역무원이 만지지 않는다");
            Assert.That(ElectricNetwork.BoardOf(otherFloor).Branches.All(c => c.Load == null), Is.True);
        }

        [Test]
        public void ABranchBreakerCutsOnlyItsMachineAndTheMainCutsEveryMachineOnTheBoard()
        {
            Board("a", Vector3.zero);
            var first = Machine("m1", new Vector3(1, 0, 0));
            var second = Machine("m2", new Vector3(2, 0, 0));
            ElectricNetwork.Build();
            var circuit = ElectricNetwork.CircuitOf(first);
            var main = circuit.Board.Main;

            Assert.That(ElectricNetwork.Switch(circuit, false, "역무원"), Is.True);
            Assert.That(ElectricNetwork.Powered(first), Is.False);
            Assert.That(ElectricNetwork.Powered(second), Is.True, "다른 기계는 그대로");
            Assert.That(first.State, Is.EqualTo("전원 차단"));
            Assert.That(ElectricNetwork.Switch(circuit, false, "역무원"), Is.False, "이미 내려간 차단기는 바뀌지 않는다");

            ElectricNetwork.Switch(circuit, true, "역무원");
            Assert.That(ElectricNetwork.Powered(first), Is.True);
            Assert.That(first.State, Is.EqualTo("정상"));

            ElectricNetwork.Switch(main, false, "역무원");
            Assert.That(ElectricNetwork.Powered(first) || ElectricNetwork.Powered(second), Is.False, "메인은 분전반 전체");
        }

        [Test]
        public void ABurntBoardTakesItsMachinesWithItAndCannotBeSwitchedBackOn()
        {
            Board("a", Vector3.zero);
            var machine = Machine("m1", new Vector3(1, 0, 0));
            ElectricNetwork.Build();
            var board = ElectricNetwork.CircuitOf(machine).Board;

            board.Damaged = true;
            ElectricNetwork.Switch(board.Main, false, "화재로 소손");

            Assert.That(ElectricNetwork.Powered(machine), Is.False);
            Assert.That(ElectricNetwork.Switch(board.Main, true, "역무원"), Is.False, "소손된 분전반은 올려지지 않는다");
            Assert.That(board.Main.On, Is.False);
        }

        [Test]
        public void ABurntMachineStaysDeadEvenWhenItsBreakerIsOn()
        {
            Board("a", Vector3.zero);
            var machine = Machine("m1", new Vector3(1, 0, 0), "charging_kiosk");
            ElectricNetwork.Build();

            machine.GetComponent<ElectricLoad>().Burn();

            Assert.That(ElectricNetwork.Powered(machine), Is.False);
            Assert.That(machine.State, Is.EqualTo("소손"));
        }
    }
}
