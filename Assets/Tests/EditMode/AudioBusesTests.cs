using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class AudioBusesTests
    {
        [Test]
        public void Effective_mute_zeros_all_buses()
        {
            var b = new AudioBuses { MasterMute = true, Master = 1f, Sfx = 1f, Ambience = 1f, Music = 1f };
            Assert.AreEqual(0f, b.Effective(AudioBus.Sfx));
            Assert.AreEqual(0f, b.Effective(AudioBus.Ambience));
        }

        [Test]
        public void Effective_is_master_times_bus()
        {
            var b = new AudioBuses { MasterMute = false, Master = 0.5f, Sfx = 0.4f };
            Assert.AreEqual(0.2f, b.Effective(AudioBus.Sfx), 0.0001f);
        }
    }
}
