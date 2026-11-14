using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class MetroTrainMotionTests
    {
        [Test]
        public void CollectStopPathDistances_centers_train_on_station()
        {
            const int minX = -80;
            const float span = 181f;
            const float car = 6f;
            var stations = new List<RectInt> { new RectInt(0, -6, 8, 3) };
            var stops = new List<float>();
            MetroTrainMotion.CollectStopPathDistances(stations, minX, span, car, stops);
            Assert.AreEqual(1, stops.Count);
            // Station center at x=4 → stop left = 4 - (-80) - 3 = 81 → path = 87
            Assert.AreEqual(87f, stops[0], 0.001f);
        }

        [Test]
        public void Advance_dwells_five_game_minutes_at_station()
        {
            const float span = 100f;
            const float car = 6f;
            const float speed = 6f;
            var stops = new List<float> { 30f };
            var state = new MetroTrainMotion.State();

            // Reach the stop exactly.
            state = MetroTrainMotion.Advance(
                state, 30f / speed, span, car, speed, MetroTrainMotion.StationDwellGameMinutes, stops);
            Assert.AreEqual(30f, state.PathDistance, 0.001f);
            Assert.AreEqual(MetroTrainMotion.StationDwellGameMinutes, state.DwellGameMinutesRemaining, 0.001f);

            // Mid-dwell: do not move.
            state = MetroTrainMotion.Advance(
                state, 2f, span, car, speed, MetroTrainMotion.StationDwellGameMinutes, stops);
            Assert.AreEqual(30f, state.PathDistance, 0.001f);
            Assert.AreEqual(3f, state.DwellGameMinutesRemaining, 0.001f);

            // Finish dwell and roll past with leftover minutes.
            state = MetroTrainMotion.Advance(
                state, 4f, span, car, speed, MetroTrainMotion.StationDwellGameMinutes, stops);
            Assert.AreEqual(0f, state.DwellGameMinutesRemaining, 0.001f);
            Assert.AreEqual(30f + speed * 1f, state.PathDistance, 0.001f);
        }

        [Test]
        public void Advance_does_not_redwell_same_station_until_loop()
        {
            const float span = 40f;
            const float car = 6f;
            const float speed = 10f;
            var travel = MetroTrainMotion.TravelLength(span, car);
            var stops = new List<float> { 20f };
            var state = new MetroTrainMotion.State();

            state = MetroTrainMotion.Advance(
                state, 20f / speed, span, car, speed, MetroTrainMotion.StationDwellGameMinutes, stops);
            Assert.Greater(state.DwellGameMinutesRemaining, 0f);

            state = MetroTrainMotion.Advance(
                state, MetroTrainMotion.StationDwellGameMinutes, span, car, speed,
                MetroTrainMotion.StationDwellGameMinutes, stops);
            Assert.AreEqual(0f, state.DwellGameMinutesRemaining, 0.001f);

            // Cruise past wrap without stopping again at 20 until mask clears.
            state = MetroTrainMotion.Advance(
                state, (travel - 20f) / speed, span, car, speed,
                MetroTrainMotion.StationDwellGameMinutes, stops);
            Assert.AreEqual(0f, state.PathDistance, 0.05f);
            Assert.AreEqual(0, state.ServedStopMask);

            state = MetroTrainMotion.Advance(
                state, 20f / speed, span, car, speed, MetroTrainMotion.StationDwellGameMinutes, stops);
            Assert.AreEqual(MetroTrainMotion.StationDwellGameMinutes, state.DwellGameMinutesRemaining, 0.001f);
        }

        [Test]
        public void Advance_paused_clock_does_not_move()
        {
            var stops = new List<float> { 10f };
            var state = new MetroTrainMotion.State { PathDistance = 4f };
            var next = MetroTrainMotion.Advance(
                state, 0f, 50f, 6f, 6f, MetroTrainMotion.StationDwellGameMinutes, stops);
            Assert.AreEqual(4f, next.PathDistance, 0.001f);
        }

        [Test]
        public void StationDwell_is_five_game_minutes()
        {
            Assert.AreEqual(5f, MetroTrainMotion.StationDwellGameMinutes);
        }
    }
}
