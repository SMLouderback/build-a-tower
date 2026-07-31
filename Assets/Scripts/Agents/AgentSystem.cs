using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class AgentSystem
    {
        public const float StressGainPerSecond = 12f;
        public const float StressDecayPerSecond = 4f;
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

        readonly List<Agent> _agents = new();
        readonly TransitRouter _router;
        readonly ElevatorSystem _elevators;
        readonly System.Random _rng = new(42);
        readonly HashSet<int> _condoMoveInsNotified = new();
        System.Action<RoomInstance> _onCondoResidentMovedIn;
        int _nextId = 1;
        int _lastTotalMinutes = int.MinValue;
        float _nowTotalMinutes;
        int _streetSpawnMinuteAccumulator;

        public IReadOnlyList<Agent> Agents => _agents;
        public int Population
        {
            get
            {
                var count = 0;
                foreach (var agent in _agents)
                {
                    if (agent.Role == AgentRole.StreetVisitor) continue;
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
                    if (a.Role == AgentRole.StreetVisitor) continue;
                    sum += a.Stress;
                    count++;
                }

                return count == 0 ? 0f : sum / count;
            }
        }

        public AgentSystem(TransitRouter router)
        {
            _router = router;
            _elevators = router.Elevators;
        }

        public void SyncHomes(
            TowerGrid grid,
            System.Action<RoomInstance> onNewCondoResident = null,
            int currentStars = 0)
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
                if (_agents[i].Role == AgentRole.StreetVisitor) continue;
                if (!livingRooms.Contains(_agents[i].HomeRoom))
                {
                    CancelCommercialVisit(_agents[i]);
                    _agents.RemoveAt(i);
                }
            }

            foreach (var room in livingRooms)
            {
                var role = RoleFor(room.Type.category);
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
                    !PassesCondoDemand(room, currentStars))
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

        bool PassesCondoDemand(RoomInstance room, int currentStars)
        {
            var chance = PricePricing.DemandChance(room.PriceTier, currentStars);
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
            int currentStars = 0)
        {
            if (grid == null || clock == null) return;

            var total = clock.DayIndex * GameClock.MinutesPerDay + clock.MinuteOfDay;
            _nowTotalMinutes = total;
            var advanced = 0;
            if (_lastTotalMinutes != int.MinValue)
                advanced = Mathf.Max(0, total - _lastTotalMinutes);
            _lastTotalMinutes = total;

            for (var i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
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
                UpdateStress(agent, deltaGameMinutes);
            }

            UpdateStreetTraffic(clock, grid, currentStars, advanced);
            DespawnFinishedStreetVisitors();
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
            }
        }

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

            var shops = FindOpenShops(grid, clock.MinuteOfDay);
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

        List<RoomInstance> FindOpenShops(TowerGrid grid, int minuteOfDay)
        {
            var open = new List<RoomInstance>();
            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null) continue;
                if (!ShopVisitRules.IsShop(room.Type)) continue;
                if (!ShopVisitRules.IsOpen(room.Type, minuteOfDay)) continue;
                if (room.ConcurrentVisitors >= ShopVisitRules.SlotCount(room.Type)) continue;
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

            var shops = FindOpenShops(grid, clock.MinuteOfDay);
            if (shops.Count == 0) return false;

            var shop = shops[_rng.Next(shops.Count)];
            if (!shop.TryOccupyVisitorSlot()) return false;

            var exitCell = LobbyExitCell(grid);
            var agent = new Agent(_nextId++, AgentRole.StreetVisitor, shop, exitCell)
            {
                VisitTarget = shop,
                PhaseAfterVisit = AgentPhase.Outside,
                ReturnCell = exitCell,
                VisitDwellRemaining = ShopVisitRules.PickDwellMinutes(shop.Type, _rng)
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
                agent.Visible = false;
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
