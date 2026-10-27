using System.Collections.Generic;
using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class LeisureHudCatalogTests
    {
        [Test]
        public void MenuIconIdForFamily_leisure_returns_family_leisure()
        {
            Assert.AreEqual("family_leisure", TowerHudController.MenuIconIdForFamily(BuildFamily.Leisure));
        }

        [Test]
        public void BuildCatalog_includes_leisure_after_shops_before_utility()
        {
            var rooms = new List<RoomTypeSO>
            {
                Resources.Load<RoomTypeSO>("Rooms/ShopFastFood"),
                Resources.Load<RoomTypeSO>("Rooms/LeisureGym"),
                Resources.Load<RoomTypeSO>("Rooms/Housekeeping"),
            };

            var groups = BuildCatalog.Group(rooms);
            var families = groups.Select(g => g.Family).ToList();

            Assert.AreEqual(BuildFamily.Shops, families[0]);
            Assert.AreEqual(BuildFamily.Leisure, families[1]);
            Assert.AreEqual(BuildFamily.Utility, families[2]);
        }

        [Test]
        public void BuildMenuCatalogForTests_loads_nine_leisure_rooms()
        {
            var catalog = TowerHudController.BuildMenuCatalogForTests(new List<RoomTypeSO>());
            var leisure = catalog.FirstOrDefault(entry => entry.Family == BuildFamily.Leisure);

            Assert.IsNotNull(leisure, "Leisure family should appear in build menu catalog");
            Assert.AreEqual("Leisure", leisure.Label);
            Assert.AreEqual(10, leisure.Rooms.Count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "leisure_gym", "leisure_spa", "leisure_pool", "leisure_bowling", "leisure_theater",
                    "leisure_casino", "leisure_nightclub", "leisure_chapel", "leisure_cathedral", "leisure_atrium"
                },
                leisure.Rooms.Select(room => room.id).ToArray());
        }
    }
}
