using System.Collections.Generic;
using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CondoEmploymentTests
    {
        [TestCase(10, 20, 10)]
        [TestCase(40, 20, 10)]
        [TestCase(5, 20, 5)]
        [TestCase(0, 20, 0)]
        public void InTowerWanted_matches_mix_table(int officeDesks, int condoResidents, int expected)
        {
            Assert.AreEqual(expected, CondoEmployment.InTowerWanted(officeDesks, condoResidents));
        }

        [Test]
        public void RollCommuteOneWayMinutes_stays_in_bounds()
        {
            var rng = new System.Random(42);
            for (var i = 0; i < 500; i++)
            {
                var minutes = CondoEmployment.RollCommuteOneWayMinutes(rng);
                Assert.That(minutes, Is.InRange(15, 60));
            }
        }

        [Test]
        public void RollCommuteOneWayMinutes_mean_near_thirty_over_500_samples()
        {
            var rng = new System.Random(7);
            var sum = 0;
            const int samples = 500;
            for (var i = 0; i < samples; i++)
                sum += CondoEmployment.RollCommuteOneWayMinutes(rng);

            var mean = sum / (float)samples;
            Assert.That(mean, Is.InRange(25f, 35f));
        }

        [Test]
        public void DistributeReservedDesks_totals_reserve_count_without_exceeding_capacity()
        {
            var offices = new List<RoomInstance>
            {
                Office(3, 2),
                Office(1, 4),
                Office(2, 3),
            };

            var reserved = CondoEmployment.DistributeReservedDesks(offices, 7);

            Assert.AreEqual(7, reserved.Values.Sum());
            Assert.AreEqual(4, reserved[1]);
            Assert.AreEqual(3, reserved[2]);
            Assert.IsFalse(reserved.ContainsKey(3));
            foreach (var office in offices)
            {
                if (!reserved.TryGetValue(office.InstanceId, out var count)) continue;
                Assert.LessOrEqual(count, office.Type.maxOccupants);
            }
        }

        [Test]
        public void DistributeReservedDesks_zero_reserve_returns_empty()
        {
            var offices = new List<RoomInstance> { Office(1, 3) };
            var reserved = CondoEmployment.DistributeReservedDesks(offices, 0);
            Assert.AreEqual(0, reserved.Count);
        }

        [Test]
        public void DistributeReservedDesks_caps_when_reserve_exceeds_total_capacity()
        {
            var offices = new List<RoomInstance>
            {
                Office(1, 2),
                Office(2, 1),
            };

            var reserved = CondoEmployment.DistributeReservedDesks(offices, 10);

            Assert.AreEqual(3, reserved.Values.Sum());
            Assert.AreEqual(2, reserved[1]);
            Assert.AreEqual(1, reserved[2]);
        }

        [Test]
        public void NewAgent_condo_job_fields_have_clean_defaults()
        {
            var agent = new Agent(1, AgentRole.CondoResident, null, Vector2Int.zero);

            Assert.AreEqual(CondoJobKind.None, agent.JobKind);
            Assert.IsNull(agent.WorkplaceRoom);
            Assert.AreEqual(0, agent.WorkplaceSlot);
            Assert.AreEqual(0, agent.CommuteOneWayMinutes);
            Assert.AreEqual(0, agent.LeaveHomeMinute);
            Assert.AreEqual(0f, agent.OutsideDwellRemaining);
            Assert.AreEqual(CondoOutsidePhase.None, agent.OutsideWorkPhase);
        }

        [Test]
        public void DistributeReservedDesks_skips_zero_capacity_offices()
        {
            var offices = new List<RoomInstance>
            {
                Office(1, 0),
                Office(2, 2),
            };

            var reserved = CondoEmployment.DistributeReservedDesks(offices, 2);

            Assert.AreEqual(2, reserved.Values.Sum());
            Assert.IsFalse(reserved.ContainsKey(1));
            Assert.AreEqual(2, reserved[2]);
        }

        static RoomInstance Office(int instanceId, int maxOccupants)
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "office";
            type.category = RoomCategory.Office;
            type.maxOccupants = maxOccupants;
            type.size = new Vector2Int(9, 1);
            type.allowAboveGround = true;
            return new RoomInstance(instanceId, type, Vector2Int.zero, type.size);
        }
    }
}
