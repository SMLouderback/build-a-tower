using System.Linq;
using System.Reflection;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class MetroStationPlacementTests
    {
        [Test]
        public void MetroStation_resource_matches_catalog_spec()
        {
            var metro = LoadMetroStation();

            Assert.AreEqual("metro_station", metro.id);
            Assert.AreEqual("Metro Station", metro.displayName);
            Assert.AreEqual(RoomCategory.Transit, metro.category);
            Assert.AreEqual(new Vector2Int(8, 3), metro.size);
            Assert.AreEqual(520000, metro.buildCost);
            Assert.AreEqual(3, metro.requiredStars);
            Assert.IsFalse(metro.allowAboveGround);
            Assert.IsTrue(metro.allowBasement);
            Assert.AreEqual(BuildFamily.Transit, metro.ResolvedBuildFamily());
        }

        [Test]
        public void Build_menu_catalog_includes_metro_station_under_transit()
        {
            var catalog = TowerHudController.BuildMenuCatalogForTests(Enumerable.Empty<RoomTypeSO>());
            var transit = catalog.FirstOrDefault(entry => entry.Family == BuildFamily.Transit);

            Assert.IsNotNull(transit, "Transit family should appear in build menu catalog.");
            CollectionAssert.Contains(
                transit.Rooms.Select(room => room.id).ToArray(),
                "metro_station");
        }

        [Test]
        public void TryPlaceSelected_registers_metro_and_rejects_second_at_three_stars()
        {
            var go = new GameObject("Metro placement test tower");
            go.SetActive(false);
            try
            {
                var build = go.AddComponent<BuildController>();
                var simulation = go.AddComponent<TowerSimulation>();
                var grid = new TowerGrid();
                Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 60, TowerGrid.LobbyFloor, out _));
                SetAutoProperty(build, "Grid", grid);
                SetAutoProperty(build, "Wallet", new FundsWallet(2_000_000));
                SetField(build, "lobbyType", Lobby());

                simulation.InitializeSimulation();
                simulation.Stars.ForceStars(3);

                build.SetRoomType(LoadMetroStation());
                Assert.IsTrue(build.TryPlaceSelected(new Vector2Int(0, -3)));
                Assert.AreEqual(1, simulation.Metro.StationCount);

                Assert.IsFalse(build.TryPlaceSelected(new Vector2Int(MetroSystem.MinLeftEdgeSpacingTiles, -3)));
                Assert.AreEqual(1, simulation.Metro.StationCount);
                StringAssert.Contains("Maximum metro stations", build.HelpText);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static RoomTypeSO LoadMetroStation()
        {
            var metro = Resources.Load<RoomTypeSO>("Rooms/MetroStation");
            Assert.IsNotNull(metro, "Expected Resources/Rooms/MetroStation asset.");
            return metro;
        }

        static RoomTypeSO Lobby()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "lobby";
            type.displayName = "Lobby";
            type.category = RoomCategory.Structure;
            type.size = Vector2Int.one;
            type.isLobby = true;
            type.allowAboveGround = true;
            type.buildCost = 1000;
            return type;
        }

        static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing test setup field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
