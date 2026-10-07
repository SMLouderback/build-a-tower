using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Season tile for the top HUD bar. Loads Resources/Art/Hud/season_{name}
    /// (raw PNG in a .bytes TextAsset, or a Texture2D fallback).
    /// </summary>
    public static class SeasonHudArt
    {
        public const string ResourcesRoot = "Art/Hud/";

        static readonly Dictionary<Season, Texture2D> _cache = new();
        static readonly HashSet<Season> _owned = new();

        /// <summary>Resources key (no extension) for a season tile.</summary>
        public static string ResourceFor(Season season)
        {
            switch (season)
            {
                case Season.Spring: return ResourcesRoot + "season_spring";
                case Season.Fall: return ResourcesRoot + "season_fall";
                case Season.Winter: return ResourcesRoot + "season_winter";
                default: return ResourcesRoot + "season_summer";
            }
        }

        /// <summary>Weather system season, else calendar month, else Summer.</summary>
        public static Season Resolve(TowerSimulation sim)
        {
            if (sim == null) return Season.Summer;
            if (sim.Weather != null) return sim.Weather.CurrentSeason;
            var clock = sim.Clock;
            return clock == null
                ? Season.Summer
                : SeasonUtil.FromMonth(GameClock.DateForDayIndex(clock.DayIndex).Month);
        }

        public static string DisplayName(Season season) => season.ToString();

        public static bool TryGetTexture(Season season, out Texture2D tex)
        {
            if (_cache.TryGetValue(season, out tex) && tex != null)
                return true;

            tex = Load(season);
            _cache[season] = tex;
            return tex != null;
        }

        public static void ResetCache()
        {
            foreach (var s in _owned)
            {
                if (_cache.TryGetValue(s, out var t) && t != null)
                {
                    if (Application.isPlaying) Object.Destroy(t);
                    else Object.DestroyImmediate(t);
                }
            }

            _cache.Clear();
            _owned.Clear();
        }

        static Texture2D Load(Season season)
        {
            var path = ResourceFor(season);
            var bytesAsset = Resources.Load<TextAsset>(path);
            var png = bytesAsset != null ? bytesAsset.bytes : null;
            if (png != null && png.Length >= 32)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = path
                };
                if (tex.LoadImage(png, false))
                {
                    _owned.Add(season);
                    return tex;
                }

                if (Application.isPlaying) Object.Destroy(tex);
                else Object.DestroyImmediate(tex);
                return null;
            }

            return Resources.Load<Texture2D>(path);
        }
    }
}
