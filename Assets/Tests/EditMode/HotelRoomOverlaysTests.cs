using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class HotelRoomOverlaysTests
    {
        [Test]
        public void CutawayTileTint_clean_is_white()
        {
            Assert.AreEqual(Color.white, HotelRoomOverlays.CutawayTileTint(false, false));
        }

        [Test]
        public void CutawayTileTint_dirty_only_is_warm_brownish()
        {
            var tint = HotelRoomOverlays.CutawayTileTint(true, false);
            Assert.AreEqual(HotelRoomOverlays.DirtyOnlyTint, tint);
            Assert.AreNotEqual(Color.white, tint);
            Assert.Less(tint.b, tint.r);
            Assert.Less(tint.g, tint.r);
        }

        [Test]
        public void CutawayTileTint_broken_only_matches_office_wash()
        {
            Assert.AreEqual(
                OfficeCondemnedOverlay.BrokenTileTint,
                HotelRoomOverlays.CutawayTileTint(false, true));
        }

        [Test]
        public void CutawayTileTint_dirty_and_broken_keeps_both_cues()
        {
            var broken = HotelRoomOverlays.CutawayTileTint(false, true);
            var both = HotelRoomOverlays.CutawayTileTint(true, true);

            Assert.AreEqual(HotelRoomOverlays.DirtyAndBrokenTint, both);
            Assert.AreNotEqual(broken, both);
            // Stronger brown in the wash: bluer channel drops vs broken-only grey.
            Assert.Less(both.b, broken.b);
            Assert.Less(both.g, broken.g);
        }

        [Test]
        public void CutawayTileTint_room_overload_uses_Dirty_and_IsBroken()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "hotel_base";
            type.category = RoomCategory.Hotel;
            type.size = new Vector2Int(3, 1);

            var room = new RoomInstance(1, type, new Vector2Int(0, 2), new Vector2Int(3, 1));
            Assert.AreEqual(Color.white, HotelRoomOverlays.CutawayTileTint(room));

            room.MarkDirty();
            Assert.AreEqual(HotelRoomOverlays.DirtyOnlyTint, HotelRoomOverlays.CutawayTileTint(room));

            room.Condition = 0;
            Assert.AreEqual(HotelRoomOverlays.DirtyAndBrokenTint, HotelRoomOverlays.CutawayTileTint(room));

            room.ClearDirty();
            Assert.AreEqual(OfficeCondemnedOverlay.BrokenTileTint, HotelRoomOverlays.CutawayTileTint(room));
        }
    }
}
