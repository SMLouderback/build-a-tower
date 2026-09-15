using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class TowerAudioTests
    {
        [Test]
        public void CanPlayBuildSfx_blocks_within_cooldown()
        {
            Assert.IsFalse(TowerAudio.CanPlayBuildSfx(1.05f, 1.00f, 0.12f));
            Assert.IsFalse(TowerAudio.CanPlayBuildSfx(1.119f, 1.00f, 0.12f));
        }

        [Test]
        public void CanPlayBuildSfx_allows_after_cooldown()
        {
            Assert.IsTrue(TowerAudio.CanPlayBuildSfx(1.13f, 1.00f, 0.12f));
            Assert.IsTrue(TowerAudio.CanPlayBuildSfx(2.00f, 1.00f, 0.12f));
        }

        [Test]
        public void CanPlayBuildSfx_allows_first_play_when_last_is_negative()
        {
            Assert.IsTrue(TowerAudio.CanPlayBuildSfx(0f, float.NegativeInfinity));
        }

        [Test]
        public void AmbienceResourcePath_uses_lowercase_profile_name()
        {
            Assert.AreEqual("Audio/Ambience/office", TowerAudio.AmbienceResourcePath(SoundProfile.Office));
            Assert.AreEqual("Audio/Ambience/outdoor", TowerAudio.AmbienceResourcePath(SoundProfile.Outdoor));
            Assert.AreEqual("Audio/Ambience/parking", TowerAudio.AmbienceResourcePath(SoundProfile.Parking));
        }

        [Test]
        public void SfxResourcePaths_match_plan_constants()
        {
            Assert.AreEqual("Audio/Sfx/build_place", TowerAudio.SfxBuildPlacePath);
            Assert.AreEqual("Audio/Sfx/elevator_ping", TowerAudio.SfxElevatorPingPath);
            Assert.AreEqual("Audio/Sfx/elevator_door", TowerAudio.SfxElevatorDoorPath);
        }

        [Test]
        public void RescaleAmbienceVoice_zeros_when_new_gain_is_zero()
        {
            Assert.AreEqual(0f, TowerAudio.RescaleAmbienceVoice(0.35f, previousGain: 0.7f, newGain: 0f));
        }

        [Test]
        public void RescaleAmbienceVoice_scales_by_gain_ratio()
        {
            // Voice was 0.4 at gain 0.8 → half master/ambience should become 0.2
            Assert.AreEqual(0.2f, TowerAudio.RescaleAmbienceVoice(0.4f, previousGain: 0.8f, newGain: 0.4f), 1e-5f);
        }

        [Test]
        public void RescaleAmbienceVoice_keeps_volume_when_previous_gain_was_zero()
        {
            // Cannot reconstruct mix until next TickAmbient; leave as-is.
            Assert.AreEqual(0.15f, TowerAudio.RescaleAmbienceVoice(0.15f, previousGain: 0f, newGain: 0.7f));
        }
    }
}
