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
        public void Open_does_not_require_or_invoke_pause_callbacks()
        {
            // Modal API has no pause parameters; constructing/opening must not throw.
            var modal = new TrophyRoomGalleryModal();
            modal.Open();
            modal.Close();
            Assert.Pass();
        }
    }
}
