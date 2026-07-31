using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Progressive IMGUI HUD: core strip + accordion sections.
    /// Keep the Game tab Scale at 1x (or Scale to Fit). Zoom &gt; 1x crops the HUD.
    /// </summary>
    public sealed class TowerHudController : MonoBehaviour
    {
        [SerializeField] BuildController build;
        [SerializeField] TowerSimulation simulation;
        [SerializeField] List<RoomTypeSO> placeableRooms = new();
        [SerializeField] RoomTypeSO stairsRoom;
        [SerializeField] RoomTypeSO elevatorRoom;

        [SerializeField] float panelWidth = 280f;
        [SerializeField] float edgeGapPixels = 12f;

        Rect _panelRect;
        readonly List<RoomTypeSO> _roomButtons = new();
        List<BuildCatalogFamily> _catalog = new();

        bool _goalsOpen;
        bool _economyOpen;
        bool _buildOpen = true;
        bool _selectionOpen = true;
        BuildFamily? _expandedFamily;
        BuildSubgroup? _expandedShopSubgroup;

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
                AddRoomButton(room);

            if (stairsRoom != null && !_roomButtons.Contains(stairsRoom))
            {
                _roomButtons.RemoveAll(r => r != null && r.id == "stairs");
                _roomButtons.Add(stairsRoom);
            }

            if (elevatorRoom != null && !_roomButtons.Contains(elevatorRoom))
            {
                _roomButtons.RemoveAll(r => r != null && r.id == "elevator_normal");
                _roomButtons.Add(elevatorRoom);
            }

            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/CondoPremium"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/HotelPremium"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/OfficePremium"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopFastFood"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopRestaurant"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopRetail"));
            _catalog = BuildCatalog.Group(_roomButtons);
        }

        void AddRoomButton(RoomTypeSO room)
        {
            if (room != null && !room.isLobby && !_roomButtons.Contains(room))
                _roomButtons.Add(room);
        }

        void OnGUI()
        {
            if (build == null) return;
            if (_roomButtons.Count == 0 || _catalog.Count == 0)
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
            const float roomBtnH = 34f;

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

            var section = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            var roomButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };

            var help = string.IsNullOrEmpty(build.HelpText) ? "—" : build.HelpText;
            var helpHeight = Mathf.Clamp(
                label.CalcHeight(new GUIContent(help), inner),
                row,
                row * 3f);

            var stars = simulation?.Stars;
            var agents = simulation?.Agents;
            var population = agents != null ? agents.Population : 0;
            var averageStress = agents != null ? agents.AverageStress : 0f;
            var goalsUnlocked = build.Grid != null && build.Grid.HasLobby;
            var economyUnlocked = simulation?.Economy != null && simulation.Economy.HasRecordedEconomyEvent;
            var hasSelection = build.SelectedRoom != null;

            // First pass estimate; final rect set from cy. Tall box so expanded Build stays covered.
            _panelRect = new Rect(x, y, width, Mathf.Max(240f, Screen.height - y - gap));
            GUI.Box(_panelRect, GUIContent.none);

            var cx = x + 8f;
            var cy = y + 8f;

            GUI.Label(new Rect(cx, cy, inner, row), "Build-A-Tower", title);
            cy += row;

            GUI.Label(new Rect(cx, cy, inner, helpHeight), help, label);
            cy += helpHeight + 4f;

            GUI.Label(new Rect(cx, cy, inner, row), $"Funds: ${build.Wallet.Balance:N0}", label);
            cy += row;

            GUI.Label(
                new Rect(cx, cy, inner, row),
                stars != null ? $"Stars: {stars.CurrentStars}/{StarSystem.MaxStars}" : "Stars: —",
                label);
            cy += row;

            var clockText = simulation?.Clock != null ? simulation.Clock.FormatHud() : "—";
            GUI.Label(new Rect(cx, cy, inner, row), $"Time: {clockText}", label);
            cy += row;

            DrawTimeSpeedButtons(cx, cy, inner, btnH);
            cy += btnH + 6f;

            if (goalsUnlocked)
            {
                if (DrawSectionHeader(ref cy, cx, inner, btnH, section, "Goals", ref _goalsOpen))
                {
                    var starGoalLines = stars != null
                        ? stars.FormatNextStarGoal(build.Grid, averageStress, population).Split('\n')
                        : new[] { "Next ★: —" };
                    foreach (var goalLine in starGoalLines)
                    {
                        GUI.Label(new Rect(cx, cy, inner, row), goalLine, label);
                        cy += row;
                    }

                    cy += 4f;
                }
            }

            if (economyUnlocked)
            {
                if (DrawSectionHeader(ref cy, cx, inner, btnH, section, "Economy", ref _economyOpen))
                {
                    if (agents != null)
                    {
                        GUI.Label(
                            new Rect(cx, cy, inner, row),
                            $"Population: {agents.Population} | Stress: {agents.AverageStress:0}",
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
                    cy += row + 4f;
                }
            }

            if (DrawSectionHeader(ref cy, cx, inner, btnH, section, "Build", ref _buildOpen))
            {
                var roomName = build.SelectedRoomType != null ? build.SelectedRoomType.displayName : "—";
                GUI.Label(new Rect(cx, cy, inner, row), $"Tool: {build.CurrentTool} / {roomName}", label);
                cy += row;

                foreach (var economyLine in SelectedEconomyLines(build.SelectedRoomType))
                {
                    GUI.Label(new Rect(cx, cy, inner, row), economyLine, label);
                    cy += row;
                }

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

                cy += row + 4f;

                cy = DrawNestedCatalog(cx, cy, inner, row, btnH, roomBtnH, roomButton, section, stars);
                cy += 4f;

                GUI.Label(new Rect(cx, cy, inner, row), "Tools", title);
                cy += row;

                if (GUI.Button(new Rect(cx, cy, inner, btnH), "Selector"))
                    build.SelectTool();
                cy += btnH + 4f;

                if (GUI.Button(new Rect(cx, cy, inner, btnH), "Extend Lobby"))
                    build.SelectLobbyTool();
                cy += btnH + 4f;

                if (GUI.Button(new Rect(cx, cy, inner, btnH), "Bulldoze"))
                    build.SetTool(BuildTool.Bulldoze);
                cy += btnH + 6f;
            }

            if (hasSelection)
            {
                if (DrawSectionHeader(ref cy, cx, inner, btnH, section, "Selection", ref _selectionOpen))
                {
                    var selection = build.GetSelectionSummary();
                    if (selection != null)
                    {
                        foreach (var line in selection.Split('\n'))
                        {
                            GUI.Label(new Rect(cx, cy, inner, row), line, label);
                            cy += row;
                        }
                    }

                    foreach (var line in RoomEconomyFormat.SelectedUnitLines(
                                 build.SelectedRoom,
                                 agents?.Agents,
                                 simulation?.Economy))
                    {
                        GUI.Label(new Rect(cx, cy, inner, row), line, label);
                        cy += row;
                    }

                    if (PricePricing.IsPricedRoom(build.SelectedRoom?.Type))
                        cy = DrawPriceTierButtons(cx, cy, inner, btnH, row, label, stars);

                    var elevStatus = build.GetElevatorStatusText();
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

                    cy += 4f;
                }
            }

            _panelRect = new Rect(x, y, width, cy + 8f - y);
        }

        static bool DrawSectionHeader(
            ref float cy,
            float cx,
            float inner,
            float btnH,
            GUIStyle style,
            string name,
            ref bool open)
        {
            var arrow = open ? "▼" : "▶";
            if (GUI.Button(new Rect(cx, cy, inner, btnH), $"{arrow} {name}", style))
                open = !open;
            cy += btnH + 4f;
            return open;
        }

        float DrawNestedCatalog(
            float cx,
            float cy,
            float inner,
            float row,
            float btnH,
            float roomBtnH,
            GUIStyle roomButton,
            GUIStyle section,
            StarSystem stars)
        {
            foreach (var family in _catalog)
            {
                var expanded = _expandedFamily == family.Family;
                var arrow = expanded ? "▼" : "▶";
                if (GUI.Button(new Rect(cx, cy, inner, btnH), $"{arrow} {family.Label}", section))
                    _expandedFamily = expanded ? null : family.Family;
                cy += btnH + 4f;

                if (_expandedFamily != family.Family)
                    continue;

                if (family.Family == BuildFamily.Shops)
                {
                    foreach (var subgroup in family.Subgroups)
                    {
                        var subOpen = _expandedShopSubgroup == subgroup.Subgroup;
                        var subArrow = subOpen ? "▼" : "▶";
                        if (GUI.Button(
                                new Rect(cx + 8f, cy, inner - 8f, btnH),
                                $"{subArrow} {subgroup.Label}",
                                section))
                            _expandedShopSubgroup = subOpen ? null : subgroup.Subgroup;
                        cy += btnH + 4f;

                        if (_expandedShopSubgroup == subgroup.Subgroup)
                            cy = DrawRoomGrid(cx + 8f, cy, inner - 8f, roomBtnH, roomButton, subgroup.Rooms, stars);
                    }
                }
                else
                {
                    cy = DrawRoomGrid(cx + 8f, cy, inner - 8f, roomBtnH, roomButton, family.Rooms, stars);
                }
            }

            return cy;
        }

        float DrawRoomGrid(
            float cx,
            float cy,
            float inner,
            float roomBtnH,
            GUIStyle roomButton,
            List<RoomTypeSO> rooms,
            StarSystem stars)
        {
            var col = 0;
            var rowY = cy;
            foreach (var room in rooms)
            {
                if (room == null) continue;
                var captured = room;
                var nameText = ShortLabel(room.displayName);
                var bw = (inner - 4f) * 0.5f;
                var bx = cx + col * (bw + 4f);
                var canBuild = stars == null || stars.CanBuild(room);
                if (!canBuild)
                    nameText = $"{nameText} ({room.requiredStars}★)";
                var labelText = $"{nameText}\n{RoomEconomyFormat.ButtonTag(room)}";
                var wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && canBuild;
                if (GUI.Button(new Rect(bx, rowY, bw, roomBtnH), labelText, roomButton))
                    build.SetRoomType(captured);
                GUI.enabled = wasEnabled;

                col++;
                if (col >= 2)
                {
                    col = 0;
                    rowY += roomBtnH + 4f;
                }
            }

            if (col != 0) rowY += roomBtnH + 4f;
            return rowY + 4f;
        }

        float DrawPriceTierButtons(
            float cx,
            float cy,
            float inner,
            float btnH,
            float row,
            GUIStyle label,
            StarSystem stars)
        {
            var room = build.SelectedRoom;
            var currentStars = stars?.CurrentStars ?? 0;
            var climateOffset = simulation?.Climate?.ComfortTierOffset ?? 0;
            GUI.Label(new Rect(cx, cy, inner, row), "Price", label);
            cy += row;

            const float gap = 4f;
            var count = PricePricing.Labels.Length;
            var bw = (inner - gap * (count - 1)) / count;
            for (var i = 0; i < count; i++)
            {
                var tier = i;
                var active = room.PriceTier == tier;
                var rect = new Rect(cx + i * (bw + gap), cy, bw, btnH);
                if (GUI.Toggle(rect, active, PricePricing.Labels[i], GUI.skin.button) && !active)
                    build.TrySetSelectedPriceTier(tier);
            }

            cy += btnH + 2f;
            GUI.Label(
                new Rect(cx, cy, inner, row),
                PricePricing.MarketHint(room.PriceTier, currentStars, climateOffset),
                label);
            cy += row + 4f;
            return cy;
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

        static List<string> SelectedEconomyLines(RoomTypeSO type)
        {
            var lines = new List<string>();
            if (type == null) return lines;

            lines.Add(RoomEconomyFormat.CostLine(type));
            lines.Add(RoomEconomyFormat.IncomeLine(type));

            var upkeep = RoomEconomyFormat.UpkeepLine(type);
            if (upkeep != null)
                lines.Add(upkeep);

            return lines;
        }

        static string ShortLabel(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "Room";
            if (displayName.StartsWith("Hotel")) return "Hotel";
            if (displayName.StartsWith("Retail")) return "Retail";
            if (displayName.StartsWith("Stairs")) return "Stairs";
            if (displayName.StartsWith("Elevator")) return "Elevator";
            if (displayName.StartsWith("Office")) return displayName.Contains("Premium") ? "Prem. Office" : "Office";
            if (displayName.StartsWith("Condo")) return displayName.Contains("Premium") ? "Prem. Condo" : "Condo";
            return displayName;
        }
    }
}
