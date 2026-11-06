using System.Collections.Generic;
using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BuildATower.Tests
{
    public class MenuCatalogTests
    {
#if UNITY_EDITOR
        static RoomTypeSO LoadLegacyRoom(string assetName) =>
            AssetDatabase.LoadAssetAtPath<RoomTypeSO>(
                $"Assets/ScriptableObjects/Rooms/{assetName}.asset");
#endif

        static IEnumerable<RoomTypeSO> AllCatalogRooms(IEnumerable<BuildCatalogFamily> catalog)
        {
            foreach (var family in catalog)
            {
                foreach (var room in family.Rooms)
                    yield return room;
                foreach (var subgroup in family.Subgroups)
                foreach (var room in subgroup.Rooms)
                    yield return room;
            }
        }

        static BuildCatalogFamily FindFamily(IEnumerable<BuildCatalogFamily> catalog, BuildFamily family) =>
            catalog.FirstOrDefault(entry => entry.Family == family);

        [Test]
        public void Catalog_excludes_legacy_office_hotel_condo()
        {
#if UNITY_EDITOR
            var legacyPlaceable = new List<RoomTypeSO>
            {
                LoadLegacyRoom("Office"),
                LoadLegacyRoom("HotelSingle"),
                LoadLegacyRoom("Condo"),
            };

            Assert.IsNotNull(legacyPlaceable[0], "Office legacy asset should load");
            Assert.IsNotNull(legacyPlaceable[1], "HotelSingle legacy asset should load");
            Assert.IsNotNull(legacyPlaceable[2], "Condo legacy asset should load");
            Assert.AreEqual("office", legacyPlaceable[0].id);
            Assert.AreEqual("hotel_single", legacyPlaceable[1].id);
            Assert.AreEqual("condo", legacyPlaceable[2].id);

            var catalog = TowerHudController.BuildMenuCatalogForTests(legacyPlaceable);
            var ids = AllCatalogRooms(catalog).Select(room => room.id).ToList();

            CollectionAssert.DoesNotContain(ids, "office");
            CollectionAssert.DoesNotContain(ids, "hotel");
            CollectionAssert.DoesNotContain(ids, "hotel_single");
            CollectionAssert.DoesNotContain(ids, "condo");
#else
            Assert.Inconclusive("Legacy ScriptableObject assets require Unity Editor.");
#endif
        }

        [Test]
        public void Catalog_office_hotel_condo_families_each_have_nine_luxury_rooms()
        {
            var catalog = TowerHudController.BuildMenuCatalogForTests(new List<RoomTypeSO>());

            var office = FindFamily(catalog, BuildFamily.Office);
            var hotel = FindFamily(catalog, BuildFamily.Hotel);
            var condo = FindFamily(catalog, BuildFamily.Condo);

            Assert.IsNotNull(office, "Office family should exist");
            Assert.IsNotNull(hotel, "Hotel family should exist");
            Assert.IsNotNull(condo, "Condo family should exist");
            Assert.AreEqual(9, office.Rooms.Count);
            Assert.AreEqual(9, hotel.Rooms.Count);
            Assert.AreEqual(9, condo.Rooms.Count);

            foreach (var room in office.Rooms)
                StringAssert.StartsWith("office_", room.id);
            foreach (var room in hotel.Rooms)
                StringAssert.StartsWith("hotel_", room.id);
            foreach (var room in condo.Rooms)
                StringAssert.StartsWith("condo_", room.id);
        }
    }
}
