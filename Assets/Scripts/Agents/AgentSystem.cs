using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class AgentSystem
    {
        public const float StressGainPerSecond = 12f;
        public const float StressDecayPerSecond = 4f;
        public const float LowConditionStressPerDay = 8f;
        public const float CrimeStressPerDayAtMax = 12f;
        public const float CriminalProximityStressPerMinute = 0.4f;
        /// <summary>
        /// Walk speed in cells per game minute. At 1x (1 game minute / real second) this
        /// matches the old cells-per-real-second feel; faster presets scale movement with the clock.
        /// </summary>
        public const float MoveCellsPerSecond = 2.5f;

        /// <summary>Distance from shaft centre to the first waiter, so the line starts outside the shaft cell.</summary>
        public const float QueueLaneOffset = 0.8f;
        public const float QueueSpacing = 0.34f;

        public const int MaxConcurrentStreetVisitors = 8;
        public const int StreetSpawnIntervalMinutes = 8;
        public const float StreetSpawnBaseChance = 0.25f;
        public const string HousekeepingId = "service_housekeeping";
        public const string MaintenanceId = "service_maintenance";
        public const string SecurityId = "service_security";
        public const float PatrolDwellMinutes = 8f;
        public const int MaxConcurrentCriminals = 3;
        public const float CriminalSpawnMinAvg = 15f;
        public const float CriminalSpawnChancePerMinute = 0.08f;
        public const float CriminalLifeMinutes = 180f;
        public const float CriminalFloorDwellMinutes = 8f;

        readonly List<Agent> _agents = new();
        readonly TransitRouter _router;
        readonly ElevatorSystem _elevators;
        readonly System.Random _rng = new(42);
        readonly HashSet<int> _condoMoveInsNotified = new();
        readonly HashSet<int> _patrolFloorScratch = new();
        readonly HashSet<int> _criminalFloorScratch = new();
        System.Action<RoomInstance> _onCondoResidentMovedIn;
        MarketClimate _climate;
        CrimeSystem _crime;
        int _nextId = 1;
        int _lastTotalMinutes = int.MinValue;
        float _nowTotalMinutes;
        int _streetSpawnMinuteAccumulator;

        public IReadOnlyList<Agent> Agents => _agents;
        public string LastCaptureMessage { get; private set; }
        public int Population
        {
            get
            {
                var count = 0;
                foreach (var agent in _agents)
                {
                    if (IsNonPopulationRole(agent.Role)) continue;
                    if (agent.Role != AgentRole.CondoResident || agent.HasMovedIn)
                        count++;
                }

                return count;
            }
        }

        public float AverageStress
        {
            get
            {
                var count = 0;
                var sum = 0f;
                foreach (var a in _agents)
                {
                    if (IsNonPopulationRole(a.Role)) continue;
                    sum += a.Stress;
                    count++;
                }

                return count == 0 ? 0f : sum / count;
            }
        }

        public AgentSystem(TransitRouter router, MarketClimate climate = null)
        {
            _router = router;
            _elevators = router.Elevators;
            _climate = climate;
        }

        public void SetClimate(MarketClimate climate) => _climate = climate;

        public void SyncHomes(
            TowerGrid grid,
            System.Action<RoomInstance> onNewCondoResident = null,
            int currentStars = 0,
            int climateOffset = 0)
        {
            _onCondoResidentMovedIn = onNewCondoResident;
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
                if (IsEphemeralOrStaffRole(_agents[i].Role)) continue;
                if (!livingRooms.Contains(_agents[i].HomeRoom))
                {
                    CancelCommercialVisit(_agents[i]);
                    _agents.RemoveAt(i);
                }
            }

            SyncServiceStaff(grid);

            foreach (var room in livingRooms)
            {
                var role = RoleFor(room.Type.category);
                if (room.IsBroken) continue;
                if (role == AgentRole.HotelGuest && room.Dirty) continue;

                var existing = 0;
                foreach (var a in _agents)
                    if (ReferenceEquals(a.HomeRoom, room)) existing++;

                if (role == AgentRole.CondoResident &&
                    !room.CondoSold &&
                    existing == 0 &&
                    !CanReachCondoFromLobby(grid, room))
                    continue;

                if (role == AgentRole.CondoResident &&
                    !room.CondoSold &&
                    existing == 0 &&
                    !PassesCondoDemand(room, currentStars, climateOffset))
                    continue;

                var want = Mathf.Max(1, room.Type.maxOccupants);
                while (existing < want)
                {
                    var homeCell = HomeCell(room, existing);
                    var agent = new Agent(_nextId++, role, room, homeCell);
                    ConfigureSchedule(agent);
                    _agents.Add(agent);
                    existing++;
                }

                if (role == AgentRole.CondoResident && !room.CondoSold)
                {
                    foreach (var agent in _agents)
                    {
                        if (!ReferenceEquals(agent.HomeRoom, room) ||
                            agent.Phase != AgentPhase.Moving ||
                            agent.Path == null ||
                            agent.Path.Count > 0)
                            continue;

                        ReplanTrip(agent, allowReplan: true);
                    }
                }
            }
        }

        bool PassesCondoDemand(RoomInstance room, int currentStars, int climateOffset = 0)
        {
            var chance = PricePricing.DemandChance(room.PriceTier, currentStars, climateOffset);
            if (chance >= 1f) return true;
            if (chance <= 0f) return false;
            return _rng.NextDouble() < chance;
        }

        /// <param name="deltaGameMinutes">
        /// Game minutes advanced this frame (typically <see cref="GameClock.LastTickGameMinutes"/>).
        /// Walking and stress scale with this so agents keep up when time speed changes.
        /// </param>
        public void Tick(
            float deltaGameMinutes,
            GameClock clock,
            TowerGrid grid,
            int currentStars = 0,
            MarketClimate climate = null,
            CrimeSystem crime = null)
        {
            if (grid == null || clock == null) return;
            if (climate != null)
                _climate = climate;
            _crime = crime;

            var total = clock.DayIndex * GameClock.MinutesPerDay + clock.MinuteOfDay;
            _nowTotalMinutes = total;
            var advanced = 0;
            if (_lastTotalMinutes != int.MinValue)
                advanced = Mathf.Max(0, total - _lastTotalMinutes);
            _lastTotalMinutes = total;

            for (var i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
                EnsureDisposable(agent, clock.DayIndex);
                ApplyLowConditionStress(agent, clock.DayIndex);
                ApplyCrimeStressDaily(agent, _crime, clock.DayIndex);
                if (agent.Phase == AgentPhase.Working && advanced > 0)
                    agent.WorkedMinutes += advanced;

                UpdateSchedule(agent, clock, grid);
                if (agent.Phase == AgentPhase.WaitingAtElevator)
                {
                    agent.ElevatorWaitMinutes += deltaGameMinutes;
                    // Watchdog: covers maintenance toggles, shortening, and demolition.
                    if (IsElevatorWaitOrphaned(agent))
                        ReplanTrip(agent, allowReplan: true);
                    else
                        TryRescoreElevatorWait(agent, total);
                }

                StepMovement(agent, deltaGameMinutes);
                UpdateVisitingShop(agent, deltaGameMinutes, grid);
                UpdateServiceWork(agent, deltaGameMinutes, grid);
                UpdateCriminalWork(agent, deltaGameMinutes, grid);
                UpdateStress(agent, deltaGameMinutes);
                UpdateCrimeProximityStress(agent, _agents, deltaGameMinutes);
            }

            UpdateStreetTraffic(clock, grid, currentStars, advanced);
            UpdateCriminalTraffic(deltaGameMinutes, grid, _crime);
            // Single capture pass after movement so one Security captures at most one Criminal per Tick.
            TryCaptureCriminals(_crime);
            DespawnFinishedStreetVisitors();
            DespawnFinishedCriminals();
        }

        /// <summary>
        /// Fills <paramref name="into"/> with current floors for agents of <paramref name="role"/>
        /// that are inside the tower (not <see cref="AgentPhase.Outside"/>).
        /// </summary>
        public void CollectFloorsForRole(AgentRole role, List<int> into)
        {
            into?.Clear();
            if (into == null) return;
            foreach (var agent in _agents)
            {
                if (agent == null || agent.Role != role) continue;
                if (agent.Phase == AgentPhase.Outside) continue;
                into.Add(agent.Cell.y);
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
                    UpdateCondo(agent, clock, grid);
                    break;
                case AgentRole.Maid:
                case AgentRole.Handyman:
                    UpdateServiceAgent(agent, grid);
                    break;
                case AgentRole.Security:
                    UpdateSecurityAgent(agent, grid, _crime);
                    break;
                case AgentRole.Criminal:
                    UpdateCriminalAgent(agent, grid, _crime);
                    break;
            }
        }

        /// <summary>
        /// Assigns idle Maid/Handyman agents to open jobs. Returns true if any job was claimed.
        /// </summary>
        public bool TryAssignServiceJobs(TowerGrid grid)
        {
            if (grid == null) return false;
            var assigned = false;
            foreach (var agent in _agents)
            {
                if (agent.Role is not (AgentRole.Maid or AgentRole.Handyman)) continue;
                if (agent.ServiceTarget != null) continue;
                if (agent.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator or AgentPhase.Riding)
                    continue;
                if (TryAssignJobFor(agent, grid))
                    assigned = true;
            }

            return assigned;
        }

        /// <summary>
        /// Instantly finishes the agent's active clean/repair job (test / debug hook).
        /// </summary>
        public bool ForceCompleteServiceWork(Agent agent)
        {
            if (agent == null || agent.ServiceTarget == null || !IsServiceRole(agent.Role))
                return false;

            FinishServiceJob(agent);
            agent.Phase = AgentPhase.AtHome;
            agent.GoalCell = null;
            agent.Path?.Clear();
            agent.TripLegs?.Clear();
            agent.TripLegIndex = 0;
            ClearElevatorTripState(agent);
            return true;
        }

        void SyncServiceStaff(TowerGrid grid)
        {
            var staffHomes = new HashSet<RoomInstance>();
            foreach (var room in grid.Rooms)
            {
                if (room?.Type?.id == null) continue;
                if (room.Type.id is not (HousekeepingId or MaintenanceId or SecurityId)) continue;
                staffHomes.Add(room);
                var role = room.Type.id switch
                {
                    HousekeepingId => AgentRole.Maid,
                    MaintenanceId => AgentRole.Handyman,
                    _ => AgentRole.Security
                };
                var want = room.StaffedWorkers;
                var existing = 0;
                for (var i = 0; i < _agents.Count; i++)
                {
                    if (ReferenceEquals(_agents[i].HomeRoom, room) && _agents[i].Role == role)
                        existing++;
                }

                while (existing > want)
                {
                    RemoveOneStaffAgent(room, role);
                    existing--;
                }

                while (existing < want)
                {
                    var homeCell = HomeCell(room, existing);
                    var agent = new Agent(_nextId++, role, room, homeCell)
                    {
                        Phase = AgentPhase.AtHome,
                        Visible = true
                    };
                    _agents.Add(agent);
                    existing++;
                }
            }

            for (var i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (!IsServiceRole(agent.Role)) continue;
                if (staffHomes.Contains(agent.HomeRoom)) continue;
                ClearServiceClaim(agent);
                _agents.RemoveAt(i);
            }
        }

        void RemoveOneStaffAgent(RoomInstance home, AgentRole role)
        {
            var removeIndex = -1;
            for (var i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (!ReferenceEquals(agent.HomeRoom, home) || agent.Role != role) continue;
                if (agent.ServiceTarget == null)
                {
                    removeIndex = i;
                    break;
                }

                if (removeIndex < 0)
                    removeIndex = i;
            }

            if (removeIndex < 0) return;
            ClearServiceClaim(_agents[removeIndex]);
            _agents.RemoveAt(removeIndex);
        }

        void UpdateServiceAgent(Agent agent, TowerGrid grid)
        {
            if (agent.ServiceTarget != null)
            {
                if (agent.Phase == AgentPhase.Working) return;
                if (agent.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator or AgentPhase.Riding)
                    return;

                // Claim lost its trip — re-path or drop.
                if (!BeginTrip(
                        agent,
                        agent.Cell,
                        HomeCell(agent.ServiceTarget, 0),
                        AgentPhase.Working,
                        grid))
                    ClearServiceClaim(agent);
                return;
            }

            if (agent.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator or AgentPhase.Riding)
                return;

            if (TryAssignJobFor(agent, grid))
                return;

            if (agent.Phase != AgentPhase.AtHome)
            {
                BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, 0), AgentPhase.AtHome, grid);
                return;
            }

            agent.Visible = true;
        }

        void UpdateCriminalAgent(Agent agent, TowerGrid grid, CrimeSystem crime)
        {
            // Life + floor dwell countdown live in UpdateCriminalWork.
            if (agent.Phase == AgentPhase.Working) return;
            if (agent.Phase is AgentPhase.WaitingAtElevator or AgentPhase.Riding)
                return;

            if (agent.Phase == AgentPhase.Moving)
            {
                if (agent.Path == null || agent.Path.Count == 0 || agent.PathIndex >= agent.Path.Count)
                    ReplanTrip(agent, allowReplan: true);
                return;
            }

            if (agent.CriminalDwellRemaining <= 0f)
            {
                BeginLeaveTower(agent, grid);
                return;
            }

            TryStartCriminalRoam(agent, grid, crime);
        }

        void UpdateCriminalWork(Agent agent, float deltaGameMinutes, TowerGrid grid)
        {
            if (agent == null || agent.Role != AgentRole.Criminal) return;
            if (deltaGameMinutes <= 0f) return;

            if (agent.Phase != AgentPhase.Outside)
                agent.CriminalDwellRemaining -= deltaGameMinutes;

            if (agent.CriminalDwellRemaining <= 0f)
            {
                agent.CriminalDwellRemaining = 0f;
                if (agent.Phase is AgentPhase.Outside or AgentPhase.WaitingAtElevator or AgentPhase.Riding
                    or AgentPhase.Moving)
                    return;
                BeginLeaveTower(agent, grid);
                return;
            }

            if (agent.Phase != AgentPhase.Working) return;

            agent.VisitDwellRemaining -= deltaGameMinutes;
            if (agent.VisitDwellRemaining > 0f) return;
            agent.VisitDwellRemaining = 0f;
            if (!TryStartCriminalRoam(agent, grid, _crime))
            {
                // No roam target: end life so leave/despawn frees the concurrent slot.
                agent.CriminalDwellRemaining = 0f;
                BeginLeaveTower(agent, grid);
            }
        }

        void BeginLeaveTower(Agent agent, TowerGrid grid)
        {
            if (agent == null || grid == null) return;
            if (agent.Role == AgentRole.Criminal)
                agent.CriminalDwellRemaining = 0f;
            if (agent.Phase == AgentPhase.Outside) return;
            if (agent.Phase is AgentPhase.WaitingAtElevator or AgentPhase.Riding) return;
            if (agent.Phase == AgentPhase.Moving &&
                agent.PhaseAfterMove == AgentPhase.Outside)
                return;

            var exitCell = LobbyExitCell(grid);
            BeginTrip(agent, agent.Cell, exitCell, AgentPhase.Outside, grid);
        }

        bool TryStartCriminalRoam(Agent agent, TowerGrid grid, CrimeSystem crime)
        {
            if (agent == null || grid == null) return false;

            var floor = PickCriminalRoamFloor(crime, grid);
            if (floor == null) return false;

            var cell = PatrolCellOnFloor(grid, floor.Value);
            if (cell == null) return false;

            agent.VisitDwellRemaining = CriminalFloorDwellMinutes;
            agent.Visible = true;

            if (agent.Phase == AgentPhase.Outside)
            {
                if (BeginTrip(agent, LobbyExitCell(grid), cell.Value, AgentPhase.Working, grid))
                    return true;
                // Path unavailable — still enter on the roam floor (avoids Outside + life soft-lock).
                PlaceCriminalWorkingAt(agent, cell.Value);
                return true;
            }

            if (agent.Cell.y == floor.Value)
            {
                PlaceCriminalWorkingAt(agent, agent.Cell);
                return true;
            }

            if (BeginTrip(agent, agent.Cell, cell.Value, AgentPhase.Working, grid))
                return true;

            agent.VisitDwellRemaining = 0f;
            return false;
        }

        static void PlaceCriminalWorkingAt(Agent agent, Vector2Int cell)
        {
            agent.Cell = cell;
            agent.WorldPosition = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            agent.Phase = AgentPhase.Working;
            agent.PhaseAfterMove = AgentPhase.Working;
            agent.GoalCell = null;
            agent.Path?.Clear();
            agent.PathIndex = 0;
            agent.TripLegs?.Clear();
            agent.TripLegIndex = 0;
            ClearElevatorTripState(agent);
            agent.Visible = true;
        }

        int? PickCriminalRoamFloor(CrimeSystem crime, TowerGrid grid)
        {
            if (grid == null) return null;

            _criminalFloorScratch.Clear();
            foreach (var room in grid.Rooms)
            {
                // Use `is null` (not ==) so net8 hosts can use uninitialized RoomTypeSO fixtures.
                if (room is null || room.Type is null) continue;
                var isTarget =
                    ShopVisitRules.IsShop(room.Type) ||
                    room.Type.category == RoomCategory.Hotel;
                if (!isTarget) continue;
                var minY = room.Origin.y;
                var maxY = room.Origin.y + room.Size.y - 1;
                for (var y = minY; y <= maxY; y++)
                    _criminalFloorScratch.Add(y);
            }

            if (_criminalFloorScratch.Count == 0) return null;

            int? best = null;
            var bestCrime = -1f;
            foreach (var floor in _criminalFloorScratch)
            {
                var c = crime?.GetCrime(floor) ?? 0f;
                if (best == null ||
                    c > bestCrime ||
                    (Mathf.Approximately(c, bestCrime) && floor < best.Value))
                {
                    best = floor;
                    bestCrime = c;
                }
            }

            return best;
        }

        void UpdateSecurityAgent(Agent agent, TowerGrid grid, CrimeSystem crime)
        {
            // Dwell countdown + replan live in UpdateServiceWork (same as maid/handyman Working).
            if (agent.Phase == AgentPhase.Working) return;

            // Do not replan every tick while waiting/riding.
            if (agent.Phase is AgentPhase.WaitingAtElevator or AgentPhase.Riding)
                return;

            if (agent.Phase == AgentPhase.Moving)
            {
                if (agent.Path == null || agent.Path.Count == 0 || agent.PathIndex >= agent.Path.Count)
                    ReplanTrip(agent, allowReplan: true);
                return;
            }

            if (TryStartPatrol(agent, grid, crime))
                return;

            if (agent.Phase != AgentPhase.AtHome)
            {
                BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, 0), AgentPhase.AtHome, grid);
                return;
            }

            agent.Visible = true;
        }

        bool TryStartPatrol(Agent agent, TowerGrid grid, CrimeSystem crime)
        {
            if (agent == null || grid == null) return false;

            var floor = PickPatrolFloor(crime, grid);
            if (floor == null) return false;

            var cell = PatrolCellOnFloor(grid, floor.Value);
            if (cell == null) return false;

            agent.ServiceTarget = null;
            agent.ServiceWorkRemaining = PatrolDwellMinutes;
            agent.Visible = true;

            if (agent.Cell.y == floor.Value)
            {
                agent.Phase = AgentPhase.Working;
                agent.PhaseAfterMove = AgentPhase.Working;
                agent.GoalCell = null;
                agent.Path?.Clear();
                agent.PathIndex = 0;
                agent.TripLegs?.Clear();
                agent.TripLegIndex = 0;
                ClearElevatorTripState(agent);
                return true;
            }

            if (BeginTrip(agent, agent.Cell, cell.Value, AgentPhase.Working, grid))
                return true;

            agent.ServiceWorkRemaining = 0f;
            return false;
        }

        int? PickPatrolFloor(CrimeSystem crime, TowerGrid grid)
        {
            if (crime == null || grid == null) return null;

            var shopLoad = CrimeFloorLoads.ShopLoadByFloor(grid);
            var hotelLoad = CrimeFloorLoads.HotelLoadByFloor(grid, _agents);

            _patrolFloorScratch.Clear();
            foreach (var room in grid.Rooms)
            {
                if (room == null) continue;
                var minY = room.Origin.y;
                var maxY = room.Origin.y + room.Size.y - 1;
                for (var y = minY; y <= maxY; y++)
                    _patrolFloorScratch.Add(y);
            }

            foreach (var floor in shopLoad.Keys)
                _patrolFloorScratch.Add(floor);
            foreach (var floor in hotelLoad.Keys)
                _patrolFloorScratch.Add(floor);

            int? bestBusy = null;
            var bestBusyCrime = 0f;
            int? bestAny = null;
            var bestAnyCrime = 0f;

            foreach (var floor in _patrolFloorScratch)
            {
                var c = crime.GetCrime(floor);
                if (c <= 0f) continue;

                var busy =
                    (shopLoad.TryGetValue(floor, out var shop) && shop > 0f) ||
                    (hotelLoad.TryGetValue(floor, out var hotel) && hotel > 0f);

                if (busy &&
                    (bestBusy == null ||
                     c > bestBusyCrime ||
                     (Mathf.Approximately(c, bestBusyCrime) && floor < bestBusy.Value)))
                {
                    bestBusy = floor;
                    bestBusyCrime = c;
                }

                if (bestAny == null ||
                    c > bestAnyCrime ||
                    (Mathf.Approximately(c, bestAnyCrime) && floor < bestAny.Value))
                {
                    bestAny = floor;
                    bestAnyCrime = c;
                }
            }

            return bestBusy ?? bestAny;
        }

        static Vector2Int? PatrolCellOnFloor(TowerGrid grid, int floor)
        {
            if (grid == null) return null;

            if (grid.HasLobby && floor == TowerGrid.LobbyFloor)
                return LobbyExitCell(grid);

            RoomInstance best = null;
            foreach (var room in grid.Rooms)
            {
                if (room is null || room.Type is null) continue;
                if (floor < room.Origin.y || floor >= room.Origin.y + room.Size.y) continue;
                if (best == null || room.InstanceId < best.InstanceId)
                    best = room;
            }

            if (best == null) return null;
            return new Vector2Int(best.Origin.x, floor);
        }

        void UpdateServiceWork(Agent agent, float deltaGameMinutes, TowerGrid grid)
        {
            if (!IsServiceRole(agent.Role)) return;
            if (agent.Phase != AgentPhase.Working) return;

            if (agent.Role == AgentRole.Security)
            {
                agent.ServiceWorkRemaining -= deltaGameMinutes;
                if (agent.ServiceWorkRemaining > 0f) return;
                agent.ServiceWorkRemaining = 0f;
                if (!TryStartPatrol(agent, grid, _crime))
                    BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, 0), AgentPhase.AtHome, grid);
                return;
            }

            if (agent.ServiceTarget == null) return;

            // Midnight can decay 1→0 while a handyman job is in progress; abort before repairing.
            if (agent.Role == AgentRole.Handyman && agent.ServiceTarget.IsBroken)
            {
                ClearServiceClaim(agent);
                if (!TryAssignJobFor(agent, grid))
                    BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, 0), AgentPhase.AtHome, grid);
                return;
            }

            agent.ServiceWorkRemaining -= deltaGameMinutes;
            if (agent.ServiceWorkRemaining > 0f) return;

            FinishServiceJob(agent);
            if (!TryAssignJobFor(agent, grid))
                BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, 0), AgentPhase.AtHome, grid);
        }

        bool TryAssignJobFor(Agent agent, TowerGrid grid)
        {
            if (agent == null || grid == null || agent.ServiceTarget != null) return false;

            RoomInstance target = null;
            float workMinutes = 0f;
            if (agent.Role == AgentRole.Maid)
            {
                target = FindOldestDirtyHotel(grid);
                if (target != null)
                    workMinutes = RoomConditionRules.CleanMinutes(target.Type);
            }
            else if (agent.Role == AgentRole.Handyman)
            {
                target = FindLowestConditionRepairTarget(grid);
                if (target != null)
                    workMinutes = RoomConditionRules.RepairMinutesPerChunk;
            }

            if (target == null) return false;

            agent.ServiceTarget = target;
            agent.ServiceWorkRemaining = workMinutes;
            agent.Visible = true;
            if (BeginTrip(agent, agent.Cell, HomeCell(target, 0), AgentPhase.Working, grid))
                return true;

            ClearServiceClaim(agent);
            return false;
        }

        RoomInstance FindOldestDirtyHotel(TowerGrid grid)
        {
            RoomInstance best = null;
            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null || room.Type.category != RoomCategory.Hotel) continue;
                if (!room.Dirty || room.IsBroken) continue;
                if (IsServiceTargetClaimed(room)) continue;
                if (best == null || room.InstanceId < best.InstanceId)
                    best = room;
            }

            return best;
        }

        RoomInstance FindLowestConditionRepairTarget(TowerGrid grid)
        {
            RoomInstance best = null;
            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null || !RoomConditionRules.CanDegrade(room.Type)) continue;
                if (room.IsBroken || room.Condition < 1 || room.Condition > 99) continue;
                if (IsServiceTargetClaimed(room)) continue;
                if (best == null ||
                    room.Condition < best.Condition ||
                    (room.Condition == best.Condition && room.InstanceId < best.InstanceId))
                    best = room;
            }

            return best;
        }

        bool IsServiceTargetClaimed(RoomInstance room)
        {
            foreach (var agent in _agents)
            {
                if (ReferenceEquals(agent.ServiceTarget, room))
                    return true;
            }

            return false;
        }

        static void FinishServiceJob(Agent agent)
        {
            var target = agent.ServiceTarget;
            if (agent.Role == AgentRole.Maid)
                target?.ClearDirty();
            else if (agent.Role == AgentRole.Handyman)
            {
                // Do not revive Broken rooms if Condition hit 0 mid-job (e.g. midnight decay).
                if (target != null && !target.IsBroken)
                    RoomConditionRules.ApplyRepairTick(target);
            }

            ClearServiceClaim(agent);
        }

        static void ClearServiceClaim(Agent agent)
        {
            if (agent == null) return;
            agent.ServiceTarget = null;
            agent.ServiceWorkRemaining = 0f;
        }

        static bool IsServiceRole(AgentRole role) =>
            role is AgentRole.Maid or AgentRole.Handyman or AgentRole.Security;

        static bool IsNonPopulationRole(AgentRole role) =>
            role is AgentRole.StreetVisitor or AgentRole.Maid or AgentRole.Handyman or AgentRole.Security
                or AgentRole.Criminal;

        static bool IsEphemeralOrStaffRole(AgentRole role) =>
            role is AgentRole.StreetVisitor or AgentRole.Maid or AgentRole.Handyman or AgentRole.Security
                or AgentRole.Criminal;

        void UpdateCondo(Agent agent, GameClock clock, TowerGrid grid)
        {
            var minute = clock.MinuteOfDay;
            if (agent.HasMovedIn &&
                agent.Phase == AgentPhase.AtHome &&
                minute >= 12 * 60 &&
                minute <= 17 * 60 &&
                agent.CommercialTripDay != clock.DayIndex)
                TryBeginCommercialTrip(agent, grid, clock, AgentPhase.AtHome);

            if (agent.HasMovedIn || agent.Phase == AgentPhase.AtHome)
                return;

            var home = HomeCell(agent.HomeRoom, 0);
            if (agent.Phase == AgentPhase.Outside)
            {
                BeginTrip(agent, LobbyExitCell(grid), home, AgentPhase.AtHome, grid);
                return;
            }

            if (agent.Phase == AgentPhase.Moving &&
                (agent.Path == null || agent.Path.Count == 0) &&
                agent.GoalCell.HasValue)
                ReplanTrip(agent, allowReplan: true);
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

            if (agent.Phase == AgentPhase.Working &&
                minute >= 11 * 60 + 30 &&
                minute <= 13 * 60 + 30 &&
                agent.CommercialTripDay != clock.DayIndex)
                TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working);

            // Reset daily commute flag at midnight window.
            if (minute < 5 * 60)
                agent.CheckedOutToday = false;
        }

        /// <summary>
        /// Starts a once-per-day commercial visit when an open reachable shop has capacity.
        /// Reused by office lunch (Task 2) and hotel/condo windows (Task 3).
        /// </summary>
        public bool TryBeginCommercialTrip(
            Agent agent,
            TowerGrid grid,
            GameClock clock,
            AgentPhase afterVisit)
        {
            if (agent == null || grid == null || clock == null) return false;
            if (agent.CommercialTripDay == clock.DayIndex) return false;

            EnsureDisposable(agent, clock.DayIndex);
            var shops = FindOpenShops(grid, clock.MinuteOfDay, agent.DisposableRemaining);
            if (shops.Count == 0) return false;

            var shop = shops[_rng.Next(shops.Count)];
            if (!shop.TryOccupyVisitorSlot()) return false;

            agent.CommercialTripDay = clock.DayIndex;
            agent.VisitTarget = shop;
            agent.PhaseAfterVisit = afterVisit;
            agent.ReturnCell = agent.Cell;
            agent.VisitDwellRemaining = ShopVisitRules.PickDwellMinutes(shop.Type, _rng);
            if (BeginTrip(agent, agent.Cell, ShopEntryCell(shop), AgentPhase.VisitingShop, grid))
                return true;

            CancelCommercialVisit(agent);
            return false;
        }

        static void CancelCommercialVisit(Agent agent)
        {
            if (agent == null) return;

            agent.VisitTarget?.ReleaseVisitorSlot();
            agent.VisitTarget = null;
            agent.CommercialTripDay = -1;
            agent.VisitDwellRemaining = 0f;
            agent.ReturnCell = null;
        }

        void EnsureDisposable(Agent agent, int dayIndex)
        {
            if (agent == null || agent.DisposableDayIndex == dayIndex) return;

            var homeType = agent.Role == AgentRole.StreetVisitor ? null : agent.HomeRoom?.Type;
            var band = AgentWealth.ResolveBand(agent.Role, homeType);
            var mult = _climate?.SpendMultiplier ?? 1f;
            agent.DisposableRemaining = AgentWealth.RollDailyDisposable(band, mult, _rng);
            agent.DisposableDayIndex = dayIndex;
        }

        List<RoomInstance> FindOpenShops(TowerGrid grid, int minuteOfDay, int? disposableRemaining = null)
        {
            var open = new List<RoomInstance>();
            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null) continue;
                if (!ShopVisitRules.IsShop(room.Type)) continue;
                if (!ShopVisitRules.IsOpen(room.Type, minuteOfDay)) continue;
                if (room.ConcurrentVisitors >= ShopVisitRules.SlotCount(room.Type)) continue;
                if (disposableRemaining.HasValue &&
                    !AgentWealth.CanAfford(disposableRemaining.Value, room.Type))
                    continue;
                if (!CanReachShopFromLobby(grid, room)) continue;
                open.Add(room);
            }

            return open;
        }

        bool CanReachShopFromLobby(TowerGrid grid, RoomInstance shop) =>
            _router.TryPlanTrip(LobbyExitCell(grid), ShopEntryCell(shop), out var legs) &&
            legs.Count > 0;

        static Vector2Int ShopEntryCell(RoomInstance shop) =>
            shop == null ? Vector2Int.zero : shop.Origin;

        void UpdateVisitingShop(Agent agent, float deltaGameMinutes, TowerGrid grid)
        {
            if (agent.Phase != AgentPhase.VisitingShop) return;

            agent.VisitDwellRemaining -= deltaGameMinutes;
            if (agent.VisitDwellRemaining > 0f) return;

            var shop = agent.VisitTarget;
            if (shop != null)
            {
                var spent = AgentWealth.RollSpend(agent.DisposableRemaining, shop.Type, _rng);
                agent.DisposableRemaining -= spent;
                shop.RecordShopSpend(spent);
                shop.RecordVisit();
                shop.ReleaseVisitorSlot();
            }

            agent.VisitTarget = null;
            agent.VisitDwellRemaining = 0f;
            var returnCell = agent.ReturnCell ?? agent.Cell;
            var after = agent.PhaseAfterVisit;
            agent.ReturnCell = null;
            BeginTrip(agent, agent.Cell, returnCell, after, grid);
        }

        void UpdateStreetTraffic(GameClock clock, TowerGrid grid, int stars, int advancedMinutes)
        {
            if (advancedMinutes <= 0) return;

            _streetSpawnMinuteAccumulator += advancedMinutes;
            while (_streetSpawnMinuteAccumulator >= StreetSpawnIntervalMinutes)
            {
                _streetSpawnMinuteAccumulator -= StreetSpawnIntervalMinutes;
                if (CountStreetVisitors() >= MaxConcurrentStreetVisitors) continue;
                if (FindOpenShops(grid, clock.MinuteOfDay).Count == 0) continue;

                var chance = Mathf.Clamp01(StreetSpawnBaseChance * (1 + Mathf.Max(0, stars)));
                if (_rng.NextDouble() >= chance) continue;

                TrySpawnStreetVisitor(grid, clock);
            }
        }

        /// <summary>
        /// Spawns an ephemeral street visitor Outside that visits an open shop then leaves.
        /// HomeRoom is the chosen shop (soft home) so SyncHomes can skip living-room removal.
        /// </summary>
        public bool TrySpawnStreetVisitor(TowerGrid grid, GameClock clock)
        {
            if (grid == null || clock == null) return false;
            if (CountStreetVisitors() >= MaxConcurrentStreetVisitors) return false;

            var remaining = AgentWealth.RollDailyDisposable(
                WealthBand.Street,
                _climate?.SpendMultiplier ?? 1f,
                _rng);
            var shops = FindOpenShops(grid, clock.MinuteOfDay, remaining);
            if (shops.Count == 0) return false;

            var shop = shops[_rng.Next(shops.Count)];
            if (!shop.TryOccupyVisitorSlot()) return false;

            var exitCell = LobbyExitCell(grid);
            var agent = new Agent(_nextId++, AgentRole.StreetVisitor, shop, exitCell)
            {
                VisitTarget = shop,
                PhaseAfterVisit = AgentPhase.Outside,
                ReturnCell = exitCell,
                VisitDwellRemaining = ShopVisitRules.PickDwellMinutes(shop.Type, _rng),
                DisposableRemaining = remaining,
                DisposableDayIndex = clock.DayIndex
            };
            _agents.Add(agent);
            if (BeginTrip(agent, exitCell, ShopEntryCell(shop), AgentPhase.VisitingShop, grid))
                return true;

            CancelCommercialVisit(agent);
            _agents.RemoveAt(_agents.Count - 1);
            return false;
        }

        int CountStreetVisitors()
        {
            var count = 0;
            foreach (var agent in _agents)
            {
                if (agent.Role == AgentRole.StreetVisitor)
                    count++;
            }

            return count;
        }

        void DespawnFinishedStreetVisitors()
        {
            for (var i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (agent.Role != AgentRole.StreetVisitor) continue;
                if (agent.Phase != AgentPhase.Outside) continue;
                if (agent.VisitTarget != null) continue;
                _agents.RemoveAt(i);
            }
        }

        void UpdateCriminalTraffic(float deltaGameMinutes, TowerGrid grid, CrimeSystem crime)
        {
            if (deltaGameMinutes <= 0f || grid == null || crime == null) return;
            if (!grid.HasLobby) return;
            if (CountCriminals() >= MaxConcurrentCriminals) return;
            if (crime.AverageCrime < CriminalSpawnMinAvg) return;

            var chance = CriminalSpawnChancePerMinute * deltaGameMinutes * (crime.AverageCrime / 100f);
            if (_rng.NextDouble() >= chance) return;

            TrySpawnCriminal(grid, crime);
        }

        /// <summary>Runs same-floor Security→Criminal capture (EditMode / debug).</summary>
        public int CaptureCriminalsNow(CrimeSystem crime)
        {
            if (crime == null) return 0;
            var before = _agents.Count;
            TryCaptureCriminals(crime);
            return before - _agents.Count;
        }

        /// <summary>
        /// Spawns an ephemeral Criminal at the lobby exit that roams high-crime shop/hotel floors.
        /// </summary>
        public bool TrySpawnCriminal(TowerGrid grid, CrimeSystem crime)
        {
            if (CountCriminals() >= MaxConcurrentCriminals) return false;
            if (crime == null || crime.AverageCrime < CriminalSpawnMinAvg) return false;
            if (grid == null || !grid.HasLobby) return false;

            var exitCell = LobbyExitCell(grid);
            var agent = new Agent(_nextId++, AgentRole.Criminal, null, exitCell)
            {
                CriminalDwellRemaining = CriminalLifeMinutes,
                Phase = AgentPhase.Outside,
                Visible = false
            };
            _agents.Add(agent);

            if (TryStartCriminalRoam(agent, grid, crime))
                return true;

            // No roam target — do not consume a concurrent slot (Outside + life > 0 soft-lock).
            agent.CriminalDwellRemaining = 0f;
            agent.Phase = AgentPhase.Outside;
            agent.Visible = false;
            _agents.Remove(agent);
            return false;
        }

        int CountCriminals()
        {
            var count = 0;
            foreach (var agent in _agents)
            {
                if (agent.Role == AgentRole.Criminal)
                    count++;
            }

            return count;
        }

        void TryCaptureCriminals(CrimeSystem crime)
        {
            if (crime == null) return;
            var captures = CrimeCapture.TryCapture(_agents, crime, out var message);
            if (captures > 0 && !string.IsNullOrEmpty(message))
                LastCaptureMessage = message;
        }

        void DespawnFinishedCriminals()
        {
            for (var i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (agent.Role != AgentRole.Criminal) continue;
                if (agent.Phase != AgentPhase.Outside) continue;
                // Outside criminals never linger: life should already be 0 after leave/spawn-fail.
                // Clear any leftover life so a stuck Outside agent cannot hold a concurrent slot.
                agent.CriminalDwellRemaining = 0f;
                _agents.RemoveAt(i);
            }
        }

        void UpdateHotel(Agent agent, GameClock clock, TowerGrid grid)
        {
            var minute = clock.MinuteOfDay;
            var exitCell = LobbyExitCell(grid);
            var home = HomeCell(agent.HomeRoom, 0);

            if (agent.Phase == AgentPhase.Outside &&
                minute >= 16 * 60 &&
                agent.CheckInDay != clock.DayIndex &&
                agent.HomeRoom != null &&
                !agent.HomeRoom.Dirty &&
                !agent.HomeRoom.IsBroken)
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
                agent.HomeRoom?.MarkDirty();
            }

            if (agent.Phase == AgentPhase.Staying &&
                minute >= 18 * 60 &&
                minute <= 21 * 60 &&
                agent.CommercialTripDay != clock.DayIndex)
                TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Staying);
        }

        bool BeginTrip(
            Agent agent,
            Vector2Int spawnIfOutside,
            Vector2Int to,
            AgentPhase after,
            TowerGrid grid)
        {
            if (agent.GoalCell == to &&
                agent.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator or AgentPhase.Riding)
                return true;

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
                return true;
            }

            StallInPlace(agent);
            return false;
        }

        void StepMovement(Agent agent, float deltaGameMinutes)
        {
            if (agent.Phase == AgentPhase.WaitingAtElevator)
            {
                var shaft = CurrentElevatorShaft(agent);
                if (shaft == null) return;

                if (shaft.Car.PassengerIds.Contains(agent.Id))
                {
                    agent.Phase = AgentPhase.Riding;
                    FollowElevatorCar(agent, shaft);
                    return;
                }

                // Re-place each tick so the line closes up as people board.
                PlaceInQueueLane(agent, shaft);
                return;
            }

            if (agent.Phase == AgentPhase.Riding)
            {
                var shaft = CurrentElevatorShaft(agent);
                if (shaft == null)
                {
                    // Shaft vanished under the rider (demolished): drop off and re-plan.
                    _elevators.RemoveFromQueues(agent.Id);
                    ReplanTrip(agent, allowReplan: true);
                    return;
                }

                FollowElevatorCar(agent, shaft);
                if (shaft.Car.Floor != agent.ElevatorDestFloor ||
                    shaft.Car.State != ElevatorCarState.DoorsOpen ||
                    shaft.Car.PassengerIds.Contains(agent.Id))
                    return;

                agent.Cell = new Vector2Int(shaft.X, agent.ElevatorDestFloor);
                agent.WorldPosition = new Vector2(agent.Cell.x + 0.5f, agent.Cell.y + 0.5f);
                AdvanceLeg(agent);
                if (agent.Phase == AgentPhase.Moving)
                    StepMovement(agent, deltaGameMinutes);
                return;
            }

            if (agent.Phase != AgentPhase.Moving) return;

            var remaining = deltaGameMinutes;
            while (remaining > 0.0001f &&
                   agent.Phase == AgentPhase.Moving &&
                   agent.Path != null &&
                   agent.Path.Count > 0 &&
                   agent.PathIndex < agent.Path.Count)
            {
                var target = agent.Path[agent.PathIndex];
                var targetPos = new Vector2(target.x + 0.5f, target.y + 0.5f);
                var distance = Vector2.Distance(agent.WorldPosition, targetPos);
                var maxStep = MoveCellsPerSecond * remaining;

                if (distance <= maxStep)
                {
                    agent.WorldPosition = targetPos;
                    agent.Cell = target;
                    agent.PathIndex++;
                    remaining -= MoveCellsPerSecond > 0f ? distance / MoveCellsPerSecond : remaining;
                    if (agent.PathIndex < agent.Path.Count) continue;

                    agent.Path.Clear();
                    AdvanceLeg(agent);
                    continue;
                }

                agent.WorldPosition = Vector2.MoveTowards(
                    agent.WorldPosition,
                    targetPos,
                    maxStep);
                break;
            }
        }

        void ApplyLowConditionStress(Agent agent, int dayIndex)
        {
            if (agent == null || IsNonPopulationRole(agent.Role)) return;
            if (agent.HomeRoom == null) return;
            if (agent.HomeRoom.Condition >= RoomConditionRules.StressBelow) return;
            if (agent.LowConditionStressDay == dayIndex) return;

            agent.Stress = Mathf.Min(100f, agent.Stress + LowConditionStressPerDay);
            agent.LowConditionStressDay = dayIndex;
        }

        public static void ApplyCrimeStressDaily(Agent agent, CrimeSystem crime, int dayIndex)
        {
            if (agent == null || crime == null) return;
            if (IsCrimeStressExempt(agent.Role)) return;
            if (agent.CrimeStressDay == dayIndex) return;
            var c = crime.GetCrime(agent.Cell.y);
            if (c <= 0f) return;
            agent.Stress = Mathf.Min(100f, agent.Stress + CrimeStressPerDayAtMax * (c / 100f));
            agent.CrimeStressDay = dayIndex;
        }

        public static void UpdateCrimeProximityStress(
            Agent agent,
            IReadOnlyList<Agent> agents,
            float deltaGameMinutes)
        {
            if (agent == null || agents == null || deltaGameMinutes <= 0f) return;
            if (IsCrimeStressExempt(agent.Role)) return;
            if (agent.Phase == AgentPhase.Outside) return;

            var floor = agent.Cell.y;
            var criminalNearby = false;
            for (var i = 0; i < agents.Count; i++)
            {
                var other = agents[i];
                if (other == null || other == agent) continue;
                if (other.Role != AgentRole.Criminal) continue;
                if (other.Phase == AgentPhase.Outside) continue;
                if (other.Cell.y != floor) continue;
                criminalNearby = true;
                break;
            }

            if (!criminalNearby) return;
            agent.Stress = Mathf.Min(
                100f,
                agent.Stress + CriminalProximityStressPerMinute * deltaGameMinutes);
        }

        static bool IsCrimeStressExempt(AgentRole role) =>
            role is AgentRole.Security or AgentRole.Maid or AgentRole.Handyman or AgentRole.Criminal;

        void UpdateStress(Agent agent, float deltaGameMinutes)
        {
            var stuck = (agent.Phase == AgentPhase.Moving &&
                         (agent.Path == null || agent.Path.Count == 0) &&
                         agent.GoalCell.HasValue) ||
                        (agent.Phase == AgentPhase.WaitingAtElevator &&
                         agent.ElevatorWaitMinutes > 10f);
            if (stuck)
                agent.Stress = Mathf.Min(100f, agent.Stress + StressGainPerSecond * deltaGameMinutes);
            else
                agent.Stress = Mathf.Max(0f, agent.Stress - StressDecayPerSecond * deltaGameMinutes);
        }

        void StartLeg(Agent agent, TransitLeg leg, bool allowReplan = true)
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
            agent.ElevatorEntryFloor = leg.EntryFloor;
            agent.ElevatorQueueSide = QueueSideFor(agent, leg);
            agent.ElevatorWaitMinutes = 0f;
            var direction = leg.ExitFloor > leg.EntryFloor
                ? ElevatorDirection.Up
                : ElevatorDirection.Down;

            _elevators.SetPassengerDestination(agent.Id, leg.ExitFloor);
            if (!_elevators.TryEnqueue(agent.Id, leg.ElevatorX, leg.EntryFloor, direction))
            {
                // Shaft refused the call (for example it just entered maintenance).
                _elevators.ClearPassengerDestination(agent.Id);
                ClearElevatorTripState(agent);
                if (allowReplan)
                    ReplanTrip(agent, allowReplan: false);
                else
                    StallInPlace(agent);
                return;
            }

            agent.Phase = AgentPhase.WaitingAtElevator;
            agent.NextElevatorRescoreTotalMinutes =
                _nowTotalMinutes + ElevatorRouting.RescoreIntervalGameMinutes;
            var shaft = _elevators.FindShaftAt(leg.ElevatorX, leg.EntryFloor, leg.ExitFloor);
            if (shaft == null) return;

            agent.ElevatorShaftId = shaft.RoomInstanceId;
            PlaceInQueueLane(agent, shaft);
        }

        /// <summary>
        /// Waiters stand beside the shaft on the side they walked in from, so a long
        /// line is visible instead of agents stacking inside the shaft cell.
        /// </summary>
        void PlaceInQueueLane(Agent agent, ElevatorShaftRuntime shaft)
        {
            var direction = agent.ElevatorDestFloor > agent.ElevatorEntryFloor
                ? ElevatorDirection.Up
                : ElevatorDirection.Down;
            var index = _elevators.GetQueueIndex(
                shaft,
                agent.ElevatorEntryFloor,
                direction,
                agent.Id);
            var slot = Mathf.Max(0, index);
            var side = agent.ElevatorQueueSide >= 0 ? 1f : -1f;
            var x = shaft.X + 0.5f + side * (QueueLaneOffset + slot * QueueSpacing);
            agent.WorldPosition = new Vector2(x, agent.ElevatorEntryFloor + 0.5f);
        }

        static int QueueSideFor(Agent agent, TransitLeg leg)
        {
            if (agent.TripLegs != null && agent.TripLegIndex > 0)
            {
                var previous = agent.TripLegs[agent.TripLegIndex - 1];
                if (previous.Cells != null)
                {
                    for (var i = previous.Cells.Count - 1; i >= 0; i--)
                    {
                        var dx = previous.Cells[i].x - leg.ElevatorX;
                        if (dx != 0) return dx > 0 ? 1 : -1;
                    }
                }
            }

            return agent.Cell.x >= leg.ElevatorX ? 1 : -1;
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
            ClearElevatorTripState(agent);
            if (agent.Role == AgentRole.CondoResident && agent.Phase == AgentPhase.AtHome)
            {
                agent.HasMovedIn = true;
                if (_condoMoveInsNotified.Add(agent.HomeRoom.InstanceId))
                    _onCondoResidentMovedIn?.Invoke(agent.HomeRoom);
            }
            if (agent.Phase == AgentPhase.Outside)
            {
                agent.Visible = false;
                if (agent.Role == AgentRole.Criminal)
                    agent.CriminalDwellRemaining = 0f;
            }
        }

        /// <summary>
        /// Resolves the committed shaft by id, not by route search, so a shaft entering
        /// maintenance never orphans agents already waiting in or riding it.
        /// </summary>
        ElevatorShaftRuntime CurrentElevatorShaft(Agent agent)
        {
            if (agent.ElevatorShaftId != 0)
                return _elevators.FindByRoomId(agent.ElevatorShaftId);

            if (agent.TripLegs == null ||
                agent.TripLegIndex < 0 ||
                agent.TripLegIndex >= agent.TripLegs.Count)
                return null;

            var leg = agent.TripLegs[agent.TripLegIndex];
            if (leg.Kind != TransitLegKind.Elevator)
                return null;
            return _elevators.FindServing(leg.ElevatorX, leg.EntryFloor, leg.ExitFloor);
        }

        /// <summary>
        /// True when a waiting agent can no longer be served by its shaft: the shaft is
        /// gone, no longer spans the trip, or the agent lost its queue slot.
        /// </summary>
        bool IsElevatorWaitOrphaned(Agent agent)
        {
            var shaft = CurrentElevatorShaft(agent);
            if (shaft == null) return true;
            if (shaft.Car.PassengerIds.Contains(agent.Id)) return false;
            if (!shaft.Serves(agent.ElevatorEntryFloor) ||
                !shaft.Serves(agent.ElevatorDestFloor))
                return true;

            var direction = agent.ElevatorDestFloor > agent.ElevatorEntryFloor
                ? ElevatorDirection.Up
                : ElevatorDirection.Down;
            return _elevators.GetQueueIndex(
                shaft,
                agent.ElevatorEntryFloor,
                direction,
                agent.Id) < 0;
        }

        /// <summary>
        /// Cleanup hook for entering or leaving maintenance. Riders and correctly queued
        /// waiters are left to finish; anyone the shaft can no longer serve is re-routed.
        /// </summary>
        public void OnElevatorServiceChanged(int shaftRoomInstanceId)
        {
            foreach (var agent in _agents)
            {
                if (agent.ElevatorShaftId != shaftRoomInstanceId) continue;
                if (agent.Phase is not (AgentPhase.WaitingAtElevator or AgentPhase.Riding))
                    continue;

                var shaft = _elevators.FindByRoomId(shaftRoomInstanceId);
                if (shaft != null && shaft.Car.PassengerIds.Contains(agent.Id))
                    continue;
                if (agent.Phase == AgentPhase.WaitingAtElevator && !IsElevatorWaitOrphaned(agent))
                    continue;

                _elevators.RemoveFromQueues(agent.Id);
                ReplanTrip(agent, allowReplan: true);
            }
        }

        /// <summary>
        /// Re-score the current elevator wait against other serving shafts. Switches when an
        /// alternate is meaningfully better and the switch cooldown has elapsed.
        /// </summary>
        public bool TryRescoreElevatorWait(Agent agent, float totalMinutes)
        {
            if (agent == null || agent.Phase != AgentPhase.WaitingAtElevator)
                return false;
            if (!agent.GoalCell.HasValue)
                return false;
            if (totalMinutes < agent.NextElevatorRescoreTotalMinutes)
                return false;

            agent.NextElevatorRescoreTotalMinutes =
                totalMinutes + ElevatorRouting.RescoreIntervalGameMinutes;
            if (totalMinutes <
                agent.LastElevatorSwitchTotalMinutes + ElevatorRouting.SwitchCooldownGameMinutes)
                return false;

            var current = CurrentElevatorShaft(agent);
            if (current == null)
                return false;

            var entryFloor = agent.ElevatorEntryFloor;
            var destFloor = agent.ElevatorDestFloor;
            if (!current.Serves(entryFloor) || !current.Serves(destFloor))
                return false;

            var direction = destFloor > entryFloor
                ? ElevatorDirection.Up
                : ElevatorDirection.Down;
            if (direction == ElevatorDirection.None)
                return false;

            var goal = agent.GoalCell.Value;
            var start = new Vector2Int(agent.Cell.x, entryFloor);
            if (!TryScoreShaftWait(
                    agent,
                    current,
                    start,
                    goal,
                    entryFloor,
                    direction,
                    isCurrent: true,
                    out var currentScore))
                return false;

            var bestScore = currentScore;
            var foundAlternate = false;
            foreach (var shaft in _elevators.GetServingShafts(entryFloor, destFloor))
            {
                if (shaft.RoomInstanceId == current.RoomInstanceId)
                    continue;
                if (!TryScoreShaftWait(
                        agent,
                        shaft,
                        start,
                        goal,
                        entryFloor,
                        direction,
                        isCurrent: false,
                        out var score))
                    continue;

                foundAlternate = true;
                if (score < bestScore)
                    bestScore = score;
            }

            if (!foundAlternate ||
                !ElevatorRouting.IsMeaningfullyBetter(currentScore, bestScore))
                return false;

            _elevators.RemoveFromQueues(agent.Id);
            agent.LastElevatorSwitchTotalMinutes = totalMinutes;
            _nowTotalMinutes = totalMinutes;
            ReplanTrip(agent, allowReplan: true);
            return true;
        }

        bool TryScoreShaftWait(
            Agent agent,
            ElevatorShaftRuntime shaft,
            Vector2Int start,
            Vector2Int goal,
            int entryFloor,
            ElevatorDirection direction,
            bool isCurrent,
            out float score)
        {
            score = 0f;
            if (shaft == null)
                return false;

            // Same walk model as TransitRouter.TryPlanTrip (§4.3): path cell counts, skip if either walk fails.
            if (!_router.TryShaftWalkPaths(start, goal, shaft, out var toShaft, out var fromShaft))
                return false;

            float wait;
            if (isCurrent)
            {
                var index = _elevators.GetQueueIndex(shaft, entryFloor, direction, agent.Id);
                if (index < 0)
                    return false;
                var sameWay = _elevators.SameWayPassengerCount(shaft, direction);
                var busy = ElevatorRouting.NeedsBusyPenalty(shaft, entryFloor, direction);
                wait = ElevatorRouting.EstimateWaitMinutes(index, sameWay, busy);
            }
            else
            {
                wait = _elevators.EstimateWaitMinutes(shaft, entryFloor, direction);
            }

            var walkCost = toShaft.Count + fromShaft.Count;
            score = ElevatorRouting.Score(walkCost, wait);
            return true;
        }

        void ReplanTrip(Agent agent, bool allowReplan)
        {
            ClearElevatorTripState(agent);
            if (!agent.GoalCell.HasValue)
            {
                agent.Phase = agent.PhaseAfterMove;
                return;
            }

            if (_router.TryPlanTrip(agent.Cell, agent.GoalCell.Value, out var legs) &&
                legs.Count > 0)
            {
                agent.TripLegs = legs;
                agent.TripLegIndex = 0;
                StartLeg(agent, legs[0], allowReplan);
                return;
            }

            StallInPlace(agent);
        }

        static void ClearElevatorTripState(Agent agent)
        {
            agent.ElevatorShaftId = 0;
            agent.ElevatorWaitMinutes = 0f;
        }

        /// <summary>No route available: hold position and let stress build.</summary>
        static void StallInPlace(Agent agent)
        {
            agent.TripLegs = new List<TransitLeg>();
            agent.TripLegIndex = 0;
            agent.Path = new List<Vector2Int>();
            agent.PathIndex = 0;
            agent.Phase = AgentPhase.Moving;
        }

        static void FollowElevatorCar(Agent agent, ElevatorShaftRuntime shaft)
        {
            agent.Cell = new Vector2Int(shaft.X, shaft.Car.Floor);
            agent.WorldPosition = new Vector2(shaft.X + 0.5f, shaft.Car.Floor + 0.5f);
        }

        bool CanReachCondoFromLobby(TowerGrid grid, RoomInstance room)
        {
            return _router.TryPlanTrip(
                       LobbyExitCell(grid),
                       HomeCell(room, 0),
                       out var legs) &&
                   legs.Count > 0;
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
