using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public sealed class TrophyRoomGalleryModalTests
    {
        [Test]
        public void Open_and_Close_toggle_IsOpen_and_BlocksWorld()
        {
            var modal = new TrophyRoomGalleryModal();
            Assert.IsFalse(modal.IsOpen);
            Assert.IsFalse(modal.BlocksWorld);

            modal.Open();
            Assert.IsTrue(modal.IsOpen);
            Assert.IsTrue(modal.BlocksWorld);

            modal.Close();
            Assert.IsFalse(modal.IsOpen);
            Assert.IsFalse(modal.BlocksWorld);
        }

        [Test]
        public void HandleEsc_closes_when_open_and_returns_true()
        {
            var modal = new TrophyRoomGalleryModal();
            modal.Open();
            Assert.IsTrue(modal.HandleEsc());
            Assert.IsFalse(modal.IsOpen);
            Assert.IsFalse(modal.HandleEsc());
        }

        [Test]
        public void Open_Close_API_has_no_pause_or_speed_members()
        {
            var modalType = typeof(TrophyRoomGalleryModal);
            foreach (var method in modalType.GetMethods(
                         System.Reflection.BindingFlags.Instance
                         | System.Reflection.BindingFlags.Public
                         | System.Reflection.BindingFlags.DeclaredOnly))
            {
                var name = method.Name;
                Assert.IsFalse(
                    name.IndexOf("Pause", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Speed", System.StringComparison.OrdinalIgnoreCase) >= 0,
                    $"Unexpected pause/speed API on gallery modal: {name}");
            }

            var modal = new TrophyRoomGalleryModal();
            modal.Open();
            Assert.IsTrue(modal.IsOpen);
            modal.Close();
            Assert.IsFalse(modal.IsOpen);
        }

        [Test]
        public void BlocksWorldInput_composition_includes_gallery_flag()
        {
            Assert.IsTrue(
                TrophyRoomGalleryModal.ComposeBlocksWorld(
                    escPause: false,
                    restoreNotice: false,
                    rewardTutorial: false,
                    gallery: true,
                    celebrationActive: false));
            Assert.IsFalse(
                TrophyRoomGalleryModal.ComposeBlocksWorld(
                    escPause: false,
                    restoreNotice: false,
                    rewardTutorial: false,
                    gallery: false,
                    celebrationActive: false));
        }
    }
}
