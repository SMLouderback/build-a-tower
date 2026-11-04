using System.Linq;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public sealed class TrophyRoomDisplayTests
    {
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
    }
}
