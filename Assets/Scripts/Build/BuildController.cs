using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class BuildController : MonoBehaviour
    {
        [SerializeField] TilemapTowerView view;
        [SerializeField] Camera worldCamera;
        [SerializeField] TowerHudController hud;
        [SerializeField] RoomTypeSO lobbyType;
        [SerializeField] int startingFunds = 2_000_000;

        public TowerGrid Grid { get; private set; }
        public FundsWallet Wallet { get; private set; }
        public BuildTool CurrentTool { get; private set; } = BuildTool.PlaceRoom;
        public RoomTypeSO SelectedRoomType { get; private set; }
        public RoomInstance SelectedRoom { get; private set; }
        public Vector2Int? HoverCell { get; private set; }
        public string HelpText { get; private set; }
        public RoomTypeSO LobbyType => lobbyType;
        public ElevatorCorrectionWindow ActiveCorrectionWindow { get; private set; }
        public event Action StateChanged;
        public event Action GridChanged;

        const int GuideMinX = -5;
        const int GuideMaxX = 40;

        bool _draggingLobby;
        int _dragStartX;
        bool _draggingElevator;
        int _dragStartY;
        RoomInstance _elevatorToExtend;
        bool _draggingElevatorEdge;
        bool _dragTopEdge;
        RoomInstance _elevatorEdgeShaft;
        bool _clearedFloorOneHint;
        readonly Dictionary<int, (bool dirty, bool broken)> _roomVisualState = new();

        void Awake()
        {
            Grid = new TowerGrid();
            Wallet = new FundsWallet(startingFunds);
            SelectedRoomType = lobbyType;
            RefreshHelpText();
            if (GetComponent<TowerSimulation>() == null)
                gameObject.AddComponent<TowerSimulation>();
        }

        void Start()
        {
            if (view != null)
                view.PaintStarterGuides(GuideMinX, GuideMaxX);

            if (worldCamera != null)
            {
                var camTransform = worldCamera.transform;
                camTransform.position = new Vector3(12f, 3f, -10f);
                worldCamera.orthographic = true;
                worldCamera.orthographicSize = 8f;
            }

            StateChanged?.Invoke();
        }

        void Update()
        {
            SyncConditionVisuals();
            if (worldCamera == null) return;
            if (IsPointerOverHud(Input.mousePosition))
            {
                if (!_draggingLobby && !_draggingElevator && !_draggingElevatorEdge)
                    view.ClearGhost();
                HoverCell = null;
                return;
            }

            var cell = ScreenToCell(Input.mousePosition);
            HoverCell = cell;
            HandleLobbyDrag(cell);
            HandleElevatorDrag(cell);
            HandleElevatorEdgeDrag(cell);
            HandleHoverGhost(cell);
            HandleClicks(cell);
            if (!_draggingElevatorEdge)
                RefreshSelectionVisuals();
        }

        public void SetTool(BuildTool tool)
        {
            CurrentTool = tool;
            if (tool != BuildTool.PlaceRoom)
                SelectedRoomType = null;
            if (tool != BuildTool.Select)
                ClearSelection();
            view.ClearGhost();
            RefreshHelpText();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Housekeeping, Maintenance, Security, and Research start with one hired worker on place.
        /// </summary>
        public static void ApplyAutoHireOnPlace(RoomInstance room)
        {
            if (room?.Type == null) return;
            if (room.Type.id is "service_housekeeping" or "service_maintenance" or "service_security"
                or "service_research")
                room.SetStaffedWorkers(1);
        }

        public void SetRoomType(RoomTypeSO type)
        {
            SelectedRoomType = type;
            CurrentTool = BuildTool.PlaceRoom;
            ClearSelection();
            RefreshHelpText();
            StateChanged?.Invoke();
        }

        public void SelectLobbyTool()
        {
            if (lobbyType == null) return;
            SelectedRoomType = lobbyType;
            CurrentTool = BuildTool.PlaceRoom;
            ClearSelection();
            RefreshHelpText();
            StateChanged?.Invoke();
        }

        public void SelectTool()
        {
            CurrentTool = BuildTool.Select;
            SelectedRoomType = null;
            view.ClearGhost();
            RefreshHelpText();
            StateChanged?.Invoke();
        }

        public void ClearSelection()
        {
            SelectedRoom = null;
            _draggingElevatorEdge = false;
            _elevatorEdgeShaft = null;
            if (view != null)
            {
                view.ClearSelection();
                view.ClearEdgeHandles();
            }
        }

        public bool TrySelectAt(Vector2Int cell)
        {
            if (!Grid.TryGetRoomAt(cell, out var room) || room?.Type == null)
            {
                ClearSelection();
                RefreshHelpText();
                StateChanged?.Invoke();
                return false;
            }

            SelectedRoom = room;
            RefreshSelectionVisuals();
            RefreshHelpText();
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySetSelectedPriceTier(int tier)
        {
            if (SelectedRoom?.Type == null || !PricePricing.IsPricedRoom(SelectedRoom.Type))
                return false;

            SelectedRoom.PriceTier = PricePricing.ClampTier(tier);
            RefreshHelpText();
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySetStaffedWorkers(int count)
        {
            if (SelectedRoom?.Type == null || !IsStaffedServiceRoom(SelectedRoom.Type))
                return false;

            SelectedRoom.SetStaffedWorkers(count);
            if (view != null)
                view.PaintRoom(SelectedRoom);
            NotifyGridChanged();
            RefreshHelpText();
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySetSelectedElevatorMaintenance(bool inMaintenance)
        {
            if (SelectedRoom?.Type == null || !SelectedRoom.Type.isElevatorShaft)
                return false;
            var sim = GetComponent<TowerSimulation>();
            if (sim?.Elevators == null) return false;
            if (!sim.Elevators.TrySetMaintenance(SelectedRoom.InstanceId, inMaintenance))
                return false;

            // Re-route anyone the shaft can no longer serve so nobody is left queued.
            sim.Agents?.OnElevatorServiceChanged(SelectedRoom.InstanceId);
            RefreshHelpText();
            StateChanged?.Invoke();
            return true;
        }

        public string GetSelectionSummary()
        {
            if (SelectedRoom?.Type == null) return null;
            var room = SelectedRoom;
            var minY = room.Origin.y;
            var maxY = minY + room.Size.y - 1;
            var floors = minY == maxY
                ? FloorLabel(minY)
                : $"{FloorLabel(minY)}–{FloorLabel(maxY)}";
            var flags = room.IsBroken
                ? "Broken"
                : room.Dirty
                    ? "Dirty"
                    : "OK";
            var summary =
                $"{room.Type.displayName} #{room.InstanceId}\n" +
                $"Origin ({room.Origin.x}, {FloorLabel(room.Origin.y)})  " +
                $"Size {room.Size.x}×{room.Size.y}  Floors {floors}\n" +
                $"Condition {room.Condition}  {flags}";
            if (IsStaffedServiceRoom(room.Type))
                summary += $"\nStaff {room.StaffedWorkers}/4";
            if (room.Type.id == "service_security")
                summary += $"\nGuards on patrol: {CountSecurityAgentsForHome(room)}";
            var now = Time.realtimeSinceStartup;
            if (RoomInstance.IsGraceRefundEligible(room.Type) && room.IsInBuildGrace(now))
            {
                var secs = room.PlacedAtRealtime + RoomInstance.BuildGraceSeconds - now;
                summary += $"\nUndo refund {secs:0.0}s (${room.GraceRefundAmount():N0})";
            }
            return summary;
        }

        public static bool IsStaffedServiceRoom(RoomTypeSO type) =>
            type != null && type.id is "service_housekeeping" or "service_maintenance" or "service_security"
                or "service_research";

        public string GetElevatorStatusText()
        {
            if (SelectedRoom?.Type == null || !SelectedRoom.Type.isElevatorShaft)
                return null;

            var now = Time.realtimeSinceStartup;
            if (ActiveCorrectionWindow != null &&
                ActiveCorrectionWindow.ShaftInstanceId == SelectedRoom.InstanceId &&
                ActiveCorrectionWindow.IsActive(now))
            {
                return $"Correction {ActiveCorrectionWindow.SecondsRemaining(now):0.0}s";
            }

            var sim = GetComponent<TowerSimulation>();
            var shaft = sim?.Elevators?.FindByRoomId(SelectedRoom.InstanceId);
            if (shaft == null) return "Service";
            if (!shaft.InMaintenance) return "Service";
            return sim.Elevators.IsDrained(shaft) ? "Ready to shorten" : "Draining";
        }

        int CountSecurityAgentsForHome(RoomInstance home)
        {
            var agents = GetComponent<TowerSimulation>()?.Agents?.Agents;
            if (agents == null || home == null) return 0;
            var count = 0;
            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent != null &&
                    agent.Role == AgentRole.Security &&
                    ReferenceEquals(agent.HomeRoom, home) &&
                    agent.Phase is not (AgentPhase.AtHome or AgentPhase.Outside))
                    count++;
            }
            return count;
        }

        static string FloorLabel(int y) =>
            y > 0 ? y.ToString() : y < 0 ? $"B{-y}" : "G";

        public bool TryPlaceLobby(int minX, int maxX)
        {
            if (lobbyType == null || maxX < minX) return false;

            var cost = (maxX - minX + 1) * lobbyType.buildCost;
            if (!Grid.CanPlaceLobby(minX, maxX, TowerGrid.LobbyFloor) || !Wallet.TrySpend(cost)) return false;
            if (!Grid.TryPlaceLobby(lobbyType, minX, maxX, TowerGrid.LobbyFloor, out var room))
            {
                Wallet.Add(cost);
                return false;
            }

            ClearFloorOneHintIfNeeded();
            view.PaintRoom(room);
            SelectedRoomType = null;
            RefreshHelpText();
            NotifyGridChanged();
            StateChanged?.Invoke();
            return true;
        }

        public bool TryExtendLobby(int newMinX, int newMaxX)
        {
            if (lobbyType == null || newMaxX < newMinX) return false;
            if (!Grid.CanExtendLobby(newMinX, newMaxX)) return false;

            var added = (newMaxX - newMinX + 1) - (Grid.MaxX - Grid.MinX + 1);
            var cost = added * lobbyType.buildCost;
            if (!Wallet.TrySpend(cost)) return false;

            RoomInstance oldLobby = null;
            foreach (var room in Grid.Rooms)
            {
                if (room.Type != null && room.Type.isLobby)
                {
                    oldLobby = room;
                    break;
                }
            }

            if (!Grid.TryExtendLobby(lobbyType, newMinX, newMaxX, out var lobby, out _))
            {
                Wallet.Add(cost);
                return false;
            }

            if (oldLobby != null)
                view.ClearRoom(oldLobby);
            view.PaintRoom(lobby);
            RefreshHelpText();
            NotifyGridChanged();
            StateChanged?.Invoke();
            return true;
        }

        public bool TryPlaceSelected(Vector2Int cell)
        {
            if (!Grid.HasLobby ||
                CurrentTool != BuildTool.PlaceRoom ||
                SelectedRoomType == null ||
                SelectedRoomType.isLobby)
            {
                return false;
            }

            var simulation = GetComponent<TowerSimulation>();
            if (simulation?.Stars != null && !simulation.Stars.CanBuild(SelectedRoomType))
            {
                HelpText = $"Needs {SelectedRoomType.requiredStars}★.";
                StateChanged?.Invoke();
                return false;
            }

            var cost = SelectedRoomType.buildCost *
                       (SelectedRoomType.isElevatorShaft ? SelectedRoomType.size.y : 1);
            if (!Grid.CanPlace(SelectedRoomType, cell) || !Wallet.TrySpend(cost)) return false;
            if (!Grid.TryPlace(SelectedRoomType, cell, out var room, out var clearedScaffolding))
            {
                Wallet.Add(cost);
                return false;
            }

            room.RecordConstructionSpend(cost, Time.realtimeSinceStartup, isInitialPlace: true);
            ApplyAutoHireOnPlace(room);

            foreach (var scaffold in clearedScaffolding)
                view.ClearRoom(scaffold);
            view.PaintRoom(room);
            // Transit sits on the rooms layer; keep it visible over rooms built behind.
            if (IsVisibleTransit(room))
            {
                foreach (var c in room.OccupiedCells())
                    view.PaintCell(c, room);
            }
            else
            {
                foreach (var c in room.OccupiedCells())
                {
                    if (Grid.TryGetRoomAt(c, out var at) && IsVisibleTransit(at))
                        view.PaintCell(c, at);
                }
            }

            RefreshHelpText();
            NotifyGridChanged();
            StateChanged?.Invoke();
            return true;
        }

        public bool TryExtendElevator(RoomInstance shaft, int newMinY, int newMaxY)
        {
            if (shaft?.Type == null || !shaft.Type.isElevatorShaft) return false;
            var simulation = GetComponent<TowerSimulation>();
            if (simulation?.Stars != null && !simulation.Stars.CanBuild(shaft.Type))
                return false;

            var oldMin = shaft.Origin.y;
            var oldMax = oldMin + shaft.Size.y - 1;
            var added = newMaxY - newMinY + 1 - shaft.Size.y;
            var cost = added * shaft.Type.buildCost;
            if (added <= 0 ||
                !Grid.CanExtendElevator(shaft, newMinY, newMaxY) ||
                !Wallet.TrySpend(cost))
                return false;

            var instanceId = shaft.InstanceId;
            if (!Grid.TryExtendElevator(shaft, newMinY, newMaxY, out _))
            {
                Wallet.Add(cost);
                return false;
            }

            if (TryFindRoomById(instanceId, out var currentShaft))
                currentShaft.RecordConstructionSpend(cost, Time.realtimeSinceStartup, isInitialPlace: false);

            BeginOrRefreshCorrectionWindow(instanceId, oldMin, oldMax);
            RepaintAfterElevatorResize(shaft, instanceId);
            if (SelectedRoom != null && SelectedRoom.InstanceId == instanceId)
                ReselectById(instanceId);

            RefreshHelpText();
            NotifyGridChanged();
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Resize under UX policy: extend always; shorten via correction window or drained maintenance.
        /// </summary>
        public bool TryResizeSelectedElevator(int newMinY, int newMaxY)
        {
            if (SelectedRoom?.Type == null || !SelectedRoom.Type.isElevatorShaft)
                return false;
            return TryResizeElevator(SelectedRoom, newMinY, newMaxY);
        }

        public bool TryResizeElevator(RoomInstance shaft, int newMinY, int newMaxY)
        {
            if (shaft?.Type == null || !shaft.Type.isElevatorShaft) return false;
            if (!Grid.CanResizeElevator(shaft, newMinY, newMaxY)) return false;

            var oldMin = shaft.Origin.y;
            var oldMax = oldMin + shaft.Size.y - 1;
            var growing = newMinY < oldMin || newMaxY > oldMax;
            var shrinking = newMinY > oldMin || newMaxY < oldMax;
            var delta = (newMaxY - newMinY + 1) - shaft.Size.y;
            var now = Time.realtimeSinceStartup;

            if (shrinking)
            {
                if (!CanShortenElevator(shaft, oldMin, oldMax, newMinY, newMaxY, now))
                    return false;
            }

            if (growing)
            {
                var cost = delta * shaft.Type.buildCost;
                if (!Wallet.TrySpend(cost)) return false;
            }

            var instanceId = shaft.InstanceId;
            var growCost = growing ? delta * shaft.Type.buildCost : 0;
            if (!Grid.TryResizeElevator(shaft, newMinY, newMaxY, out _))
            {
                if (growing)
                    Wallet.Add(growCost);
                return false;
            }

            if (growing)
            {
                if (TryFindRoomById(instanceId, out var currentShaft))
                    currentShaft.RecordConstructionSpend(growCost, Time.realtimeSinceStartup, isInitialPlace: false);
                BeginOrRefreshCorrectionWindow(instanceId, oldMin, oldMax);
            }
            else if (ActiveCorrectionWindow != null &&
                     ActiveCorrectionWindow.ShaftInstanceId == instanceId &&
                     newMinY == ActiveCorrectionWindow.PreviousMinY &&
                     newMaxY == ActiveCorrectionWindow.PreviousMaxY)
            {
                ActiveCorrectionWindow = null;
            }

            RepaintAfterElevatorResize(shaft, instanceId);
            ReselectById(instanceId);
            RefreshHelpText();
            NotifyGridChanged();
            StateChanged?.Invoke();
            return true;
        }

        bool CanShortenElevator(
            RoomInstance shaft,
            int oldMin,
            int oldMax,
            int newMinY,
            int newMaxY,
            float now)
        {
            var sim = GetComponent<TowerSimulation>();
            var runtime = sim?.Elevators?.FindByRoomId(shaft.InstanceId);

            if (ActiveCorrectionWindow != null &&
                ActiveCorrectionWindow.ShaftInstanceId == shaft.InstanceId &&
                ActiveCorrectionWindow.AllowsResize(oldMin, oldMax, newMinY, newMaxY, now))
            {
                // Quick undo: only if no agents depend on the floors being removed.
                return runtime == null || sim.Elevators.CanVacateFloors(runtime, newMinY, newMaxY);
            }

            return runtime != null &&
                   runtime.InMaintenance &&
                   sim.Elevators.IsDrained(runtime);
        }

        void BeginOrRefreshCorrectionWindow(int instanceId, int previousMin, int previousMax)
        {
            var now = Time.realtimeSinceStartup;
            if (ActiveCorrectionWindow != null &&
                ActiveCorrectionWindow.ShaftInstanceId == instanceId)
            {
                // Keep the original pre-extension bounds; only refresh the timer.
                ActiveCorrectionWindow.RefreshDeadline(now);
                return;
            }

            ActiveCorrectionWindow = new ElevatorCorrectionWindow(
                instanceId,
                previousMin,
                previousMax,
                now);
        }

        void RepaintAfterElevatorResize(RoomInstance oldShaft, int instanceId)
        {
            foreach (var c in oldShaft.OccupiedCells())
            {
                view.ClearCell(c, structureMap: false);
                view.ClearCell(c, structureMap: true);
                if (Grid.TryGetRoomAt(c, out var at))
                    view.PaintCell(c, at);
            }

            foreach (var room in Grid.Rooms)
            {
                if (room.InstanceId != instanceId) continue;
                view.PaintRoom(room);
                break;
            }
        }

        void ReselectById(int instanceId)
        {
            if (TryFindRoomById(instanceId, out var room))
            {
                SelectedRoom = room;
                RefreshSelectionVisuals();
                return;
            }

            ClearSelection();
        }

        bool TryFindRoomById(int instanceId, out RoomInstance room)
        {
            foreach (var candidate in Grid.Rooms)
            {
                if (candidate.InstanceId != instanceId) continue;
                room = candidate;
                return true;
            }

            room = null;
            return false;
        }

        public bool TryDemolishAt(Vector2Int cell)
        {
            if (!Grid.TryDemolishAt(cell, out var removed, out var scaffoldsPlaced, out _))
                return false;

            var refundDelta = BuildGraceRefund.WalletDelta(removed, Time.realtimeSinceStartup);
            if (refundDelta > 0) Wallet.Add(refundDelta);
            else if (refundDelta < 0) Wallet.Subtract(-refundDelta);

            // Always clear vacated cells first. Scaffolding paints on the structure
            // layer; leaving the old rooms-layer tile made demolished rooms look present.
            foreach (var c in removed.OccupiedCells())
            {
                view.ClearCell(c, structureMap: false);
                view.ClearCell(c, structureMap: true);
                if (Grid.TryGetRoomAt(c, out var at))
                    view.PaintCell(c, at);
            }

            foreach (var scaffold in scaffoldsPlaced)
                view.PaintRoom(scaffold);
            RefreshHelpText();
            NotifyGridChanged();
            StateChanged?.Invoke();
            return true;
        }

        bool IsLobbyToolActive() =>
            CurrentTool == BuildTool.PlaceRoom &&
            SelectedRoomType != null &&
            SelectedRoomType.isLobby;

        void HandleLobbyDrag(Vector2Int cell)
        {
            if (lobbyType == null) return;

            // Initial place: always lobby tool (default at start) or explicit lobby selection.
            var placingNew = !Grid.HasLobby;
            var extending = Grid.HasLobby && IsLobbyToolActive();
            if (!placingNew && !extending) return;

            if (Input.GetMouseButtonDown(0))
            {
                _draggingLobby = true;
                _dragStartX = cell.x;
            }

            if (!_draggingLobby) return;

            if (placingNew)
            {
                var minX = Mathf.Min(_dragStartX, cell.x);
                var maxX = Mathf.Max(_dragStartX, cell.x);
                var width = maxX - minX + 1;
                var cost = width * lobbyType.buildCost;
                var valid = Grid.CanPlaceLobby(minX, maxX, TowerGrid.LobbyFloor) && Wallet.CanAfford(cost);
                view.SetGhost(
                    new Vector2Int(minX, TowerGrid.LobbyFloor),
                    new Vector2Int(width, 1),
                    lobbyType.placeholderColor,
                    valid);

                if (Input.GetMouseButtonUp(0))
                {
                    _draggingLobby = false;
                    if (valid) TryPlaceLobby(minX, maxX);
                    view.ClearGhost();
                }

                return;
            }

            // Extend: drag defines a span that must contain the existing lobby.
            var dragMin = Mathf.Min(_dragStartX, cell.x);
            var dragMax = Mathf.Max(_dragStartX, cell.x);
            var newMin = Mathf.Min(Grid.MinX, dragMin);
            var newMax = Mathf.Max(Grid.MaxX, dragMax);
            var added = (newMax - newMin + 1) - (Grid.MaxX - Grid.MinX + 1);
            var extendCost = added * lobbyType.buildCost;
            var extendValid = added > 0 &&
                              Grid.CanExtendLobby(newMin, newMax) &&
                              Wallet.CanAfford(extendCost);

            view.SetGhost(
                new Vector2Int(newMin, TowerGrid.LobbyFloor),
                new Vector2Int(newMax - newMin + 1, 1),
                lobbyType.placeholderColor,
                extendValid);

            if (Input.GetMouseButtonUp(0))
            {
                _draggingLobby = false;
                if (extendValid) TryExtendLobby(newMin, newMax);
                view.ClearGhost();
            }
        }

        void HandleElevatorDrag(Vector2Int cell)
        {
            if (!IsElevatorToolActive()) return;

            if (Input.GetMouseButtonDown(0) &&
                Grid.TryGetRoomAt(cell, out var room) &&
                room.Type != null &&
                room.Type.isElevatorShaft)
            {
                _draggingElevator = true;
                _dragStartY = cell.y;
                _elevatorToExtend = room;
            }

            if (!_draggingElevator || _elevatorToExtend == null) return;

            var oldMin = _elevatorToExtend.Origin.y;
            var oldMax = oldMin + _elevatorToExtend.Size.y - 1;
            var dragMin = Mathf.Min(_dragStartY, cell.y);
            var dragMax = Mathf.Max(_dragStartY, cell.y);
            var newMin = Mathf.Min(oldMin, dragMin);
            var newMax = Mathf.Max(oldMax, dragMax);
            var added = newMax - newMin + 1 - _elevatorToExtend.Size.y;
            var cost = added * _elevatorToExtend.Type.buildCost;
            var valid = added > 0 &&
                        Grid.CanExtendElevator(_elevatorToExtend, newMin, newMax) &&
                        Wallet.CanAfford(cost);

            view.SetGhost(
                new Vector2Int(_elevatorToExtend.Origin.x, newMin),
                new Vector2Int(1, newMax - newMin + 1),
                _elevatorToExtend.Type.placeholderColor,
                valid);

            if (!Input.GetMouseButtonUp(0)) return;

            var shaft = _elevatorToExtend;
            _draggingElevator = false;
            _elevatorToExtend = null;
            if (valid) TryExtendElevator(shaft, newMin, newMax);
            view.ClearGhost();
        }

        void HandleElevatorEdgeDrag(Vector2Int cell)
        {
            if (CurrentTool != BuildTool.Select ||
                SelectedRoom?.Type == null ||
                !SelectedRoom.Type.isElevatorShaft)
                return;

            var shaft = SelectedRoom;
            var oldMin = shaft.Origin.y;
            var oldMax = oldMin + shaft.Size.y - 1;
            var x = shaft.Origin.x;

            if (Input.GetMouseButtonDown(0) && cell.x == x)
            {
                if (cell.y == oldMax)
                {
                    _draggingElevatorEdge = true;
                    _dragTopEdge = true;
                    _elevatorEdgeShaft = shaft;
                    _dragStartY = cell.y;
                }
                else if (cell.y == oldMin)
                {
                    _draggingElevatorEdge = true;
                    _dragTopEdge = false;
                    _elevatorEdgeShaft = shaft;
                    _dragStartY = cell.y;
                }
            }

            if (!_draggingElevatorEdge || _elevatorEdgeShaft == null) return;

            shaft = _elevatorEdgeShaft;
            oldMin = shaft.Origin.y;
            oldMax = oldMin + shaft.Size.y - 1;
            var newMin = oldMin;
            var newMax = oldMax;
            if (_dragTopEdge)
                newMax = Mathf.Max(oldMin + 1, cell.y);
            else
                newMin = Mathf.Min(oldMax - 1, cell.y);

            var now = Time.realtimeSinceStartup;
            var growing = newMin < oldMin || newMax > oldMax;
            var shrinking = newMin > oldMin || newMax < oldMax;
            var delta = (newMax - newMin + 1) - shaft.Size.y;
            var cost = growing ? delta * shaft.Type.buildCost : 0;
            var geometricallyOk = Grid.CanResizeElevator(shaft, newMin, newMax);
            var policyOk = !shrinking ||
                           CanShortenElevator(shaft, oldMin, oldMax, newMin, newMax, now);
            var valid = geometricallyOk &&
                        policyOk &&
                        (!growing || Wallet.CanAfford(cost));

            view.SetGhost(
                new Vector2Int(x, newMin),
                new Vector2Int(1, newMax - newMin + 1),
                shaft.Type.placeholderColor,
                valid);

            if (!Input.GetMouseButtonUp(0)) return;

            _draggingElevatorEdge = false;
            _elevatorEdgeShaft = null;
            if (valid) TryResizeElevator(shaft, newMin, newMax);
            view.ClearGhost();
        }

        void RefreshSelectionVisuals()
        {
            if (view == null) return;
            if (SelectedRoom == null)
            {
                view.ClearSelection();
                view.ClearEdgeHandles();
                return;
            }

            view.SetSelection(SelectedRoom);
            if (SelectedRoom.Type != null && SelectedRoom.Type.isElevatorShaft)
                view.SetElevatorEdgeHandles(SelectedRoom);
            else
                view.ClearEdgeHandles();
        }

        void HandleHoverGhost(Vector2Int cell)
        {
            if (_draggingLobby || _draggingElevator || _draggingElevatorEdge) return;

            if (CurrentTool == BuildTool.Select)
            {
                view.ClearGhost();
                return;
            }

            if (!Grid.HasLobby)
            {
                if (lobbyType == null)
                {
                    view.ClearGhost();
                    return;
                }

                var onLobbyFloor = cell.y == TowerGrid.LobbyFloor;
                view.SetGhost(
                    new Vector2Int(cell.x, TowerGrid.LobbyFloor),
                    Vector2Int.one,
                    lobbyType.placeholderColor,
                    onLobbyFloor && Wallet.CanAfford(lobbyType.buildCost));
                return;
            }

            if (IsLobbyToolActive())
            {
                // Preview one-cell extension toward cursor when outside current lobby.
                if (cell.y != TowerGrid.LobbyFloor)
                {
                    view.ClearGhost();
                    return;
                }

                var newMin = Mathf.Min(Grid.MinX, cell.x);
                var newMax = Mathf.Max(Grid.MaxX, cell.x);
                var added = (newMax - newMin + 1) - (Grid.MaxX - Grid.MinX + 1);
                if (added <= 0)
                {
                    view.ClearGhost();
                    return;
                }

                var cost = added * lobbyType.buildCost;
                var valid = Grid.CanExtendLobby(newMin, newMax) && Wallet.CanAfford(cost);
                view.SetGhost(
                    new Vector2Int(newMin, TowerGrid.LobbyFloor),
                    new Vector2Int(newMax - newMin + 1, 1),
                    lobbyType.placeholderColor,
                    valid);
                return;
            }

            if (CurrentTool != BuildTool.PlaceRoom ||
                SelectedRoomType == null ||
                SelectedRoomType.isLobby)
            {
                view.ClearGhost();
                return;
            }

            var roomCost = SelectedRoomType.buildCost *
                           (SelectedRoomType.isElevatorShaft ? SelectedRoomType.size.y : 1);
            var roomValid = Grid.CanPlace(SelectedRoomType, cell) && Wallet.CanAfford(roomCost);
            view.SetGhost(cell, SelectedRoomType.size, SelectedRoomType.placeholderColor, roomValid);
        }

        void ClearFloorOneHintIfNeeded()
        {
            if (_clearedFloorOneHint || view == null) return;
            view.ClearStructureRow(TowerGrid.LobbyFloor, GuideMinX, GuideMaxX);
            _clearedFloorOneHint = true;
        }

        void RefreshHelpText()
        {
            if (!Grid.HasLobby)
            {
                HelpText =
                    "Drag LEFT→RIGHT on Floor G (lobby) to place the Lobby. RMB pan · Scroll zoom.";
                return;
            }

            if (CurrentTool == BuildTool.Select)
            {
                if (SelectedRoom?.Type != null && SelectedRoom.Type.isElevatorShaft)
                {
                    HelpText =
                        "Elevator selected: drag top/bottom edges to extend. " +
                        "Shorten within 10s of an extension, or use Maintenance then drain to shorten later.";
                    return;
                }

                HelpText = SelectedRoom != null
                    ? "Inspecting selection. Click empty space to clear, or pick another room."
                    : "Selector: click any built room to inspect. Elevators show edge handles when selected.";
                return;
            }

            if (CurrentTool == BuildTool.Bulldoze)
            {
                HelpText =
                    "Click a room to remove it. Under floors above, wood scaffolding stays so the tower does not float.";
                return;
            }

            if (IsLobbyToolActive())
            {
                HelpText = "Lobby tool: drag on Floor G past the lobby ends to extend it.";
                return;
            }

            HelpText =
                SelectedRoomType == null
                    ? "Pick Selector to inspect, or Lobby / Office / Condo / Hotel / Retail / Stairs / Elevator to build."
                    : SelectedRoomType.isStairs
                        ? "Stairs (2×2): BL→UR run. Stack next flight one floor up (share connecting floor). Roles 1+4 cannot overlap; 2+3 can."
                        : SelectedRoomType.isElevatorShaft
                            ? "Elevator (1×2): click to place. Or use Selector and drag shaft edges to resize (30 floors max)."
                        : $"Selected: {SelectedRoomType.displayName}. Build only on top of the floor below (no overhangs).";
        }

        void HandleClicks(Vector2Int cell)
        {
            if (!Input.GetMouseButtonDown(0) ||
                _draggingLobby ||
                _draggingElevator ||
                _draggingElevatorEdge)
                return;

            if (CurrentTool == BuildTool.Select)
            {
                // Edge clicks start a drag; don't also re-select / clear.
                if (SelectedRoom?.Type != null &&
                    SelectedRoom.Type.isElevatorShaft &&
                    cell.x == SelectedRoom.Origin.x)
                {
                    var minY = SelectedRoom.Origin.y;
                    var maxY = minY + SelectedRoom.Size.y - 1;
                    if (cell.y == minY || cell.y == maxY)
                        return;
                }

                TrySelectAt(cell);
                return;
            }

            if (CurrentTool == BuildTool.Bulldoze)
            {
                TryDemolishAt(cell);
                return;
            }

            if (IsLobbyToolActive()) return; // handled by drag extend
            TryPlaceSelected(cell);
        }

        bool IsPointerOverHud(Vector3 screen)
        {
            var guiPoint = new Vector2(screen.x, Screen.height - screen.y);
            return (hud != null && hud.PanelScreenRect.Contains(guiPoint)) ||
                   CutawayCamera.HorizontalScrollbarScreenRect.Contains(guiPoint) ||
                   CutawayCamera.VerticalScrollbarScreenRect.Contains(guiPoint);
        }

        Vector2Int ScreenToCell(Vector3 screen)
        {
            var world = worldCamera.ScreenToWorldPoint(screen);
            return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        }

        void NotifyGridChanged() => GridChanged?.Invoke();

        void SyncConditionVisuals()
        {
            if (view == null || Grid == null) return;

            var selectedChanged = false;
            foreach (var room in Grid.Rooms)
            {
                if (room?.Type == null) continue;
                var dirty = room.Dirty;
                var broken = room.IsBroken;
                if (_roomVisualState.TryGetValue(room.InstanceId, out var prev) &&
                    prev.dirty == dirty &&
                    prev.broken == broken)
                    continue;

                _roomVisualState[room.InstanceId] = (dirty, broken);
                view.PaintRoom(room);
                if (ReferenceEquals(room, SelectedRoom))
                    selectedChanged = true;
            }

            if (selectedChanged)
            {
                RefreshHelpText();
                StateChanged?.Invoke();
            }
        }

        bool IsElevatorToolActive() =>
            CurrentTool == BuildTool.PlaceRoom &&
            SelectedRoomType != null &&
            SelectedRoomType.isElevatorShaft;

        static bool IsVisibleTransit(RoomInstance room) =>
            room?.Type != null && (room.Type.isStairs || room.Type.isElevatorShaft);
    }
}
