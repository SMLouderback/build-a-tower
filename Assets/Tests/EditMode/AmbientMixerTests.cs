using System.Linq;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class AmbientMixerTests
    {
        [Test]
        public void RankProfiles_keeps_top_four_and_boosts_selection()
        {
            var rooms = new[]
            {
                (SoundProfile.Office, 1f),
                (SoundProfile.Office, 1f),
                (SoundProfile.Restaurant, 0.8f),
                (SoundProfile.Parking, 0.7f),
                (SoundProfile.Condo, 0.6f),
                (SoundProfile.Retail, 0.5f),
            };
            var ranked = AmbientMixer.RankProfiles(rooms, SoundProfile.Condo, maxVoices: 4);
            Assert.AreEqual(4, ranked.Count);
            Assert.IsTrue(ranked.Exists(e => e.profile == SoundProfile.Condo));
            var condo = ranked.Find(e => e.profile == SoundProfile.Condo).weight;
            Assert.Greater(condo, 0.6f * 2f);
        }

        [Test]
        public void ProfileBias_marks_loud_and_quiet_types()
        {
            Assert.Greater(AmbientMixer.ProfileBias(SoundProfile.Parking), 1.1f);
            Assert.Less(AmbientMixer.ProfileBias(SoundProfile.Stairs), 0.8f);
        }

        [Test]
        public void ScoreRoom_multiplies_visibility_activity_and_bias()
        {
            var weight = AmbientMixer.ScoreRoom(0.5f, 0.8f, SoundProfile.Parking);
            Assert.AreEqual(0.5f * 0.8f * AmbientMixer.ProfileBias(SoundProfile.Parking), weight, 0.0001f);
        }

        [Test]
        public void RankProfiles_aggregates_same_profile_before_ranking()
        {
            var rooms = new[]
            {
                (SoundProfile.Office, 0.4f),
                (SoundProfile.Office, 0.3f),
                (SoundProfile.Retail, 0.5f),
            };
            var ranked = AmbientMixer.RankProfiles(rooms, null);
            Assert.AreEqual(SoundProfile.Office, ranked[0].profile);
            Assert.AreEqual(0.7f, ranked[0].weight, 0.0001f);
        }
    }
}
