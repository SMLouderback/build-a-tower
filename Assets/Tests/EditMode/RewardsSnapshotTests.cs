using System;
using System.Reflection;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class RewardsSnapshotTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var build in UnityEngine.Object.FindObjectsByType<BuildController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(build.gameObject);

            GameSession.ResetForTests();
        }

        [Test]
        public void Capture_and_restore_round_trip_reward_ledger_trophies_contracts_tutorial_and_extras()
        {
            var build = CreateBuild(out var simulation);
            var rewards = new RewardsSystem(extras: new RewardExtras(currentDayIndex: 12));
            rewards.RewardTutorialSeen = true;
            rewards.Ledger.EnqueuePending("pop_100");
            Assert.IsTrue(ChallengeCatalog.TryGet("demolish_10", out var claimedDef));
            rewards.Ledger.EnqueuePending(claimedDef.Id);
            Assert.IsTrue(rewards.Ledger.TryClaim(
                claimedDef.Id,
                new FundsWallet(0),
                rewards.TrophyCase,
                rewards.Extras,
                claimedDef).Ok);
            rewards.TrophyCase.SetMetadata(ChallengeSubject.Demolition, "Renovator", "Dusty Ribbon");
            rewards.Extras.OnClaim(
                new ChallengeDef(
                    "extra_pack",
                    "Extra Pack",
                    "Extra Pack",
                    ChallengeSubject.Construction,
                    TrophyMetal.None,
                    ChallengeKind.Milestone,
                    ChallengeCondition.Threshold(ChallengeMetric.BuildLifetime, 1),
                    cash: 0,
                    extras: new RewardExtrasGrant(buildCredit: 4000, demandMultiplier: 1.25f, demandDays: 5, crewDays: 2)),
                rewards.TrophyCase);
            rewards.Board.Evaluate(new RewardsMetrics { Population = 99, DayIndex = 2 }, rewards.Ledger);
            SetField(simulation, "_rewards", rewards);

            var captured = TowerSnapshotMapper.Capture("save_rewards", "Rewards Tower", build, simulation, DateTime.UtcNow);
            var json = JsonUtility.ToJson(captured);
            var roundTripped = JsonUtility.FromJson<TowerSnapshotV1>(json);
            var restored = new RewardsSystem();
            restored.RestoreSnapshot(roundTripped.rewards);

            Assert.IsTrue(restored.Ledger.IsPending("pop_100"));
            Assert.IsTrue(restored.Ledger.IsClaimed("demolish_10"));
            Assert.AreEqual(TrophyMetal.Bronze, restored.TrophyCase.GetMetal(ChallengeSubject.Demolition));
            Assert.AreEqual("Renovator", restored.TrophyCase.GetTitle(ChallengeSubject.Demolition));
            Assert.AreEqual("Dusty Ribbon", restored.TrophyCase.GetRibbon(ChallengeSubject.Demolition));
            Assert.IsTrue(restored.RewardTutorialSeen);
            Assert.AreEqual(4000, restored.Extras.PendingBuildCredit);
            Assert.AreEqual(1.25f, restored.Extras.DemandMultiplier);
            Assert.AreEqual(17, restored.Extras.DemandExpiresDayIndex);
            Assert.AreEqual(2, restored.Extras.CrewDays);
            Assert.IsTrue(restored.Board.Contains("contract_pop_150"));
            Assert.AreEqual(99, ContractProgress(restored, "contract_pop_150"));
        }

        [Test]
        public void Null_or_absent_reward_snapshot_restores_defaults()
        {
            var legacy = JsonUtility.FromJson<TowerSnapshotV1>("{\"schemaVersion\":1}");
            var rewards = new RewardsSystem();

            rewards.RestoreSnapshot(legacy.rewards);

            Assert.IsFalse(rewards.RewardTutorialSeen);
            Assert.IsFalse(rewards.Ledger.IsPending("pop_100"));
            Assert.IsFalse(rewards.Ledger.IsClaimed("demolish_10"));
            Assert.AreEqual(TrophyMetal.None, rewards.TrophyCase.GetMetal(ChallengeSubject.Population));
            Assert.AreEqual(0, rewards.Extras.PendingBuildCredit);
            Assert.AreEqual(1f, rewards.Extras.DemandMultiplier);
            Assert.AreEqual(-1, rewards.Extras.DemandExpiresDayIndex);
            Assert.AreEqual(0, rewards.Extras.CrewDays);
            Assert.GreaterOrEqual(rewards.Board.Active.Count, 3);
        }

        [Test]
        public void Absent_reward_snapshot_seeds_contracts_on_restored_day_not_day_zero()
        {
            const int restoredDay = 42;
            var rewards = new RewardsSystem();

            rewards.RestoreSnapshot(null, restoredDay);

            Assert.IsFalse(rewards.RewardTutorialSeen);
            Assert.GreaterOrEqual(rewards.Board.Active.Count, 3);
            for (var i = 0; i < rewards.Board.Active.Count; i++)
            {
                var contract = rewards.Board.Active[i];
                Assert.AreEqual(restoredDay, contract.StartedDayIndex, contract.Def.Id);
                Assert.AreEqual(restoredDay + 7, contract.ExpiresDayIndex, contract.Def.Id);
            }
        }

        static int ContractProgress(RewardsSystem rewards, string id)
        {
            for (var i = 0; i < rewards.Board.Active.Count; i++)
            {
                if (rewards.Board.Active[i].Def.Id == id)
                    return rewards.Board.Active[i].ProgressValue;
            }

            Assert.Fail($"Missing contract {id}.");
            return 0;
        }

        static BuildController CreateBuild(out TowerSimulation simulation)
        {
            var gameObject = new GameObject("Rewards Snapshot Test Tower");
            var build = gameObject.AddComponent<BuildController>();
            SetAutoProperty(build, "Grid", new TowerGrid());
            SetAutoProperty(build, "Wallet", new FundsWallet(100));
            simulation = gameObject.AddComponent<TowerSimulation>();
            SetField(simulation, "_clock", new GameClock());
            SetField(simulation, "_stars", new StarSystem());
            return build;
        }

        static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing test setup field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
