using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public enum RewardBookTab
    {
        Ready,
        Catalog,
        Contracts,
        Trophies
    }

    public static class RewardBookHud
    {
        static readonly string[] Tabs = { "Ready", "Catalog", "Contracts", "Trophies" };

        public static bool ShouldShowButton(bool hasLobby, int pendingCount) =>
            hasLobby || pendingCount > 0;

        public static string BadgeText(int pendingCount)
        {
            if (pendingCount <= 0)
                return string.Empty;
            return pendingCount > 99 ? "99+" : pendingCount.ToString();
        }

        public static string[] TabLabels() => (string[])Tabs.Clone();

        public static float BadgePulseScale(int pendingCount, float unscaledTime)
        {
            if (pendingCount <= 0)
                return 1f;
            return 1f + Mathf.Sin(unscaledTime * 7f) * 0.08f;
        }

        public static float BadgePulseAlpha(int pendingCount, float unscaledTime)
        {
            if (pendingCount <= 0)
                return 0.9f;
            return 0.65f + (Mathf.Sin(unscaledTime * 7f) + 1f) * 0.175f;
        }

        public static List<ChallengeDef> ReadyRewards(
            RewardsSystem rewards,
            IReadOnlyList<ChallengeDef> catalog = null)
        {
            var ready = new List<ChallengeDef>();
            if (rewards == null)
                return ready;

            catalog ??= ChallengeCatalog.All;
            for (var i = 0; i < catalog.Count; i++)
            {
                var def = catalog[i];
                if (rewards.Ledger.IsPending(def.Id))
                    ready.Add(def);
            }

            return ready;
        }

        public static ClaimAllResult ClaimAllReady(
            RewardsSystem rewards,
            FundsWallet wallet,
            IReadOnlyList<ChallengeDef> catalog = null,
            IRewardExtras extrasOverride = null)
        {
            if (rewards == null || wallet == null)
                return new ClaimAllResult(0, false);

            var totalCash = 0;
            var firstTrophy = false;
            var extras = extrasOverride ?? rewards.Extras;
            var ready = ReadyRewards(rewards, catalog);

            for (var i = 0; i < ready.Count; i++)
            {
                var result = rewards.Ledger.TryClaim(
                    ready[i].Id,
                    wallet,
                    rewards.TrophyCase,
                    extras,
                    ready[i]);
                if (!result.Ok)
                    continue;

                totalCash += result.CashPaid;
                firstTrophy |= result.FirstTrophyOnTower;
            }

            return new ClaimAllResult(totalCash, firstTrophy, NeedsTutorial(rewards, firstTrophy));
        }

        public static ClaimResult ClaimReady(
            RewardsSystem rewards,
            FundsWallet wallet,
            ChallengeDef def)
        {
            if (rewards == null || wallet == null)
                return ClaimResult.Failed;
            var result = rewards.Ledger.TryClaim(def.Id, wallet, rewards.TrophyCase, rewards.Extras, def);
            if (!result.Ok)
                return result;
            return new ClaimResult(
                result.Ok,
                result.CashPaid,
                result.MetalRaised,
                result.FirstTrophyOnTower,
                NeedsTutorial(rewards, result.FirstTrophyOnTower));
        }

        public static void Draw(
            Rect rect,
            RewardsSystem rewards,
            FundsWallet wallet,
            ref RewardBookTab tab,
            GUIStyle label,
            GUIStyle button,
            Action onNeedsTutorial = null)
        {
            GUI.Box(rect, GUIContent.none);
            var pad = 8f;
            var x = rect.x + pad;
            var y = rect.y + pad;
            var inner = rect.width - pad * 2f;
            var tabW = inner / Tabs.Length;

            for (var i = 0; i < Tabs.Length; i++)
            {
                var nextTab = (RewardBookTab)i;
                var caption = tab == nextTab ? $"▼ {Tabs[i]}" : Tabs[i];
                if (GUI.Button(new Rect(x + tabW * i, y, tabW - 3f, 22f), caption, button))
                    tab = nextTab;
            }

            y += 30f;
            switch (tab)
            {
                case RewardBookTab.Ready:
                    DrawReady(x, y, inner, rect.height - (y - rect.y) - pad, rewards, wallet, label, button, onNeedsTutorial);
                    break;
                case RewardBookTab.Catalog:
                    DrawCatalog(x, y, inner, rect.height - (y - rect.y) - pad, rewards, label);
                    break;
                case RewardBookTab.Contracts:
                    DrawContracts(x, y, inner, rect.height - (y - rect.y) - pad, rewards, label);
                    break;
                case RewardBookTab.Trophies:
                    DrawTrophies(x, y, inner, rect.height - (y - rect.y) - pad, rewards, label);
                    break;
            }
        }

        static void DrawReady(
            float x,
            float y,
            float width,
            float height,
            RewardsSystem rewards,
            FundsWallet wallet,
            GUIStyle label,
            GUIStyle button,
            Action onNeedsTutorial)
        {
            var ready = ReadyRewards(rewards);
            var row = 24f;
            var bottom = y + height;
            GUI.Label(new Rect(x, y, width - 94f, row), $"Ready to claim: {ready.Count}", label);
            if (ready.Count > 0 && GUI.Button(new Rect(x + width - 90f, y, 90f, row), "Claim All", button))
            {
                var result = ClaimAllReady(rewards, wallet);
                if (result.NeedsTutorial)
                    onNeedsTutorial?.Invoke();
            }
            y += row + 4f;

            if (ready.Count == 0)
            {
                GUI.Label(new Rect(x, y, width, row), "No rewards waiting. Check Catalog or Contracts.", label);
                return;
            }

            for (var i = 0; i < ready.Count && y + row <= bottom; i++)
            {
                var def = ready[i];
                GUI.Label(new Rect(x, y, width - 76f, row), $"{def.Title} · ${def.Cash:n0} · {def.Metal}", label);
                if (GUI.Button(new Rect(x + width - 72f, y, 72f, row), "Claim", button))
                {
                    var result = ClaimReady(rewards, wallet, def);
                    if (result.NeedsTutorial)
                        onNeedsTutorial?.Invoke();
                }
                y += row;
            }
        }

        static bool NeedsTutorial(RewardsSystem rewards, bool firstTrophyOnTower) =>
            firstTrophyOnTower && rewards != null && !rewards.RewardTutorialSeen;

        static void DrawCatalog(float x, float y, float width, float height, RewardsSystem rewards, GUIStyle label)
        {
            var row = 18f;
            var bottom = y + height;
            var catalog = ChallengeCatalog.All;
            for (var i = 0; i < catalog.Count && y + row <= bottom; i++)
            {
                var def = catalog[i];
                if (def.Kind == ChallengeKind.Contract)
                    continue;
                var state = rewards == null ? "Locked" :
                    rewards.Ledger.IsClaimed(def.Id) ? "Claimed" :
                    rewards.Ledger.IsPending(def.Id) ? "Ready" : "Open";
                GUI.Label(new Rect(x, y, width, row), $"{state}: {def.Title} · {def.Kind} · ${def.Cash:n0}", label);
                y += row;
            }
        }

        static void DrawContracts(float x, float y, float width, float height, RewardsSystem rewards, GUIStyle label)
        {
            var row = 20f;
            var bottom = y + height;
            var contracts = rewards?.Board?.Active;
            if (contracts == null || contracts.Count == 0)
            {
                GUI.Label(new Rect(x, y, width, row), "No active contracts. New offers refresh weekly.", label);
                return;
            }

            for (var i = 0; i < contracts.Count && y + row <= bottom; i++)
            {
                var c = contracts[i];
                var target = Math.Max(1, c.Def.Condition.CounterTarget > 0 ? c.Def.Condition.CounterTarget : c.Def.Condition.ThresholdValue);
                GUI.Label(
                    new Rect(x, y, width, row),
                    $"{c.Def.Title}: {c.ProgressValue}/{target} · expires day {c.ExpiresDayIndex} · ${c.Def.Cash:n0}",
                    label);
                y += row;
            }
        }

        static void DrawTrophies(float x, float y, float width, float height, RewardsSystem rewards, GUIStyle label)
        {
            var row = 20f;
            var bottom = y + height;
            foreach (ChallengeSubject subject in Enum.GetValues(typeof(ChallengeSubject)))
            {
                if (y + row > bottom)
                    break;
                var metal = rewards?.TrophyCase?.GetMetal(subject) ?? TrophyMetal.None;
                GUI.Label(new Rect(x, y, width, row), $"{subject}: {metal}", label);
                y += row;
            }
        }
    }
}
