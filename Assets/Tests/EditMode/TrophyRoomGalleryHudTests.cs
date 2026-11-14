using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class TrophyRoomGalleryHudTests
    {
        [Test]
        public void FormatPedestalLabels_counts_every_subject_and_marks_empty_slots()
        {
            var labels = TrophyRoomGalleryHud.FormatPedestalLabels(new TrophyCase());

            Assert.AreEqual(10, labels.Length);
            Assert.IsTrue(labels.All(label => label.Contains("Empty")));
            CollectionAssert.AreEqual(
                System.Enum.GetValues(typeof(ChallengeSubject)).Cast<ChallengeSubject>().Select(subject => subject.ToString()),
                labels.Select(label => label.Split('\n')[0]));
        }

        [Test]
        public void FormatPedestalLabel_names_subject_and_metal_sprite()
        {
            var slot = new TrophyRoomDisplay.PedestalSlot(ChallengeSubject.Stars, TrophyMetal.Silver);

            var label = TrophyRoomGalleryHud.FormatPedestalLabel(slot);

            StringAssert.Contains("Stars", label);
            StringAssert.Contains("Silver", label);
            Assert.AreEqual(
                "Art/Trophies/trophy_stars_silver",
                TrophyRoomGalleryHud.PedestalSpritePath(slot));
        }

        [Test]
        public void Empty_slot_uses_placeholder_instead_of_a_metal_sprite()
        {
            var slot = new TrophyRoomDisplay.PedestalSlot(ChallengeSubject.Time, TrophyMetal.None);

            StringAssert.Contains("Time", TrophyRoomGalleryHud.FormatPedestalLabel(slot));
            StringAssert.Contains("Empty", TrophyRoomGalleryHud.FormatPedestalLabel(slot));
            Assert.AreEqual(string.Empty, TrophyRoomGalleryHud.PedestalSpritePath(slot));
        }

        [Test]
        public void LayoutPedestalCells_covers_ten_readable_slots_without_overlap()
        {
            var panel = new Rect(0f, 0f, TrophyRoomGalleryHud.PreferredPanelWidth, TrophyRoomGalleryHud.PreferredPanelHeight);
            var content = TrophyRoomGalleryHud.PedestalContentRect(panel);
            var cells = TrophyRoomGalleryHud.LayoutPedestalCells(content, 10);

            Assert.AreEqual(10, cells.Length);
            for (var i = 0; i < cells.Length; i++)
            {
                Assert.GreaterOrEqual(cells[i].width, 80f, "pedestal column should stay readable");
                Assert.GreaterOrEqual(cells[i].height, 100f, "pedestal column should fit a medal and a name");
                Assert.GreaterOrEqual(cells[i].x, content.x - 0.01f);
                Assert.GreaterOrEqual(cells[i].y, content.y - 0.01f);
                Assert.LessOrEqual(cells[i].xMax, content.xMax + 0.01f);
                Assert.LessOrEqual(cells[i].yMax, content.yMax + 0.01f);
                for (var j = i + 1; j < cells.Length; j++)
                    Assert.IsFalse(cells[i].Overlaps(cells[j]));
            }
        }

        [Test]
        public void Background_prefers_closeup_plate_and_falls_back_to_shell()
        {
            Assert.AreEqual(
                "Art/Dollhouse/trophy_room_gallery_closeup",
                TrophyRoomGalleryHud.ResolveBackgroundPath(closeupPresent: true));
            Assert.AreEqual(
                "Art/Dollhouse/trophy_room_6x1",
                TrophyRoomGalleryHud.ResolveBackgroundPath(closeupPresent: false));
        }

        [Test]
        public void Closeup_plate_is_larger_than_the_cutaway_shell_and_bytes_match()
        {
            var dir = Path.Combine(Application.dataPath, "Resources", "Art", "Dollhouse");
            var png = Path.Combine(dir, "trophy_room_gallery_closeup.png");
            var bytesPath = Path.Combine(dir, "trophy_room_gallery_closeup.bytes");
            var shell = Path.Combine(dir, "trophy_room_6x1.png");

            Assert.IsTrue(File.Exists(png), png);
            Assert.IsTrue(File.Exists(bytesPath), bytesPath);
            CollectionAssert.AreEqual(File.ReadAllBytes(png), File.ReadAllBytes(bytesPath));
            Assert.Greater(new FileInfo(png).Length, new FileInfo(shell).Length);
        }
    }
}
