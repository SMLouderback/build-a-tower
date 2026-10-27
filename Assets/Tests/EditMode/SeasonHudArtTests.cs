using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class SeasonHudArtTests
    {
        [TestCase(Season.Spring, "Art/Hud/season_spring")]
        [TestCase(Season.Summer, "Art/Hud/season_summer")]
        [TestCase(Season.Fall, "Art/Hud/season_fall")]
        [TestCase(Season.Winter, "Art/Hud/season_winter")]
        public void ResourceFor_maps_season_to_resource_key(Season season, string expected)
        {
            Assert.AreEqual(expected, SeasonHudArt.ResourceFor(season));
        }

        [Test]
        public void Every_season_tile_loads_as_texture()
        {
            SeasonHudArt.ResetCache();
            try
            {
                foreach (Season s in System.Enum.GetValues(typeof(Season)))
                {
                    Assert.IsTrue(SeasonHudArt.TryGetTexture(s, out var tex), s.ToString());
                    Assert.IsNotNull(tex, s.ToString());
                    Assert.Greater(tex.width, 8, s.ToString());
                }
            }
            finally
            {
                SeasonHudArt.ResetCache();
            }
        }

        [Test]
        public void Resolve_without_simulation_defaults_to_summer()
        {
            Assert.AreEqual(Season.Summer, SeasonHudArt.Resolve(null));
        }
    }
}
