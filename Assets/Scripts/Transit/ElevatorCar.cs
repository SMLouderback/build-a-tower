using System.Collections.Generic;

namespace BuildATower
{
    public enum ElevatorDirection
    {
        None,
        Up,
        Down
    }

    public enum ElevatorCarState
    {
        Idle,
        Moving,
        DoorsOpen
    }

    public sealed class ElevatorCar
    {
        public const int Capacity = 8;
        public const float MinutesPerFloor = 2f;
        public const float DoorDwellMinutes = 1f;

        public int Floor;
        public ElevatorDirection Direction;
        public ElevatorCarState State;
        public readonly List<int> PassengerIds = new();

        internal float StateMinutes;
    }
}
