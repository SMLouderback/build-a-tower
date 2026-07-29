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

        public bool Serves(int floor) => floor >= MinFloor && floor <= MaxFloor;
    }
}
