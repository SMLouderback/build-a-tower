using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class WeatherGameplayTests
    {
        [Test]
        public void StreetSpawnChance_neutral_weather_matches_legacy_formula()
        {
            var chance = AgentSystem.StreetSpawnChance(stars: 2, atriumMultiplier: 1.15f, weatherMultiplier: 1f);
            Assert.AreEqual(AgentSystem.StreetSpawnBaseChance * 3 * 1.15f, chance, 0.0001f);
        }

        [Test]
        public void StreetSpawnChance_scales_with_weather_multiplier()
        {
            var fair = AgentSystem.StreetSpawnChance(0, 1f, WeatherSystem.FairStreet);
            var wet = AgentSystem.StreetSpawnChance(0, 1f, WeatherSystem.WetStreet);
            Assert.AreEqual(fair * WeatherSystem.WetStreet, wet, 0.0001f);
        }

        [Test]
        public void StreetSpawnChance_is_zero_when_severe_weather()
        {
            Assert.AreEqual(0f, AgentSystem.StreetSpawnChance(5, 1.15f, WeatherSystem.SevereStreet));
        }

        [Test]
        public void StreetSpawnChance_clamps_to_one_and_ignores_negative_weather()
        {
            Assert.AreEqual(1f, AgentSystem.StreetSpawnChance(100, 2f, 1f));
            Assert.AreEqual(0f, AgentSystem.StreetSpawnChance(3, 1f, -1f));
        }

        [Test]
        public void StreetSpendMultiplier_stacks_climate_and_weather()
        {
            Assert.AreEqual(1.2f * 0.8f, AgentSystem.StreetSpendMultiplier(1.2f, WeatherSystem.WetSpend), 0.0001f);
            Assert.AreEqual(1f, AgentSystem.StreetSpendMultiplier(1f, 1f), 0.0001f);
        }

        [Test]
        public void StreetMultipliers_follow_live_weather_state()
        {
            var weather = new WeatherSystem(new System.Random(1));
            weather.ForceRoll(WeatherKind.Thunderstorm, 10, 600);

            Assert.AreEqual(0f, AgentSystem.StreetSpawnChance(3, 1f, weather.StreetTrafficMultiplier));
            Assert.AreEqual(
                WeatherSystem.SevereSpend,
                AgentSystem.StreetSpendMultiplier(1f, weather.ShopSpendMultiplier),
                0.0001f);
        }
    }
}
