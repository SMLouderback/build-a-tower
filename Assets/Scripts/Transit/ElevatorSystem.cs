using System;
using System.Collections.Generic;

namespace BuildATower
{
    public sealed class ElevatorSystem
    {
        const float TimeEpsilon = 0.0001f;

        readonly List<ElevatorShaftRuntime> _shafts = new();
        readonly Dictionary<int, int> _passengerDestFloor = new();

        public IReadOnlyList<ElevatorShaftRuntime> Shafts => _shafts;

        public void SyncFromGrid(TowerGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var existingById = new Dictionary<int, ElevatorShaftRuntime>();
            foreach (var shaft in _shafts)
                existingById[shaft.RoomInstanceId] = shaft;

            _shafts.Clear();
            foreach (var room in grid.Rooms)
            {
                if (room.Type == null || !room.Type.isElevatorShaft)
                    continue;

                if (!existingById.TryGetValue(room.InstanceId, out var shaft))
                {
                    var minFloor = room.Origin.y;
                    var maxFloor = room.Origin.y + room.Size.y - 1;
                    shaft = new ElevatorShaftRuntime
                    {
                        RoomInstanceId = room.InstanceId,
                        Car = new ElevatorCar
                        {
                            Floor = Clamp(TowerGrid.LobbyFloor, minFloor, maxFloor),
                            Direction = ElevatorDirection.None,
                            State = ElevatorCarState.Idle
                        },
                        UpQueues = new Dictionary<int, Queue<int>>(),
                        DownQueues = new Dictionary<int, Queue<int>>()
                    };
                }

                shaft.X = room.Origin.x;
                shaft.MinFloor = room.Origin.y;
                shaft.MaxFloor = room.Origin.y + room.Size.y - 1;
                shaft.Car.Floor = Clamp(shaft.Car.Floor, shaft.MinFloor, shaft.MaxFloor);
                SyncQueues(shaft.UpQueues, shaft.MinFloor, shaft.MaxFloor);
                SyncQueues(shaft.DownQueues, shaft.MinFloor, shaft.MaxFloor);
                _shafts.Add(shaft);
            }
        }

        public void Tick(float deltaGameMinutes)
        {
            if (deltaGameMinutes <= 0f)
                return;

            foreach (var shaft in _shafts)
                TickShaft(shaft, deltaGameMinutes);
        }

        public bool TryEnqueue(
            int agentId,
            int x,
            int floor,
            ElevatorDirection direction)
        {
            if (direction == ElevatorDirection.None)
                return false;

            foreach (var shaft in _shafts)
            {
                if (shaft.X != x || !shaft.Serves(floor))
                    continue;

                var queues = direction == ElevatorDirection.Up
                    ? shaft.UpQueues
                    : shaft.DownQueues;
                queues[floor].Enqueue(agentId);
                return true;
            }

            return false;
        }

        public void SetPassengerDestination(int agentId, int floor)
        {
            _passengerDestFloor[agentId] = floor;
        }

        public ElevatorShaftRuntime FindServing(int x, int floorA, int floorB)
        {
            foreach (var shaft in _shafts)
            {
                if (shaft.X == x && shaft.Serves(floorA) && shaft.Serves(floorB))
                    return shaft;
            }

            return null;
        }

        public ElevatorShaftRuntime FindServing(int floorA, int floorB)
        {
            foreach (var shaft in _shafts)
            {
                if (shaft.Serves(floorA) && shaft.Serves(floorB))
                    return shaft;
            }

            return null;
        }

        void TickShaft(ElevatorShaftRuntime shaft, float minutes)
        {
            var remaining = minutes;
            while (remaining > TimeEpsilon)
            {
                switch (shaft.Car.State)
                {
                    case ElevatorCarState.Idle:
                        if (!TryChooseTarget(shaft, out var idleTarget))
                            return;

                        if (idleTarget == shaft.Car.Floor)
                        {
                            OpenDoors(shaft);
                            continue;
                        }

                        shaft.Car.Direction = DirectionTo(shaft.Car.Floor, idleTarget);
                        shaft.Car.State = ElevatorCarState.Moving;
                        shaft.Car.StateMinutes = 0f;
                        continue;

                    case ElevatorCarState.Moving:
                        var travelRemaining =
                            ElevatorCar.MinutesPerFloor - shaft.Car.StateMinutes;
                        if (remaining + TimeEpsilon < travelRemaining)
                        {
                            shaft.Car.StateMinutes += remaining;
                            return;
                        }

                        remaining -= travelRemaining;
                        shaft.Car.StateMinutes = 0f;
                        shaft.Car.Floor += shaft.Car.Direction == ElevatorDirection.Up ? 1 : -1;

                        if (ShouldStop(shaft))
                            OpenDoors(shaft);
                        break;

                    case ElevatorCarState.DoorsOpen:
                        var dwellRemaining =
                            ElevatorCar.DoorDwellMinutes - shaft.Car.StateMinutes;
                        if (remaining + TimeEpsilon < dwellRemaining)
                        {
                            shaft.Car.StateMinutes += remaining;
                            return;
                        }

                        remaining -= dwellRemaining;
                        shaft.Car.StateMinutes = 0f;
                        shaft.Car.State = ElevatorCarState.Idle;
                        shaft.Car.Direction = ElevatorDirection.None;
                        break;
                }
            }
        }

        bool TryChooseTarget(ElevatorShaftRuntime shaft, out int target)
        {
            if (TryChoosePassengerTarget(shaft, out target))
                return true;

            var found = false;
            var bestDistance = int.MaxValue;
            target = shaft.Car.Floor;
            for (var floor = shaft.MinFloor; floor <= shaft.MaxFloor; floor++)
            {
                if (shaft.UpQueues[floor].Count == 0 &&
                    shaft.DownQueues[floor].Count == 0)
                    continue;

                var distance = Math.Abs(floor - shaft.Car.Floor);
                if (found && distance >= bestDistance)
                    continue;

                found = true;
                bestDistance = distance;
                target = floor;
            }

            return found;
        }

        bool TryChoosePassengerTarget(ElevatorShaftRuntime shaft, out int target)
        {
            var found = false;
            var bestDistance = int.MaxValue;
            target = shaft.Car.Floor;
            foreach (var agentId in shaft.Car.PassengerIds)
            {
                if (!_passengerDestFloor.TryGetValue(agentId, out var destination) ||
                    !shaft.Serves(destination))
                    continue;

                var distance = Math.Abs(destination - shaft.Car.Floor);
                if (found && distance >= bestDistance)
                    continue;

                found = true;
                bestDistance = distance;
                target = destination;
            }

            return found;
        }

        bool ShouldStop(ElevatorShaftRuntime shaft)
        {
            foreach (var agentId in shaft.Car.PassengerIds)
            {
                if (_passengerDestFloor.TryGetValue(agentId, out var destination) &&
                    destination == shaft.Car.Floor)
                    return true;
            }

            var directionQueue = shaft.Car.Direction == ElevatorDirection.Up
                ? shaft.UpQueues
                : shaft.DownQueues;
            if (directionQueue[shaft.Car.Floor].Count > 0)
                return true;

            return shaft.Car.PassengerIds.Count == 0 &&
                   (shaft.UpQueues[shaft.Car.Floor].Count > 0 ||
                    shaft.DownQueues[shaft.Car.Floor].Count > 0);
        }

        void OpenDoors(ElevatorShaftRuntime shaft)
        {
            shaft.Car.State = ElevatorCarState.DoorsOpen;
            shaft.Car.StateMinutes = 0f;
            Alight(shaft);

            var boardingDirection = ChooseBoardingDirection(shaft);
            shaft.Car.Direction = boardingDirection;
            Board(shaft, boardingDirection);
        }

        void Alight(ElevatorShaftRuntime shaft)
        {
            for (var i = shaft.Car.PassengerIds.Count - 1; i >= 0; i--)
            {
                var agentId = shaft.Car.PassengerIds[i];
                if (!_passengerDestFloor.TryGetValue(agentId, out var destination) ||
                    destination != shaft.Car.Floor)
                    continue;

                shaft.Car.PassengerIds.RemoveAt(i);
                _passengerDestFloor.Remove(agentId);
            }
        }

        void Board(ElevatorShaftRuntime shaft, ElevatorDirection direction)
        {
            if (direction == ElevatorDirection.None)
                return;

            var queues = direction == ElevatorDirection.Up
                ? shaft.UpQueues
                : shaft.DownQueues;
            var queue = queues[shaft.Car.Floor];
            while (queue.Count > 0 &&
                   shaft.Car.PassengerIds.Count < ElevatorCar.Capacity)
                shaft.Car.PassengerIds.Add(queue.Dequeue());
        }

        ElevatorDirection ChooseBoardingDirection(ElevatorShaftRuntime shaft)
        {
            if (shaft.Car.PassengerIds.Count > 0 &&
                shaft.Car.Direction != ElevatorDirection.None)
                return shaft.Car.Direction;

            var floor = shaft.Car.Floor;
            if (shaft.Car.Direction == ElevatorDirection.Up &&
                shaft.UpQueues[floor].Count > 0)
                return ElevatorDirection.Up;
            if (shaft.Car.Direction == ElevatorDirection.Down &&
                shaft.DownQueues[floor].Count > 0)
                return ElevatorDirection.Down;
            if (shaft.UpQueues[floor].Count > 0)
                return ElevatorDirection.Up;
            if (shaft.DownQueues[floor].Count > 0)
                return ElevatorDirection.Down;
            return shaft.Car.Direction;
        }

        static ElevatorDirection DirectionTo(int floor, int target) =>
            target > floor ? ElevatorDirection.Up : ElevatorDirection.Down;

        static int Clamp(int value, int min, int max) =>
            Math.Max(min, Math.Min(max, value));

        static void SyncQueues(
            Dictionary<int, Queue<int>> queues,
            int minFloor,
            int maxFloor)
        {
            var removedFloors = new List<int>();
            foreach (var floor in queues.Keys)
            {
                if (floor < minFloor || floor > maxFloor)
                    removedFloors.Add(floor);
            }

            foreach (var floor in removedFloors)
                queues.Remove(floor);
            for (var floor = minFloor; floor <= maxFloor; floor++)
            {
                if (!queues.ContainsKey(floor))
                    queues[floor] = new Queue<int>();
            }
        }
    }
}
