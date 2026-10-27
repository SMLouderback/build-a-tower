namespace BuildATower
{
    /// <summary>
    /// Tenant-class stress thresholds from logistics metrics (elevator wait seconds, game time).
    /// Pure helpers — feed float metrics into stress/fill paths; do not mutate agents here.
    /// Upper: wait &gt; 15s. Mid: between Upper and Lower. Lower: resilient (no elev-wait stress).
    /// </summary>
    public static class TenantClassStress
    {
        public const float UpperElevWaitStressSeconds = 15f;
        public const float MidElevWaitStressSeconds = 25f;

        public static float ElevWaitThresholdSeconds(TenantClass cls) => cls switch
        {
            TenantClass.Upper => UpperElevWaitStressSeconds,
            TenantClass.Mid => MidElevWaitStressSeconds,
            _ => float.PositiveInfinity
        };

        public static bool ElevWaitStress(TenantClass cls, float waitSeconds)
        {
            var threshold = ElevWaitThresholdSeconds(cls);
            if (float.IsPositiveInfinity(threshold)) return false;
            return waitSeconds > threshold;
        }
    }
}
