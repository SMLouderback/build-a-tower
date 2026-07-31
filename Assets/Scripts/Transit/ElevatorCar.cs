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
        public const int Capacity = 10;

        /// <summary>Travel time when the next floor is a planned stop.</summary>
        public const float MinutesPerFloor = 0.75f;

        /// <summary>Travel time when passing a floor with no planned stop.</summary>
        public const float MinutesPerPassingFloor = 0.35f;

        /// <summary>Door open / load-unload dwell (~15 simulated seconds).</summary>
        public const float DoorDwellMinutes = 0.25f;

        public int Floor;
        public ElevatorDirection Direction;
        public ElevatorCarState State;
        public readonly List<int> PassengerIds = new();

        internal float StateMinutes;
    }
}
