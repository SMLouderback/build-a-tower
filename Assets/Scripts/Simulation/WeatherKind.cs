namespace BuildATower
{
    /// <summary>Concrete weather shown to the player. Separate from economic <see cref="MarketClimate"/>.</summary>
    public enum WeatherKind
    {
        /// <summary>Fair — full street traffic.</summary>
        Clear = 0,
        /// <summary>Fair bucket sibling — same traffic as Clear, softer sky.</summary>
        PartlyCloudy = 1,
        Cloudy = 2,
        /// <summary>Wet, non-winter.</summary>
        Rain = 3,
        /// <summary>Wet, winter.</summary>
        Snow = 4,
        /// <summary>Severe, non-winter.</summary>
        Thunderstorm = 5,
        /// <summary>Severe, winter.</summary>
        Blizzard = 6
    }

    /// <summary>Season-independent roll bucket (75 / 10 / 10 / 5).</summary>
    public enum WeatherBucket
    {
        Fair = 0,
        Cloudy = 1,
        Wet = 2,
        Severe = 3
    }

    /// <summary>Lingering street penalty after a blizzard.</summary>
    public enum WeatherHangover
    {
        None = 0,
        /// <summary>Storm ended mid-day; remainder of that day is crushed.</summary>
        BlizzardSameDayCleanup = 1,
        /// <summary>The calendar day after the blizzard day: half street traffic.</summary>
        BlizzardNextDayHalf = 2
    }
}
