using System;
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
        /// <summary>Office cutaway kit variant (1 or 2). 0 = unset; paint treats as 1.</summary>
        public int ArtVariant { get; set; }

        public float PlacedAtRealtime { get; private set; } = -1f;
        public int ConstructionSpent { get; private set; }
        public int LifetimeIncome { get; private set; }
        public int LifetimeExpense { get; private set; }

        public int VisitsToday { get; private set; }
        public int ShopEarningsToday { get; private set; }
        public int ConcurrentVisitors { get; private set; }
        public int ShopRevenueYesterday { get; private set; }
        public int ShopUpkeepYesterday { get; private set; }
        public int ShopNetYesterday => ShopRevenueYesterday - ShopUpkeepYesterday;

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

        public void ArchiveShopDay(int creditedRevenue, int upkeep)
        {
            ShopRevenueYesterday = Mathf.Max(0, creditedRevenue);
            ShopUpkeepYesterday = Mathf.Max(0, upkeep);
            PushVisitHistoryDay();
            ResetVisitsToday();
        }

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

        public RoomSnapshotV1 CaptureSnapshot(float nowRealtime)
        {
            var remaining = IsInBuildGrace(nowRealtime)
                ? Mathf.Max(0f, PlacedAtRealtime + BuildGraceSeconds - nowRealtime)
                : 0f;

            return new RoomSnapshotV1
            {
                instanceId = InstanceId,
                roomTypeId = Type == null ? null : Type.id,
                originX = Origin.x,
                originY = Origin.y,
                width = Size.x,
                height = Size.y,
                evaluation = Evaluation,
                condition = Condition,
                dirty = Dirty,
                cleanWorkRemaining = CleanWorkRemaining,
                repairJobsRemaining = RepairJobsRemaining,
                repairJobMinutes = RepairJobMinutes,
                staffedWorkers = StaffedWorkers,
                condoSold = CondoSold,
                priceTier = PriceTier,
                artVariant = ArtVariant,
                buildGraceSecondsRemaining = remaining,
                constructionSpent = ConstructionSpent,
                lifetimeIncome = LifetimeIncome,
                lifetimeExpense = LifetimeExpense,
                visitsToday = VisitsToday,
                shopEarningsToday = ShopEarningsToday,
                shopRevenueYesterday = ShopRevenueYesterday,
                shopUpkeepYesterday = ShopUpkeepYesterday,
                visitHistory = _visitHistory.CaptureValues()
            };
        }

        public void RestoreSnapshot(RoomSnapshotV1 snapshot, float nowRealtime)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var typeId = Type == null ? null : Type.id;
            if (snapshot.instanceId != InstanceId)
                throw new ArgumentException("Snapshot instance ID does not match this room.", nameof(snapshot));
            if (!string.Equals(snapshot.roomTypeId, typeId, StringComparison.Ordinal))
                throw new ArgumentException("Snapshot room type ID does not match this room.", nameof(snapshot));
            if (snapshot.originX != Origin.x || snapshot.originY != Origin.y)
                throw new ArgumentException("Snapshot origin does not match this room.", nameof(snapshot));
            if (snapshot.width != Size.x || snapshot.height != Size.y)
                throw new ArgumentException("Snapshot size does not match this room.", nameof(snapshot));

            Evaluation = Mathf.Clamp(snapshot.evaluation, 0, 100);
            Condition = Mathf.Clamp(snapshot.condition, 0, 100);
            Dirty = snapshot.dirty;
            CleanWorkRemaining = Dirty ? Mathf.Max(0f, snapshot.cleanWorkRemaining) : 0f;
            RepairJobsRemaining = Mathf.Max(0, snapshot.repairJobsRemaining);
            RepairJobMinutes = RepairJobsRemaining > 0
                ? Mathf.Max(0f, snapshot.repairJobMinutes)
                : 0f;
            StaffedWorkers = Mathf.Clamp(snapshot.staffedWorkers, 0, 4);
            CondoSold = snapshot.condoSold;
            PriceTier = PricePricing.ClampTier(snapshot.priceTier);
            ArtVariant = Mathf.Max(0, snapshot.artVariant);

            var remaining = Mathf.Clamp(
                snapshot.buildGraceSecondsRemaining,
                0f,
                BuildGraceSeconds);
            PlacedAtRealtime = remaining > 0f
                ? nowRealtime - (BuildGraceSeconds - remaining)
                : -1f;

            ConstructionSpent = Mathf.Max(0, snapshot.constructionSpent);
            LifetimeIncome = Mathf.Max(0, snapshot.lifetimeIncome);
            LifetimeExpense = Mathf.Max(0, snapshot.lifetimeExpense);
            VisitsToday = Mathf.Max(0, snapshot.visitsToday);
            ShopEarningsToday = Mathf.Max(0, snapshot.shopEarningsToday);
            ConcurrentVisitors = 0;
            ShopRevenueYesterday = Mathf.Max(0, snapshot.shopRevenueYesterday);
            ShopUpkeepYesterday = Mathf.Max(0, snapshot.shopUpkeepYesterday);
            _visitHistory.RestoreValues(snapshot.visitHistory);
        }
    }
}
