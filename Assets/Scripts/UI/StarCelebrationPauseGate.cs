using System;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Snapshot / restore helpers for celebration pause, mirroring Esc pause restore.
    /// </summary>
    public static class StarCelebrationPauseGate
    {
        public readonly struct SpeedSnapshot
        {
            public float MinutesPerRealSecond { get; }
            public bool Paused { get; }

            public SpeedSnapshot(float minutesPerRealSecond, bool paused)
            {
                MinutesPerRealSecond = minutesPerRealSecond;
                Paused = paused;
            }
        }

        public static SpeedSnapshot Capture(GameClock clock)
        {
            if (clock == null)
                return new SpeedSnapshot(1f, paused: false);
            return new SpeedSnapshot(clock.MinutesPerRealSecond, clock.Paused);
        }

        /// <summary>Testable restore path (same branching as <see cref="TowerHudController"/> resume).</summary>
        public static void Apply(Action<float, bool> setSpeedPreset, SpeedSnapshot snap)
        {
            if (setSpeedPreset == null) return;
            if (snap.Paused)
                setSpeedPreset(snap.MinutesPerRealSecond, true);
            else
                setSpeedPreset(Mathf.Max(0.01f, snap.MinutesPerRealSecond), false);
        }

        public static void Apply(TowerSimulation sim, SpeedSnapshot snap)
        {
            if (sim == null) return;
            Apply(sim.SetSpeedPreset, snap);
        }
    }
}
