using System.IO;
using System.Linq;
using System.Reflection;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class CathedralCatalogTests
    {
        [Test]
        public void Cathedral_resource_matches_catalog_spec()
        {
            var cathedral = LoadCathedral();
            var chapel = Resources.Load<RoomTypeSO>("Rooms/LeisureChapel");
            Assert.IsNotNull(chapel, "Chapel is the prestige baseline.");

            Assert.AreEqual("leisure_cathedral", cathedral.id);
            Assert.AreEqual("Cathedral", cathedral.displayName);
            Assert.AreEqual(chapel.category, cathedral.category);
            Assert.AreEqual(new Vector2Int(10, 2), cathedral.size);
            Assert.AreEqual(420000, cathedral.buildCost);
            Assert.AreEqual(4, cathedral.requiredStars);
            Assert.AreEqual(BuildFamily.Leisure, cathedral.ResolvedBuildFamily());
            Assert.AreEqual(IncomeModel.TrafficVariable, cathedral.incomeModel);
            Assert.IsTrue(cathedral.allowAboveGround);
            Assert.IsTrue(cathedral.allowBasement);

            Assert.Greater(cathedral.streetVisitWeight, chapel.streetVisitWeight);
            Assert.Greater(cathedral.baseIncome, chapel.baseIncome);
            Assert.Greater(cathedral.maxOccupants, chapel.maxOccupants);
            Assert.Greater(
                AmenitySystem.ReliefForId(cathedral.id),
                AmenitySystem.ReliefForId(chapel.id));

            var cathedralDwell = ShopVisitRules.DwellRangeForId(cathedral.id);
            var chapelDwell = ShopVisitRules.DwellRangeForId(chapel.id);
            Assert.Greater(cathedralDwell.lo, chapelDwell.lo);
        }

        [Test]
        public void CanPlace_requires_sixteen_tiles_between_left_edges()
        {
            Assert.AreEqual(16, CathedralPlacement.MinLeftEdgeSpacingTiles);

            var existing = new[] { new RectInt(0, 2, 10, 2) };
            Assert.IsTrue(CathedralPlacement.CanPlace(null, new RectInt(0, 1, 10, 2), out var emptyReason));
            Assert.IsEmpty(emptyReason);

            Assert.IsFalse(
                CathedralPlacement.CanPlace(existing, new RectInt(15, 4, 10, 2), out var closeReason));
            StringAssert.Contains("16", closeReason);

            Assert.IsTrue(
                CathedralPlacement.CanPlace(existing, new RectInt(16, 4, 10, 2), out var spacedReason));
            Assert.IsEmpty(spacedReason);

            Assert.IsTrue(
                CathedralPlacement.CanPlace(existing, new RectInt(-16, 4, 10, 2), out var leftReason));
            Assert.IsEmpty(leftReason);
        }

        [Test]
        public void Build_menu_catalog_includes_cathedral_under_leisure()
        {
            var catalog = TowerHudController.BuildMenuCatalogForTests(Enumerable.Empty<RoomTypeSO>());
            var leisure = catalog.FirstOrDefault(entry => entry.Family == BuildFamily.Leisure);

            Assert.IsNotNull(leisure, "Leisure family should appear in build menu catalog.");
            CollectionAssert.Contains(
                leisure.Rooms.Select(room => room.id).ToArray(),
                "leisure_cathedral");
        }

        [Test]
        public void TryPlaceSelected_rejects_cathedral_closer_than_sixteen_tiles()
        {
            var go = new GameObject("Cathedral placement test tower");
            go.SetActive(false);
            try
            {
                var build = go.AddComponent<BuildController>();
                var simulation = go.AddComponent<TowerSimulation>();
                var grid = new TowerGrid();
                Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 80, TowerGrid.LobbyFloor, out _));
                SetAutoProperty(build, "Grid", grid);
                SetAutoProperty(build, "Wallet", new FundsWallet(2_000_000));
                SetField(build, "lobbyType", Lobby());

                simulation.InitializeSimulation();
                simulation.Stars.ForceStars(4);

                var cathedral = LoadCathedral();
                build.SetRoomType(cathedral);
                Assert.IsTrue(build.TryPlaceSelected(new Vector2Int(0, 1)));

                Assert.IsFalse(build.TryPlaceSelected(new Vector2Int(15, 1)));
                StringAssert.Contains("16", build.HelpText);
                Assert.AreEqual(1, CountCathedrals(grid));

                Assert.IsTrue(build.TryPlaceSelected(new Vector2Int(16, 1)));
                Assert.AreEqual(2, CountCathedrals(grid));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Cathedral_dollhouse_and_menu_art_resolve()
        {
            Assert.AreEqual("cathedral_10x2", RoomDollhouseArt.ResourceLeaf("leisure_cathedral"));
            Assert.AreEqual(
                "Art/Dollhouse/cathedral_10x2",
                RoomDollhouseArt.ResourcePath("leisure_cathedral"));

            var dollhouse = Path.Combine(Application.dataPath, "Resources/Art/Dollhouse");
            var bytes = Path.Combine(dollhouse, "cathedral_10x2.bytes");
            var png = Path.Combine(dollhouse, "cathedral_10x2.png");
            Assert.IsTrue(File.Exists(bytes), bytes);
            Assert.IsTrue(File.Exists(png), png);
            Assert.Greater(new FileInfo(bytes).Length, 1024);

            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "leisure_cathedral";
            Assert.AreEqual("leisure_cathedral", TowerHudController.MenuIconIdForRoom(room));
            Assert.IsTrue(MenuIconArt.TryGetTexture("leisure_cathedral", out var icon), "leisure_cathedral");
            Assert.IsNotNull(icon);
            Assert.AreEqual(MenuIconArt.OutputPixels, icon.width);
            Assert.AreEqual(MenuIconArt.OutputPixels, icon.height);

            var menuBytes = Path.Combine(Application.dataPath, "Resources/Art/Menu/leisure_cathedral.bytes");
            Assert.IsTrue(File.Exists(menuBytes), menuBytes);
            Assert.Greater(new FileInfo(menuBytes).Length, 1024);
        }

        static int CountCathedrals(TowerGrid grid)
        {
            var count = 0;
            foreach (var room in grid.Rooms)
            {
                if (room?.Type != null && room.Type.id == "leisure_cathedral")
                    count++;
            }

            return count;
        }

        static RoomTypeSO LoadCathedral()
        {
            var cathedral = Resources.Load<RoomTypeSO>("Rooms/LeisureCathedral");
            Assert.IsNotNull(cathedral, "Expected Resources/Rooms/LeisureCathedral asset.");
            return cathedral;
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
