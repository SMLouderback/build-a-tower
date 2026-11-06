using System.Collections.Generic;
using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildCatalogUtilitySubgroupTests
    {
        static RoomTypeSO Make(
            string id,
            string displayName,
            RoomCategory category,
            bool stairs = false,
            bool elevator = false,
            BuildFamily family = BuildFamily.None,
            BuildSubgroup subgroup = BuildSubgroup.None)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.displayName = displayName;
            so.category = category;
            so.isStairs = stairs;
            so.isElevatorShaft = elevator;
            so.buildFamily = family;
            so.buildSubgroup = subgroup;
            return so;
        }

        static BuildCatalogFamily UtilityGroup(IEnumerable<RoomTypeSO> rooms)
        {
            var groups = BuildCatalog.Group(rooms);
            return groups.FirstOrDefault(g => g.Family == BuildFamily.Utility);
        }

        static BuildCatalogSubgroup FindSubgroup(BuildCatalogFamily utility, BuildSubgroup subgroup) =>
            utility.Subgroups.FirstOrDefault(s => s.Subgroup == subgroup);

        [Test]
        public void Group_utility_has_four_subgroups_in_spec_order()
        {
            var rooms = new List<RoomTypeSO>
            {
                Make("service_clinic", "Clinic", RoomCategory.Service, family: BuildFamily.Utility),
                Make("service_power", "Breaker", RoomCategory.Service, family: BuildFamily.Utility),
                Make("service_pump", "Pump", RoomCategory.Service, family: BuildFamily.Utility),
                Make("service_treatment", "Treatment", RoomCategory.Service, family: BuildFamily.Utility),
                Make("service_trophy_room", "Trophy Room", RoomCategory.Service, family: BuildFamily.Utility),
            };

            var utility = UtilityGroup(rooms);
            Assert.IsNotNull(utility);
            Assert.AreEqual(0, utility.Rooms.Count);
            Assert.AreEqual(4, utility.Subgroups.Count);
            Assert.AreEqual(BuildSubgroup.HealthSanitation, utility.Subgroups[0].Subgroup);
            Assert.AreEqual(BuildSubgroup.Power, utility.Subgroups[1].Subgroup);
            Assert.AreEqual(BuildSubgroup.Water, utility.Subgroups[2].Subgroup);
            Assert.AreEqual(BuildSubgroup.Services, utility.Subgroups[3].Subgroup);
        }

        [Test]
        public void SubgroupLabel_matches_spec_for_utility_subgroups()
        {
            Assert.AreEqual("Health / Sanitation", BuildCatalog.SubgroupLabel(BuildSubgroup.HealthSanitation));
            Assert.AreEqual("Power", BuildCatalog.SubgroupLabel(BuildSubgroup.Power));
            Assert.AreEqual("Water", BuildCatalog.SubgroupLabel(BuildSubgroup.Water));
            Assert.AreEqual("Services", BuildCatalog.SubgroupLabel(BuildSubgroup.Services));
        }

        [Test]
        public void ResolvedBuildSubgroup_maps_utility_ids_via_fallback()
        {
            Assert.AreEqual(BuildSubgroup.HealthSanitation,
                Make("service_clinic", "Clinic", RoomCategory.Service).ResolvedBuildSubgroup());
            Assert.AreEqual(BuildSubgroup.Power,
                Make("service_power", "Breaker", RoomCategory.Service).ResolvedBuildSubgroup());
            Assert.AreEqual(BuildSubgroup.Water,
                Make("service_pump", "Pump", RoomCategory.Service).ResolvedBuildSubgroup());
            Assert.AreEqual(BuildSubgroup.Water,
                Make("service_treatment", "Treatment", RoomCategory.Service).ResolvedBuildSubgroup());
            Assert.AreEqual(BuildSubgroup.Services,
                Make("service_trophy_room", "Trophy", RoomCategory.Service).ResolvedBuildSubgroup());
        }

        [Test]
        public void Group_buckets_utility_rooms_into_expected_subgroups()
        {
            var rooms = new List<RoomTypeSO>
            {
                Make("service_clinic", "Clinic", RoomCategory.Service, family: BuildFamily.Utility),
                Make("service_power", "Breaker", RoomCategory.Service, family: BuildFamily.Utility),
                Make("service_pump", "Pump", RoomCategory.Service, family: BuildFamily.Utility),
                Make("service_treatment", "Treatment", RoomCategory.Service, family: BuildFamily.Utility),
                Make("service_trophy_room", "Trophy Room", RoomCategory.Service, family: BuildFamily.Utility),
            };

            var utility = UtilityGroup(rooms);
            var health = FindSubgroup(utility, BuildSubgroup.HealthSanitation);
            var power = FindSubgroup(utility, BuildSubgroup.Power);
            var water = FindSubgroup(utility, BuildSubgroup.Water);
            var services = FindSubgroup(utility, BuildSubgroup.Services);

            Assert.IsNotNull(health);
            Assert.AreEqual("service_clinic", health.Rooms[0].id);
            Assert.IsNotNull(power);
            Assert.AreEqual("service_power", power.Rooms[0].id);
            Assert.IsNotNull(water);
            Assert.AreEqual(2, water.Rooms.Count);
            CollectionAssert.AreEquivalent(
                new[] { "service_pump", "service_treatment" },
                water.Rooms.Select(r => r.id).ToArray());
            Assert.IsNotNull(services);
            Assert.AreEqual("service_trophy_room", services.Rooms[0].id);
        }

        [Test]
        public void Group_places_stairs_under_transit_not_utility()
        {
            var rooms = new List<RoomTypeSO>
            {
                Make("service_clinic", "Clinic", RoomCategory.Service, family: BuildFamily.Utility),
                Make("stairs", "Stairs", RoomCategory.Transit, stairs: true),
            };

            var groups = BuildCatalog.Group(rooms);
            var transit = groups.FirstOrDefault(g => g.Family == BuildFamily.Transit);
            Assert.IsNotNull(transit);
            Assert.AreEqual(1, transit.Rooms.Count);
            Assert.AreEqual("stairs", transit.Rooms[0].id);
            Assert.IsFalse(groups.Any(g => g.Family == BuildFamily.Utility && g.Subgroups.Count == 0));
        }
    }
}
