using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Simple IMGUI HUD in the Game view.
    /// Important: keep the Game tab Scale at 1x (or Scale to Fit). Zoom &gt; 1x crops the HUD.
    /// </summary>
    public sealed class TowerHudController : MonoBehaviour
    {
        [SerializeField] BuildController build;
        [SerializeField] TowerSimulation simulation;
        [SerializeField] List<RoomTypeSO> placeableRooms = new();
        [SerializeField] RoomTypeSO stairsRoom;
        [SerializeField] RoomTypeSO elevatorRoom;

        [SerializeField] float panelWidth = 260f;
        [SerializeField] float edgeGapPixels = 12f;

        Rect _panelRect;
        readonly List<RoomTypeSO> _roomButtons = new();

        public Rect PanelScreenRect => _panelRect;

        void Awake()
        {
            if (simulation == null && build != null)
                simulation = build.GetComponent<TowerSimulation>();
            EnsureElevatorAndCatalog();
        }

        void EnsureElevatorAndCatalog()
        {
            if (stairsRoom == null)
                stairsRoom = Resources.Load<RoomTypeSO>("Rooms/Stairs");
            if (elevatorRoom == null)
                elevatorRoom = Resources.Load<RoomTypeSO>("Rooms/ElevatorNormal");

            _roomButtons.Clear();
            foreach (var room in placeableRooms)
            {
                if (room == null || room.isLobby) continue;
                if (!_roomButtons.Contains(room))
                    _roomButtons.Add(room);
            }

            // Always expose Stairs even if the scene list ref is missing.
            if (stairsRoom != null && !_roomButtons.Contains(stairsRoom))
            {
                // Prefer stairs asset over a broken duplicate id entry.
                _roomButtons.RemoveAll(r => r != null && r.id == "stairs");
                _roomButtons.Add(stairsRoom);
            }

            // Always expose Elevator even if the scene list ref is missing.
            if (elevatorRoom != null && !_roomButtons.Contains(elevatorRoom))
            {
                _roomButtons.RemoveAll(r => r != null && r.id == "elevator_normal");
                _roomButtons.Add(elevatorRoom);
            }
        }

        void OnGUI()
        {
            if (build == null) return;
            if (_roomButtons.Count == 0)
                EnsureElevatorAndCatalog();

            if (simulation == null)
                simulation = build.GetComponent<TowerSimulation>() ?? FindAnyObjectByType<TowerSimulation>();

            var gap = edgeGapPixels;
            var x = gap;
            var y = gap;
            var width = Mathf.Min(panelWidth, Screen.width - gap * 2f);
            var inner = Mathf.Max(80f, width - 16f);
            const float row = 22f;
            const float btnH = 26f;

            var label = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = 12
            };
            var title = new GUIStyle(label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
            title.normal.textColor = new Color(1f, 0.86f, 0.47f);
            label.normal.textColor = Color.white;

            var help = string.IsNullOrEmpty(build.HelpText) ? "—" : build.HelpText;
            var helpHeight = Mathf.Clamp(
                label.CalcHeight(new GUIContent(help), inner),
                row,
                row * 3f);

            var roomCount = _roomButtons.Count;
            var roomRows = Mathf.Max(1, (roomCount + 1) / 2);
            var selection = build.GetSelectionSummary();
            var elevStatus = build.GetElevatorStatusText();
            var selectionExtra = 0f;
            if (selection != null)
            {
                selectionExtra += 4f + row * selection.Split('\n').Length;
                if (elevStatus != null)
                    selectionExtra += row + btnH;
            }

            var height =
                8f +
                row +
                helpHeight + 4f +
                row * 5f +
                selectionExtra +
                4f + row +
                roomRows * (btnH + 4f) +
                4f + row +
                btnH + // Selector
                4f + btnH + // Stairs tool
                4f + btnH + // Elevator tool
                4f + btnH + // Extend Lobby
                4f + btnH + // Bulldoze
                10f;

            _panelRect = new Rect(x, y, width, height);
            GUI.Box(_panelRect, GUIContent.none);

            var cx = x + 8f;
            var cy = y + 8f;

            GUI.Label(new Rect(cx, cy, inner, row), "Build-A-Tower", title);
            cy += row;

            GUI.Label(new Rect(cx, cy, inner, helpHeight), help, label);
            cy += helpHeight + 4f;

            GUI.Label(new Rect(cx, cy, inner, row), $"Funds: ${build.Wallet.Balance:N0}", label);
            cy += row;

            var clockText = simulation?.Clock != null ? simulation.Clock.FormatHud() : "—";
            GUI.Label(new Rect(cx, cy, inner, row), $"Time: {clockText}", label);
            cy += row;

            if (simulation?.Agents != null)
            {
                GUI.Label(
                    new Rect(cx, cy, inner, row),
                    $"Agents: {simulation.Agents.Agents.Count} | Stress: {simulation.Agents.AverageStress:0}",
                    label);
            }
            else
            {
                GUI.Label(new Rect(cx, cy, inner, row), "Agents: —", label);
            }

            cy += row;

            var roomName = build.SelectedRoomType != null ? build.SelectedRoomType.displayName : "—";
            GUI.Label(new Rect(cx, cy, inner, row), $"Tool: {build.CurrentTool} / {roomName}", label);
            cy += row;

            if (build.HoverCell.HasValue)
            {
                var c = build.HoverCell.Value;
                var floorLabel = c.y > 0 ? c.y.ToString() : c.y < 0 ? $"B{-c.y}" : "G";
                GUI.Label(new Rect(cx, cy, inner, row), $"Cell: ({c.x}, floor {floorLabel})", label);
            }
            else
            {
                GUI.Label(new Rect(cx, cy, inner, row), "Cell: —", label);
            }

            cy += row;

            if (selection != null)
            {
                cy += 4f;
                foreach (var line in selection.Split('\n'))
                {
                    GUI.Label(new Rect(cx, cy, inner, row), line, label);
                    cy += row;
                }

                if (elevStatus != null)
                {
                    GUI.Label(new Rect(cx, cy, inner, row), $"Elevator: {elevStatus}", label);
                    cy += row;
                    var simElev = simulation?.Elevators?.FindByRoomId(build.SelectedRoom.InstanceId);
                    var inMaint = simElev != null && simElev.InMaintenance;
                    var maintLabel = inMaint ? "Exit Maintenance" : "Enter Maintenance";
                    if (GUI.Button(new Rect(cx, cy, inner, btnH), maintLabel))
                        build.TrySetSelectedElevatorMaintenance(!inMaint);
                    cy += btnH;
                }
            }

            cy += 4f;
            GUI.Label(new Rect(cx, cy, inner, row), "Rooms", title);
            cy += row;

            var col = 0;
            var rowY = cy;
            foreach (var room in _roomButtons)
            {
                if (room == null) continue;
                // Stairs also has a Tools button; keep it in Rooms grid too for discoverability.
                var captured = room;
                var labelText = ShortLabel(room.displayName);
                var bw = (inner - 4f) * 0.5f;
                var bx = cx + col * (bw + 4f);
                if (GUI.Button(new Rect(bx, rowY, bw, btnH), labelText))
                    build.SetRoomType(captured);

                col++;
                if (col >= 2)
                {
                    col = 0;
                    rowY += btnH + 4f;
                }
            }

            if (col != 0) rowY += btnH + 4f;
            cy = rowY + 4f;

            GUI.Label(new Rect(cx, cy, inner, row), "Tools", title);
            cy += row;

            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Selector"))
                build.SelectTool();
            cy += btnH + 4f;

            if (stairsRoom != null && GUI.Button(new Rect(cx, cy, inner, btnH), "Stairs"))
                build.SetRoomType(stairsRoom);
            cy += btnH + 4f;

            if (elevatorRoom != null && GUI.Button(new Rect(cx, cy, inner, btnH), "Elevator"))
                build.SetRoomType(elevatorRoom);
            cy += btnH + 4f;

            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Extend Lobby"))
                build.SelectLobbyTool();
            cy += btnH + 4f;

            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Bulldoze"))
                build.SetTool(BuildTool.Bulldoze);

            _panelRect = new Rect(x, y, width, cy + btnH + 8f - y);
        }

        static string ShortLabel(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "Room";
            if (displayName.StartsWith("Hotel")) return "Hotel";
            if (displayName.StartsWith("Retail")) return "Retail";
            if (displayName.StartsWith("Stairs")) return "Stairs";
            if (displayName.StartsWith("Elevator")) return "Elevator";
            return displayName;
        }
    }
}
