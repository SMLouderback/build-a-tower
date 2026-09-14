using System.Collections.Generic;
using System.Linq;

namespace BuildATower
{
    public static class AmbientMixer
    {
        public const float SelectionBoostMultiplier = 2.75f;

        public static float ProfileBias(SoundProfile profile) => profile switch
        {
            SoundProfile.Parking => 1.3f,
            SoundProfile.Restaurant => 1.3f,
            SoundProfile.Elevator => 1.25f,
            SoundProfile.Stairs => 0.6f,
            SoundProfile.Utility => 0.65f,
            _ => 1f
        };

        public static float ScoreRoom(float visibility, float activity, SoundProfile profile)
        {
            return visibility * activity * ProfileBias(profile);
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

            if (selectedBoost.HasValue && aggregated.ContainsKey(selectedBoost.Value))
                aggregated[selectedBoost.Value] *= SelectionBoostMultiplier;

            return aggregated
                .OrderByDescending(kv => kv.Value)
                .Take(maxVoices)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }
    }
}
