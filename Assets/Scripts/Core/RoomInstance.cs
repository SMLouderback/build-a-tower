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
        /// <summary>0–100 structural/cleanliness condition. 0 means broken.</summary>
        public int Condition { get; set; } = 100;
        public bool IsBroken => Condition <= 0;
        public bool Dirty { get; private set; }
        /// <summary>
        /// Total maid-minutes of cleaning still owed (shared pool). Multiple maids chip away
        /// in short shifts instead of locking exclusive multi-hour jobs.
        /// </summary>
        public float CleanWorkRemaining { get; private set; }
        /// <summary>Handyman repair shifts still outstanding (venue post-event work).</summary>
        public int RepairJobsRemaining { get; private set; }
        /// <summary>Game minutes for each outstanding repair shift (0 = use default chunk time).</summary>
        public float RepairJobMinutes { get; private set; }
        /// <summary>Hired staff count for housekeeping/maintenance rooms. Clamped 0–4.</summary>
        public int StaffedWorkers { get; private set; }
        public bool CondoSold { get; set; }
        /// <summary>0=Low, 1=Normal, 2=High, 3=Max. Default Normal.</summary>
        public int PriceTier { get; set; } = PricePricing.TierNormal;

        public float PlacedAtRealtime { get; private set; } = -1f;
        public int ConstructionSpent { get; private set; }
        public int LifetimeIncome { get; private set; }
        public int LifetimeExpense { get; private set; }

        public int VisitsToday { get; private set; }
        public int ShopEarningsToday { get; private set; }
        public int ConcurrentVisitors { get; private set; }

        readonly VisitHistoryRing _visitHistory = new();

        /// <summary>Shop visits recorded at the last midnight push (0 if none yet).</summary>
        public int VisitsYesterday => _visitHistory.Yesterday;

        /// <summary>Mean daily visits over up to the last 7 recorded midnights.</summary>
        public float AverageVisitsLast7Days => _visitHistory.Average();

        public int VisitHistoryRecordedDays => _visitHistory.RecordedDays;

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

        public void RecordVisit() => VisitsToday++;

        public void RecordShopSpend(int amount)
        {
            if (amount > 0) ShopEarningsToday += amount;
        }

        /// <summary>Archives today's visit count into the 7-day ring (call before reset at midnight).</summary>
        public void PushVisitHistoryDay() => _visitHistory.Push(VisitsToday);

        public void ResetVisitsToday()
        {
            VisitsToday = 0;
            ShopEarningsToday = 0;
            ConcurrentVisitors = 0;
        }

        public bool TryOccupyVisitorSlot()
        {
            var cap = ShopVisitRules.SlotCount(Type);
            if (ConcurrentVisitors >= cap) return false;
            ConcurrentVisitors++;
            return true;
        }

        public void ReleaseVisitorSlot()
        {
            if (ConcurrentVisitors > 0) ConcurrentVisitors--;
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

        public void MarkDirty()
        {
            Dirty = true;
        }

        /// <summary>Marks dirty and adds maid-minutes (hotels on checkout, venues after use).</summary>
        public void QueueCleanWork(float minutes)
        {
            if (minutes <= 0f) return;
            Dirty = true;
            CleanWorkRemaining += minutes;
        }

        /// <summary>
        /// Legacy helper: <paramref name="jobs"/> maid-shifts × <paramref name="minutesPerJob"/>
        /// added to the shared clean pool.
        /// </summary>
        public void QueueCleaning(int jobs, float minutesPerJob)
        {
            if (jobs <= 0 || minutesPerJob <= 0f) return;
            QueueCleanWork(jobs * minutesPerJob);
        }

        public void ClearDirty()
        {
            Dirty = false;
            CleanWorkRemaining = 0f;
        }

        /// <summary>Applies completed maid progress; clears Dirty when the pool hits zero.</summary>
        public void ApplyCleanWork(float minutes)
        {
            if (minutes <= 0f) return;
            CleanWorkRemaining = Mathf.Max(0f, CleanWorkRemaining - minutes);
            if (CleanWorkRemaining <= 0.001f)
                ClearDirty();
        }

        public void QueueRepairs(int jobs, float minutesPerJob)
        {
            if (jobs <= 0) return;
            RepairJobsRemaining += jobs;
            if (minutesPerJob > 0f)
                RepairJobMinutes = minutesPerJob;
        }

        public void CompleteRepairJob()
        {
            if (RepairJobsRemaining > 0)
                RepairJobsRemaining--;
            if (RepairJobsRemaining <= 0)
            {
                RepairJobsRemaining = 0;
                RepairJobMinutes = 0f;
            }
        }

        public void SetStaffedWorkers(int count) =>
            StaffedWorkers = Mathf.Clamp(count, 0, 4);
    }
}
