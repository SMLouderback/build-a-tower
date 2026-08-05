using System;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AgentWealthTests
    {
        static RoomTypeSO Living(RoomCategory category, string id = "room", string display = "Room",
            int requiredStars = 0, LuxuryBand luxuryBand = LuxuryBand.None)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.displayName = display;
            so.category = category;
            so.requiredStars = requiredStars;
            so.luxuryBand = luxuryBand;
            return so;
        }

        static RoomTypeSO Shop(int pay)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.incomeModel = IncomeModel.TrafficVariable;
            so.baseIncome = pay;
            return so;
        }

        [Test]
        public void ResolveBand_street_visitor_is_street()
        {
            Assert.AreEqual(WealthBand.Street,
                AgentWealth.ResolveBand(AgentRole.StreetVisitor, null, new System.Random(0)));
        }

        [Test]
        public void ResolveBand_event_visitor_is_mid()
        {
            Assert.AreEqual(WealthBand.Mid,
                AgentWealth.ResolveBand(AgentRole.EventVisitor, null, new System.Random(0)));
        }

        [Test]
        public void ResolveBand_hotel_base_is_basic()
        {
            Assert.AreEqual(WealthBand.Basic,
                AgentWealth.ResolveBand(AgentRole.HotelGuest,
                    Living(RoomCategory.Hotel, "hotel_base", luxuryBand: LuxuryBand.Base),
                    new System.Random(0)));
        }

        [Test]
        public void ResolveBand_hotel_mid_is_mid()
        {
            Assert.AreEqual(WealthBand.Mid,
                AgentWealth.ResolveBand(AgentRole.HotelGuest,
                    Living(RoomCategory.Hotel, "hotel_mid", luxuryBand: LuxuryBand.Mid),
                    new System.Random(0)));
        }

        [Test]
        public void ResolveBand_hotel_upper_non_suite_is_upper()
        {
            Assert.AreEqual(WealthBand.Upper,
                AgentWealth.ResolveBand(AgentRole.HotelGuest,
                    Living(RoomCategory.Hotel, "hotel_upper_king", luxuryBand: LuxuryBand.Upper),
                    new System.Random(0)));
        }

        [Test]
        public void ResolveBand_hotel_upper_suite_is_upper_or_premium()
        {
            var suite = Living(RoomCategory.Hotel, "hotel_upper_suite", luxuryBand: LuxuryBand.Upper);
            var upper = 0;
            var premium = 0;
            var rng = new System.Random(42);
            for (var i = 0; i < 400; i++)
            {
                var band = AgentWealth.ResolveBand(AgentRole.HotelGuest, suite, rng);
                if (band == WealthBand.Upper) upper++;
                else if (band == WealthBand.Premium) premium++;
                else Assert.Fail("unexpected band " + band);
            }

            Assert.That(upper, Is.InRange(140, 260));
            Assert.That(premium, Is.InRange(140, 260));
        }

        [Test]
        public void ResolveBand_legacy_hotel_premium_without_band_is_mid()
        {
            Assert.AreEqual(WealthBand.Mid,
                AgentWealth.ResolveBand(AgentRole.HotelGuest,
                    Living(RoomCategory.Hotel, "hotel_premium"),
                    new System.Random(0)));
            Assert.AreEqual(WealthBand.Mid,
                AgentWealth.ResolveBand(AgentRole.HotelGuest,
                    Living(RoomCategory.Hotel, "hotel_single", display: "Premium Room"),
                    new System.Random(0)));
        }

        [Test]
        public void ResolveBand_unbanded_hotel_defaults_basic()
        {
            Assert.AreEqual(WealthBand.Basic,
                AgentWealth.ResolveBand(AgentRole.HotelGuest,
                    Living(RoomCategory.Hotel, "hotel_single"),
                    new System.Random(0)));
        }

        [Test]
        public void ResolveBand_office_mid_is_mid()
        {
            var so = Living(RoomCategory.Office, requiredStars: 2, luxuryBand: LuxuryBand.Mid);
            var rng = new System.Random(1);
            Assert.AreEqual(WealthBand.Mid, AgentWealth.ResolveBand(AgentRole.OfficeWorker, so, rng));
        }

        [Test]
        public void ResolveBand_condo_mid_is_mid()
        {
            var so = Living(RoomCategory.Condo, requiredStars: 2, luxuryBand: LuxuryBand.Mid);
            Assert.AreEqual(WealthBand.Mid,
                AgentWealth.ResolveBand(AgentRole.CondoResident, so, new System.Random(1)));
        }

        [Test]
        public void ResolveBand_condo_penthouse_mixes_upper_premium()
        {
            var so = Living(RoomCategory.Condo, CondoLuxury.UpperPenthouseId,
                requiredStars: 3, luxuryBand: LuxuryBand.Upper);
            var upper = 0;
            var premium = 0;
            var rng = new System.Random(42);
            for (var i = 0; i < 400; i++)
            {
                var band = AgentWealth.ResolveBand(AgentRole.CondoResident, so, rng);
                if (band == WealthBand.Upper) upper++;
                else if (band == WealthBand.Premium) premium++;
                else Assert.Fail("unexpected band " + band);
            }

            Assert.That(upper, Is.InRange(140, 260));
            Assert.That(premium, Is.InRange(140, 260));
        }

        [Test]
        public void ResolveBand_office_condo_low_stars_mix()
        {
            var office = Living(RoomCategory.Office);
            var condo = Living(RoomCategory.Condo);
            var basic = 0;
            var mid = 0;
            var rng = new System.Random(7);
            for (var i = 0; i < 1000; i++)
            {
                var home = i % 2 == 0 ? office : condo;
                var role = i % 2 == 0 ? AgentRole.OfficeWorker : AgentRole.CondoResident;
                var band = AgentWealth.ResolveBand(role, home, rng);
                if (band == WealthBand.Basic) basic++;
                else if (band == WealthBand.Mid) mid++;
                else Assert.Fail("unexpected band " + band);
            }

            Assert.That(basic, Is.InRange(220, 380));
            Assert.That(mid, Is.InRange(620, 780));
        }

        [Test]
        public void ResolveBand_office_condo_high_stars_mix()
        {
            var office = Living(RoomCategory.Office, requiredStars: 2);
            var condo = Living(RoomCategory.Condo, requiredStars: 3);
            var upper = 0;
            var premium = 0;
            var rng = new System.Random(11);
            for (var i = 0; i < 1000; i++)
            {
                var home = i % 2 == 0 ? office : condo;
                var role = i % 2 == 0 ? AgentRole.OfficeWorker : AgentRole.CondoResident;
                var band = AgentWealth.ResolveBand(role, home, rng);
                if (band == WealthBand.Upper) upper++;
                else if (band == WealthBand.Premium) premium++;
                else Assert.Fail("unexpected band " + band);
            }

            Assert.That(upper, Is.InRange(620, 780));
            Assert.That(premium, Is.InRange(220, 380));
        }

        [Test]
        public void RollDailyDisposable_respects_band_ranges_and_climate()
        {
            var rng = new System.Random(7);
            for (var i = 0; i < 40; i++)
            {
                Assert.That(AgentWealth.RollDailyDisposable(WealthBand.Street, 1f, rng), Is.InRange(35, 90));
                Assert.That(AgentWealth.RollDailyDisposable(WealthBand.Basic, 1f, rng), Is.InRange(55, 110));
                Assert.That(AgentWealth.RollDailyDisposable(WealthBand.Mid, 1f, rng), Is.InRange(90, 160));
                Assert.That(AgentWealth.RollDailyDisposable(WealthBand.Upper, 1f, rng), Is.InRange(140, 220));
                Assert.That(AgentWealth.RollDailyDisposable(WealthBand.Premium, 1f, rng), Is.InRange(200, 320));
            }

            var boom = AgentWealth.RollDailyDisposable(WealthBand.Basic, 1.3f, new System.Random(1));
            Assert.That(boom, Is.InRange(72, 143));
            Assert.GreaterOrEqual(boom, 0);

            var recession = AgentWealth.RollDailyDisposable(WealthBand.Street, 0.7f, new System.Random(2));
            Assert.That(recession, Is.InRange(25, 63));
            Assert.GreaterOrEqual(recession, 0);
        }

        [Test]
        public void CanAfford_uses_soft_gate_not_full_list_price()
        {
            var restaurant = Shop(120);
            // Gate = min(120, max(25, 60)) = 60
            Assert.IsTrue(AgentWealth.CanAfford(120, restaurant));
            Assert.IsTrue(AgentWealth.CanAfford(60, restaurant));
            Assert.IsFalse(AgentWealth.CanAfford(59, restaurant));
            Assert.IsFalse(AgentWealth.CanAfford(30, restaurant));
        }

        [Test]
        public void RollSpend_is_between_one_and_min_price_remaining()
        {
            var retail = Shop(80);
            var rng = new System.Random(99);
            for (var i = 0; i < 30; i++)
            {
                var spent = AgentWealth.RollSpend(50, retail, rng);
                Assert.That(spent, Is.InRange(1, 50));
            }

            for (var i = 0; i < 30; i++)
            {
                var spent = AgentWealth.RollSpend(200, retail, rng);
                Assert.That(spent, Is.InRange(1, 80));
            }
        }

        [Test]
        public void Agent_exposes_disposable_fields()
        {
            var home = new RoomInstance(1, Living(RoomCategory.Office), Vector2Int.zero, Vector2Int.one);
            var agent = new Agent(1, AgentRole.OfficeWorker, home, Vector2Int.zero);
            agent.DisposableRemaining = 75;
            agent.DisposableDayIndex = 3;
            Assert.AreEqual(75, agent.DisposableRemaining);
            Assert.AreEqual(3, agent.DisposableDayIndex);
        }

        [Test]
        public void ShopEarningsToday_accumulates_and_resets_with_visits()
        {
            var shopType = Shop(40);
            var room = new RoomInstance(2, shopType, Vector2Int.zero, new Vector2Int(2, 1));
            Assert.AreEqual(0, room.ShopEarningsToday);

            room.RecordShopSpend(25);
            room.RecordVisit();
            room.RecordShopSpend(10);
            Assert.AreEqual(35, room.ShopEarningsToday);
            Assert.AreEqual(1, room.VisitsToday);

            room.ResetVisitsToday();
            Assert.AreEqual(0, room.ShopEarningsToday);
            Assert.AreEqual(0, room.VisitsToday);
        }

        [Test]
        public void RecordShopSpend_ignores_non_positive()
        {
            var room = new RoomInstance(3, Shop(40), Vector2Int.zero, Vector2Int.one);
            room.RecordShopSpend(0);
            room.RecordShopSpend(-5);
            Assert.AreEqual(0, room.ShopEarningsToday);
        }
    }
}
