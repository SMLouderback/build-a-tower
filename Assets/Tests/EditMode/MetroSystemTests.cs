using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class MetroSystemTests
    {
        static RectInt Footprint(int leftX, int y = -3) => new RectInt(leftX, y, 8, 3);

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void MaxStationsFor_below_three_stars_is_zero(int stars)
        {
            Assert.AreEqual(0, MetroSystem.MaxStationsFor(stars));
        }

        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        [TestCase(6, 3)]
        [TestCase(10, 3)]
        public void MaxStationsFor_ladder(int stars, int expected)
        {
            Assert.AreEqual(expected, MetroSystem.MaxStationsFor(stars));
        }

        [Test]
        public void CanPlace_accepts_when_left_edges_are_at_least_24_tiles_apart()
        {
            var metro = new MetroSystem();
            var existing = new List<RectInt> { Footprint(0) };
            var candidate = Footprint(24);

            Assert.IsTrue(metro.CanPlace(existing, candidate, 5, out var reason));
            Assert.IsEmpty(reason);
        }

        [Test]
        public void CanPlace_rejects_when_left_edges_are_closer_than_24_tiles()
        {
            var metro = new MetroSystem();
            var existing = new List<RectInt> { Footprint(0) };
            var candidate = Footprint(23);

            Assert.IsFalse(metro.CanPlace(existing, candidate, 5, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void CanPlace_rejects_when_star_cap_would_be_exceeded()
        {
            var metro = new MetroSystem();
            var existing = new List<RectInt> { Footprint(0) };
            var candidate = Footprint(MetroSystem.MinLeftEdgeSpacingTiles);

            Assert.IsFalse(metro.CanPlace(existing, candidate, 3, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [TestCase(0, 1f)]
        [TestCase(1, 1.08f)]
        [TestCase(2, 1.16f)]
        [TestCase(3, 1.20f)]
        public void StreetTrafficMultiplier_soft_caps_at_point_twenty(int stationCount, float expected)
        {
            var metro = new MetroSystem();
            for (var i = 0; i < stationCount; i++)
                metro.RegisterStation(Footprint(i * MetroSystem.MinLeftEdgeSpacingTiles));

            Assert.AreEqual(expected, metro.StreetTrafficMultiplier, 0.0001f);
        }

        [TestCase(0, 1f)]
        [TestCase(1, 1.05f)]
        [TestCase(2, 1.10f)]
        [TestCase(3, 1.12f)]
        public void TravelReliefMultiplier_soft_caps_at_point_twelve(int stationCount, float expected)
        {
            var metro = new MetroSystem();
            for (var i = 0; i < stationCount; i++)
                metro.RegisterStation(Footprint(i * MetroSystem.MinLeftEdgeSpacingTiles));

            Assert.AreEqual(expected, metro.TravelReliefMultiplier, 0.0001f);
        }

        [Test]
        public void HasTunnel_is_false_until_a_station_is_registered()
        {
            var metro = new MetroSystem();
            Assert.IsFalse(metro.HasTunnel);
            Assert.AreEqual(0, metro.StationCount);

            metro.RegisterStation(Footprint(0));
            Assert.IsTrue(metro.HasTunnel);
            Assert.AreEqual(1, metro.StationCount);
        }

        [Test]
        public void UnregisterStation_and_Clear_remove_stations()
        {
            var metro = new MetroSystem();
            var first = Footprint(0);
            var second = Footprint(MetroSystem.MinLeftEdgeSpacingTiles);
            metro.RegisterStation(first);
            metro.RegisterStation(second);
            Assert.AreEqual(2, metro.StationCount);

            metro.UnregisterStation(first);
            Assert.AreEqual(1, metro.StationCount);
            Assert.IsTrue(metro.HasTunnel);

            metro.Clear();
            Assert.AreEqual(0, metro.StationCount);
            Assert.IsFalse(metro.HasTunnel);
        }
    }
}
