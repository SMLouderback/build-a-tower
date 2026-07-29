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
                AddRoomButton(room);
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

            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoPremium"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/HotelPremium"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/OfficePremium"));
        }

        void AddRoomButton(RoomTypeSO room)
        {
            if (room != null && !room.isLobby && !_roomButtons.Contains(room))
                _roomButtons.Add(room);
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
                row * 7f +
                btnH + 4f +
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

            var stars = simulation?.Stars;
            GUI.Label(
                new Rect(cx, cy, inner, row),
                stars != null ? $"Stars: {stars.CurrentStars}/2" : "Stars: —",
                label);
            cy += row;

            var clockText = simulation?.Clock != null ? simulation.Clock.FormatHud() : "—";
            GUI.Label(new Rect(cx, cy, inner, row), $"Time: {clockText}", label);
            cy += row;

            DrawTimeSpeedButtons(cx, cy, inner, btnH);
            cy += btnH + 4f;

            if (simulation?.Agents != null)
            {
                GUI.Label(
                    new Rect(cx, cy, inner, row),
                    $"Population: {simulation.Agents.Agents.Count} | Stress: {simulation.Agents.AverageStress:0}",
                    label);
            }
            else
            {
                GUI.Label(new Rect(cx, cy, inner, row), "Population: —", label);
            }

            cy += row;

            var economy = simulation?.Economy;
            GUI.Label(
                new Rect(cx, cy, inner, row),
                economy != null
                    ? $"Last Net: ${economy.LastNet:N0} (${economy.LastIncome:N0} / -${economy.LastExpense:N0})"
                    : "Last Net: —",
                label);
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
                var canBuild = stars == null || stars.CanBuild(room);
                if (!canBuild)
                    labelText = $"{labelText} ({room.requiredStars}★)";
                var wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && canBuild;
                if (GUI.Button(new Rect(bx, rowY, bw, btnH), labelText))
                    build.SetRoomType(captured);
                GUI.enabled = wasEnabled;

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

            if (stairsRoom != null)
            {
                var wasEnabled = GUI.enabled;
                var canBuild = stars == null || stars.CanBuild(stairsRoom);
                GUI.enabled = wasEnabled && canBuild;
                var stairsLabel = canBuild ? "Stairs" : $"Stairs ({stairsRoom.requiredStars}★)";
                if (GUI.Button(new Rect(cx, cy, inner, btnH), stairsLabel))
                    build.SetRoomType(stairsRoom);
                GUI.enabled = wasEnabled;
            }
            cy += btnH + 4f;

            if (elevatorRoom != null)
            {
                var wasEnabled = GUI.enabled;
                var canBuild = stars == null || stars.CanBuild(elevatorRoom);
                GUI.enabled = wasEnabled && canBuild;
                var elevatorLabel = canBuild ? "Elevator" : $"Elevator ({elevatorRoom.requiredStars}★)";
                if (GUI.Button(new Rect(cx, cy, inner, btnH), elevatorLabel))
                    build.SetRoomType(elevatorRoom);
                GUI.enabled = wasEnabled;
            }
            cy += btnH + 4f;

            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Extend Lobby"))
                build.SelectLobbyTool();
            cy += btnH + 4f;

            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Bulldoze"))
                build.SetTool(BuildTool.Bulldoze);

            _panelRect = new Rect(x, y, width, cy + btnH + 8f - y);
        }

        void DrawTimeSpeedButtons(float x, float y, float width, float height)
        {
            if (simulation?.Clock == null) return;

            var labels = new[] { "||", "1x", "2x", "5x", "10x", "60x" };
            var speeds = new[] { 0f, 1f, 2f, 5f, 10f, 60f };
            const float gap = 4f;
            var buttonWidth = (width - gap * (labels.Length - 1)) / labels.Length;
            var clock = simulation.Clock;

            for (var i = 0; i < labels.Length; i++)
            {
                var active = i == 0
                    ? clock.Paused
                    : !clock.Paused && Mathf.Approximately(clock.MinutesPerRealSecond, speeds[i]);
                var rect = new Rect(x + i * (buttonWidth + gap), y, buttonWidth, height);
                if (GUI.Toggle(rect, active, labels[i], GUI.skin.button) && !active)
                    simulation.SetSpeedPreset(speeds[i], paused: i == 0);
            }
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
