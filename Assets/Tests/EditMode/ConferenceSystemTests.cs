using System;
using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ConferenceSystemTests
    {
        [Test]
        public void DailyMeetingPayout_zero_total_capacity_returns_zero()
        {
            Assert.AreEqual(0, ConferenceMath.DailyMeetingPayout(10, 40, 0, 0, 1f));
            Assert.AreEqual(0, ConferenceMath.DailyMeetingPayout(10, 40, -1, 0, 1f));
        }

        [Test]
        public void DailyMeetingPayout_single_hall_gets_full_raw()
        {
            // 10 workers * 15 * (1 + 0) * 1 = 150
            Assert.AreEqual(150, ConferenceMath.DailyMeetingPayout(10, 40, 40, 0, 1f));
        }

        [Test]
        public void DailyMeetingPayout_splits_by_capacity_share()
        {
            // raw = 10 * 15 = 150; hall 40 of 80 → 75
            Assert.AreEqual(75, ConferenceMath.DailyMeetingPayout(10, 40, 80, 0, 1f));
            // hall 60 of 80 → Mathf.RoundToInt(112.5) = 112 (banker's round)
            Assert.AreEqual(112, ConferenceMath.DailyMeetingPayout(10, 60, 80, 0, 1f));
        }

        [Test]
        public void DailyMeetingPayout_applies_stars_and_climate()
        {
            // raw = 10 * 15 * (1 + 2*0.25) * 1.3 = 292.5 → Mathf.RoundToInt = 292
            Assert.AreEqual(292, ConferenceMath.DailyMeetingPayout(10, 40, 40, 2, 1.3f));
        }

        [Test]
        public void DailyMeetingPayout_caps_at_capacity_times_50()
        {
            // raw huge; cap = 2 * 50 = 100
            Assert.AreEqual(100, ConferenceMath.DailyMeetingPayout(10_000, 2, 2, 4, 1.3f));
        }

        [Test]
        public void ComputeDailyMeetings_sums_share_across_conferences()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 16, 0, out _);
            Assert.IsTrue(grid.TryPlace(Conference(40), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(Conference(40), new Vector2Int(8, 1), out _));

            var system = new ConferenceSystem();
            // two equal halls → full raw 150 split as 75+75
            Assert.AreEqual(150, system.ComputeDailyMeetings(grid, 10, 0, 1f));
        }

        [Test]
        public void ComputeDailyMeetings_skips_booked_hall()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 16, 0, out _);
            Assert.IsTrue(grid.TryPlace(Conference(40), new Vector2Int(0, 1), out var a));
            Assert.IsTrue(grid.TryPlace(Conference(40), new Vector2Int(8, 1), out var b));

            var system = new ConferenceSystem();
            system.BookedHallInstanceIds.Add(a.InstanceId);

            Assert.IsTrue(system.IsHallBooked(a));
            Assert.IsFalse(system.IsHallBooked(b));
            // only b eligible → full 150
            Assert.AreEqual(150, system.ComputeDailyMeetings(grid, 10, 0, 1f));
        }

        [Test]
        public void ComputeDailyMeetings_excludes_event_hall_and_broken()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 20, 0, out _);
            Assert.IsTrue(grid.TryPlace(Conference(40), new Vector2Int(0, 1), out var conf));
            Assert.IsTrue(grid.TryPlace(EventHall(120), new Vector2Int(8, 1), out _));
            Assert.IsTrue(grid.TryPlace(Conference(40), new Vector2Int(0, 2), out var broken));
            broken.Condition = 0;

            var system = new ConferenceSystem();
            Assert.AreEqual(150, system.ComputeDailyMeetings(grid, 10, 0, 1f));
            Assert.IsFalse(conf.IsBroken);
            Assert.IsTrue(broken.IsBroken);
        }

        [Test]
        public void ComputeDailyMeetings_fallback_capacity_when_event_capacity_zero()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 16, 0, out _);
            var type = Conference(0);
            type.size = new Vector2Int(4, 1); // fallback 4*1*5 = 20
            Assert.IsTrue(grid.TryPlace(type, new Vector2Int(0, 1), out _));

            var system = new ConferenceSystem();
            Assert.AreEqual(150, system.ComputeDailyMeetings(grid, 10, 0, 1f));
        }

        [Test]
        public void Midnight_credits_daily_meeting_income()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 16, 0, out _);
            Assert.IsTrue(grid.TryPlace(Conference(40), new Vector2Int(0, 1), out _));

            // Workers without rent-paying homes so LastIncome is meetings-only.
            var agents = new List<Agent>();
            for (var i = 1; i <= 10; i++)
                agents.Add(new Agent(i, AgentRole.OfficeWorker, null, Vector2Int.zero));

            var wallet = new FundsWallet(100_000);
            var economy = new EconomySystem(seed: 1);
            var conference = new ConferenceSystem();

            economy.OnNewDay(
                grid,
                agents,
                wallet,
                currentStars: 0,
                climateOffset: 0,
                research: null,
                climateSpendMult: 1f,
                conference: conference);

            Assert.AreEqual(150, economy.LastIncome);
            Assert.AreEqual(100_150, wallet.Balance);
        }

        [Test]
        public void MajorEventLumpPayout_matches_formula()
        {
            // 10 guests * 2 stars * 120 cap * 8 * 1.0 = 19200
            Assert.AreEqual(19200, ConferenceSystem.MajorEventLumpPayout(10, 2, 120, 1f));
            Assert.AreEqual(
                ConferenceSystem.EventPayMult,
                8f);
        }

        [Test]
        public void MajorEventLumpPayout_applies_climate_and_rounds()
        {
            // 10 * 1 * 100 * 8 * 1.3 = 10400
            Assert.AreEqual(10400, ConferenceSystem.MajorEventLumpPayout(10, 1, 100, 1.3f));
            // 3 * 1 * 10 * 8 * 1.15 = 276
            Assert.AreEqual(276, ConferenceSystem.MajorEventLumpPayout(3, 1, 10, 1.15f));
        }

        [Test]
        public void TickDay_schedules_and_sets_upcoming_on_foreshadow()
        {
            var grid = EventGrid(out _, out _);
            var system = new ConferenceSystem();
            var news = new TowerNews();
            var wallet = new FundsWallet(0);
            // gap=14, duration=2 → StartDay=14 when scheduled on day 0; foreshadow day=12
            var rng = new ScriptedRandom(14, 2);

            system.TickDay(0, grid, 0, 1, 1f, wallet, news, rng);

            Assert.AreEqual(14, system.Active.StartDayIndex);
            Assert.AreEqual(15, system.Active.EndDayIndex);
            Assert.AreEqual(MajorEventPhase.None, system.Active.Phase);
            Assert.AreEqual(0, news.Items.Count);

            system.TickDay(12, grid, 0, 1, 1f, wallet, news, rng);

            Assert.AreEqual(MajorEventPhase.Upcoming, system.Active.Phase);
            Assert.AreEqual(1, news.Items.Count);
            Assert.AreEqual(TowerNewsCategory.MajorEvent, news.Items[0].Category);
            StringAssert.Contains("upcoming", news.Items[0].Text.ToLowerInvariant());
        }

        [Test]
        public void TickDay_start_books_highest_event_hall_and_pays_lump()
        {
            var grid = EventGrid(out var low, out var high);
            var system = new ConferenceSystem();
            var news = new TowerNews();
            var wallet = new FundsWallet(1_000);
            var rng = new ScriptedRandom(14, 1); // Start=14, End=14

            system.TickDay(0, grid, 10, 2, 1f, wallet, news, rng);
            system.TickDay(12, grid, 10, 2, 1f, wallet, news, rng);
            system.TickDay(14, grid, 10, 2, 1f, wallet, news, rng);

            Assert.AreEqual(MajorEventPhase.Live, system.Active.Phase);
            Assert.AreEqual(1, system.Active.BookedHallInstanceIds.Count);
            Assert.AreEqual(high.InstanceId, system.Active.BookedHallInstanceIds[0]);
            Assert.IsTrue(system.IsHallBooked(high));
            Assert.IsFalse(system.IsHallBooked(low));

            var lump = ConferenceSystem.MajorEventLumpPayout(10, 2, 200, 1f);
            Assert.AreEqual(1_000 + lump, wallet.Balance);
            Assert.IsTrue(HasMajorNews(news, "live"));
        }

        [Test]
        public void TickDay_live_pays_daily_after_start_then_ends()
        {
            var grid = EventGrid(out _, out var high);
            var system = new ConferenceSystem();
            var news = new TowerNews();
            var wallet = new FundsWallet(0);
            var rng = new ScriptedRandom(14, 2); // Start=14, End=15

            system.TickDay(0, grid, 10, 2, 1f, wallet, news, rng);
            system.TickDay(12, grid, 10, 2, 1f, wallet, news, rng);

            var lump = ConferenceSystem.MajorEventLumpPayout(10, 2, 200, 1f);
            system.TickDay(14, grid, 10, 2, 1f, wallet, news, rng);
            Assert.AreEqual(lump, wallet.Balance);

            var daily = Mathf.RoundToInt(lump * ConferenceSystem.EventDailyWhileLiveMult);
            system.TickDay(15, grid, 10, 2, 1f, wallet, news, rng);
            Assert.AreEqual(MajorEventPhase.Live, system.Active.Phase);
            Assert.AreEqual(lump + daily, wallet.Balance);

            system.TickDay(16, grid, 10, 2, 1f, wallet, news, rng);
            Assert.AreEqual(MajorEventPhase.None, system.Active.Phase);
            Assert.AreEqual(0, system.BookedHallInstanceIds.Count);
            Assert.IsFalse(system.IsHallBooked(high));
            Assert.IsTrue(HasMajorNews(news, "ended"));
        }

        [Test]
        public void TickDay_booked_hall_does_not_block_conference_meetings()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 24, 0, out _);
            Assert.IsTrue(grid.TryPlace(Conference(40), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(EventHall(120), new Vector2Int(8, 1), out var hall));

            var system = new ConferenceSystem();
            var news = new TowerNews();
            var wallet = new FundsWallet(0);
            var rng = new ScriptedRandom(14, 1);

            system.TickDay(0, grid, 5, 1, 1f, wallet, news, rng);
            system.TickDay(12, grid, 5, 1, 1f, wallet, news, rng);
            system.TickDay(14, grid, 5, 1, 1f, wallet, news, rng);

            Assert.IsTrue(system.IsHallBooked(hall));
            // Event Hall booking must not zero Conference daily meetings.
            Assert.AreEqual(150, system.ComputeDailyMeetings(grid, 10, 0, 1f));
        }

        static bool HasMajorNews(TowerNews news, string needle)
        {
            foreach (var item in news.Items)
            {
                if (item.Category != TowerNewsCategory.MajorEvent) continue;
                if (item.Text != null && item.Text.ToLowerInvariant().Contains(needle))
                    return true;
            }

            return false;
        }

        sealed class ScriptedRandom : System.Random
        {
            readonly Queue<int> _values;

            public ScriptedRandom(params int[] values) =>
                _values = new Queue<int>(values ?? Array.Empty<int>());

            public override int Next(int minValue, int maxValue)
            {
                if (_values.Count == 0)
                    throw new InvalidOperationException("ScriptedRandom exhausted");
                return _values.Dequeue();
            }
        }

        static TowerGrid EventGrid(out RoomInstance low, out RoomInstance high)
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 28, 0, out _);
            Assert.IsTrue(grid.TryPlace(EventHall(100), new Vector2Int(0, 1), out low));
            Assert.IsTrue(grid.TryPlace(EventHall(200), new Vector2Int(12, 1), out high));
            return grid;
        }

        static RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO Conference(int eventCapacity)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ConferenceSystem.ConferenceId;
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(8, 1);
            so.allowAboveGround = true;
            so.eventCapacity = eventCapacity;
            so.baseIncome = 0;
            return so;
        }

        static RoomTypeSO EventHall(int eventCapacity)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ConferenceSystem.EventHallId;
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(12, 2);
            so.allowAboveGround = true;
            so.eventCapacity = eventCapacity;
            so.baseIncome = 0;
            return so;
        }

    }
}
