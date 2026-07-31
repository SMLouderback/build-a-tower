using System;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AgentWealthTests
    {
        static RoomTypeSO Living(RoomCategory category, string id = "room", string display = "Room",
            int requiredStars = 0)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.displayName = display;
            so.category = category;
            so.requiredStars = requiredStars;
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
            Assert.AreEqual(WealthBand.Street, AgentWealth.ResolveBand(AgentRole.StreetVisitor, null));
        }

        [Test]
        public void ResolveBand_basic_living_homes()
        {
            Assert.AreEqual(WealthBand.Basic,
                AgentWealth.ResolveBand(AgentRole.OfficeWorker, Living(RoomCategory.Office)));
            Assert.AreEqual(WealthBand.Basic,
                AgentWealth.ResolveBand(AgentRole.HotelGuest, Living(RoomCategory.Hotel, "hotel_single")));
            Assert.AreEqual(WealthBand.Basic,
                AgentWealth.ResolveBand(AgentRole.CondoResident, Living(RoomCategory.Condo)));
        }

        [Test]
        public void ResolveBand_premium_by_stars_or_name()
        {
            Assert.AreEqual(WealthBand.Premium,
                AgentWealth.ResolveBand(AgentRole.OfficeWorker,
                    Living(RoomCategory.Office, requiredStars: 2)));
            Assert.AreEqual(WealthBand.Premium,
                AgentWealth.ResolveBand(AgentRole.HotelGuest,
                    Living(RoomCategory.Hotel, id: "hotel_premium")));
            Assert.AreEqual(WealthBand.Premium,
                AgentWealth.ResolveBand(AgentRole.CondoResident,
                    Living(RoomCategory.Condo, display: "Premium Suite")));
        }

        [Test]
        public void RollDailyDisposable_respects_band_ranges_and_climate()
        {
            var rng = new System.Random(7);
            for (var i = 0; i < 40; i++)
            {
                var street = AgentWealth.RollDailyDisposable(WealthBand.Street, 1f, rng);
                Assert.That(street, Is.InRange(20, 60));

                var basic = AgentWealth.RollDailyDisposable(WealthBand.Basic, 1f, rng);
                Assert.That(basic, Is.InRange(40, 100));

                var premium = AgentWealth.RollDailyDisposable(WealthBand.Premium, 1f, rng);
                Assert.That(premium, Is.InRange(90, 200));
            }

            var boom = AgentWealth.RollDailyDisposable(WealthBand.Basic, 1.3f, new System.Random(1));
            Assert.That(boom, Is.InRange(52, 130));
            Assert.GreaterOrEqual(boom, 0);

            var recession = AgentWealth.RollDailyDisposable(WealthBand.Street, 0.7f, new System.Random(2));
            Assert.That(recession, Is.InRange(14, 42));
            Assert.GreaterOrEqual(recession, 0);
        }

        [Test]
        public void CanAfford_compares_pay_to_remaining()
        {
            var restaurant = Shop(120);
            Assert.IsTrue(AgentWealth.CanAfford(120, restaurant));
            Assert.IsTrue(AgentWealth.CanAfford(200, restaurant));
            Assert.IsFalse(AgentWealth.CanAfford(119, restaurant));
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
