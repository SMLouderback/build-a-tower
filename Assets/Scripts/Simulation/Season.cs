using System;

namespace BuildATower
{
    /// <summary>Classic N. Hemisphere calendar seasons (hard cuts on month boundaries).</summary>
    public enum Season
    {
        Winter = 0,
        Spring = 1,
        Summer = 2,
        Fall = 3
    }

    public static class SeasonUtil
    {
        /// <summary>Dec–Feb Winter, Mar–May Spring, Jun–Aug Summer, Sep–Nov Fall. <paramref name="month"/> is 1–12.</summary>
        public static Season FromMonth(int month)
        {
            switch (month)
            {
                case 12:
                case 1:
                case 2:
                    return Season.Winter;
                case 3:
                case 4:
                case 5:
                    return Season.Spring;
                case 6:
                case 7:
                case 8:
                    return Season.Summer;
                case 9:
                case 10:
                case 11:
                    return Season.Fall;
                default:
                    throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be 1–12.");
            }
        }
    }
}
