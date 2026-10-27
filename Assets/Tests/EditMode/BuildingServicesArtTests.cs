using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildingServicesArtTests
    {
        [Test]
        public void Dollhouse_maps_mail_recycling_loading_dock()
        {
            Assert.AreEqual("mail_4x1", RoomDollhouseArt.ResourceLeaf(ParkingStalls.MailId));
            Assert.AreEqual("recycling_6x1", RoomDollhouseArt.ResourceLeaf(ParkingStalls.RecyclingId));
            Assert.AreEqual("loading_dock_8x1", RoomDollhouseArt.ResourceLeaf(ParkingStalls.LoadingDockId));

            Assert.IsTrue(RoomDollhouseArt.IsMapped(Type(ParkingStalls.MailId)));
            Assert.IsTrue(RoomDollhouseArt.IsMapped(Type(ParkingStalls.RecyclingId)));
            Assert.IsTrue(RoomDollhouseArt.IsMapped(Type(ParkingStalls.LoadingDockId)));
        }

        [Test]
        public void Menu_icons_load_for_building_services()
        {
            MenuIconArt.ResetCache();
            Assert.IsTrue(MenuIconArt.TryGetTexture(ParkingStalls.MailId, out var mail) && mail != null);
            Assert.IsTrue(MenuIconArt.TryGetTexture(ParkingStalls.RecyclingId, out var recycling) && recycling != null);
            Assert.IsTrue(MenuIconArt.TryGetTexture(ParkingStalls.LoadingDockId, out var dock) && dock != null);
            MenuIconArt.ResetCache();
        }

        static RoomTypeSO Type(string id)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.buildFamily = BuildFamily.Utility;
            so.category = RoomCategory.Service;
            return so;
        }
    }
}
