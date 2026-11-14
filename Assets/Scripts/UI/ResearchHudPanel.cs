using UnityEngine;

namespace BuildATower
{
    public static class ResearchHudPanel
    {
        public static string StatusCaption(ResearchSystem research)
        {
            if (research == null || !research.IsRunning)
                return "Research · Idle";

            if (research.IsPaused)
                return "Research · Paused";

            var branch = research.ActiveBranch!.Value;
            return $"Research · {ResearchCatalog.BranchDisplayName(branch)} L{research.ActiveLevel}";
        }

        public static float Draw(
            float cx,
            float cy,
            float inner,
            float btnH,
            float row,
            ResearchSystem research,
            TowerGrid grid,
            MarketClimate climate,
            ref ResearchBranch pickBranch,
            ref int pickLevel,
            GUIStyle label)
        {
            if (research == null || grid == null)
                return cy;

            SyncResearchPick(research, ref pickBranch, ref pickLevel);

            var pool = EconomySystem.CountResearcherPool(grid);
            var labs = EconomySystem.CountNonBrokenResearchLabs(grid);
            var climateName = climate?.Name ?? "—";
            var climateMult = climate?.SpendMultiplier ?? 1f;
            var idleDay = ResearchCatalog.IdlePerLabPerDay * labs;
            var activeDay = research.IsRunning && !research.IsPaused
                ? ResearchCatalog.ActivePerDay
                : 0;

            GUI.Label(new Rect(cx, cy, inner, row), $"Researchers in pool: {pool}", label);
            cy += row;

            var branches = (ResearchBranch[])System.Enum.GetValues(typeof(ResearchBranch));
            const float levelGap = 3f;
            var nameW = Mathf.Min(118f, inner * 0.42f);
            var levelW = (inner - nameW - levelGap * 2f) / 3f;

            foreach (var branch in branches)
            {
                GUI.Label(
                    new Rect(cx, cy, nameW, btnH),
                    ResearchCatalog.BranchDisplayName(branch),
                    label);

                for (var level = 1; level <= ResearchCatalog.MaxLevel; level++)
                {
                    var rect = new Rect(
                        cx + nameW + (level - 1) * (levelW + levelGap),
                        cy,
                        levelW,
                        btnH);
                    var picked = pickBranch == branch && pickLevel == level;
                    var caption = ResearchLevelCaption(research, branch, level);
                    var wasEnabled = GUI.enabled;
                    var locked = !research.IsComplete(branch, level) && !research.CanStart(branch, level);
                    GUI.enabled = wasEnabled && !locked;
                    if (GUI.Toggle(rect, picked, caption, GUI.skin.button) && !picked && !locked)
                    {
                        pickBranch = branch;
                        pickLevel = level;
                    }

                    GUI.enabled = wasEnabled;
                }

                cy += btnH + 2f;
            }

            cy += 2f;
            var pickComplete = research.IsComplete(pickBranch, pickLevel);
            var isPickActive = research.ActiveBranch == pickBranch && research.ActiveLevel == pickLevel;
            var canStart = research.CanStart(pickBranch, pickLevel);

            var effect = ResearchCatalog.LevelEffectSummary(pickBranch, pickLevel);
            if (!string.IsNullOrEmpty(effect))
            {
                var effectH = row * 2.2f;
                GUI.Label(
                    new Rect(cx, cy, inner, effectH),
                    $"Effect: {effect}",
                    label);
                cy += effectH + 2f;
            }

            const float actionGap = 4f;
            var actionW = (inner - actionGap) * 0.5f;
            var startEnabled = canStart && !(isPickActive && !research.IsPaused);
            var pauseEnabled = isPickActive && !research.IsPaused;

            var prev = GUI.enabled;
            GUI.enabled = prev && startEnabled;
            if (GUI.Button(new Rect(cx, cy, actionW, btnH), "Start") && startEnabled)
                research.TryStart(pickBranch, pickLevel);
            GUI.enabled = prev && pauseEnabled;
            if (GUI.Button(new Rect(cx + actionW + actionGap, cy, actionW, btnH), "Pause") && pauseEnabled)
                research.Pause();
            GUI.enabled = prev;
            cy += btnH + 4f;

            if (pickComplete)
            {
                GUI.Label(new Rect(cx, cy, inner, row), "Selected tech: complete ✓", label);
                cy += row;
            }
            else
            {
                var eta = research.EstimateEtaMinutes(pickBranch, pickLevel, pool);
                var est = research.EstimateRemainingCost(
                    pickBranch, pickLevel, pool, labs, climateMult);
                GUI.Label(new Rect(cx, cy, inner, row), $"ETA: {FormatResearchEta(eta)}", label);
                cy += row;
                GUI.Label(new Rect(cx, cy, inner, row), $"Est. remaining $: ${est:N0}", label);
                cy += row;
            }

            GUI.Label(new Rect(cx, cy, inner, row), $"Idle/day: ${idleDay:N0}", label);
            cy += row;
            GUI.Label(new Rect(cx, cy, inner, row), $"Active/day: ${activeDay:N0}", label);
            cy += row;
            GUI.Label(
                new Rect(cx, cy, inner, row),
                $"Climate: {climateName} ×{climateMult:0.00}",
                label);
            cy += row;

            if (research.IsRunning && research.IsPaused)
            {
                GUI.Label(new Rect(cx, cy, inner, row), "Paused — progress decaying", label);
                cy += row;
            }

            GUI.Label(
                new Rect(cx, cy, inner, row * 2f),
                "Estimate at current climate & staff; actual burn changes if climate shifts.",
                label);
            cy += row * 2f + 4f;
            return cy;
        }

        static void SyncResearchPick(
            ResearchSystem research,
            ref ResearchBranch pickBranch,
            ref int pickLevel)
        {
            var pickOk =
                research.IsComplete(pickBranch, pickLevel) ||
                research.CanStart(pickBranch, pickLevel) ||
                (research.ActiveBranch == pickBranch &&
                 research.ActiveLevel == pickLevel);
            if (pickOk)
                return;

            if (research.IsRunning && research.ActiveBranch.HasValue)
            {
                pickBranch = research.ActiveBranch.Value;
                pickLevel = research.ActiveLevel;
                return;
            }

            foreach (ResearchBranch branch in System.Enum.GetValues(typeof(ResearchBranch)))
            {
                for (var level = 1; level <= ResearchCatalog.MaxLevel; level++)
                {
                    if (!research.CanStart(branch, level)) continue;
                    pickBranch = branch;
                    pickLevel = level;
                    return;
                }
            }
        }

        static string ResearchLevelCaption(ResearchSystem research, ResearchBranch branch, int level)
        {
            var roman = level switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                _ => level.ToString()
            };

            if (research.IsComplete(branch, level))
                return $"{roman} ✓";
            if (!research.CanStart(branch, level))
                return $"{roman} locked";

            var pct = research.GetProgressPercent(branch, level);
            if (pct > 0.05f ||
                (research.ActiveBranch == branch && research.ActiveLevel == level))
                return $"{roman} {pct:0}%";
            return roman;
        }

        static string FormatResearchEta(float etaMinutes)
        {
            if (float.IsInfinity(etaMinutes))
                return "∞ (need researchers)";
            if (etaMinutes <= 0f)
                return "—";

            var totalMinutes = Mathf.CeilToInt(etaMinutes);
            var days = totalMinutes / (24 * 60);
            var hours = totalMinutes % (24 * 60) / 60;
            var mins = totalMinutes % 60;
            if (days > 0)
                return $"{days}d {hours}h";
            if (hours > 0)
                return $"{hours}h {mins}m";
            return $"{mins}m";
        }
    }
}
