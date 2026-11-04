using NUnit.Framework;

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
        public void Crew_day_token_consumes_once()
        {
            var extras = new RewardExtras();
            extras.OnClaim(MakeDef("crew", RewardExtrasGrant.CrewDays(1)), new TrophyCase());

            Assert.IsTrue(extras.TryConsumeCrewDay());
            Assert.AreEqual(0, extras.CrewDays);
            Assert.IsFalse(extras.TryConsumeCrewDay());
        }
    }
}
