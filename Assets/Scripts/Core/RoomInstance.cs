using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class RoomInstance
    {
        public const float BuildGraceSeconds = 10f;

        public int InstanceId { get; }
        public RoomTypeSO Type { get; }
        public Vector2Int Origin { get; }
        public Vector2Int Size { get; }
        public int Evaluation { get; set; } = 100;
        public bool CondoSold { get; set; }
        /// <summary>0=Low, 1=Normal, 2=High, 3=Max. Default Normal.</summary>
        public int PriceTier { get; set; } = PricePricing.TierNormal;

        public float PlacedAtRealtime { get; private set; } = -1f;
        public int ConstructionSpent { get; private set; }
        public int LifetimeIncome { get; private set; }
        public int LifetimeExpense { get; private set; }

        public RoomInstance(int instanceId, RoomTypeSO type, Vector2Int origin, Vector2Int size)
        {
            InstanceId = instanceId;
            Type = type;
            Origin = origin;
            Size = size;
            PriceTier = PricePricing.TierNormal;
        }

        public IEnumerable<Vector2Int> OccupiedCells()
        {
            for (var dy = 0; dy < Size.y; dy++)
            for (var dx = 0; dx < Size.x; dx++)
                yield return new Vector2Int(Origin.x + dx, Origin.y + dy);
        }

        public void RecordConstructionSpend(int amount, float nowRealtime, bool isInitialPlace)
        {
            if (amount < 0) return;
            ConstructionSpent += amount;
            if (isInitialPlace)
                PlacedAtRealtime = nowRealtime;
        }

        public void RecordLifetimeIncome(int amount)
        {
            if (amount > 0) LifetimeIncome += amount;
        }

        public void RecordLifetimeExpense(int amount)
        {
            if (amount > 0) LifetimeExpense += amount;
        }

        public bool IsInBuildGrace(float nowRealtime) =>
            PlacedAtRealtime >= 0f &&
            nowRealtime < PlacedAtRealtime + BuildGraceSeconds;

        public int GraceRefundAmount() =>
            ConstructionSpent - (LifetimeIncome - LifetimeExpense);

        public void CopyBuildGraceLedgerFrom(RoomInstance source)
        {
            if (source == null) return;
            PlacedAtRealtime = source.PlacedAtRealtime;
            ConstructionSpent = source.ConstructionSpent;
            LifetimeIncome = source.LifetimeIncome;
            LifetimeExpense = source.LifetimeExpense;
        }

        public static bool IsGraceRefundEligible(RoomTypeSO type) =>
            type != null && !type.isLobby && !type.isScaffolding;
    }
}
