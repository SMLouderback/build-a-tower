namespace BuildATower
{
    /// <summary>Ordered trophy metals; higher values outrank lower on claim.</summary>
    public enum TrophyMetal
    {
        None = 0,
        Bronze,
        Silver,
        Gold,
        Platinum,
        Diamond,
        Rainbow,
        Galactic
    }

    public static class TrophyMetalCompare
    {
        public static TrophyMetal Max(TrophyMetal a, TrophyMetal b) =>
            (TrophyMetal)System.Math.Max((int)a, (int)b);

        public static bool IsHigher(TrophyMetal candidate, TrophyMetal current) =>
            candidate > current;
    }
}
