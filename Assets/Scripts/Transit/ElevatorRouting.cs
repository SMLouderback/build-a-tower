using System;

namespace BuildATower
{
    public static class ElevatorRouting
    {
        public const int StairsComfortFloorSpan = 3;
        public const float StairsOverCapPenaltyPerFloor = 40f;
        public const float StairsOverCapStressPerFloor = 25f;

        public const float WaitWeight = 3f;
        public const float BoardCycleMinutes = 2f;
        public const float BusyPenaltyMinutes = 1.5f;
        public const float SwitchImproveRatio = 0.25f;
        public const float RescoreIntervalGameMinutes = 10f;
        public const float SwitchCooldownGameMinutes = 30f;

        public static float StairsOverCapPenalty(int stairFloorSpan) =>
            Math.Max(0, stairFloorSpan - StairsComfortFloorSpan) * StairsOverCapPenaltyPerFloor;

        public static int MaxAffordableOverCapFloors(float currentStress)
        {
            var n = 0;
            var s = currentStress;
            while (s < 100f)
            {
                n++;
                s += StairsOverCapStressPerFloor;
            }

            return n;
        }

        public static float Score(int walkCost, float waitEstimate, float waitWeightScale = 1f) =>
            walkCost + WaitWeight * waitWeightScale * waitEstimate;

        public static bool IsMeaningfullyBetter(float currentScore, float alternateScore)
        {
            if (alternateScore >= currentScore) return false;
            var improve = (currentScore - alternateScore) / Math.Max(1f, currentScore);
            return improve >= SwitchImproveRatio;
        }

        /// <summary>
        /// Rough wait estimate from queue depth, same-way load, and optional busy penalty.
        /// </summary>
        public static float EstimateWaitMinutes(
            int queueAhead,
            int sameWayPassengers,
            bool applyBusyPenalty)
        {
            var load = Math.Max(0, queueAhead) + Math.Max(0, sameWayPassengers);
            var wait = load / (float)ElevatorCar.Capacity * BoardCycleMinutes;
            if (applyBusyPenalty)
                wait += BusyPenaltyMinutes;
            return wait;
        }

        public static bool NeedsBusyPenalty(
            ElevatorShaftRuntime shaft,
            int entryFloor,
            ElevatorDirection direction)
        {
            if (shaft?.Car == null || direction == ElevatorDirection.None)
                return false;

            var car = shaft.Car;
            if (car.Direction != ElevatorDirection.None && car.Direction != direction)
                return true;

            return car.Floor != entryFloor;
        }
    }
}
