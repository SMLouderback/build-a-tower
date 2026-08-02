using System;
using System.Collections.Generic;

namespace BuildATower
{
    public sealed class ElevatorSystem
    {
        const float TimeEpsilon = 0.0001f;

        readonly List<ElevatorShaftRuntime> _shafts = new();
        readonly Dictionary<int, int> _passengerDestFloor = new();
        float _speedMultiplier = 1f;

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

        public void Tick(float deltaGameMinutes, float speedMultiplier = 1f)
        {
            if (deltaGameMinutes <= 0f)
                return;

            _speedMultiplier = speedMultiplier > 0f ? speedMultiplier : 1f;
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
                if (shaft.InMaintenance || shaft.X != x || !shaft.Serves(floor))
                    continue;

                var queues = direction == ElevatorDirection.Up
                    ? shaft.UpQueues
                    : shaft.DownQueues;
                queues[floor].Enqueue(agentId);
                return true;
            }

            return false;
        }

        public bool TrySetMaintenance(int roomInstanceId, bool inMaintenance)
        {
            foreach (var shaft in _shafts)
            {
                if (shaft.RoomInstanceId != roomInstanceId) continue;
                shaft.InMaintenance = inMaintenance;
                return true;
            }

            return false;
        }

        public ElevatorShaftRuntime FindByRoomId(int roomInstanceId)
        {
            foreach (var shaft in _shafts)
            {
                if (shaft.RoomInstanceId == roomInstanceId)
                    return shaft;
            }

            return null;
        }

        public bool IsDrained(ElevatorShaftRuntime shaft)
        {
            if (shaft == null) return false;
            if (shaft.Car.PassengerIds.Count > 0) return false;
            for (var floor = shaft.MinFloor; floor <= shaft.MaxFloor; floor++)
            {
                if (shaft.UpQueues[floor].Count > 0 || shaft.DownQueues[floor].Count > 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// True when no passengers or queues depend on floors outside [newMin,newMax].
        /// Used so a correction-window shrink cannot strand agents.
        /// </summary>
        public bool CanVacateFloors(ElevatorShaftRuntime shaft, int newMin, int newMax)
        {
            if (shaft == null) return false;
            foreach (var agentId in shaft.Car.PassengerIds)
            {
                if (!_passengerDestFloor.TryGetValue(agentId, out var destination))
                    return false;
                if (destination < newMin || destination > newMax)
                    return false;
            }

            if (shaft.Car.Floor < newMin || shaft.Car.Floor > newMax)
            {
                // Car may be empty on a floor about to vanish — only OK if drained of passengers.
                if (shaft.Car.PassengerIds.Count > 0)
                    return false;
            }

            for (var floor = shaft.MinFloor; floor <= shaft.MaxFloor; floor++)
            {
                if (floor >= newMin && floor <= newMax) continue;
                if (shaft.UpQueues[floor].Count > 0 || shaft.DownQueues[floor].Count > 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Position of an agent in a landing queue, or -1 when not queued.
        /// Index 0 is next to board, so views can lay waiters out in order.
        /// </summary>
        public int GetQueueIndex(
            ElevatorShaftRuntime shaft,
            int floor,
            ElevatorDirection direction,
            int agentId)
        {
            if (shaft == null || direction == ElevatorDirection.None) return -1;
            var queues = direction == ElevatorDirection.Up
                ? shaft.UpQueues
                : shaft.DownQueues;
            if (queues == null || !queues.TryGetValue(floor, out var queue)) return -1;

            var index = 0;
            foreach (var id in queue)
            {
                if (id == agentId) return index;
                index++;
            }

            return -1;
        }

        /// <summary>
        /// Records a passenger's destination floor. Call before or when boarding;
        /// passengers without a recorded destination will not alight.
        /// </summary>
        public void SetPassengerDestination(int agentId, int floor)
        {
            _passengerDestFloor[agentId] = floor;
        }

        public void ClearPassengerDestination(int agentId)
        {
            _passengerDestFloor.Remove(agentId);
        }

        /// <summary>
        /// Drops an agent from every landing queue, car passenger list, and destination map.
        /// Used when a trip is abandoned so stale entries cannot strand anyone or fill the car.
        /// </summary>
        public bool RemoveFromQueues(int agentId)
        {
            var removed = false;
            foreach (var shaft in _shafts)
            {
                removed |= RemoveFromQueueMap(shaft.UpQueues, agentId);
                removed |= RemoveFromQueueMap(shaft.DownQueues, agentId);
                if (shaft.Car?.PassengerIds != null &&
                    shaft.Car.PassengerIds.Remove(agentId))
                    removed = true;
            }

            _passengerDestFloor.Remove(agentId);
            return removed;
        }

        static bool RemoveFromQueueMap(Dictionary<int, Queue<int>> queues, int agentId)
        {
            if (queues == null) return false;
            var removed = false;
            foreach (var pair in queues)
            {
                var queue = pair.Value;
                if (queue == null || queue.Count == 0) continue;

                var kept = new Queue<int>(queue.Count);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    if (id == agentId)
                    {
                        removed = true;
                        continue;
                    }

                    kept.Enqueue(id);
                }

                while (kept.Count > 0)
                    queue.Enqueue(kept.Dequeue());
            }

            return removed;
        }

        public ElevatorShaftRuntime FindServing(int x, int floorA, int floorB)
        {
            foreach (var shaft in _shafts)
            {
                if (shaft.InMaintenance) continue;
                if (shaft.X == x && shaft.Serves(floorA) && shaft.Serves(floorB))
                    return shaft;
            }

            return null;
        }

        public ElevatorShaftRuntime FindServing(int floorA, int floorB)
        {
            foreach (var shaft in _shafts)
            {
                if (shaft.InMaintenance) continue;
                if (shaft.Serves(floorA) && shaft.Serves(floorB))
                    return shaft;
            }

            return null;
        }

        /// <summary>
        /// All non-maintenance shafts that serve both floors (for scored routing).
        /// </summary>
        public IReadOnlyList<ElevatorShaftRuntime> GetServingShafts(int floorA, int floorB)
        {
            var result = new List<ElevatorShaftRuntime>();
            foreach (var shaft in _shafts)
            {
                if (shaft.InMaintenance) continue;
                if (shaft.Serves(floorA) && shaft.Serves(floorB))
                    result.Add(shaft);
            }

            return result;
        }

        public int QueueLength(
            ElevatorShaftRuntime shaft,
            int floor,
            ElevatorDirection direction)
        {
            if (shaft == null || direction == ElevatorDirection.None)
                return 0;

            var queues = direction == ElevatorDirection.Up
                ? shaft.UpQueues
                : shaft.DownQueues;
            if (queues == null || !queues.TryGetValue(floor, out var queue))
                return 0;

            return queue.Count;
        }

        /// <summary>
        /// Passengers already aboard whose destination continues in <paramref name="direction"/>.
        /// </summary>
        public int SameWayPassengerCount(
            ElevatorShaftRuntime shaft,
            ElevatorDirection direction)
        {
            if (shaft?.Car == null || direction == ElevatorDirection.None)
                return 0;

            var count = 0;
            var carFloor = shaft.Car.Floor;
            foreach (var agentId in shaft.Car.PassengerIds)
            {
                if (!_passengerDestFloor.TryGetValue(agentId, out var destination))
                    continue;

                if (direction == ElevatorDirection.Up && destination > carFloor)
                    count++;
                else if (direction == ElevatorDirection.Down && destination < carFloor)
                    count++;
            }

            return count;
        }

        public float EstimateWaitMinutes(
            ElevatorShaftRuntime shaft,
            int entryFloor,
            ElevatorDirection direction)
        {
            if (shaft == null || direction == ElevatorDirection.None)
                return 0f;

            var queueAhead = QueueLength(shaft, entryFloor, direction);
            var sameWay = SameWayPassengerCount(shaft, direction);
            var busy = ElevatorRouting.NeedsBusyPenalty(shaft, entryFloor, direction);
            return ElevatorRouting.EstimateWaitMinutes(queueAhead, sameWay, busy);
        }

        /// <summary>
        /// Shaft covering a column and floor span regardless of maintenance state.
        /// Use for agents already committed to a shaft; use FindServing for planning.
        /// </summary>
        public ElevatorShaftRuntime FindShaftAt(int x, int floorA, int floorB)
        {
            foreach (var shaft in _shafts)
            {
                if (shaft.X == x && shaft.Serves(floorA) && shaft.Serves(floorB))
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
                        var travelMinutes = TravelMinutesForNextFloor(shaft);
                        var travelRemaining = travelMinutes - shaft.Car.StateMinutes;
                        if (remaining + TimeEpsilon < travelRemaining)
                        {
                            shaft.Car.StateMinutes += remaining;
                            return;
                        }

                        remaining -= Math.Max(0f, travelRemaining);
                        shaft.Car.StateMinutes = 0f;
                        shaft.Car.Floor += shaft.Car.Direction == ElevatorDirection.Up ? 1 : -1;

                        // Never walk the car off the shaft (ghost passengers / cleared dest).
                        if (shaft.Car.Floor > shaft.MaxFloor || shaft.Car.Floor < shaft.MinFloor)
                        {
                            shaft.Car.Floor = Clamp(shaft.Car.Floor, shaft.MinFloor, shaft.MaxFloor);
                            OpenDoors(shaft);
                            break;
                        }

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

        bool ShouldStop(ElevatorShaftRuntime shaft) =>
            WillStopAt(shaft, shaft.Car.Floor);

        float TravelMinutesForNextFloor(ElevatorShaftRuntime shaft)
        {
            float baseMinutes;
            if (shaft.Car.Direction == ElevatorDirection.None)
            {
                baseMinutes = ElevatorCar.MinutesPerFloor;
            }
            else
            {
                var nextFloor = shaft.Car.Floor +
                                (shaft.Car.Direction == ElevatorDirection.Up ? 1 : -1);
                baseMinutes = WillStopAt(shaft, nextFloor)
                    ? ElevatorCar.MinutesPerFloor
                    : ElevatorCar.MinutesPerPassingFloor;
            }

            return baseMinutes / _speedMultiplier;
        }

        bool WillStopAt(ElevatorShaftRuntime shaft, int floor)
        {
            if (!shaft.Serves(floor)) return false;

            foreach (var agentId in shaft.Car.PassengerIds)
            {
                if (_passengerDestFloor.TryGetValue(agentId, out var destination) &&
                    destination == floor)
                    return true;
            }

            var directionQueue = shaft.Car.Direction == ElevatorDirection.Up
                ? shaft.UpQueues
                : shaft.DownQueues;
            if (directionQueue.TryGetValue(floor, out var matching) && matching.Count > 0)
                return true;

            return shaft.Car.PassengerIds.Count == 0 &&
                   ((shaft.UpQueues.TryGetValue(floor, out var up) && up.Count > 0) ||
                    (shaft.DownQueues.TryGetValue(floor, out var down) && down.Count > 0));
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
                    !shaft.Serves(destination))
                {
                    // No/invalid destination — eject so ghosts cannot fill capacity forever.
                    shaft.Car.PassengerIds.RemoveAt(i);
                    _passengerDestFloor.Remove(agentId);
                    continue;
                }

                if (destination != shaft.Car.Floor)
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
