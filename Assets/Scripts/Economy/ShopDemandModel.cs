using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public enum ShopDemandFamily
    {
        Food,
        Retail
    }

    public enum ShopDemandTier
    {
        Budget,
        Mid,
        Premium
    }

    public readonly struct ShopDemandKey : IEquatable<ShopDemandKey>
    {
        public ShopDemandFamily Family { get; }
        public ShopDemandTier Tier { get; }

        public ShopDemandKey(ShopDemandFamily family, ShopDemandTier tier)
        {
            Family = family;
            Tier = tier;
        }

        public bool Equals(ShopDemandKey other) =>
            Family == other.Family && Tier == other.Tier;

        public override bool Equals(object obj) =>
            obj is ShopDemandKey other && Equals(other);

        public override int GetHashCode() =>
            ((int)Family * 397) ^ (int)Tier;
    }

    public readonly struct ShopDemandPoolSnapshot
    {
        public int Generated { get; }
        public int Served { get; }
        public int SpilledIn { get; }
        public int SpilledOut { get; }
        public int Remaining { get; }
        public int Unmet => Remaining;
        public float Saturation => Generated <= 0
            ? 0f
            : Mathf.Clamp01((Served + SpilledOut) / (float)Generated);

        public ShopDemandPoolSnapshot(
            int generated,
            int served,
            int spilledIn,
            int spilledOut,
            int remaining)
        {
            Generated = generated;
            Served = served;
            SpilledIn = spilledIn;
            SpilledOut = spilledOut;
            Remaining = remaining;
        }
    }

    public sealed class ShopDemandSnapshot
    {
        readonly Dictionary<ShopDemandKey, ShopDemandPoolSnapshot> pools;

        public int TotalGenerated { get; }
        public int TotalUnmet { get; }
        public float TotalUnmetRatio =>
            TotalGenerated <= 0 ? 0f : TotalUnmet / (float)TotalGenerated;

        public ShopDemandSnapshot(
            IReadOnlyDictionary<ShopDemandKey, ShopDemandPoolSnapshot> source)
        {
            pools = new Dictionary<ShopDemandKey, ShopDemandPoolSnapshot>(6);

            foreach (ShopDemandFamily family in Enum.GetValues(typeof(ShopDemandFamily)))
            {
                foreach (ShopDemandTier tier in Enum.GetValues(typeof(ShopDemandTier)))
                {
                    var key = new ShopDemandKey(family, tier);
                    var pool = source != null && source.TryGetValue(key, out var value)
                        ? value
                        : default;
                    pools.Add(key, pool);
                    TotalGenerated += pool.Generated;
                    TotalUnmet += pool.Unmet;
                }
            }
        }

        public ShopDemandPoolSnapshot Pool(
            ShopDemandFamily family,
            ShopDemandTier tier) =>
            pools[new ShopDemandKey(family, tier)];
    }
}
