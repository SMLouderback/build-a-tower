using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace BuildATower.Tests
{
    public class RewardExtrasTests
    {
        static ChallengeDef MakeDef(string id, RewardExtrasGrant extras) =>
            new ChallengeDef(
                id,
                title: id,
                description: id,
                ChallengeSubject.Construction,
                TrophyMetal.Bronze,
                ChallengeKind.Milestone,
                ChallengeCondition.Threshold(ChallengeMetric.BuildLifetime, 1),
                cash: 0,
                extras: extras);

        [Test]
        public void Claim_applies_title_and_ribbon_to_trophy_case_metadata()
        {
            var ledger = new ClaimLedger();
            var wallet = new FundsWallet(0);
            var trophyCase = new TrophyCase();
            var extras = new RewardExtras();
            var def = MakeDef(
                "builder_badge",
                RewardExtrasGrant.Metadata(title: "Master Builder", ribbon: "Fast Crew"));

            ledger.EnqueuePending(def.Id);
            var result = ledger.TryClaim(def.Id, wallet, trophyCase, extras, def);

            Assert.IsTrue(result.Ok);
            Assert.AreEqual("Master Builder", trophyCase.GetTitle(ChallengeSubject.Construction));
            Assert.AreEqual("Fast Crew", trophyCase.GetRibbon(ChallengeSubject.Construction));
        }

        [Test]
        public void Claim_adds_reward_tokens_to_inventory()
        {
            var extras = new RewardExtras(currentDayIndex: 12);
            var def = MakeDef(
                "token_pack",
                new RewardExtrasGrant(
                    buildCredit: 4000,
                    demandMultiplier: 1.25f,
                    demandDays: 5,
                    crewDays: 1));

            extras.OnClaim(def, new TrophyCase());

            Assert.AreEqual(4000, extras.PendingBuildCredit);
            Assert.AreEqual(1.25f, extras.DemandMultiplier);
            Assert.AreEqual(17, extras.DemandExpiresDayIndex);
            Assert.AreEqual(1, extras.CrewDays);
        }

        [Test]
        public void Build_credit_discounts_next_successful_placement_and_then_clears()
        {
            var extras = new RewardExtras();
            extras.OnClaim(MakeDef("credit", RewardExtrasGrant.BuildCredit(4000)), new TrophyCase());

            var discount = extras.ConsumeBuildCreditForSuccessfulPlacement(roomCost: 2500);

            Assert.AreEqual(2500, discount);
            Assert.AreEqual(0, extras.PendingBuildCredit);
            Assert.AreEqual(0, extras.ConsumeBuildCreditForSuccessfulPlacement(roomCost: 5000));
        }

        [Test]
        public void Build_controller_consumes_build_credit_after_successful_paid_placement()
        {
            GameSession.ResetForTests();
            GameSession.StartNewGame(GameDifficulty.Normal);
            var go = new GameObject("Reward build credit placement test");
            go.SetActive(false);
            try
            {
                var build = go.AddComponent<BuildController>();
                var simulation = go.AddComponent<TowerSimulation>();
                var grid = new TowerGrid();
                Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 8, TowerGrid.LobbyFloor, out _));
                var wallet = new FundsWallet(100_000);
                var roomType = OfficeType();
                var cost = BuildEconomy.PlacementCost(roomType);
                Assert.Greater(cost, 0);

                SetAutoProperty(build, "Grid", grid);
                SetAutoProperty(build, "Wallet", wallet);
                SetAutoProperty(build, "SelectedRoomType", roomType);
                simulation.InitializeSimulation();
                simulation.Rewards.Extras.OnClaim(MakeDef("credit", RewardExtrasGrant.BuildCredit(500)), new TrophyCase());

                var start = wallet.Balance;
                Assert.IsTrue(build.TryPlaceSelected(new Vector2Int(0, 1)));

                var expectedNetCost = Math.Max(0, cost - 500);
                Assert.AreEqual(start - expectedNetCost, wallet.Balance);
                Assert.AreEqual(0, simulation.Rewards.Extras.PendingBuildCredit);
                Assert.IsTrue(grid.TryGetRoomAt(new Vector2Int(0, 1), out var room));
                Assert.AreEqual(expectedNetCost, room.ConstructionSpent);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                GameSession.ResetForTests();
            }
        }

        [Test]
        public void Crew_day_token_consumes_once()
        {
            var extras = new RewardExtras();
            extras.OnClaim(MakeDef("crew", RewardExtrasGrant.CrewDays(1)), new TrophyCase());

            Assert.IsTrue(extras.TryConsumeCrewDay());
            Assert.AreEqual(0, extras.CrewDays);
            Assert.IsFalse(extras.TryConsumeCrewDay());
        }

        [Test]
        public void Demand_multiplier_is_active_until_expiry_day()
        {
            var extras = new RewardExtras(currentDayIndex: 10);
            extras.OnClaim(
                MakeDef(
                    "demand",
                    new RewardExtrasGrant(demandMultiplier: 1.25f, demandDays: 2)),
                new TrophyCase());

            Assert.AreEqual(1.25f, extras.ActiveDemandMultiplier(10));
            Assert.AreEqual(1.25f, extras.ActiveDemandMultiplier(11));
            Assert.AreEqual(1f, extras.ActiveDemandMultiplier(12));

            extras.AdvanceDay(12);
            Assert.AreEqual(1f, extras.DemandMultiplier);
            Assert.AreEqual(-1, extras.DemandExpiresDayIndex);
        }

        static RoomTypeSO LobbyType()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "lobby";
            type.displayName = "Lobby";
            type.category = RoomCategory.Structure;
            type.size = Vector2Int.one;
            type.isLobby = true;
            type.allowAboveGround = true;
            type.buildCost = 1_000;
            return type;
        }

        static RoomTypeSO OfficeType()
        {
            var loaded = Resources.Load<RoomTypeSO>("Rooms/OfficeMicro");
            if (loaded != null)
                return loaded;

            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "reward_credit_office";
            type.displayName = "Reward Credit Office";
            type.category = RoomCategory.Office;
            type.incomeModel = IncomeModel.QuarterlyRent;
            type.baseIncome = 100;
            type.maxOccupants = 1;
            type.size = Vector2Int.one;
            type.allowAboveGround = true;
            type.buildCost = 1_000;
            return type;
        }

        static void SetAutoProperty(object target, string propertyName, object value) =>
            SetField(target, $"<{propertyName}>k__BackingField", value);

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing test setup field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
