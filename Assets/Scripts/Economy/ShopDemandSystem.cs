using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class ShopDemandSystem
    {
        sealed class PoolState
        {
            public int Generated;
            public int Served;
            public int SpilledIn;
            public int SpilledOut;
            public int Remaining;

            public void Reset(int generated)
            {
                Generated = generated;
                Served = 0;
                SpilledIn = 0;
                SpilledOut = 0;
                Remaining = generated;
            }

            public ShopDemandPoolSnapshot Snapshot() =>
                new ShopDemandPoolSnapshot(
                    Generated,
                    Served,
                    SpilledIn,
                    SpilledOut,
                    Remaining);
        }

        readonly Dictionary<ShopDemandKey, PoolState> pools =
            new Dictionary<ShopDemandKey, PoolState>(6);
        readonly List<ShopDemandSnapshot> history =
            new List<ShopDemandSnapshot>(ShopDemandBalance.HistoryCapacity);
        readonly IReadOnlyList<ShopDemandSnapshot> readOnlyHistory;

        public ShopDemandSnapshot Snapshot => CreateSnapshot();
        public IReadOnlyList<ShopDemandSnapshot> History => readOnlyHistory;

        public ShopDemandSystem()
        {
            readOnlyHistory = history.AsReadOnly();
            foreach (ShopDemandFamily family in Enum.GetValues(typeof(ShopDemandFamily)))
            {
                foreach (ShopDemandTier tier in Enum.GetValues(typeof(ShopDemandTier)))
                    pools.Add(new ShopDemandKey(family, tier), new PoolState());
            }
        }

        public void BeginDay(
            IReadOnlyList<Agent> agents,
            int stars,
            float climateMultiplier)
        {
            var generated = CreateEmptyGeneration();

            if (agents != null)
            {
                for (var i = 0; i < agents.Count; i++)
                    AddOccupantDemand(generated, agents[i]);
            }

            AddStreetBaseline(generated, Mathf.Max(0, stars));

            var climate = Mathf.Max(0f, climateMultiplier);
            foreach (var pair in pools)
            {
                var amount = (int)Math.Round(
                    generated[pair.Key] * climate,
                    MidpointRounding.AwayFromZero);
                pair.Value.Reset(amount);
            }
        }

        Dictionary<ShopDemandKey, float> CreateEmptyGeneration()
        {
            var generated = new Dictionary<ShopDemandKey, float>(6);
            foreach (var key in pools.Keys)
                generated.Add(key, 0f);
            return generated;
        }

        static void AddOccupantDemand(
            IDictionary<ShopDemandKey, float> generated,
            Agent agent)
        {
            if (agent == null)
                return;

            float food;
            float retail;
            switch (agent.Role)
            {
                case AgentRole.OfficeWorker:
                    food = ShopDemandBalance.OfficeFood;
                    retail = ShopDemandBalance.OfficeRetail;
                    break;
                case AgentRole.CondoResident:
                    food = ShopDemandBalance.CondoFood;
                    retail = ShopDemandBalance.CondoRetail;
                    break;
                case AgentRole.HotelGuest:
                    food = ShopDemandBalance.HotelFood;
                    retail = ShopDemandBalance.HotelRetail;
                    break;
                default:
                    return;
            }

            var tier = ShopDemandBalance.TierForWealth(agent.Wealth);
            Add(generated, ShopDemandFamily.Food, tier, food);
            Add(generated, ShopDemandFamily.Retail, tier, retail);
        }

        static void AddStreetBaseline(
            IDictionary<ShopDemandKey, float> generated,
            int stars)
        {
            Add(generated, ShopDemandFamily.Food, ShopDemandTier.Budget, 2 + stars);
            Add(generated, ShopDemandFamily.Retail, ShopDemandTier.Budget, 1 + stars / 2);
            Add(generated, ShopDemandFamily.Food, ShopDemandTier.Mid, Mathf.Max(0, stars - 1));
            Add(generated, ShopDemandFamily.Retail, ShopDemandTier.Mid, Mathf.Max(0, stars - 2));
        }

        static void Add(
            IDictionary<ShopDemandKey, float> generated,
            ShopDemandFamily family,
            ShopDemandTier tier,
            float amount)
        {
            var key = new ShopDemandKey(family, tier);
            generated[key] += amount;
        }

        ShopDemandSnapshot CreateSnapshot()
        {
            var snapshot = new Dictionary<ShopDemandKey, ShopDemandPoolSnapshot>(6);
            foreach (var pair in pools)
                snapshot.Add(pair.Key, pair.Value.Snapshot());
            return new ShopDemandSnapshot(snapshot);
        }
    }
}
