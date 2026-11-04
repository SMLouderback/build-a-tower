using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public sealed class TrophyRoomOpenPolicyTests
    {
        const string TrophyId = "service_trophy_room";

        [Test]
        public void First_select_of_trophy_room_does_not_open()
        {
            Assert.IsFalse(TrophyRoomOpenPolicy.ShouldOpenGallery(
                TrophyId, 7, selectedTypeId: null, selectedInstanceId: 0, hasSelection: false));
        }

        [Test]
        public void Second_select_of_same_trophy_room_opens()
        {
            Assert.IsTrue(TrophyRoomOpenPolicy.ShouldOpenGallery(
                TrophyId, 7, selectedTypeId: TrophyId, selectedInstanceId: 7, hasSelection: true));
        }

        [Test]
        public void Select_of_different_room_does_not_open()
        {
            Assert.IsFalse(TrophyRoomOpenPolicy.ShouldOpenGallery(
                TrophyId, 8, selectedTypeId: TrophyId, selectedInstanceId: 7, hasSelection: true));
            Assert.IsFalse(TrophyRoomOpenPolicy.ShouldOpenGallery(
                "service_mail", 7, selectedTypeId: TrophyId, selectedInstanceId: 7, hasSelection: true));
        }
    }
}
