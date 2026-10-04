using System.Linq;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class BuildingServicesHudTests
    {
        [Test]
        public void BuildMenuCatalogForTests_includes_building_services_in_utility()
        {
            var catalog = TowerHudController.BuildMenuCatalogForTests(new System.Collections.Generic.List<RoomTypeSO>());
            var utility = catalog.FirstOrDefault(entry => entry.Family == BuildFamily.Utility);

            Assert.IsNotNull(utility, "Utility family should appear in build menu catalog");
            CollectionAssert.Contains(utility.Rooms.Select(r => r.id).ToArray(), "service_mail");
            CollectionAssert.Contains(utility.Rooms.Select(r => r.id).ToArray(), "service_recycling");
            CollectionAssert.Contains(utility.Rooms.Select(r => r.id).ToArray(), "service_loading_dock");
        }

        [Test]
        public void BuildMenuCatalogForTests_building_services_have_expected_star_gates()
        {
            var catalog = TowerHudController.BuildMenuCatalogForTests(new System.Collections.Generic.List<RoomTypeSO>());
            var utility = catalog.First(entry => entry.Family == BuildFamily.Utility);
            var byId = utility.Rooms.ToDictionary(r => r.id);

            Assert.AreEqual(1, byId["service_mail"].requiredStars);
            Assert.AreEqual(2, byId["service_recycling"].requiredStars);
            Assert.AreEqual(2, byId["service_loading_dock"].requiredStars);
        }
    }
}
