namespace BuildATower
{
    public static class CleanlinessSystem
    {
        static readonly int[] Outputs = { 12, 20, 32, 48 };
        static readonly int[] Fees = { 30000, 70000, 140000 };
        static readonly int[] Minutes = { 1440, 4320, 10080 };

        public static float Target(int supply, int sewage) =>
            sewage <= 0 ? 100f : supply / (float)sewage * 100f;

        public static float Step(float stored, float target) =>
            stored + (target - stored) * 0.10f;

        public static float Dirt(float stored) =>
            stored >= 100f ? 0f : 1f - stored / 100f;

        public static float FillMultiplier(LuxuryBand band, float stored)
        {
            var atZero = band == LuxuryBand.Mid ? 0.65f : band == LuxuryBand.Upper ? 0.35f : 0.85f;
            var dirt = Dirt(stored);
            return atZero + (1f - atZero) * (1f - dirt);
        }

        public static float IllnessPerMinute(float stored) => 0.08f * Dirt(stored);
        public static float RestaurantWeightFactor(float stored) => 1f - 0.40f * Dirt(stored);

        public static int TreatmentSupply(int level) =>
            level < 0 || level > 3 ? 0 : Outputs[level];

        public static int TreatmentUpgradeFee(int nextLevel) =>
            nextLevel < 1 || nextLevel > 3 ? 0 : Fees[nextLevel - 1];

        public static int TreatmentUpgradeMinutes(int nextLevel) =>
            nextLevel < 1 || nextLevel > 3 ? 0 : Minutes[nextLevel - 1];

        /// <summary>
        /// True for older saves with no cleanliness block. <c>JsonUtility</c> materializes a missing
        /// serializable class as an all-default object, so <c>present == false</c> counts as absent too.
        /// </summary>
        public static bool IsAbsent(CleanlinessSnapshotV1 snapshot) =>
            snapshot == null || !snapshot.present;
    }
}
