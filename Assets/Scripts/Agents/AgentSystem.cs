using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class AgentSystem
    {
        public const float StressGainPerSecond = 12f;
        public const float StressDecayPerSecond = 4f;
        /// <summary>Elevator wait stress begins after this many game minutes in queue.</summary>
        public const float ElevatorWaitStressStartMinutes = 5f;
        /// <summary>At/above this wait, stress gain is at max elevator multiplier.</summary>
        public const float ElevatorWaitStressFullMinutes = 25f;
        public const float ElevatorWaitStressMinMult = 0.4f;
        public const float ElevatorWaitStressMaxMult = 2.0f;
        /// <summary>
        /// While path-stuck (Moving, has goal, no walkable path progress), replan this often.
        /// </summary>
        public const float PathStuckReplanIntervalMinutes = 5f;
        /// <summary>Hotel guests may begin check-in from this minute of day (4:00 PM).</summary>
        public const int HotelCheckInMinute = 16 * 60;
        /// <summary>Latest staggered hotel check-in (7:00 PM).</summary>
        public const int HotelCheckInLatestMinute = 19 * 60;
        /// <summary>Earliest hotel checkout on the morning after check-in (6:00 AM).</summary>
        public const int HotelCheckoutEarliestMinute = 6 * 60;
        /// <summary>Latest / typical hotel checkout deadline (11:00 AM).</summary>
        public const int HotelCheckoutLatestMinute = 11 * 60;
        /// <summary>Maids/handymen drop a claimed job after waiting this long for an elevator.</summary>
        public const float ServiceAbandonWaitMinutes = 45f;
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
        public const int MaxConcurrentEventVisitors = 24;
        public const float EventHotelBookFraction = 0.25f;
        public const float EventHallDwellMinMinutes = 18f;
        public const float EventHallDwellMaxMinutes = 40f;
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
        ResearchSystem _research;
        int _nextId = 1;
        int _lastTotalMinutes = int.MinValue;
        float _nowTotalMinutes;
        int _streetSpawnMinuteAccumulator;
        int _lastEventVisitorSpawnDay = int.MinValue;

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
                    var agent = new Agent(_nextId++, role, room, homeCell)
                    {
                        HomeSlot = existing
                    };
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
            CrimeSystem crime = null,
            ResearchSystem research = null)
        {
            if (grid == null || clock == null) return;
            if (climate != null)
                _climate = climate;
            _crime = crime;
            _research = research;

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
                RecoverIfPathStuck(agent, deltaGameMinutes);
                UpdateVisitingShop(agent, deltaGameMinutes, grid);
                UpdateEventHallDwell(agent, deltaGameMinutes, grid, clock);
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
            DespawnFinishedEventVisitors();
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
                case AgentRole.EventVisitor:
                    UpdateEventVisitor(agent, clock, grid);
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
                        HomeSlot = existing,
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

                // Healthy wait/ride: do not replan every tick (duplicate queue IDs).
                if (agent.Phase is AgentPhase.WaitingAtElevator or AgentPhase.Riding)
                {
                    // Extreme wait: drop claim so another worker can clean/repair.
                    if (agent.ElevatorWaitMinutes > ServiceAbandonWaitMinutes)
                    {
                        ClearServiceClaim(agent);
                        AbandonServiceTripToHome(agent, grid);
                    }
                    return;
                }

                if (agent.Phase == AgentPhase.Moving)
                {
                    var exhausted = agent.Path == null || agent.Path.Count == 0 ||
                                    agent.PathIndex >= agent.Path.Count;
                    if (exhausted)
                    {
                        ReplanTrip(agent, allowReplan: true);
                        // Still stalled with an empty path → release Dirty/repair claim.
                        if (agent.Phase == AgentPhase.Moving &&
                            (agent.Path == null || agent.Path.Count == 0 ||
                             agent.PathIndex >= agent.Path.Count))
                        {
                            ClearServiceClaim(agent);
                            AbandonServiceTripToHome(agent, grid);
                        }
                    }
                    return;
                }

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
                BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, agent.HomeSlot), AgentPhase.AtHome, grid);
                return;
            }

            agent.Visible = true;
        }

        void AbandonServiceTripToHome(Agent agent, TowerGrid grid)
        {
            if (agent?.HomeRoom == null || grid == null) return;
            BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, agent.HomeSlot), AgentPhase.AtHome, grid);
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

            var exitCell = LobbyExitCell(grid, agent.Cell.x);
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
                if (BeginTrip(agent, LobbyExitCell(grid, cell.Value.x), cell.Value, AgentPhase.Working, grid))
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
                BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, agent.HomeSlot), AgentPhase.AtHome, grid);
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
                    BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, agent.HomeSlot), AgentPhase.AtHome, grid);
                return;
            }

            if (agent.ServiceTarget == null) return;

            // Midnight can decay 1→0 while a handyman job is in progress; abort before repairing.
            if (agent.Role == AgentRole.Handyman && agent.ServiceTarget.IsBroken)
            {
                ClearServiceClaim(agent);
                if (!TryAssignJobFor(agent, grid))
                    BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, agent.HomeSlot), AgentPhase.AtHome, grid);
                return;
            }

            agent.ServiceWorkRemaining -= deltaGameMinutes;
            if (agent.ServiceWorkRemaining > 0f) return;

            FinishServiceJob(agent);
            if (!TryAssignJobFor(agent, grid))
                BeginTrip(agent, agent.Cell, HomeCell(agent.HomeRoom, agent.HomeSlot), AgentPhase.AtHome, grid);
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
                    workMinutes = RoomConditionRules.CleanMinutes(
                        target.Type,
                        ResearchEffects.CleanMinutesMultiplier(_research));
            }
            else if (agent.Role == AgentRole.Handyman)
            {
                target = FindLowestConditionRepairTarget(grid);
                if (target != null)
                    workMinutes = RoomConditionRules.RepairMinutes(
                        ResearchEffects.RepairMinutesMultiplier(_research));
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

        void FinishServiceJob(Agent agent)
        {
            var target = agent.ServiceTarget;
            if (agent.Role == AgentRole.Maid)
                target?.ClearDirty();
            else if (agent.Role == AgentRole.Handyman)
            {
                // Do not revive Broken rooms if Condition hit 0 mid-job (e.g. midnight decay).
                if (target != null && !target.IsBroken)
                    RoomConditionRules.ApplyRepairTick(
                        target,
                        ResearchEffects.RepairChunkMultiplier(_research));
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
            role is AgentRole.StreetVisitor or AgentRole.EventVisitor or AgentRole.Maid
                or AgentRole.Handyman or AgentRole.Security or AgentRole.Criminal;

        static bool IsEphemeralOrStaffRole(AgentRole role) =>
            role is AgentRole.StreetVisitor or AgentRole.EventVisitor or AgentRole.Maid
                or AgentRole.Handyman or AgentRole.Security or AgentRole.Criminal;

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

            var home = HomeCell(agent.HomeRoom, agent.HomeSlot);
            if (agent.Phase == AgentPhase.Outside)
            {
                BeginTrip(agent, LobbyExitCell(grid, home.x), home, AgentPhase.AtHome, grid);
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
            var home = HomeCell(agent.HomeRoom, agent.HomeSlot);
            var exitCell = LobbyExitCell(grid, home.x);

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
                BeginTrip(agent, agent.Cell, LobbyExitCell(grid, agent.Cell.x), AgentPhase.Outside, grid);

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

            var homeType = agent.Role is AgentRole.StreetVisitor or AgentRole.EventVisitor
                ? null
                : agent.HomeRoom?.Type;
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
            _router.TryPlanTrip(LobbyExitCell(grid, ShopEntryCell(shop).x), ShopEntryCell(shop), out var legs) &&
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
                var recorded = Mathf.Max(
                    0,
                    Mathf.RoundToInt(spent * ResearchEffects.ShopSpendMultiplier(_research)));
                shop.RecordShopSpend(recorded);
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

            var shopCell = ShopEntryCell(shop);
            var exitCell = LobbyExitCell(grid, shopCell.x);
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
            if (BeginTrip(agent, exitCell, shopCell, AgentPhase.VisitingShop, grid))
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

        /// <summary>
        /// Spawn-per-day while a major event is Live:
        /// <c>min(MaxConcurrent, bookedCapacity / 5)</c>.
        /// </summary>
        public static int ComputeEventVisitorSpawnPerDay(int bookedCapacity)
        {
            if (bookedCapacity <= 0) return 0;
            return Mathf.Min(MaxConcurrentEventVisitors, bookedCapacity / 5);
        }

        /// <summary>
        /// While <see cref="MajorEventPhase.Live"/>, spawn up to today's quota under the concurrent cap.
        /// When the event is not live, force-despawn remaining EventVisitors (hotel guests check out Dirty).
        /// </summary>
        public void SyncEventVisitors(ConferenceSystem conference, TowerGrid grid, GameClock clock)
        {
            if (grid == null || clock == null)
                return;

            if (conference == null || conference.Active == null ||
                conference.Active.Phase != MajorEventPhase.Live)
            {
                DespawnAllEventVisitors(forceHotelCheckout: true);
                _lastEventVisitorSpawnDay = int.MinValue;
                return;
            }

            if (_lastEventVisitorSpawnDay == clock.DayIndex)
                return;

            _lastEventVisitorSpawnDay = clock.DayIndex;
            var spawnTarget = ComputeEventVisitorSpawnPerDay(conference.SumBookedHallCapacity(grid));
            if (spawnTarget <= 0) return;

            var hall = conference.FindBookedHall(grid);
            if (hall == null) return;

            var toSpawn = Mathf.Min(spawnTarget, MaxConcurrentEventVisitors - CountEventVisitors());
            for (var i = 0; i < toSpawn; i++)
            {
                var preferHotel = _rng.NextDouble() < EventHotelBookFraction;
                if (preferHotel && TrySpawnEventHotelVisitor(grid, clock))
                    continue;
                if (!TrySpawnEventDayVisitor(grid, clock, hall))
                    break;
            }
        }

        public bool TrySpawnEventDayVisitor(TowerGrid grid, GameClock clock, RoomInstance hall)
        {
            if (grid == null || clock == null || hall == null) return false;
            if (CountEventVisitors() >= MaxConcurrentEventVisitors) return false;
            if (!grid.HasLobby) return false;

            var hallCell = HallEntryCell(hall);
            var exitCell = LobbyExitCell(grid, hallCell.x);
            var dwell = EventHallDwellMinMinutes +
                        (float)_rng.NextDouble() *
                        (EventHallDwellMaxMinutes - EventHallDwellMinMinutes);
            var agent = new Agent(_nextId++, AgentRole.EventVisitor, hall, exitCell)
            {
                VisitDwellRemaining = dwell,
                PhaseAfterVisit = AgentPhase.Outside,
                ReturnCell = exitCell,
                DisposableDayIndex = -1
            };
            EnsureDisposable(agent, clock.DayIndex);
            _agents.Add(agent);
            if (BeginTrip(agent, exitCell, hallCell, AgentPhase.Working, grid))
                return true;

            _agents.RemoveAt(_agents.Count - 1);
            return false;
        }

        public bool TrySpawnEventHotelVisitor(TowerGrid grid, GameClock clock)
        {
            if (grid == null || clock == null) return false;
            if (CountEventVisitors() >= MaxConcurrentEventVisitors) return false;
            if (!TryClaimHotelBedForEvent(grid, out var hotel, out var slot))
                return false;

            var home = HomeCell(hotel, slot);
            var exitCell = LobbyExitCell(grid, home.x);
            var agent = new Agent(_nextId++, AgentRole.EventVisitor, hotel, exitCell)
            {
                HomeSlot = slot,
                CheckInMinute = RollHotelCheckInMinute(_rng),
                CheckoutMinute = RollHotelCheckoutMinute(_rng),
                CheckInDay = -1,
                CheckedOutToday = false,
                DisposableDayIndex = -1
            };
            EnsureDisposable(agent, clock.DayIndex);
            _agents.Add(agent);
            return true;
        }

        int CountEventVisitors()
        {
            var count = 0;
            foreach (var agent in _agents)
            {
                if (agent.Role == AgentRole.EventVisitor)
                    count++;
            }

            return count;
        }

        void UpdateEventVisitor(Agent agent, GameClock clock, TowerGrid grid)
        {
            if (IsEventHotelVisitor(agent))
            {
                UpdateHotel(agent, clock, grid);
                return;
            }

            // Day crowd: retry hall trip if still Outside with remaining hall dwell.
            if (agent.Phase == AgentPhase.Outside &&
                agent.VisitDwellRemaining > 0f &&
                agent.HomeRoom != null)
            {
                var hallCell = HallEntryCell(agent.HomeRoom);
                var exitCell = LobbyExitCell(grid, hallCell.x);
                BeginTrip(agent, exitCell, hallCell, AgentPhase.Working, grid);
            }
        }

        /// <summary>
        /// Hall dwell uses <see cref="Agent.VisitDwellRemaining"/> while <see cref="AgentPhase.Working"/>.
        /// Called from Tick after schedule so dwell scales with <paramref name="deltaGameMinutes"/>.
        /// </summary>
        void UpdateEventHallDwell(Agent agent, float deltaGameMinutes, TowerGrid grid, GameClock clock)
        {
            if (agent == null || agent.Role != AgentRole.EventVisitor) return;
            if (IsEventHotelVisitor(agent)) return;
            if (agent.Phase != AgentPhase.Working) return;
            if (agent.VisitTarget != null) return;

            agent.VisitDwellRemaining -= deltaGameMinutes;
            if (agent.VisitDwellRemaining > 0f) return;

            agent.VisitDwellRemaining = 0f;
            var exitCell = LobbyExitCell(grid, agent.Cell.x);
            // One optional shop stop, then leave via lobby.
            if (agent.CommercialTripDay != clock.DayIndex &&
                TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Outside))
            {
                agent.ReturnCell = exitCell;
                return;
            }

            BeginTrip(agent, agent.Cell, exitCell, AgentPhase.Outside, grid);
        }

        void DespawnFinishedEventVisitors()
        {
            for (var i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (agent.Role != AgentRole.EventVisitor) continue;
                // Hotel-backed visitors persist for event nights; removed only on event end.
                if (IsEventHotelVisitor(agent)) continue;
                if (agent.Phase != AgentPhase.Outside) continue;
                if (agent.VisitTarget != null) continue;

                CancelCommercialVisit(agent);
                _agents.RemoveAt(i);
            }
        }

        void DespawnAllEventVisitors(bool forceHotelCheckout)
        {
            for (var i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (agent.Role != AgentRole.EventVisitor) continue;

                CancelCommercialVisit(agent);
                if (forceHotelCheckout && IsEventHotelVisitor(agent))
                {
                    // Dirty when the hotel path was used (checked in or still staying).
                    if (agent.CheckInDay >= 0 || agent.Phase == AgentPhase.Staying)
                        agent.HomeRoom?.MarkDirty();
                }

                agent.Phase = AgentPhase.Outside;
                agent.Visible = false;
                agent.Path?.Clear();
                agent.TripLegs?.Clear();
                agent.GoalCell = null;
                _agents.RemoveAt(i);
            }
        }

        bool TryClaimHotelBedForEvent(TowerGrid grid, out RoomInstance hotel, out int slot)
        {
            hotel = null;
            slot = 0;
            if (grid == null) return false;

            // Prefer a truly vacant clean hotel slot.
            foreach (var room in grid.Rooms)
            {
                if (!IsClaimableHotel(room)) continue;
                var max = Mathf.Max(1, room.Type.maxOccupants);
                var occupied = CountHomeOccupants(room);
                if (occupied >= max) continue;
                hotel = room;
                slot = occupied;
                return true;
            }

            // Otherwise displace an Outside HotelGuest (bed reserved but not currently staying).
            for (var i = _agents.Count - 1; i >= 0; i--)
            {
                var guest = _agents[i];
                if (guest.Role != AgentRole.HotelGuest) continue;
                if (guest.Phase != AgentPhase.Outside) continue;
                if (guest.HomeRoom == null || !IsClaimableHotel(guest.HomeRoom)) continue;

                hotel = guest.HomeRoom;
                slot = guest.HomeSlot;
                CancelCommercialVisit(guest);
                _agents.RemoveAt(i);
                return true;
            }

            return false;
        }

        int CountHomeOccupants(RoomInstance room)
        {
            var count = 0;
            foreach (var agent in _agents)
            {
                if (ReferenceEquals(agent.HomeRoom, room))
                    count++;
            }

            return count;
        }

        static bool IsClaimableHotel(RoomInstance room) =>
            room?.Type != null &&
            room.Type.category == RoomCategory.Hotel &&
            !room.Dirty &&
            !room.IsBroken &&
            room.Type.maxOccupants > 0;

        static bool IsEventHotelVisitor(Agent agent) =>
            agent != null &&
            agent.Role == AgentRole.EventVisitor &&
            agent.HomeRoom?.Type != null &&
            agent.HomeRoom.Type.category == RoomCategory.Hotel;

        static Vector2Int HallEntryCell(RoomInstance hall) =>
            hall == null ? Vector2Int.zero : hall.Origin;

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
            var home = HomeCell(agent.HomeRoom, agent.HomeSlot);
            var exitCell = LobbyExitCell(grid, home.x);

            if (agent.Phase == AgentPhase.Outside &&
                agent.CheckInDay != clock.DayIndex &&
                agent.HomeRoom != null &&
                !agent.HomeRoom.Dirty &&
                !agent.HomeRoom.IsBroken &&
                IsHotelCheckInDue(minute, agent.CheckInMinute))
            {
                BeginTrip(agent, exitCell, home, AgentPhase.Staying, grid);
                agent.CheckInDay = clock.DayIndex;
                agent.CheckedOutToday = false;
                agent.CheckoutMinute = RollHotelCheckoutMinute(_rng);
            }

            if (agent.Phase == AgentPhase.Staying &&
                agent.CheckInDay >= 0 &&
                agent.CheckInDay < clock.DayIndex &&
                !agent.CheckedOutToday &&
                IsHotelCheckoutDue(minute, agent.CheckoutMinute))
            {
                BeginTrip(agent, agent.Cell, LobbyExitCell(grid, agent.Cell.x), AgentPhase.Outside, grid);
                agent.CheckedOutToday = true;
                agent.CheckInMinute = RollHotelCheckInMinute(_rng);
                agent.HomeRoom?.MarkDirty();
            }

            if (agent.Phase == AgentPhase.Staying &&
                minute >= 18 * 60 &&
                minute <= 21 * 60 &&
                agent.CommercialTripDay != clock.DayIndex)
                TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Staying);
        }

        /// <summary>
        /// Check-in times fall in 4:00–7:00 PM, biased toward 4:00 PM.
        /// </summary>
        public static int RollHotelCheckInMinute(System.Random rng)
        {
            if (rng == null) rng = new System.Random();
            var u = (float)rng.NextDouble();
            // Quadratic ease toward 0 → most samples near HotelCheckInMinute (4:00 PM).
            var t = u * u;
            var span = HotelCheckInLatestMinute - HotelCheckInMinute;
            return HotelCheckInMinute + Mathf.RoundToInt(t * span);
        }

        public static bool IsHotelCheckInDue(int minuteOfDay, int checkInMinute)
        {
            if (minuteOfDay < HotelCheckInMinute)
                return false;
            var due = checkInMinute;
            if (due < HotelCheckInMinute)
                due = HotelCheckInMinute;
            if (due > HotelCheckInLatestMinute)
                due = HotelCheckInLatestMinute;
            return minuteOfDay >= due || minuteOfDay >= HotelCheckInLatestMinute;
        }

        /// <summary>
        /// Checkout times fall in 6:00–11:00, biased toward 11:00 (real-world late morning).
        /// </summary>
        public static int RollHotelCheckoutMinute(System.Random rng)
        {
            if (rng == null) rng = new System.Random();
            var u = (float)rng.NextDouble();
            // Quadratic ease-in toward 1 → most samples near HotelCheckoutLatestMinute.
            var t = 1f - (1f - u) * (1f - u);
            var span = HotelCheckoutLatestMinute - HotelCheckoutEarliestMinute;
            return HotelCheckoutEarliestMinute + Mathf.RoundToInt(t * span);
        }

        public static bool IsHotelCheckoutDue(int minuteOfDay, int checkoutMinute)
        {
            if (minuteOfDay < HotelCheckoutEarliestMinute)
                return false;
            var due = checkoutMinute;
            if (due < HotelCheckoutEarliestMinute)
                due = HotelCheckoutEarliestMinute;
            if (due > HotelCheckoutLatestMinute)
                due = HotelCheckoutLatestMinute;
            // At/after personal time, or hard deadline at 11:00.
            return minuteOfDay >= due || minuteOfDay >= HotelCheckoutLatestMinute;
        }

        bool BeginTrip(
            Agent agent,
            Vector2Int spawnIfOutside,
            Vector2Int to,
            AgentPhase after,
            TowerGrid grid)
        {
            // Stalled Moving (empty/exhausted path) must not look like a healthy trip —
            // otherwise lobby spawn / schedule never retries after StallInPlace.
            if (agent.GoalCell == to &&
                agent.Phase is AgentPhase.WaitingAtElevator or AgentPhase.Riding)
                return true;
            if (agent.GoalCell == to &&
                agent.Phase == AgentPhase.Moving &&
                !IsMovementStuck(agent))
                return true;

            agent.GoalCell = to;
            agent.PhaseAfterMove = after;
            if (agent.Phase == AgentPhase.Outside)
            {
                agent.Cell = spawnIfOutside;
                agent.WorldPosition = new Vector2(spawnIfOutside.x + 0.5f, spawnIfOutside.y + 0.5f);
                agent.Visible = true;
            }

            if (_router.TryPlanTrip(agent.Cell, to, agent.Stress, out var legs) && legs.Count > 0)
            {
                agent.TripLegs = legs;
                agent.TripLegIndex = 0;
                StartLeg(agent, legs[0]);
                return true;
            }

            StallInPlace(agent);
            return false;
        }

        /// <summary>
        /// Applies over-cap stair stress for a floor crossing on a Stairs leg.
        /// Comfort floors (1–3) add no stress. Floor 4+ adds
        /// <see cref="ElevatorRouting.StairsOverCapStressPerFloor"/> when stress &lt; 100
        /// before the step (clamped to 100). Returns false and sets <paramref name="refused"/>
        /// when stress is already ≥ 100 on an over-cap floor.
        /// </summary>
        public static bool TryApplyStairFloorCrossing(
            Agent agent,
            int floorsCrossedAfterStep,
            out bool refused)
        {
            refused = false;
            if (agent == null)
            {
                refused = true;
                return false;
            }

            if (floorsCrossedAfterStep <= ElevatorRouting.StairsComfortFloorSpan)
                return true;

            if (agent.Stress >= 100f)
            {
                refused = true;
                return false;
            }

            agent.Stress = Mathf.Min(
                100f,
                agent.Stress + ElevatorRouting.StairsOverCapStressPerFloor);
            return true;
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
                    var previousCell = agent.Cell;
                    if (IsCurrentStairsLeg(agent) && target.y != previousCell.y)
                    {
                        agent.StairsFloorsCrossedThisLeg++;
                        if (!TryApplyStairFloorCrossing(
                                agent,
                                agent.StairsFloorsCrossedThisLeg,
                                out _))
                        {
                            agent.StairsFloorsCrossedThisLeg--;
                            agent.Path.Clear();
                            agent.PathIndex = 0;
                            ReplanTrip(agent, allowReplan: true);
                            break;
                        }
                    }

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
            if (IsMovementStuck(agent))
            {
                agent.Stress = Mathf.Min(100f, agent.Stress + StressGainPerSecond * deltaGameMinutes);
                return;
            }

            if (agent.Phase == AgentPhase.WaitingAtElevator &&
                agent.ElevatorWaitMinutes > ElevatorWaitStressStartMinutes)
            {
                var t = Mathf.InverseLerp(
                    ElevatorWaitStressStartMinutes,
                    ElevatorWaitStressFullMinutes,
                    agent.ElevatorWaitMinutes);
                var mult = Mathf.Lerp(ElevatorWaitStressMinMult, ElevatorWaitStressMaxMult, t);
                agent.Stress = Mathf.Min(
                    100f,
                    agent.Stress + StressGainPerSecond * mult * deltaGameMinutes);
                return;
            }

            agent.Stress = Mathf.Max(0f, agent.Stress - StressDecayPerSecond * deltaGameMinutes);
        }

        void StartLeg(Agent agent, TransitLeg leg, bool allowReplan = true)
        {
            if (leg.Kind != TransitLegKind.Elevator)
            {
                if (leg.Kind == TransitLegKind.Stairs)
                    agent.StairsFloorsCrossedThisLeg = 0;
                agent.Path = leg.Cells ?? new List<Vector2Int>();
                agent.PathIndex = 0;
                agent.Phase = AgentPhase.Moving;
                if (agent.Path.Count > 0)
                    agent.PathStuckMinutes = 0f;
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
            score = ElevatorRouting.Score(
                walkCost,
                wait,
                ResearchEffects.ElevatorRoutingWaitWeightScale(_research));
            return true;
        }

        /// <summary>
        /// Moving toward a goal but not making path progress: empty path, or index past end.
        /// Matches service-agent exhausted-path detection; lobby stalls use the empty-path form.
        /// </summary>
        public static bool IsMovementStuck(Agent agent)
        {
            if (agent == null || agent.Phase != AgentPhase.Moving || !agent.GoalCell.HasValue)
                return false;
            return agent.Path == null ||
                   agent.Path.Count == 0 ||
                   agent.PathIndex >= agent.Path.Count;
        }

        void RecoverIfPathStuck(Agent agent, float deltaGameMinutes)
        {
            if (!IsMovementStuck(agent))
            {
                agent.PathStuckMinutes = 0f;
                return;
            }

            agent.PathStuckMinutes += deltaGameMinutes;
            if (agent.PathStuckMinutes < PathStuckReplanIntervalMinutes)
                return;

            agent.PathStuckMinutes = 0f;
            ReplanTrip(agent, allowReplan: true);
        }

        void ReplanTrip(Agent agent, bool allowReplan)
        {
            ClearElevatorTripState(agent);
            if (!agent.GoalCell.HasValue)
            {
                agent.Phase = agent.PhaseAfterMove;
                agent.PathStuckMinutes = 0f;
                return;
            }

            if (_router.TryPlanTrip(agent.Cell, agent.GoalCell.Value, agent.Stress, out var legs) &&
                legs.Count > 0)
            {
                agent.TripLegs = legs;
                agent.TripLegIndex = 0;
                agent.PathStuckMinutes = 0f;
                StartLeg(agent, legs[0], allowReplan);
                return;
            }

            StallInPlace(agent);
        }

        static bool IsCurrentStairsLeg(Agent agent)
        {
            if (agent.TripLegs == null ||
                agent.TripLegIndex < 0 ||
                agent.TripLegIndex >= agent.TripLegs.Count)
                return false;
            return agent.TripLegs[agent.TripLegIndex].Kind == TransitLegKind.Stairs;
        }

        static void ClearElevatorTripState(Agent agent)
        {
            agent.ElevatorShaftId = 0;
            agent.ElevatorWaitMinutes = 0f;
        }

        /// <summary>No route available: hold position and let stress build until stuck replan.</summary>
        static void StallInPlace(Agent agent)
        {
            agent.TripLegs = new List<TransitLeg>();
            agent.TripLegIndex = 0;
            agent.Path = new List<Vector2Int>();
            agent.PathIndex = 0;
            agent.Phase = AgentPhase.Moving;
            // Keep PathStuckMinutes so an immediate prior replan failure still counts toward the next GC.
        }

        static void FollowElevatorCar(Agent agent, ElevatorShaftRuntime shaft)
        {
            agent.Cell = new Vector2Int(shaft.X, shaft.Car.Floor);
            agent.WorldPosition = new Vector2(shaft.X + 0.5f, shaft.Car.Floor + 0.5f);
        }

        bool CanReachCondoFromLobby(TowerGrid grid, RoomInstance room)
        {
            var home = HomeCell(room, 0);
            return _router.TryPlanTrip(
                       LobbyExitCell(grid, home.x),
                       home,
                       out var legs) &&
                   legs.Count > 0;
        }

        void ConfigureSchedule(Agent agent)
        {
            if (agent.Role == AgentRole.HotelGuest)
            {
                agent.CheckInMinute = RollHotelCheckInMinute(_rng);
                agent.CheckoutMinute = RollHotelCheckoutMinute(_rng);
                return;
            }

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
            LobbyExitCell(grid, grid != null && grid.HasLobby ? grid.MinX : 0);

        /// <summary>
        /// Lobby entry/exit on Floor G, clamped to the lobby span, preferring <paramref name="preferX"/>
        /// (typically the destination room's x) so traffic uses both sides of the lobby.
        /// </summary>
        public static Vector2Int LobbyExitCell(TowerGrid grid, int preferX)
        {
            if (grid == null || !grid.HasLobby) return Vector2Int.zero;
            var x = Mathf.Clamp(preferX, grid.MinX, grid.MaxX);
            return new Vector2Int(x, TowerGrid.LobbyFloor);
        }
    }
}
