using System;

namespace BuildATower
{
    public readonly struct RewardExtrasGrant
    {
        public string Title { get; }
        public string Ribbon { get; }
        public int BuildCreditAmount { get; }
        public float DemandMultiplier { get; }
        public int DemandDays { get; }
        public int CrewDayCount { get; }

        public RewardExtrasGrant(
            string title = null,
            string ribbon = null,
            int buildCredit = 0,
            float demandMultiplier = 1f,
            int demandDays = 0,
            int crewDays = 0)
        {
            Title = title;
            Ribbon = ribbon;
            BuildCreditAmount = Math.Max(0, buildCredit);
            DemandMultiplier = demandMultiplier;
            DemandDays = Math.Max(0, demandDays);
            CrewDayCount = Math.Max(0, crewDays);
        }

        public bool HasAny =>
            !string.IsNullOrWhiteSpace(Title) ||
            !string.IsNullOrWhiteSpace(Ribbon) ||
            BuildCreditAmount > 0 ||
            (DemandMultiplier > 1f && DemandDays > 0) ||
            CrewDayCount > 0;

        public static RewardExtrasGrant Metadata(string title = null, string ribbon = null) =>
            new(title: title, ribbon: ribbon);

        public static RewardExtrasGrant BuildCredit(int amount) =>
            new(buildCredit: amount);

        public static RewardExtrasGrant CrewDays(int days) =>
            new(crewDays: days);
    }

    public sealed class RewardExtras : IRewardExtras
    {
        public RewardExtras(int currentDayIndex = 0)
        {
            CurrentDayIndex = currentDayIndex;
            DemandMultiplier = 1f;
            DemandExpiresDayIndex = -1;
        }

        public int CurrentDayIndex { get; set; }
        public int PendingBuildCredit { get; private set; }
        public float DemandMultiplier { get; private set; }
        public int DemandExpiresDayIndex { get; private set; }
        public int CrewDays { get; private set; }

        public RewardExtrasSnapshotV1 CaptureSnapshot() =>
            new RewardExtrasSnapshotV1
            {
                currentDayIndex = CurrentDayIndex,
                pendingBuildCredit = PendingBuildCredit,
                demandMultiplier = DemandMultiplier,
                demandExpiresDayIndex = DemandExpiresDayIndex,
                crewDays = CrewDays
            };

        public void RestoreSnapshot(RewardExtrasSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                CurrentDayIndex = 0;
                PendingBuildCredit = 0;
                DemandMultiplier = 1f;
                DemandExpiresDayIndex = -1;
                CrewDays = 0;
                return;
            }

            CurrentDayIndex = Math.Max(0, snapshot.currentDayIndex);
            PendingBuildCredit = Math.Max(0, snapshot.pendingBuildCredit);
            DemandMultiplier = snapshot.demandMultiplier > 0f ? snapshot.demandMultiplier : 1f;
            DemandExpiresDayIndex = snapshot.demandExpiresDayIndex;
            CrewDays = Math.Max(0, snapshot.crewDays);
        }

        public void OnClaim(ChallengeDef def, TrophyCase trophyCase)
        {
            var grant = def.Extras;
            if (!grant.HasAny)
                return;

            trophyCase?.SetMetadata(def.Subject, grant.Title, grant.Ribbon);
            PendingBuildCredit += grant.BuildCreditAmount;
            CrewDays += grant.CrewDayCount;

            if (grant.DemandMultiplier > 1f && grant.DemandDays > 0)
            {
                DemandMultiplier = Math.Max(DemandMultiplier, grant.DemandMultiplier);
                DemandExpiresDayIndex = Math.Max(DemandExpiresDayIndex, CurrentDayIndex + grant.DemandDays);
            }
        }

        public int ConsumeBuildCreditForSuccessfulPlacement(int roomCost)
        {
            if (roomCost <= 0 || PendingBuildCredit <= 0)
                return 0;

            // Build credit is a one-room token: the next successful placement subtracts
            // min(total credit, room cost), then any unused credit expires with that placement.
            var discount = Math.Min(PendingBuildCredit, roomCost);
            PendingBuildCredit = 0;
            return discount;
        }

        public bool TryConsumeCrewDay()
        {
            if (CrewDays <= 0)
                return false;

            CrewDays--;
            return true;
        }
    }
}
