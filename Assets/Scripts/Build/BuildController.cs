using System;
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
        public Vector2Int? HoverCell { get; private set; }
        public string HelpText { get; private set; }
        public RoomTypeSO LobbyType => lobbyType;
        public event Action StateChanged;
        public event Action GridChanged;

        const int GuideMinX = -5;
        const int GuideMaxX = 40;

        bool _draggingLobby;
        int _dragStartX;
        bool _clearedFloorOneHint;

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
            if (worldCamera == null) return;
            if (IsPointerOverHud(Input.mousePosition))
            {
                if (!_draggingLobby)
                    view.ClearGhost();
                HoverCell = null;
                return;
            }

            var cell = ScreenToCell(Input.mousePosition);
            HoverCell = cell;
            HandleLobbyDrag(cell);
            HandleHoverGhost(cell);
            HandleClicks(cell);
        }

        public void SetTool(BuildTool tool)
        {
            CurrentTool = tool;
            view.ClearGhost();
            RefreshHelpText();
            StateChanged?.Invoke();
        }

        public void SetRoomType(RoomTypeSO type)
        {
            SelectedRoomType = type;
            CurrentTool = BuildTool.PlaceRoom;
            RefreshHelpText();
            StateChanged?.Invoke();
        }

        public void SelectLobbyTool()
        {
            if (lobbyType == null) return;
            SelectedRoomType = lobbyType;
            CurrentTool = BuildTool.PlaceRoom;
            RefreshHelpText();
            StateChanged?.Invoke();
        }

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

            var cost = SelectedRoomType.buildCost;
            if (!Grid.CanPlace(SelectedRoomType, cell) || !Wallet.TrySpend(cost)) return false;
            if (!Grid.TryPlace(SelectedRoomType, cell, out var room, out var clearedScaffolding))
            {
                Wallet.Add(cost);
                return false;
            }

            foreach (var scaffold in clearedScaffolding)
                view.ClearRoom(scaffold);
            view.PaintRoom(room);
            // Stairs sit on the rooms layer; keep them visible over rooms built behind.
            if (room.Type != null && room.Type.isStairs)
            {
                foreach (var c in room.OccupiedCells())
                    view.PaintCell(c, room);
            }
            else
            {
                foreach (var c in room.OccupiedCells())
                {
                    if (Grid.TryGetRoomAt(c, out var at) && at.Type != null && at.Type.isStairs)
                        view.PaintCell(c, at);
                }
            }

            RefreshHelpText();
            NotifyGridChanged();
            StateChanged?.Invoke();
            return true;
        }

        public bool TryDemolishAt(Vector2Int cell)
        {
            if (!Grid.TryDemolishAt(cell, out var removed, out var scaffoldsPlaced, out _))
                return false;

            if (removed.Type != null && removed.Type.isStairs)
            {
                foreach (var c in removed.OccupiedCells())
                {
                    view.ClearCell(c, structureMap: false);
                    if (Grid.TryGetRoomAt(c, out var at))
                        view.PaintCell(c, at);
                }
            }
            else
            {
                foreach (var c in removed.OccupiedCells())
                {
                    if (Grid.TryGetRoomAt(c, out var at))
                    {
                        // Stairs still punch through — keep / refresh their paint.
                        if (at.Type != null && at.Type.isStairs)
                            view.PaintCell(c, at);
                        continue;
                    }

                    view.ClearCell(c, structureMap: false);
                    view.ClearCell(c, structureMap: true);
                }
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

        void HandleHoverGhost(Vector2Int cell)
        {
            if (_draggingLobby) return;

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

            var roomCost = SelectedRoomType.buildCost;
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
                    ? "Pick Lobby to extend, or Office / Condo / Hotel / Retail / Stairs to build."
                    : SelectedRoomType.isStairs
                        ? "Stairs (2×2): BL→UR run. Stack next flight one floor up (share connecting floor). Roles 1+4 cannot overlap; 2+3 can."
                        : $"Selected: {SelectedRoomType.displayName}. Build only on top of the floor below (no overhangs).";
        }

        void HandleClicks(Vector2Int cell)
        {
            if (!Input.GetMouseButtonDown(0) || _draggingLobby) return;

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
            if (hud == null) return false;
            var guiPoint = new Vector2(screen.x, Screen.height - screen.y);
            return hud.PanelScreenRect.Contains(guiPoint);
        }

        Vector2Int ScreenToCell(Vector3 screen)
        {
            var world = worldCamera.ScreenToWorldPoint(screen);
            return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        }

        void NotifyGridChanged() => GridChanged?.Invoke();
    }
}
