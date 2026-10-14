using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    /// <summary>Spec §4 locked footprints — Room Art Face Variants (2026-10-14).</summary>
    public sealed class RoomSizeRebalanceTests
    {
        static RoomTypeSO ById(string id)
        {
            foreach (var room in Resources.LoadAll<RoomTypeSO>("Rooms"))
            {
                if (room != null && room.id == id)
                    return room;
            }

            return null;
        }

        static void AssertFootprint(string id, int width, int height, int? maxOccupants = null)
        {
            var room = ById(id);
            Assert.IsNotNull(room, $"Resources/Rooms should contain {id}");
            Assert.AreEqual(new Vector2Int(width, height), room.size, id);
            if (maxOccupants.HasValue)
                Assert.AreEqual(maxOccupants.Value, room.maxOccupants, id);
        }

        [Test]
        public void Office_footprints_match_spec_section_4_1()
        {
            AssertFootprint("office_micro", 3, 1, 1);
            AssertFootprint("office_studio", 4, 1, 2);
            AssertFootprint("office_base", 6, 1, 3);
            AssertFootprint("office_mid_standard", 8, 1, 5);
            AssertFootprint("office_mid_clinic", 9, 1, 6);
            AssertFootprint("office_mid_team", 10, 1, 8);
            AssertFootprint("office_upper_standard", 10, 1, 8);
            AssertFootprint("office_upper_corner", 12, 1, 10);
            AssertFootprint("office_upper_floor", 14, 1, 14);
        }

        [Test]
        public void Hotel_footprints_match_spec_section_4_2()
        {
            AssertFootprint("hotel_base", 3, 1, 2);
            AssertFootprint("hotel_accessible", 3, 1, 2);
            AssertFootprint("hotel_mid_standard", 4, 1, 4);
            AssertFootprint("hotel_mid_extended", 6, 1, 6);
            AssertFootprint("hotel_studio", 5, 1, 3);
            AssertFootprint("hotel_junior_suite", 5, 1, 4);
            AssertFootprint("hotel_upper_standard", 5, 1, 4);
            AssertFootprint("hotel_upper_king", 5, 1, 2);
            AssertFootprint("hotel_upper_suite", 8, 1, 8);
        }

        [Test]
        public void Condo_footprints_match_spec_section_4_3()
        {
            AssertFootprint("condo_studio", 4, 1, 1);
            AssertFootprint("condo_alcove", 5, 1, 2);
            AssertFootprint("condo_base", 8, 1, 3);
            AssertFootprint("condo_mid_standard", 9, 1, 4);
            AssertFootprint("condo_mid_loft", 10, 1, 3);
            AssertFootprint("condo_mid_family", 12, 1, 5);
            AssertFootprint("condo_upper_standard", 10, 1, 4);
            AssertFootprint("condo_upper_corner", 12, 1, 5);
            AssertFootprint("condo_upper_penthouse", 14, 1, 6);
        }

        [Test]
        public void Shop_footprints_match_spec_section_4_4()
        {
            AssertFootprint("shop_food_taco", 6, 1, 5);
            AssertFootprint("shop_food_chicken", 6, 1, 6);
            AssertFootprint("shop_food_fast", 8, 1, 6);
            AssertFootprint("shop_food_restaurant", 10, 1, 8);
            AssertFootprint("shop_food_mexican", 10, 1, 8);
            AssertFootprint("shop_food_fine", 8, 1, 10);
            AssertFootprint("shop_retail", 8, 1, 6);
            AssertFootprint("shop_retail_gifts", 6, 1, 5);
            AssertFootprint("shop_retail_shoes", 6, 1, 5);
            AssertFootprint("shop_retail_department", 12, 2, 16);
        }

        [Test]
        public void Service_and_parking_footprints_match_spec_section_4_5()
        {
            AssertFootprint("service_security", 2, 1);
            AssertFootprint("service_housekeeping", 4, 1);
            AssertFootprint("service_maintenance", 4, 1);
            AssertFootprint("service_research", 8, 1);
            AssertFootprint("service_mail", 4, 1);
            AssertFootprint("service_recycling", 6, 1);
            AssertFootprint("service_loading_dock", 8, 1);
            AssertFootprint("service_conference", 8, 1);
            AssertFootprint("service_event_hall", 12, 2);
            AssertFootprint("parking_underground", 6, 1);
            AssertFootprint("service_valet", 3, 1);
        }

        [Test]
        public void Conference_and_event_hall_event_capacity_unchanged()
        {
            Assert.AreEqual(40, ById("service_conference").eventCapacity);
            Assert.AreEqual(120, ById("service_event_hall").eventCapacity);
        }

        [Test]
        public void Leisure_and_metro_footprints_match_spec_section_4_6()
        {
            AssertFootprint("leisure_gym", 6, 1);
            AssertFootprint("leisure_spa", 6, 1);
            AssertFootprint("leisure_chapel", 6, 1);
            AssertFootprint("leisure_nightclub", 8, 1);
            AssertFootprint("leisure_bowling", 10, 1);
            AssertFootprint("leisure_casino", 10, 1);
            AssertFootprint("leisure_pool", 8, 2);
            AssertFootprint("leisure_theater", 8, 2);
            AssertFootprint("leisure_cathedral", 10, 2);
            AssertFootprint("leisure_atrium", 8, 3);
            AssertFootprint("metro_station", 8, 3);
        }
    }
}
