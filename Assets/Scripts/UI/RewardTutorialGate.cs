using System;

namespace BuildATower
{
    public sealed class RewardTutorialGate
    {
        StarCelebrationPauseGate.SpeedSnapshot _snapshot;

        public bool IsModalOpen { get; private set; }

        public bool TryOpen(
            RewardsSystem rewards,
            bool firstTrophyOnTower,
            GameClock clock,
            Action<float, bool> setSpeedPreset)
        {
            if (IsModalOpen || rewards == null || rewards.RewardTutorialSeen || !firstTrophyOnTower)
                return false;

            _snapshot = StarCelebrationPauseGate.Capture(clock);
            setSpeedPreset?.Invoke(_snapshot.MinutesPerRealSecond, true);
            IsModalOpen = true;
            return true;
        }

        public void Dismiss(RewardsSystem rewards, Action<float, bool> setSpeedPreset)
        {
            if (!IsModalOpen)
                return;

            IsModalOpen = false;
            if (rewards != null)
                rewards.RewardTutorialSeen = true;
            StarCelebrationPauseGate.Apply(setSpeedPreset, _snapshot);
        }
    }
}
