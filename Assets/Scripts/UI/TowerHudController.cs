using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BuildATower
{
    public sealed class TowerHudController : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] BuildController build;
        [SerializeField] List<RoomTypeSO> placeableRooms = new();

        Label _funds;
        Label _tool;
        Label _cell;
        VisualElement _toolbar;
        Button _bulldozeButton;
        readonly List<Button> _roomButtons = new();
        bool _initialized;

        void OnEnable()
        {
            if (_initialized) Activate();
        }

        void Start()
        {
            var root = document.rootVisualElement;
            _funds = root.Q<Label>("funds-label");
            _tool = root.Q<Label>("tool-label");
            _cell = root.Q<Label>("cell-label");
            _toolbar = root.Q<VisualElement>("toolbar");
            _bulldozeButton = root.Q<Button>("btn-bulldoze");
            _initialized = true;
            Activate();
        }

        void Activate()
        {
            ClearRoomButtons();
            _bulldozeButton.clicked += OnBulldozeClicked;

            foreach (var room in placeableRooms)
            {
                if (room == null || room.isLobby) continue;
                var captured = room;
                var btn = new Button(() => build.SetRoomType(captured)) { text = room.displayName };
                _toolbar.Add(btn);
                _roomButtons.Add(btn);
            }

            build.StateChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (build != null) build.StateChanged -= Refresh;
            if (_bulldozeButton != null) _bulldozeButton.clicked -= OnBulldozeClicked;
            ClearRoomButtons();
        }

        void OnBulldozeClicked() => build.SetTool(BuildTool.Bulldoze);

        void ClearRoomButtons()
        {
            foreach (var button in _roomButtons)
                button.RemoveFromHierarchy();
            _roomButtons.Clear();
        }

        void Update() => RefreshHoverOnly();

        void Refresh()
        {
            if (_funds == null || build == null) return;
            _funds.text = $"Funds: ${build.Wallet.Balance:N0}";
            var roomName = build.SelectedRoomType != null ? build.SelectedRoomType.displayName : "—";
            _tool.text = $"Tool: {build.CurrentTool} / {roomName}";
            RefreshHoverOnly();
        }

        void RefreshHoverOnly()
        {
            if (_cell == null || build == null) return;
            if (build.HoverCell.HasValue)
            {
                var c = build.HoverCell.Value;
                var floorLabel = c.y > 0 ? c.y.ToString() : c.y < 0 ? $"B{-c.y}" : "G";
                _cell.text = $"Cell: ({c.x}, floor {floorLabel})";
            }
            else _cell.text = "Cell: —";
        }
    }
}
