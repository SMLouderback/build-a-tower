using System.Collections.Generic;

namespace BuildATower
{
    public sealed class ElevatorShaftRuntime
    {
        public int RoomInstanceId;
        public int X;
        public int MinFloor;
        public int MaxFloor;
        public ElevatorCar Car;
        public Dictionary<int, Queue<int>> UpQueues;
        public Dictionary<int, Queue<int>> DownQueues;
        public bool InMaintenance;

        public int PassengersToday { get; private set; }
        public float WaitSumToday { get; private set; }
        public int WaitSamplesToday { get; private set; }

        readonly VisitHistoryRing _passengerHistory = new();
        readonly FloatHistoryRing _waitSumHistory = new();

        public int PassengersYesterday => _passengerHistory.Yesterday;
        public float AveragePassengersLast7Days => _passengerHistory.Average();

        public float AvgWaitToday =>
            WaitSamplesToday > 0 ? WaitSumToday / WaitSamplesToday : 0f;

        public float AvgWaitYesterday
        {
            get
            {
                var p = _passengerHistory.Yesterday;
                return p > 0 ? _waitSumHistory.Yesterday / p : 0f;
            }
        }

        public float AverageWaitLast7Days
        {
            get
            {
                var p = _passengerHistory.Sum();
                return p > 0 ? _waitSumHistory.Sum() / p : 0f;
            }
        }

        public bool Serves(int floor) => floor >= MinFloor && floor <= MaxFloor;

        public void RecordBoarding(float waitMinutes)
        {
            if (waitMinutes < 0f) waitMinutes = 0f;
            PassengersToday++;
            WaitSumToday += waitMinutes;
            WaitSamplesToday++;
        }

        public void ArchiveDay()
        {
            _passengerHistory.Push(PassengersToday);
            _waitSumHistory.Push(WaitSumToday);
            PassengersToday = 0;
            WaitSumToday = 0f;
            WaitSamplesToday = 0;
        }
    }
}
