using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ParallaxSeasonTreeTests
    {
        [TestCase(Season.Summer, "Art/Parallax/near_trees")]
        [TestCase(Season.Winter, "Art/Parallax/near_trees_winter")]
        [TestCase(Season.Spring, "Art/Parallax/near_trees_spring")]
        [TestCase(Season.Fall, "Art/Parallax/near_trees_fall")]
        public void TreeResourceFor_maps_season_to_resource_key(Season season, string expected)
        {
            Assert.AreEqual(expected, ParallaxBackdrop.TreeResourceFor(season));
        }

        [Test]
        public void TreeResourceFor_keys_are_unique_per_season()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (Season s in System.Enum.GetValues(typeof(Season)))
                Assert.IsTrue(seen.Add(ParallaxBackdrop.TreeResourceFor(s)), s.ToString());
        }

        [Test]
        public void Every_seasonal_tree_plate_exists_in_resources()
        {
            foreach (Season s in System.Enum.GetValues(typeof(Season)))
            {
                var key = ParallaxBackdrop.TreeResourceFor(s);
                var ta = Resources.Load<TextAsset>(key);
                Assert.IsNotNull(ta, key);
                Assert.Greater(ta.bytes.Length, 32, key);
            }
        }
    }
}
