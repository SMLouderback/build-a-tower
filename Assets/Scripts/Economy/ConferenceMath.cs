using UnityEngine;

namespace BuildATower
{
    public static class ConferenceMath
    {
        public const int MeetingPayPerOfficeWorker = 15;
        public const float MeetingStarsFactor = 0.25f;

        public static int DailyMeetingPayout(
            int officeWorkerCount,
            int hallCapacity,
            int totalEligibleCapacity,
            int stars,
            float climateSpendMult)
        {
            if (totalEligibleCapacity <= 0 || hallCapacity <= 0)
                return 0;

            var share = hallCapacity / (float)totalEligibleCapacity;
            var raw = officeWorkerCount
                      * MeetingPayPerOfficeWorker
                      * (1f + stars * MeetingStarsFactor)
                      * climateSpendMult;
            var uncapped = Mathf.RoundToInt(raw * share);
            var cap = hallCapacity * 50;
            return uncapped < cap ? uncapped : cap;
        }
    }
}
