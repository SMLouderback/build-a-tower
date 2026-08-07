using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class TowerMapAnalyticsTests
    {
        [Test]
        public void Blend_half_half_is_midpoint()
        {
            Assert.AreEqual(0.5f, TowerMapAnalytics.Blend(0f, 1f), 0.0001f);
            Assert.AreEqual(0.75f, TowerMapAnalytics.Blend(1f, 0.5f), 0.0001f);
        }

        [Test]
        public void GetScore_empty_is_zero()
        {
            var a = new TowerMapAnalytics();
            Assert.AreEqual(0f, a.GetScore(TowerMapMode.Crime, Vector2Int.zero));
        }

        [Test]
        public void CrimeScore_criminal_raises_patrol_lowers()
        {
            var baseScore = TowerMapAnalytics.CrimeScore(0.5f, 0f, 0f, 0f);
            var withCriminal = TowerMapAnalytics.CrimeScore(0.5f, 1f, 0f, 0f);
            var withPatrol = TowerMapAnalytics.CrimeScore(0.5f, 1f, 0f, 1f);
            Assert.Greater(withCriminal, baseScore);
            Assert.Less(withPatrol, withCriminal);
        }

        [Test]
        public void TrafficCapacityStress_rises_near_full_and_research_helps()
        {
            Assert.AreEqual(0f, TowerMapAnalytics.TrafficCapacityStress(5f, 10f), 0.0001f);
            Assert.Greater(TowerMapAnalytics.TrafficCapacityStress(9f, 10f), 0.3f);
            var stressed = TowerMapAnalytics.TrafficCapacityStress(12f, 10f, 1f);
            var eased = TowerMapAnalytics.TrafficCapacityStress(12f, 10f, 1.5f);
            Assert.Less(eased, stressed);
        }

        [Test]
        public void RebuildTraffic_today_normalizes()
        {
            var a = new TowerMapAnalytics();
            a.RecordTraversal(new Vector2Int(0, 1), 10f);
            a.RecordTraversal(new Vector2Int(1, 1), 5f);
            a.RebuildTraffic(TrafficMapWindow.Today);
            Assert.AreEqual(1f, a.GetScore(TowerMapMode.Traffic, new Vector2Int(0, 1)), 0.0001f);
            Assert.AreEqual(0.5f, a.GetScore(TowerMapMode.Traffic, new Vector2Int(1, 1)), 0.0001f);
        }

        [Test]
        public void SetEconomicScores_builds_blend()
        {
            var a = new TowerMapAnalytics();
            var cell = new Vector2Int(2, 3);
            a.SetEconomicScores(
                new System.Collections.Generic.Dictionary<Vector2Int, float> { { cell, 1f } },
                new System.Collections.Generic.Dictionary<Vector2Int, float> { { cell, 0f } });
            Assert.AreEqual(1f, a.GetScore(TowerMapMode.Economic, cell, EconomicMapView.Profit), 0.0001f);
            Assert.AreEqual(0f, a.GetScore(TowerMapMode.Economic, cell, EconomicMapView.Demand), 0.0001f);
            Assert.AreEqual(0.5f, a.GetScore(TowerMapMode.Economic, cell, EconomicMapView.Blend), 0.0001f);
        }

        [Test]
        public void SetEconomicScores_negative_profit_stored_blend_uses_positive_only()
        {
            var a = new TowerMapAnalytics();
            var cell = new Vector2Int(1, 1);
            a.SetEconomicScores(
                new System.Collections.Generic.Dictionary<Vector2Int, float> { { cell, -0.8f } },
                new System.Collections.Generic.Dictionary<Vector2Int, float> { { cell, 1f } });
            Assert.AreEqual(-0.8f, a.GetScore(TowerMapMode.Economic, cell, EconomicMapView.Profit), 0.0001f);
            // Blend(Max(0, −0.8), 1) = Blend(0, 1) = 0.5
            Assert.AreEqual(0.5f, a.GetScore(TowerMapMode.Economic, cell, EconomicMapView.Blend), 0.0001f);
        }

        [Test]
        public void NormalizeTowerProfit_extremes_and_zero()
        {
            Assert.AreEqual(1f, HeatmapColors.NormalizeTowerProfit(500, 500, 200), 0.0001f);
            Assert.AreEqual(-1f, HeatmapColors.NormalizeTowerProfit(-200, 500, 200), 0.0001f);
            Assert.AreEqual(0f, HeatmapColors.NormalizeTowerProfit(0, 500, 200), 0.0001f);
        }

        [Test]
        public void NormalizeTowerProfit_only_profits_or_only_losses()
        {
            Assert.AreEqual(0.5f, HeatmapColors.NormalizeTowerProfit(50, 100, 0), 0.0001f);
            Assert.AreEqual(0f, HeatmapColors.NormalizeTowerProfit(-20, 100, 0), 0.0001f);
            Assert.AreEqual(-0.5f, HeatmapColors.NormalizeTowerProfit(-50, 0, 100), 0.0001f);
            Assert.AreEqual(0f, HeatmapColors.NormalizeTowerProfit(20, 0, 100), 0.0001f);
        }

        [Test]
        public void RiskColor_high_is_more_red_than_low()
        {
            var low = HeatmapColors.RiskColor(0.1f);
            var high = HeatmapColors.RiskColor(1f);
            Assert.Greater(high.r, low.r);
            Assert.Less(high.b, low.b);
        }

        [Test]
        public void TryProfitColor_zero_false_extremes_tint()
        {
            Assert.IsFalse(HeatmapColors.TryProfitColor(0f, out _));
            Assert.IsTrue(HeatmapColors.TryProfitColor(1f, out var green));
            Assert.Greater(green.g, green.r);
            Assert.IsTrue(HeatmapColors.TryProfitColor(-1f, out var red));
            Assert.Greater(red.r, red.g);
        }

        [Test]
        public void NoiseEmit_shop_greater_than_lobby()
        {
            var lobby = ScriptableObject.CreateInstance<RoomTypeSO>();
            lobby.id = "lobby";
            lobby.isLobby = true;
            lobby.category = RoomCategory.Structure;

            var shop = ScriptableObject.CreateInstance<RoomTypeSO>();
            shop.id = "shop_retail";
            shop.category = RoomCategory.Commercial;
            shop.noiseOutput = 0.4f;

            var lobbyEmit = NoiseEmitterWeights.Emit(lobby, occupied: true, crimeActiveNear: false, eventOrConferenceBusy: false);
            var shopEmit = NoiseEmitterWeights.Emit(shop, occupied: true, crimeActiveNear: false, eventOrConferenceBusy: false);
            Assert.Greater(shopEmit, lobbyEmit);
        }

        [Test]
        public void ResidentialBother_hotel_higher_at_night_than_day()
        {
            var hotel = ScriptableObject.CreateInstance<RoomTypeSO>();
            hotel.id = "hotel_base";
            hotel.category = RoomCategory.Hotel;

            var day = NoiseEmitterWeights.ResidentialBotherFactor(hotel, 14 * 60);
            var night = NoiseEmitterWeights.ResidentialBotherFactor(hotel, 23 * 60);
            Assert.Greater(night, day);
        }

        [Test]
        public void LivingDemandStress_empty_office_hotter_than_full()
        {
            var empty = TowerMapAnalytics.LivingDemandStress(
                RoomCategory.Office, occupants: 0, maxOccupants: 4, condoSold: false);
            var full = TowerMapAnalytics.LivingDemandStress(
                RoomCategory.Office, occupants: 4, maxOccupants: 4, condoSold: false);
            Assert.Greater(empty, full);
            Assert.AreEqual(1f, empty, 0.0001f);
            Assert.AreEqual(0f, full, 0.0001f);
        }

        [Test]
        public void RecordDaySample_stores_economy_and_star_events()
        {
            var a = new TowerMapAnalytics();
            a.RecordDaySample(new TowerDaySample(1, 2, 1f, 0.2f, 10, 100, 40, 5000, 0));
            a.RecordDaySample(new TowerDaySample(2, 2, 1f, 0.1f, 12, 200, 50, 5200, 1));
            a.RecordDaySample(new TowerDaySample(3, 3, 1.15f, 0.05f, 15, 250, 60, 5400, 2));

            Assert.AreEqual(3, a.DayHistory.Count);
            Assert.AreEqual(12, a.DayHistory[1].Population);
            Assert.AreEqual(200, a.DayHistory[1].DailyIncome);
            Assert.AreEqual(50, a.DayHistory[1].DailyExpense);
            Assert.AreEqual(5200, a.DayHistory[1].Savings);
            Assert.AreEqual(2, a.StarEvents.Count);
            Assert.AreEqual(1, a.StarEvents[0].Stars);
            Assert.AreEqual(2, a.StarEvents[0].DayIndex);
            Assert.AreEqual(2, a.StarEvents[1].Stars);
            Assert.AreEqual(3, a.StarEvents[1].DayIndex);
        }
    }
}
