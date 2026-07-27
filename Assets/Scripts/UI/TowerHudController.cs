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

        void OnEnable()
        {
            var root = document.rootVisualElement;
            _funds = root.Q<Label>("funds-label");
            _tool = root.Q<Label>("tool-label");
            _cell = root.Q<Label>("cell-label");
            _toolbar = root.Q<VisualElement>("toolbar");

            root.Q<Button>("btn-bulldoze").clicked += () => build.SetTool(BuildTool.Bulldoze);

            foreach (var room in placeableRooms)
            {
                if (room == null || room.isLobby) continue;
                var captured = room;
                var btn = new Button(() => build.SetRoomType(captured)) { text = room.displayName };
                _toolbar.Add(btn);
            }

            build.StateChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (build != null) build.StateChanged -= Refresh;
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
                var floorLabel = c.y > 0 ? c.y.ToString() : $"B{-c.y}";
                _cell.text = $"Cell: ({c.x}, floor {floorLabel})";
            }
            else _cell.text = "Cell: —";
        }
    }
}
