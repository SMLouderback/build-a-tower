using System;
using System.Collections.Generic;

namespace BuildATower
{
    public sealed class ResearchSystem
    {
        const float EtaEpsilon = 0.0001f;

        readonly HashSet<(ResearchBranch Branch, int Level)> _completed = new();
        readonly Dictionary<(ResearchBranch Branch, int Level), float> _progress = new();

        ResearchBranch? _activeBranch;
        int _activeLevel;
        bool _paused;

        public ResearchBranch? ActiveBranch => _activeBranch;
        public int ActiveLevel => _activeBranch.HasValue ? _activeLevel : 0;
        public float ActiveProgress =>
            _activeBranch.HasValue ? GetProgress(_activeBranch.Value, _activeLevel) : 0f;
        public bool IsRunning => _activeBranch.HasValue;
        public bool IsPaused => _paused;

        public bool IsComplete(ResearchBranch branch, int level) =>
            _completed.Contains((branch, level));

        public int HighestCompleted(ResearchBranch branch)
        {
            for (var level = ResearchCatalog.MaxLevel; level >= 1; level--)
            {
                if (IsComplete(branch, level))
                    return level;
            }

            return 0;
        }

        public bool CanStart(ResearchBranch branch, int level)
        {
            if (level < 1 || level > ResearchCatalog.MaxLevel)
                return false;
            if (IsComplete(branch, level))
                return false;
            if (level == 1)
                return true;
            return IsComplete(branch, level - 1);
        }

        public bool TryStart(ResearchBranch branch, int level)
        {
            if (!CanStart(branch, level))
                return false;

            PersistActiveProgress();
            _activeBranch = branch;
            _activeLevel = level;
            _paused = false;
            return true;
        }

        public void Pause()
        {
            if (!_activeBranch.HasValue)
                return;
            PersistActiveProgress();
            _paused = true;
        }

        public void TickProgress(float deltaGameMinutes, int researcherPool)
        {
            if (!_activeBranch.HasValue)
                return;

            if (_paused || researcherPool <= 0)
            {
                _paused = true;
                PersistActiveProgress();
                return;
            }

            if (deltaGameMinutes <= 0f)
                return;

            var key = (_activeBranch.Value, _activeLevel);
            var baseWork = ResearchCatalog.BaseWorkMinutes(_activeLevel);
            var next = GetProgress(key.Item1, key.Item2) + WorkPerGameMinute(researcherPool) * deltaGameMinutes;
            if (next >= baseWork)
            {
                _completed.Add(key);
                _progress.Remove(key);
                _activeBranch = null;
                _activeLevel = 0;
                _paused = false;
                return;
            }

            _progress[key] = next;
        }

        public void TickDayDecay()
        {
            // Spec §7.3: decay all incomplete stored nodes except the currently
            // running unpaused project (switched-away progress still decays).
            if (_progress.Count == 0)
                return;

            var keys = new List<(ResearchBranch Branch, int Level)>(_progress.Keys);
            foreach (var key in keys)
            {
                var isActiveRunningUnpaused =
                    _activeBranch.HasValue &&
                    !_paused &&
                    key.Branch == _activeBranch.Value &&
                    key.Level == _activeLevel;
                if (isActiveRunningUnpaused)
                    continue;

                var baseWork = ResearchCatalog.BaseWorkMinutes(key.Level);
                var decay = ResearchCatalog.DecayFractionPerDay * baseWork;
                _progress[key] = Math.Max(0f, GetProgress(key.Branch, key.Level) - decay);
            }
        }

        public float WorkPerGameMinute(int researcherPool) =>
            researcherPool <= 0
                ? 0f
                : 1f + (researcherPool - 1) * ResearchCatalog.ResearcherSpeedBonus;

        public float EstimateEtaMinutes(int researcherPool)
        {
            if (!_activeBranch.HasValue)
                return 0f;

            var remaining = ResearchCatalog.BaseWorkMinutes(_activeLevel) - ActiveProgress;
            if (remaining <= 0f)
                return 0f;

            var rate = WorkPerGameMinute(researcherPool);
            if (rate <= EtaEpsilon)
                return float.PositiveInfinity;

            return remaining / rate;
        }

        public int EstimateRemainingCost(int researcherPool, int nonBrokenLabs, float climateMult)
        {
            var etaMinutes = EstimateEtaMinutes(researcherPool);
            if (float.IsInfinity(etaMinutes) || etaMinutes <= 0f || !_activeBranch.HasValue)
                return 0;

            var etaDays = etaMinutes / (24f * 60f);
            var daily = ResearchCatalog.IdlePerLabPerDay * Math.Max(0, nonBrokenLabs)
                        + ResearchCatalog.ActivePerDay;
            return (int)Math.Round(etaDays * daily * climateMult);
        }

        float GetProgress(ResearchBranch branch, int level) =>
            _progress.TryGetValue((branch, level), out var value) ? value : 0f;

        void PersistActiveProgress()
        {
            if (!_activeBranch.HasValue)
                return;

            var key = (_activeBranch.Value, _activeLevel);
            if (!_progress.ContainsKey(key))
                _progress[key] = 0f;
        }
    }
}
