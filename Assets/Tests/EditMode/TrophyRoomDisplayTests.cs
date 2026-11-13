using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class TrophyRoomDisplayTests
    {
        [TearDown]
        public void TearDown() => TrophyRoomDisplay.ResetChipCache();

        [Test]
        public void BuildPedestals_maps_empty_case_to_empty_slots_for_every_subject()
        {
            var slots = TrophyRoomDisplay.BuildPedestals(new TrophyCase());

            Assert.AreEqual(10, slots.Count);
            Assert.IsTrue(slots.All(slot => !slot.HasTrophy));
            Assert.IsTrue(slots.All(slot => slot.Metal == TrophyMetal.None));
            CollectionAssert.AreEqual(
                System.Enum.GetValues(typeof(ChallengeSubject)).Cast<ChallengeSubject>(),
                slots.Select(slot => slot.Subject));
        }

        [Test]
        public void BuildPedestals_binds_claimed_metals_to_subject_sprite_keys()
        {
            var trophyCase = new TrophyCase();
            trophyCase.ApplyClaimMetal(ChallengeSubject.Stars, TrophyMetal.Silver);
            trophyCase.ApplyClaimMetal(ChallengeSubject.Commerce, TrophyMetal.Gold);

            var slots = TrophyRoomDisplay.BuildPedestals(trophyCase);

            var stars = slots.Single(slot => slot.Subject == ChallengeSubject.Stars);
            var commerce = slots.Single(slot => slot.Subject == ChallengeSubject.Commerce);
            Assert.IsTrue(stars.HasTrophy);
            Assert.AreEqual(TrophyMetal.Silver, stars.Metal);
            Assert.AreEqual("trophy_stars_silver", stars.SpriteKey);
            Assert.IsTrue(commerce.HasTrophy);
            Assert.AreEqual(TrophyMetal.Gold, commerce.Metal);
            Assert.AreEqual("trophy_commerce_gold", commerce.SpriteKey);
        }

        [Test]
        public void MetalSpriteResourcePath_uses_shared_Art_Trophies_folder()
        {
            Assert.AreEqual(
                "Art/Trophies/trophy_population_bronze",
                TrophyRoomDisplay.MetalSpriteResourcePath(ChallengeSubject.Population, TrophyMetal.Bronze));
        }

        [Test]
        public void CutawayChipNormalizedCenters_one_per_subject_in_enum_order()
        {
            var centers = TrophyRoomDisplay.CutawayChipNormalizedCenters;
            Assert.AreEqual(10, centers.Length);
            foreach (var c in centers)
            {
                Assert.GreaterOrEqual(c.x, 0f);
                Assert.LessOrEqual(c.x, 1f);
                Assert.GreaterOrEqual(c.y, 0f);
                Assert.LessOrEqual(c.y, 1f);
            }
        }

        [Test]
        public void LoadChipSprite_keeps_opaque_bronze_cup_without_plate_box()
        {
            var sprite = TrophyRoomDisplay.LoadChipSprite("trophy_population_bronze");
            Assert.IsNotNull(sprite);
            Assert.IsNotNull(sprite.texture);

            var tex = sprite.texture;
            var center = tex.GetPixel(tex.width / 2, tex.height / 2);
            Assert.Greater(center.a, 0.9f, "Cup body must stay opaque");

            var clearCorners = 0;
            var corners = new[]
            {
                tex.GetPixel(0, 0),
                tex.GetPixel(tex.width - 1, 0),
                tex.GetPixel(0, tex.height - 1),
                tex.GetPixel(tex.width - 1, tex.height - 1)
            };
            foreach (var c in corners)
            {
                if (c.a < 0.08f)
                    clearCorners++;
            }

            Assert.GreaterOrEqual(clearCorners, 3, "Matted medals should not keep a solid plate box");

            var opaque = 0;
            var px = tex.GetPixels();
            for (var i = 0; i < px.Length; i++)
            {
                if (px[i].a > 0.9f)
                    opaque++;
            }

            Assert.Greater(opaque, 1500, $"Expected intact medal pixels, got {opaque}");
        }
    }
}
