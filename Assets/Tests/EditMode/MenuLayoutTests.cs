using NUnit.Framework;

namespace BuildATower.Tests
{
    public class MenuLayoutTests
    {
        [Test]
        public void Family_icon_ids_match_art_keys()
        {
            Assert.AreEqual("family_office", TowerHudController.MenuIconIdForFamily(BuildFamily.Office));
            Assert.AreEqual("family_hotel", TowerHudController.MenuIconIdForFamily(BuildFamily.Hotel));
            Assert.AreEqual("family_condo", TowerHudController.MenuIconIdForFamily(BuildFamily.Condo));
            Assert.AreEqual("family_shops", TowerHudController.MenuIconIdForFamily(BuildFamily.Shops));
            Assert.AreEqual("family_utility", TowerHudController.MenuIconIdForFamily(BuildFamily.Utility));
            Assert.AreEqual("family_transit", TowerHudController.MenuIconIdForFamily(BuildFamily.Transit));
        }

        [Test]
        public void Subgroup_icon_ids_match_art_keys()
        {
            Assert.AreEqual("subgroup_food", TowerHudController.MenuIconIdForSubgroup(BuildSubgroup.Food));
            Assert.AreEqual("subgroup_retail", TowerHudController.MenuIconIdForSubgroup(BuildSubgroup.Retail));
        }

        [Test]
        public void Tool_icon_ids_match_art_keys()
        {
            Assert.AreEqual("tool_select", TowerHudController.MenuIconIdForTool(BuildMenuTool.Select));
            Assert.AreEqual("tool_lobby", TowerHudController.MenuIconIdForTool(BuildMenuTool.Lobby));
            Assert.AreEqual("tool_sky_lobby", TowerHudController.MenuIconIdForTool(BuildMenuTool.SkyLobby));
            Assert.AreEqual("tool_scaffold", TowerHudController.MenuIconIdForTool(BuildMenuTool.Scaffold));
            Assert.AreEqual("tool_bulldoze", TowerHudController.MenuIconIdForTool(BuildMenuTool.Bulldoze));
        }

        [Test]
        public void Strip_icon_rect_lays_out_two_columns()
        {
            Assert.AreEqual(2, TowerHudController.MenuStripColumns);

            var left = TowerHudController.MenuStripIconRect(10f, 20f, 0);
            var right = TowerHudController.MenuStripIconRect(10f, 20f, 1);
            var nextRow = TowerHudController.MenuStripIconRect(10f, 20f, 2);

            Assert.AreEqual(10f, left.x);
            Assert.AreEqual(20f, left.y);
            Assert.AreEqual(TowerHudController.MenuIconSize, left.width);
            Assert.AreEqual(TowerHudController.MenuIconSize, left.height);

            Assert.AreEqual(10f + TowerHudController.MenuIconSize + TowerHudController.MenuIconGap, right.x);
            Assert.AreEqual(20f, right.y);

            Assert.AreEqual(10f, nextRow.x);
            Assert.AreEqual(20f + TowerHudController.MenuIconSize + TowerHudController.MenuIconGap, nextRow.y);
        }
    }
}
