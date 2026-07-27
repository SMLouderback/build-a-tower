using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BuildATower
{
    public sealed class BuildController : MonoBehaviour
    {
        [SerializeField] TilemapTowerView view;
        [SerializeField] Camera worldCamera;
        [SerializeField] RoomTypeSO lobbyType;
        [SerializeField] int startingFunds = 2_000_000;

        public TowerGrid Grid { get; private set; }
        public FundsWallet Wallet { get; private set; }
        public BuildTool CurrentTool { get; private set; } = BuildTool.PlaceRoom;
        public RoomTypeSO SelectedRoomType { get; private set; }
        public Vector2Int? HoverCell { get; private set; }
        public event Action StateChanged;

        bool _draggingLobby;
        int _dragStartX;

        void Awake()
        {
            Grid = new TowerGrid();
            Wallet = new FundsWallet(startingFunds);
            SelectedRoomType = lobbyType;
        }

        void Update()
        {
            if (worldCamera == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                view.ClearGhost();
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
            StateChanged?.Invoke();
        }

        public void SetRoomType(RoomTypeSO type)
        {
            SelectedRoomType = type;
            CurrentTool = BuildTool.PlaceRoom;
            StateChanged?.Invoke();
        }

        public bool DebugTryPlaceLobby(int minX, int maxX)
        {
            if (lobbyType == null || maxX < minX) return false;

            var cost = (maxX - minX + 1) * lobbyType.buildCost;
            if (!Grid.CanPlaceLobby(minX, maxX, 1) || !Wallet.TrySpend(cost)) return false;
            if (!Grid.TryPlaceLobby(lobbyType, minX, maxX, 1, out var room))
            {
                Wallet.Add(cost);
                return false;
            }

            view.PaintRoom(room);
            StateChanged?.Invoke();
            return true;
        }

        public bool DebugTryPlaceSelectedAt(Vector2Int cell)
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
            if (!Grid.TryPlace(SelectedRoomType, cell, out var room))
            {
                Wallet.Add(cost);
                return false;
            }

            view.PaintRoom(room);
            StateChanged?.Invoke();
            return true;
        }

        public bool DebugTryDemolishAt(Vector2Int cell)
        {
            if (!Grid.TryDemolishAt(cell, out var removed)) return false;

            view.ClearRoom(removed);
            StateChanged?.Invoke();
            return true;
        }

        void HandleLobbyDrag(Vector2Int cell)
        {
            if (Grid.HasLobby || lobbyType == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                _draggingLobby = true;
                _dragStartX = cell.x;
            }

            if (_draggingLobby)
            {
                var minX = Mathf.Min(_dragStartX, cell.x);
                var maxX = Mathf.Max(_dragStartX, cell.x);
                var width = maxX - minX + 1;
                var cost = width * lobbyType.buildCost;
                var valid = Grid.CanPlaceLobby(minX, maxX, 1) &&
                            cell.y == 1 &&
                            Wallet.CanAfford(cost);
                view.SetGhost(
                    new Vector2Int(minX, 1),
                    new Vector2Int(width, 1),
                    lobbyType.placeholderColor,
                    valid);

                if (Input.GetMouseButtonUp(0))
                {
                    _draggingLobby = false;
                    if (valid) DebugTryPlaceLobby(minX, maxX);
                    view.ClearGhost();
                }
            }
        }

        void HandleHoverGhost(Vector2Int cell)
        {
            if (_draggingLobby || !Grid.HasLobby) return;
            if (CurrentTool != BuildTool.PlaceRoom ||
                SelectedRoomType == null ||
                SelectedRoomType.isLobby)
            {
                view.ClearGhost();
                return;
            }

            var cost = SelectedRoomType.buildCost;
            var valid = Grid.CanPlace(SelectedRoomType, cell) && Wallet.CanAfford(cost);
            view.SetGhost(cell, SelectedRoomType.size, SelectedRoomType.placeholderColor, valid);
        }

        void HandleClicks(Vector2Int cell)
        {
            if (!Input.GetMouseButtonDown(0) || _draggingLobby) return;

            if (CurrentTool == BuildTool.Bulldoze)
            {
                DebugTryDemolishAt(cell);
                return;
            }

            DebugTryPlaceSelectedAt(cell);
        }

        Vector2Int ScreenToCell(Vector3 screen)
        {
            var world = worldCamera.ScreenToWorldPoint(screen);
            return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        }
    }
}
