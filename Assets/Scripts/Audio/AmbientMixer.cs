using System.Collections.Generic;
using System.Linq;

namespace BuildATower
{
    public static class AmbientMixer
    {
        public const float SelectionBoostMultiplier = 2.75f;

        /// <summary>Full outdoor bed weight when the tower contributes no ambience.</summary>
        public const float OutdoorFullWeight = 1f;

        /// <summary>
        /// When aggregated room weight reaches this, outdoor ambience is fully faded out.
        /// </summary>
        public const float OutdoorFadeAtTowerEnergy = 2.5f;

        public static float ProfileBias(SoundProfile profile) => profile switch
        {
            SoundProfile.Parking => 1.3f,
            SoundProfile.Restaurant => 1.3f,
            SoundProfile.Elevator => 1.25f,
            SoundProfile.Stairs => 0.6f,
            SoundProfile.Utility => 0.65f,
            SoundProfile.Outdoor => 1f,
            _ => 1f
        };

        public static float ScoreRoom(float visibility, float activity, SoundProfile profile)
        {
            return visibility * activity * ProfileBias(profile);
        }

        /// <summary>
        /// Birds/breeze bed fades as interior room energy rises.
        /// </summary>
        public static float OutdoorWeight(float towerEnergy)
        {
            if (towerEnergy <= 0f)
                return OutdoorFullWeight;
            var t = towerEnergy / OutdoorFadeAtTowerEnergy;
            if (t >= 1f)
                return 0f;
            // Smooth fade: keep outdoor audible early, then drop off.
            var remain = 1f - t;
            return OutdoorFullWeight * remain * remain;
        }

        public static float SumWeights(IEnumerable<(SoundProfile profile, float weight)> roomWeights)
        {
            var sum = 0f;
            if (roomWeights == null)
                return sum;
            foreach (var (profile, weight) in roomWeights)
            {
                if (profile == SoundProfile.None ||
                    profile == SoundProfile.Build ||
                    profile == SoundProfile.Outdoor)
                    continue;
                if (weight > 0f)
                    sum += weight;
            }

            return sum;
        }

        public static List<(SoundProfile profile, float weight)> RankProfiles(
            IEnumerable<(SoundProfile profile, float weight)> roomWeights,
            SoundProfile? selectedBoost,
            int maxVoices = 4)
        {
            var aggregated = new Dictionary<SoundProfile, float>();
            foreach (var (profile, weight) in roomWeights)
            {
                if (profile == SoundProfile.None || profile == SoundProfile.Build)
                    continue;

                if (!aggregated.ContainsKey(profile))
                    aggregated[profile] = 0f;
                aggregated[profile] += weight;
            }

            if (selectedBoost.HasValue &&
                selectedBoost.Value != SoundProfile.Outdoor &&
                aggregated.ContainsKey(selectedBoost.Value))
                aggregated[selectedBoost.Value] *= SelectionBoostMultiplier;

            return aggregated
                .OrderByDescending(kv => kv.Value)
                .Take(maxVoices)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }
    }
}
