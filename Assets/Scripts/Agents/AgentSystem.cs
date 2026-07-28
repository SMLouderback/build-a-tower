using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class AgentSystem
    {
        public const float StressGainPerSecond = 12f;
        public const float StressDecayPerSecond = 4f;
        public const float MoveCellsPerSecond = 2.5f;

        readonly List<Agent> _agents = new();
        readonly TransitRouter _router;
        readonly ElevatorSystem _elevators;
        readonly System.Random _rng = new(42);
        int _nextId = 1;
        int _lastTotalMinutes = int.MinValue;

        public IReadOnlyList<Agent> Agents => _agents;

        public float AverageStress
        {
            get
            {
                if (_agents.Count == 0) return 0f;
                var sum = 0f;
                foreach (var a in _agents) sum += a.Stress;
                return sum / _agents.Count;
            }
        }

        public AgentSystem(TransitRouter router)
        {
            _router = router;
            _elevators = router.Elevators;
        }

        public void SyncHomes(TowerGrid grid)
        {
            var livingRooms = new HashSet<RoomInstance>();
            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null) continue;
                if (room.Type.isLobby || room.Type.isStairs || room.Type.isScaffolding) continue;
                if (room.Type.maxOccupants <= 0) continue;
                if (room.Type.category is not (RoomCategory.Office or RoomCategory.Condo or RoomCategory.Hotel))
                    continue;
                livingRooms.Add(room);
            }

            for (var i = _agents.Count - 1; i >= 0; i--)
            {
                if (!livingRooms.Contains(_agents[i].HomeRoom))
                    _agents.RemoveAt(i);
            }

            foreach (var room in livingRooms)
            {
                var role = RoleFor(room.Type.category);
                var existing = 0;
                foreach (var a in _agents)
                    if (ReferenceEquals(a.HomeRoom, room)) existing++;

                var want = Mathf.Max(1, room.Type.maxOccupants);
                while (existing < want)
                {
                    var homeCell = HomeCell(room, existing);
                    var agent = new Agent(_nextId++, role, room, homeCell);
                    ConfigureSchedule(agent);
                    if (role == AgentRole.CondoResident)
                    {
                        agent.Phase = AgentPhase.AtHome;
                        agent.Visible = true;
                        agent.Cell = homeCell;
                        agent.WorldPosition = new Vector2(homeCell.x + 0.5f, homeCell.y + 0.5f);
                    }

                    _agents.Add(agent);
                    existing++;
                }
            }
        }

        public void Tick(float deltaTime, GameClock clock, TowerGrid grid)
        {
            if (grid == null || clock == null) return;

            var total = clock.DayIndex * GameClock.MinutesPerDay + clock.MinuteOfDay;
            var advanced = 0;
            if (_lastTotalMinutes != int.MinValue)
                advanced = Mathf.Max(0, total - _lastTotalMinutes);
            _lastTotalMinutes = total;

            foreach (var agent in _agents)
            {
                if (agent.Phase == AgentPhase.Working && advanced > 0)
                    agent.WorkedMinutes += advanced;

                UpdateSchedule(agent, clock, grid);
                if (agent.Phase == AgentPhase.WaitingAtElevator)
                    agent.ElevatorWaitMinutes += clock.LastTickGameMinutes;
                StepMovement(agent, deltaTime);
                UpdateStress(agent, deltaTime);
            }
        }

        void UpdateSchedule(Agent agent, GameClock clock, TowerGrid grid)
        {
            switch (agent.Role)
            {
                case AgentRole.OfficeWorker:
                    UpdateOffice(agent, clock, grid);
                    break;
                case AgentRole.HotelGuest:
                    UpdateHotel(agent, clock, grid);
                    break;
                case AgentRole.CondoResident:
                    if (agent.Phase != AgentPhase.AtHome || !agent.Visible)
                    {
                        agent.Phase = AgentPhase.AtHome;
                        agent.Visible = true;
                        agent.Cell = HomeCell(agent.HomeRoom, 0);
                        agent.WorldPosition = new Vector2(agent.Cell.x + 0.5f, agent.Cell.y + 0.5f);
                        agent.Path.Clear();
                    }

                    break;
            }
        }

        void UpdateOffice(Agent agent, GameClock clock, TowerGrid grid)
        {
            var minute = clock.MinuteOfDay;
            var exitCell = LobbyExitCell(grid);
            var home = HomeCell(agent.HomeRoom, 0);

            if (agent.Phase == AgentPhase.Outside &&
                !agent.CheckedOutToday &&
                minute >= agent.ArrivalMinute &&
                minute < 12 * 60)
            {
                agent.WorkedMinutes = 0;
                BeginTrip(agent, exitCell, home, AgentPhase.Working, grid);
                agent.CheckedOutToday = true; // reused as "started commute today"
            }

            if (agent.Phase == AgentPhase.Working && agent.WorkedMinutes >= agent.WorkMinutes)
                BeginTrip(agent, agent.Cell, exitCell, AgentPhase.Outside, grid);

            // Reset daily commute flag at midnight window.
            if (minute < 5 * 60)
                agent.CheckedOutToday = false;
        }

        void UpdateHotel(Agent agent, GameClock clock, TowerGrid grid)
        {
            var minute = clock.MinuteOfDay;
            var exitCell = LobbyExitCell(grid);
            var home = HomeCell(agent.HomeRoom, 0);

            if (agent.Phase == AgentPhase.Outside &&
                minute >= 16 * 60 &&
                agent.CheckInDay != clock.DayIndex)
            {
                BeginTrip(agent, exitCell, home, AgentPhase.Staying, grid);
                agent.CheckInDay = clock.DayIndex;
                agent.CheckedOutToday = false;
            }

            if (agent.Phase == AgentPhase.Staying &&
                minute < 11 * 60 &&
                agent.CheckInDay >= 0 &&
                agent.CheckInDay < clock.DayIndex &&
                !agent.CheckedOutToday)
            {
                BeginTrip(agent, agent.Cell, exitCell, AgentPhase.Outside, grid);
                agent.CheckedOutToday = true;
            }
        }

        void BeginTrip(
            Agent agent,
            Vector2Int spawnIfOutside,
            Vector2Int to,
            AgentPhase after,
            TowerGrid grid)
        {
            if (agent.GoalCell == to &&
                agent.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator or AgentPhase.Riding)
                return;

            agent.GoalCell = to;
            agent.PhaseAfterMove = after;
            if (agent.Phase == AgentPhase.Outside)
            {
                agent.Cell = spawnIfOutside;
                agent.WorldPosition = new Vector2(spawnIfOutside.x + 0.5f, spawnIfOutside.y + 0.5f);
                agent.Visible = true;
            }

            if (_router.TryPlanTrip(agent.Cell, to, out var legs) && legs.Count > 0)
            {
                agent.TripLegs = legs;
                agent.TripLegIndex = 0;
                StartLeg(agent, legs[0]);
            }
            else
            {
                agent.TripLegs = new List<TransitLeg>();
                agent.TripLegIndex = 0;
                agent.Path = new List<Vector2Int>();
                agent.PathIndex = 0;
                agent.Phase = AgentPhase.Moving;
            }
        }

        void StepMovement(Agent agent, float deltaTime)
        {
            if (agent.Phase == AgentPhase.WaitingAtElevator)
            {
                var shaft = CurrentElevatorShaft(agent);
                if (shaft != null && shaft.Car.PassengerIds.Contains(agent.Id))
                {
                    agent.Phase = AgentPhase.Riding;
                    FollowElevatorCar(agent, shaft);
                }

                return;
            }

            if (agent.Phase == AgentPhase.Riding)
            {
                var shaft = CurrentElevatorShaft(agent);
                if (shaft == null) return;

                FollowElevatorCar(agent, shaft);
                if (shaft.Car.Floor != agent.ElevatorDestFloor ||
                    shaft.Car.State != ElevatorCarState.DoorsOpen ||
                    shaft.Car.PassengerIds.Contains(agent.Id))
                    return;

                agent.Cell = new Vector2Int(shaft.X, agent.ElevatorDestFloor);
                agent.WorldPosition = new Vector2(agent.Cell.x + 0.5f, agent.Cell.y + 0.5f);
                AdvanceLeg(agent);
                if (agent.Phase == AgentPhase.Moving)
                    StepMovement(agent, deltaTime);
                return;
            }

            if (agent.Phase != AgentPhase.Moving) return;

            if (agent.Path == null || agent.Path.Count == 0)
                return;

            var target = agent.Path[Mathf.Min(agent.PathIndex, agent.Path.Count - 1)];
            var targetPos = new Vector2(target.x + 0.5f, target.y + 0.5f);
            agent.WorldPosition = Vector2.MoveTowards(
                agent.WorldPosition,
                targetPos,
                MoveCellsPerSecond * deltaTime);

            if ((agent.WorldPosition - targetPos).sqrMagnitude > 0.0001f) return;

            agent.Cell = target;
            agent.PathIndex++;
            if (agent.PathIndex < agent.Path.Count) return;

            agent.Path.Clear();
            AdvanceLeg(agent);
        }

        void UpdateStress(Agent agent, float deltaTime)
        {
            var stuck = (agent.Phase == AgentPhase.Moving &&
                         (agent.Path == null || agent.Path.Count == 0) &&
                         agent.GoalCell.HasValue) ||
                        (agent.Phase == AgentPhase.WaitingAtElevator &&
                         agent.ElevatorWaitMinutes > 10f);
            if (stuck)
                agent.Stress = Mathf.Min(100f, agent.Stress + StressGainPerSecond * deltaTime);
            else
                agent.Stress = Mathf.Max(0f, agent.Stress - StressDecayPerSecond * deltaTime);
        }

        void StartLeg(Agent agent, TransitLeg leg)
        {
            if (leg.Kind != TransitLegKind.Elevator)
            {
                agent.Path = leg.Cells ?? new List<Vector2Int>();
                agent.PathIndex = 0;
                agent.Phase = AgentPhase.Moving;
                return;
            }

            agent.Path.Clear();
            agent.PathIndex = 0;
            agent.ElevatorDestFloor = leg.ExitFloor;
            agent.ElevatorWaitMinutes = 0f;
            var direction = leg.ExitFloor > leg.EntryFloor
                ? ElevatorDirection.Up
                : ElevatorDirection.Down;

            _elevators.SetPassengerDestination(agent.Id, leg.ExitFloor);
            if (_elevators.TryEnqueue(agent.Id, leg.ElevatorX, leg.EntryFloor, direction))
                agent.Phase = AgentPhase.WaitingAtElevator;
            else
                agent.Phase = AgentPhase.Moving;
        }

        void AdvanceLeg(Agent agent)
        {
            agent.TripLegIndex++;
            if (agent.TripLegs != null && agent.TripLegIndex < agent.TripLegs.Count)
            {
                StartLeg(agent, agent.TripLegs[agent.TripLegIndex]);
                return;
            }

            agent.Phase = agent.PhaseAfterMove;
            agent.GoalCell = null;
            agent.ElevatorWaitMinutes = 0f;
            if (agent.Phase == AgentPhase.Outside)
                agent.Visible = false;
        }

        ElevatorShaftRuntime CurrentElevatorShaft(Agent agent)
        {
            if (agent.TripLegs == null ||
                agent.TripLegIndex < 0 ||
                agent.TripLegIndex >= agent.TripLegs.Count)
                return null;

            var leg = agent.TripLegs[agent.TripLegIndex];
            if (leg.Kind != TransitLegKind.Elevator)
                return null;
            return _elevators.FindServing(leg.ElevatorX, leg.EntryFloor, leg.ExitFloor);
        }

        static void FollowElevatorCar(Agent agent, ElevatorShaftRuntime shaft)
        {
            agent.Cell = new Vector2Int(shaft.X, shaft.Car.Floor);
            agent.WorldPosition = new Vector2(shaft.X + 0.5f, shaft.Car.Floor + 0.5f);
        }

        void ConfigureSchedule(Agent agent)
        {
            if (agent.Role != AgentRole.OfficeWorker) return;
            agent.ArrivalMinute = 6 * 60 + _rng.Next(0, 3 * 60);
            var overtime = _rng.NextDouble() < 0.08;
            agent.WorkMinutes = 8 * 60 + (overtime ? _rng.Next(30, 121) : 0);
        }

        static AgentRole RoleFor(RoomCategory category) =>
            category switch
            {
                RoomCategory.Office => AgentRole.OfficeWorker,
                RoomCategory.Hotel => AgentRole.HotelGuest,
                _ => AgentRole.CondoResident
            };

        static Vector2Int HomeCell(RoomInstance room, int slot)
        {
            var x = room.Origin.x + Mathf.Min(slot, Mathf.Max(0, room.Size.x - 1));
            return new Vector2Int(x, room.Origin.y);
        }

        public static Vector2Int LobbyExitCell(TowerGrid grid) =>
            grid.HasLobby ? new Vector2Int(grid.MinX, TowerGrid.LobbyFloor) : Vector2Int.zero;
    }
}
