using System;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class StarCelebrationPauseTests
    {
        [Test]
        public void Capture_reads_clock_speed_and_paused()
        {
            var clock = new GameClock(4f);
            clock.Paused = true;

            var snap = StarCelebrationPauseGate.Capture(clock);

            Assert.AreEqual(4f, snap.MinutesPerRealSecond);
            Assert.IsTrue(snap.Paused);
        }

        [Test]
        public void Apply_restores_unpaused_speed()
        {
            float speed = 0f;
            var paused = true;
            var snap = new StarCelebrationPauseGate.SpeedSnapshot(8f, paused: false);

            StarCelebrationPauseGate.Apply((s, p) =>
            {
                speed = s;
                paused = p;
            }, snap);

            Assert.AreEqual(8f, speed);
            Assert.IsFalse(paused);
        }

        [Test]
        public void Apply_restores_paused_flag()
        {
            float speed = -1f;
            var paused = false;
            var snap = new StarCelebrationPauseGate.SpeedSnapshot(2f, paused: true);

            StarCelebrationPauseGate.Apply((s, p) =>
            {
                speed = s;
                paused = p;
            }, snap);

            Assert.AreEqual(2f, speed);
            Assert.IsTrue(paused);
        }

        [Test]
        public void Apply_clamps_unpaused_speed_to_minimum()
        {
            float speed = 99f;
            var snap = new StarCelebrationPauseGate.SpeedSnapshot(0f, paused: false);

            StarCelebrationPauseGate.Apply((s, p) => { speed = s; }, snap);

            Assert.AreEqual(0.01f, speed);
        }
    }
}
