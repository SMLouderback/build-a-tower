using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Progressive IMGUI HUD: core strip + accordion sections + compact icon build grid.
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

        const float IconSize = 36f;
        const float IconGap = 4f;

        Rect _panelRect;
        Rect _topBarRect;
        Rect _goalsDropdownRect;
        Rect _infoDropdownRect;
        readonly List<RoomTypeSO> _roomButtons = new();
        List<BuildCatalogFamily> _catalog = new();

        enum TopInfoPanel
        {
            None,
            Shops,
            Elev,
            Tower
        }

        TopInfoPanel _infoPanel;
        bool _goalsOpen;
        bool _buildOpen = true;
        bool _selectionOpen = true;
        BuildFamily? _expandedFamily;
        BuildSubgroup? _expandedShopSubgroup;
        Vector2 _scroll;
        float _contentHeight = 400f;

        ResearchBranch _researchPickBranch = ResearchBranch.Marketing;
        int _researchPickLevel = 1;

        Texture2D _whiteTex;
        string _hoverTooltip;
        readonly TowerNewsHud _newsHud = new();

        public Rect PanelScreenRect => _panelRect;
        public Rect TopBarScreenRect => _topBarRect;

        /// <summary>True when the GUI point (IMGUI / flipped Y) is over the top bar, info/goals dropdown, or side panel.</summary>
        public bool ContainsGuiPoint(Vector2 guiPoint) =>
            _topBarRect.Contains(guiPoint) ||
            _panelRect.Contains(guiPoint) ||
            (_goalsOpen && _goalsDropdownRect.Contains(guiPoint)) ||
            (_infoPanel != TopInfoPanel.None && _infoDropdownRect.Contains(guiPoint)) ||
            _newsHud.ContainsGuiPoint(guiPoint);

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
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ShopFineDining"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/Housekeeping"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/Maintenance"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/SecurityPost"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ResearchLab"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/Conference"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/EventHall"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ParkingUnderground"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/Valet"));
            AddRoomButton(Resources.Load<RoomTypeSO>("Rooms/ParkingRamp"));
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

            _hoverTooltip = null;

            var gap = edgeGapPixels;
            const float row = 20f;
            const float btnH = 22f;

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
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            var iconStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(0, 0, 0, 0)
            };
            var barLabel = new GUIStyle(label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12
            };
            var barButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            var stars = simulation?.Stars;
            var agents = simulation?.Agents;
            var population = agents != null ? agents.Population : 0;
            var averageStress = agents != null ? agents.AverageStress : 0f;
            var goalsUnlocked = build.Grid != null && build.Grid.HasLobby;
            var economyUnlocked = simulation?.Economy != null && simulation.Economy.HasRecordedEconomyEvent;
            var hasSelection = build.SelectedRoom != null;

            var dayIndex = simulation?.Clock != null ? simulation.Clock.DayIndex : 0;
            var newsStripHeight = _newsHud.Draw(
                simulation?.News,
                dayIndex,
                gap,
                gap,
                barLabel,
                barButton);

            var topBarHeight = DrawTopInfoBar(
                gap,
                gap + newsStripHeight,
                barLabel,
                barButton,
                title,
                label,
                stars,
                agents,
                population,
                averageStress,
                goalsUnlocked,
                economyUnlocked);

            var x = gap;
            var y = gap + newsStripHeight + topBarHeight + 6f;
            var width = Mathf.Min(panelWidth, Screen.width - gap * 2f);
            var inner = Mathf.Max(80f, width - 16f);
            var maxPanelHeight = Mathf.Max(160f, Screen.height - y - gap);
            var panelHeight = Mathf.Clamp(_contentHeight + 16f, 160f, maxPanelHeight);

            var help = string.IsNullOrEmpty(build.HelpText) ? "—" : build.HelpText;
            var helpHeight = Mathf.Clamp(
                label.CalcHeight(new GUIContent(help), inner),
                row,
                row * 3f);

            _panelRect = new Rect(x, y, width, panelHeight);
            GUI.Box(_panelRect, GUIContent.none);

            var viewRect = new Rect(x + 4f, y + 4f, width - 8f, panelHeight - 8f);
            var contentWidth = inner;
            var contentRect = new Rect(0f, 0f, contentWidth - 12f, Mathf.Max(_contentHeight, viewRect.height));
            _scroll = GUI.BeginScrollView(viewRect, _scroll, contentRect, false, true);

            var cx = 4f;
            var cy = 0f;
            var contentInner = contentWidth - 20f;

            GUI.Label(new Rect(cx, cy, contentInner, row), "Build", title);
            cy += row;

            GUI.Label(new Rect(cx, cy, contentInner, helpHeight), help, label);
            cy += helpHeight + 4f;

            if (DrawSectionHeader(ref cy, cx, contentInner, btnH, section, "Catalog", ref _buildOpen))
            {
                var roomName = build.SelectedRoomType != null ? build.SelectedRoomType.displayName : "—";
                GUI.Label(new Rect(cx, cy, contentInner, row), $"Tool: {build.CurrentTool} / {roomName}", label);
                cy += row;

                foreach (var economyLine in SelectedEconomyLines(build.SelectedRoomType))
                {
                    GUI.Label(new Rect(cx, cy, contentInner, row), economyLine, label);
                    cy += row;
                }

                if (build.HoverCell.HasValue)
                {
                    var c = build.HoverCell.Value;
                    var floorLabel = c.y > 0 ? c.y.ToString() : c.y < 0 ? $"B{-c.y}" : "G";
                    GUI.Label(new Rect(cx, cy, contentInner, row), $"Cell: ({c.x}, floor {floorLabel})", label);
                }
                else
                {
                    GUI.Label(new Rect(cx, cy, contentInner, row), "Cell: —", label);
                }

                cy += row + 4f;

                cy = DrawIconCatalog(cx, cy, contentInner, row, iconStyle, stars);
                cy += 4f;

                GUI.Label(new Rect(cx, cy, contentInner, row), "Tools");
                cy += row;
                cy = DrawToolIcons(cx, cy, contentInner, iconStyle);
                cy += 6f;
            }

            if (hasSelection)
            {
                if (DrawSectionHeader(ref cy, cx, contentInner, btnH, section, "Selection", ref _selectionOpen))
                {
                    var selection = build.GetSelectionSummary();
                    if (selection != null)
                    {
                        foreach (var line in selection.Split('\n'))
                        {
                            GUI.Label(new Rect(cx, cy, contentInner, row), line, label);
                            cy += row;
                        }
                    }

                    foreach (var line in RoomEconomyFormat.SelectedUnitLines(
                                 build.SelectedRoom,
                                 agents?.Agents,
                                 simulation?.Economy))
                    {
                        GUI.Label(new Rect(cx, cy, contentInner, row), line, label);
                        cy += row;
                    }

                    foreach (var line in ConferenceSelectionLines(build.SelectedRoom))
                    {
                        GUI.Label(new Rect(cx, cy, contentInner, row), line, label);
                        cy += row;
                    }

                    if (PricePricing.IsPricedRoom(build.SelectedRoom?.Type))
                        cy = DrawPriceTierButtons(cx, cy, contentInner, btnH, row, label, stars);

                    if (BuildController.IsStaffedServiceRoom(build.SelectedRoom?.Type))
                        cy = DrawStaffStepper(cx, cy, contentInner, btnH, row, label);

                    if (build.SelectedRoom?.Type?.id == EconomySystem.ResearchId)
                        cy = DrawResearchSelection(cx, cy, contentInner, btnH, row, label);

                    var elevStatus = build.GetElevatorStatusText();
                    if (elevStatus != null)
                    {
                        GUI.Label(new Rect(cx, cy, contentInner, row), $"Elevator: {elevStatus}", label);
                        cy += row;
                        var simElev = simulation?.Elevators?.FindByRoomId(build.SelectedRoom.InstanceId);
                        if (simElev != null)
                        {
                            foreach (var line in ElevatorTrafficLines(simElev))
                            {
                                GUI.Label(new Rect(cx, cy, contentInner, row), line, label);
                                cy += row;
                            }
                        }

                        var inMaint = simElev != null && simElev.InMaintenance;
                        var maintLabel = inMaint ? "Exit Maintenance" : "Enter Maintenance";
                        if (GUI.Button(new Rect(cx, cy, contentInner, btnH), maintLabel))
                            build.TrySetSelectedElevatorMaintenance(!inMaint);
                        cy += btnH;
                    }

                    cy += 4f;
                }
            }

            _contentHeight = cy + 8f;
            GUI.EndScrollView();

            DrawHoverTooltip(label);
        }

        /// <summary>
        /// Full-width status strip. Returns fixed bar height only — Info/Goals dropdowns overlay
        /// below the bar and must not push the left build panel down.
        /// </summary>
        float DrawTopInfoBar(
            float gap,
            float barTopY,
            GUIStyle barLabel,
            GUIStyle barButton,
            GUIStyle title,
            GUIStyle wrapLabel,
            StarSystem stars,
            AgentSystem agents,
            int population,
            float averageStress,
            bool goalsUnlocked,
            bool economyUnlocked)
        {
            const float barH = 36f;
            const float pad = 8f;
            var barWidth = Screen.width - gap * 2f;
            _topBarRect = new Rect(gap, barTopY, barWidth, barH);
            _goalsDropdownRect = Rect.zero;
            _infoDropdownRect = Rect.zero;
            GUI.Box(_topBarRect, GUIContent.none);

            var x = gap + pad;
            var y = barTopY + 6f;
            var lineH = 24f;
            var right = gap + barWidth - pad;

            void DrawChip(string text, float width)
            {
                GUI.Label(new Rect(x, y, width, lineH), text, barLabel);
                x += width + 10f;
            }

            GUI.Label(new Rect(x, y, 100f, lineH), "Build-A-Tower", title);
            x += 108f;

            var economy = simulation?.Economy;
            if (economyUnlocked && economy != null)
            {
                DrawChip($"Save ${build.Wallet.Balance:N0}", 118f);
                DrawChip($"+${economy.LastIncome:N0}", 88f);
                DrawChip($"-${economy.LastExpense:N0}", 88f);
                DrawChip($"Avg ${economy.AverageDailyProfit:N0}/d", 110f);
            }
            else
            {
                DrawChip($"Save ${build.Wallet.Balance:N0}", 118f);
            }

            // Temporary tower-wide chips while a shop or elevator is selected.
            var selected = build?.SelectedRoom;
            var selectedType = selected?.Type;
            if (economyUnlocked && economy != null && selectedType != null &&
                selectedType.ResolvedBuildFamily() == BuildFamily.Shops)
            {
                DrawChip($"Shops yday {economy.LastShopVisitsYesterday}", 108f);
                DrawChip($"Shops ~{economy.AverageShopVisitsLast7Days:0.#}/d", 100f);
            }
            else if (economyUnlocked && selectedType != null && selectedType.isElevatorShaft)
            {
                var elev = simulation?.Elevators;
                if (elev != null)
                {
                    DrawChip($"El yday {elev.PassengersYesterday}", 90f);
                    DrawChip($"El ~{elev.AveragePassengersLast7Days:0.#}/d", 90f);
                    DrawChip($"Wait yday {elev.AvgWaitYesterday:0.#}m", 110f);
                    DrawChip($"Wait ~{elev.AverageWaitLast7Days:0.#}m", 100f);
                }
            }

            x = DrawStarTrack(x, y, lineH, stars != null ? stars.CurrentStars : 0);

            var clockText = simulation?.Clock != null ? simulation.Clock.FormatHud() : "—";
            DrawChip(clockText, 150f);

            var climateName = simulation?.Climate?.Name ?? "—";
            DrawChip(climateName, 78f);

            // Reserve space for right-cluster Info/Goals buttons.
            var clusterW = 0f;
            if (economyUnlocked) clusterW += 64f + 8f + 56f + 8f;
            if (goalsUnlocked) clusterW += 64f + 8f + 72f;
            else if (economyUnlocked) clusterW = Mathf.Max(0f, clusterW - 8f);

            var speedWidth = 236f;
            if (x + speedWidth < right - clusterW - 12f)
            {
                DrawTimeSpeedButtons(x, y, speedWidth, lineH);
                x += speedWidth + 10f;
            }

            DrawTopInfoButtons(
                right,
                y,
                lineH,
                gap,
                barTopY,
                barH,
                barWidth,
                barButton,
                wrapLabel,
                stars,
                agents,
                population,
                averageStress,
                goalsUnlocked,
                economyUnlocked);

            return barH;
        }

        void DrawTopInfoButtons(
            float right,
            float y,
            float lineH,
            float gap,
            float barTopY,
            float barH,
            float barWidth,
            GUIStyle barButton,
            GUIStyle wrapLabel,
            StarSystem stars,
            AgentSystem agents,
            int population,
            float averageStress,
            bool goalsUnlocked,
            bool economyUnlocked)
        {
            const float shopsW = 64f;
            const float elevW = 56f;
            const float towerW = 64f;
            const float goalsW = 72f;
            const float btnGap = 8f;

            var cursor = right;
            if (goalsUnlocked)
            {
                cursor -= goalsW;
                var goalsRect = new Rect(cursor, y, goalsW, lineH);
                var goalsArrow = _goalsOpen ? "▼" : "▶";
                if (GUI.Button(goalsRect, $"{goalsArrow} Goals", barButton))
                    _goalsOpen = !_goalsOpen;
                cursor -= btnGap;
            }

            if (goalsUnlocked)
            {
                cursor -= towerW;
                var towerRect = new Rect(cursor, y, towerW, lineH);
                var towerOpen = _infoPanel == TopInfoPanel.Tower;
                var towerArrow = towerOpen ? "▼" : "▶";
                if (GUI.Button(towerRect, $"{towerArrow} Tower", barButton))
                    _infoPanel = towerOpen ? TopInfoPanel.None : TopInfoPanel.Tower;
                cursor -= btnGap;
            }

            if (economyUnlocked)
            {
                cursor -= elevW;
                var elevRect = new Rect(cursor, y, elevW, lineH);
                var elevOpen = _infoPanel == TopInfoPanel.Elev;
                var elevArrow = elevOpen ? "▼" : "▶";
                if (GUI.Button(elevRect, $"{elevArrow} Elev", barButton))
                    _infoPanel = elevOpen ? TopInfoPanel.None : TopInfoPanel.Elev;
                cursor -= btnGap;

                cursor -= shopsW;
                var shopsRect = new Rect(cursor, y, shopsW, lineH);
                var shopsOpen = _infoPanel == TopInfoPanel.Shops;
                var shopsArrow = shopsOpen ? "▼" : "▶";
                if (GUI.Button(shopsRect, $"{shopsArrow} Shops", barButton))
                    _infoPanel = shopsOpen ? TopInfoPanel.None : TopInfoPanel.Shops;
            }

            if (!economyUnlocked && _infoPanel is TopInfoPanel.Shops or TopInfoPanel.Elev)
                _infoPanel = TopInfoPanel.None;
            if (!goalsUnlocked && _infoPanel == TopInfoPanel.Tower)
                _infoPanel = TopInfoPanel.None;

            if (_infoPanel != TopInfoPanel.None)
                DrawInfoDropdown(right, gap, barTopY, barH, barWidth, wrapLabel, agents, population, averageStress);

            if (_goalsOpen && goalsUnlocked)
            {
                var goalLines = stars != null
                    ? stars.FormatNextStarGoal(build.Grid, averageStress, population).Split('\n')
                    : new[] { "Next ★: —" };
                var dropW = Mathf.Min(320f, barWidth);
                var dropH = 8f + goalLines.Length * 18f + 8f;
                // Offset Goals panel left when an Info dropdown is also open.
                var goalsX = right - dropW;
                if (_infoPanel != TopInfoPanel.None)
                    goalsX = Mathf.Max(gap, goalsX - dropW - 8f);
                _goalsDropdownRect = new Rect(goalsX, barTopY + barH, dropW, dropH);
                GUI.Box(_goalsDropdownRect, GUIContent.none);
                var gy = _goalsDropdownRect.y + 6f;
                foreach (var goalLine in goalLines)
                {
                    GUI.Label(
                        new Rect(_goalsDropdownRect.x + 8f, gy, dropW - 16f, 18f),
                        goalLine,
                        wrapLabel);
                    gy += 18f;
                }
            }
        }

        void DrawInfoDropdown(
            float right,
            float gap,
            float barTopY,
            float barH,
            float barWidth,
            GUIStyle wrapLabel,
            AgentSystem agents,
            int population,
            float averageStress)
        {
            var lines = new List<string>();
            switch (_infoPanel)
            {
                case TopInfoPanel.Shops:
                {
                    var economy = simulation?.Economy;
                    if (economy != null)
                    {
                        lines.Add($"Shops yday {economy.LastShopVisitsYesterday}");
                        lines.Add($"Shops ~{economy.AverageShopVisitsLast7Days:0.#}/d");
                    }
                    break;
                }
                case TopInfoPanel.Elev:
                {
                    var elev = simulation?.Elevators;
                    if (elev != null)
                    {
                        lines.Add($"El yday {elev.PassengersYesterday}");
                        lines.Add($"El ~{elev.AveragePassengersLast7Days:0.#}/d");
                        lines.Add($"Wait yday {elev.AvgWaitYesterday:0.#}m");
                        lines.Add($"Wait ~{elev.AverageWaitLast7Days:0.#}m");
                    }
                    break;
                }
                case TopInfoPanel.Tower:
                {
                    var pop = agents != null ? agents.Population : population;
                    var stress = agents != null ? agents.AverageStress : averageStress;
                    lines.Add($"Pop {pop}");
                    lines.Add($"Stress {stress:0}");
                    lines.Add($"Crime {simulation?.Crime?.DisplayCrime ?? 0f:0}");
                    if (agents?.Agents != null)
                    {
                        var inTower = 0;
                        var outside = 0;
                        foreach (var agent in agents.Agents)
                        {
                            if (agent == null || agent.Role != AgentRole.CondoResident || !agent.HasMovedIn)
                                continue;
                            if (agent.JobKind == CondoJobKind.InTower) inTower++;
                            else if (agent.JobKind == CondoJobKind.Outside) outside++;
                        }

                        if (inTower + outside > 0)
                            lines.Add($"Condo jobs: {inTower} in-tower / {outside} outside");
                    }
                    break;
                }
            }

            if (lines.Count == 0)
            {
                _infoPanel = TopInfoPanel.None;
                return;
            }

            var dropW = Mathf.Min(280f, barWidth);
            var dropH = 8f + lines.Count * 18f + 8f;
            _infoDropdownRect = new Rect(right - dropW, barTopY + barH, dropW, dropH);
            GUI.Box(_infoDropdownRect, GUIContent.none);
            var ly = _infoDropdownRect.y + 6f;
            foreach (var line in lines)
            {
                GUI.Label(
                    new Rect(_infoDropdownRect.x + 8f, ly, dropW - 16f, 18f),
                    line,
                    wrapLabel);
                ly += 18f;
            }
        }

        /// <summary>
        /// Draws all <see cref="StarSystem.StarSlots"/> stars: grey until earned, gold when earned.
        /// Returns the next x after the track (+ trailing gap).
        /// </summary>
        static float DrawStarTrack(float x, float y, float lineH, int earnedStars)
        {
            const float starW = 16f;
            var gold = new Color(1f, 0.84f, 0.2f, 1f);
            var grey = new Color(0.42f, 0.42f, 0.42f, 1f);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            var filled = Mathf.Clamp(earnedStars, 0, StarSystem.StarSlots);
            for (var i = 1; i <= StarSystem.StarSlots; i++)
            {
                style.normal.textColor = i <= filled ? gold : grey;
                GUI.Label(new Rect(x, y - 1f, starW, lineH), "★", style);
                x += starW;
            }

            return x + 10f;
        }

        void DrawHoverTooltip(GUIStyle label)
        {
            var tip = string.IsNullOrEmpty(_hoverTooltip) ? GUI.tooltip : _hoverTooltip;
            if (string.IsNullOrEmpty(tip)) return;

            var mouse = Event.current.mousePosition;
            var width = 220f;
            var height = label.CalcHeight(new GUIContent(tip), width - 12f) + 10f;
            var tipX = Mathf.Min(mouse.x + 14f, Screen.width - width - 8f);
            var tipY = Mathf.Min(mouse.y + 18f, Screen.height - height - 8f);
            var rect = new Rect(tipX, tipY, width, height);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f), tip, label);
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

        float DrawIconCatalog(
            float cx,
            float cy,
            float inner,
            float row,
            GUIStyle iconStyle,
            StarSystem stars)
        {
            GUI.Label(new Rect(cx, cy, inner, row), "Categories");
            cy += row;

            cy = DrawIconRow(
                cx,
                cy,
                inner,
                _catalog.Count,
                i =>
                {
                    var family = _catalog[i];
                    var selected = _expandedFamily == family.Family;
                    var tip = $"{family.Label}\nClick to {(selected ? "collapse" : "expand")}";
                    if (DrawIconButton(
                            IconRect(cx, cy, inner, i),
                            FamilyGlyph(family.Family),
                            tip,
                            FamilyColor(family.Family),
                            selected,
                            enabled: true,
                            iconStyle))
                    {
                        _expandedFamily = selected ? null : family.Family;
                        if (_expandedFamily != BuildFamily.Shops)
                            _expandedShopSubgroup = null;
                    }
                });

            if (!_expandedFamily.HasValue)
                return cy;

            BuildCatalogFamily active = null;
            foreach (var family in _catalog)
            {
                if (family.Family == _expandedFamily)
                {
                    active = family;
                    break;
                }
            }

            if (active == null)
                return cy;

            GUI.Label(new Rect(cx, cy, inner, row), active.Label);
            cy += row;

            if (active.Family == BuildFamily.Shops)
            {
                cy = DrawIconRow(
                    cx,
                    cy,
                    inner,
                    active.Subgroups.Count,
                    i =>
                    {
                        var subgroup = active.Subgroups[i];
                        var selected = _expandedShopSubgroup == subgroup.Subgroup;
                        var tip = $"{active.Label} → {subgroup.Label}";
                        if (DrawIconButton(
                                IconRect(cx, cy, inner, i),
                                SubgroupGlyph(subgroup.Subgroup),
                                tip,
                                FamilyColor(BuildFamily.Shops),
                                selected,
                                enabled: true,
                                iconStyle))
                            _expandedShopSubgroup = selected ? null : subgroup.Subgroup;
                    });

                if (_expandedShopSubgroup.HasValue)
                {
                    foreach (var subgroup in active.Subgroups)
                    {
                        if (subgroup.Subgroup != _expandedShopSubgroup) continue;
                        GUI.Label(new Rect(cx, cy, inner, row), subgroup.Label);
                        cy += row;
                        cy = DrawRoomIconGrid(cx, cy, inner, iconStyle, subgroup.Rooms, stars);
                        break;
                    }
                }
            }
            else
            {
                cy = DrawRoomIconGrid(cx, cy, inner, iconStyle, active.Rooms, stars);
            }

            return cy;
        }

        float DrawRoomIconGrid(
            float cx,
            float cy,
            float inner,
            GUIStyle iconStyle,
            List<RoomTypeSO> rooms,
            StarSystem stars)
        {
            var count = 0;
            foreach (var room in rooms)
            {
                if (room != null) count++;
            }

            return DrawIconRow(
                cx,
                cy,
                inner,
                count,
                i =>
                {
                    RoomTypeSO room = null;
                    var n = 0;
                    foreach (var candidate in rooms)
                    {
                        if (candidate == null) continue;
                        if (n == i)
                        {
                            room = candidate;
                            break;
                        }

                        n++;
                    }

                    if (room == null) return;

                    var canBuild = stars == null || stars.CanBuild(room);
                    var tip = RoomTooltip(room, canBuild);
                    var color = room.placeholderColor;
                    if (!canBuild)
                        color = Color.Lerp(color, Color.gray, 0.55f);

                    var wasEnabled = GUI.enabled;
                    GUI.enabled = wasEnabled && canBuild;
                    if (DrawIconButton(
                            IconRect(cx, cy, inner, i),
                            RoomGlyph(room),
                            tip,
                            color,
                            selected: build.SelectedRoomType == room &&
                                      build.CurrentTool == BuildTool.PlaceRoom,
                            enabled: canBuild,
                            iconStyle))
                        build.SetRoomType(room);
                    GUI.enabled = wasEnabled;
                });
        }

        float DrawToolIcons(float cx, float cy, float inner, GUIStyle iconStyle)
        {
            var tools = new (string glyph, string tip, System.Action onClick, bool selected, Color color)[]
            {
                ("Sel", "Selector\nClick rooms on the tower to inspect them.",
                    () => build.SelectTool(),
                    build.CurrentTool == BuildTool.Select,
                    new Color(0.55f, 0.55f, 0.6f)),
                ("Lob", "Extend Lobby\nDrag to widen the lobby on floor G.",
                    () => build.SelectLobbyTool(),
                    build.CurrentTool == BuildTool.PlaceRoom &&
                    build.SelectedRoomType != null &&
                    build.SelectedRoomType.isLobby,
                    new Color(0.75f, 0.65f, 0.35f)),
                ("X", "Bulldoze\nDemolish a non-lobby room (grace refund if eligible).",
                    () => build.SetTool(BuildTool.Bulldoze),
                    build.CurrentTool == BuildTool.Bulldoze,
                    new Color(0.75f, 0.3f, 0.28f))
            };

            return DrawIconRow(
                cx,
                cy,
                inner,
                tools.Length,
                i =>
                {
                    var tool = tools[i];
                    if (DrawIconButton(
                            IconRect(cx, cy, inner, i),
                            tool.glyph,
                            tool.tip,
                            tool.color,
                            tool.selected,
                            enabled: true,
                            iconStyle))
                        tool.onClick();
                });
        }

        float DrawIconRow(
            float cx,
            float cy,
            float inner,
            int count,
            System.Action<int> drawIndex)
        {
            if (count <= 0) return cy;
            var cols = Mathf.Max(1, Mathf.FloorToInt((inner + IconGap) / (IconSize + IconGap)));
            for (var i = 0; i < count; i++)
                drawIndex(i);

            var rows = Mathf.CeilToInt(count / (float)cols);
            return cy + rows * (IconSize + IconGap) + 4f;
        }

        static Rect IconRect(float cx, float cy, float inner, int index)
        {
            var cols = Mathf.Max(1, Mathf.FloorToInt((inner + IconGap) / (IconSize + IconGap)));
            var col = index % cols;
            var row = index / cols;
            return new Rect(
                cx + col * (IconSize + IconGap),
                cy + row * (IconSize + IconGap),
                IconSize,
                IconSize);
        }

        bool DrawIconButton(
            Rect rect,
            string glyph,
            string tooltip,
            Color color,
            bool selected,
            bool enabled,
            GUIStyle style)
        {
            EnsureWhiteTex();
            var prevBg = GUI.backgroundColor;
            var prevContent = GUI.contentColor;

            var fill = color;
            fill.a = enabled ? 0.85f : 0.35f;
            if (selected)
                fill = Color.Lerp(fill, Color.white, 0.25f);

            GUI.DrawTexture(rect, _whiteTex, ScaleMode.StretchToFill, false, 0f, fill, 0f, 0f);
            if (selected)
            {
                var outline = new Color(1f, 0.9f, 0.4f, 1f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
                GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
            }

            GUI.backgroundColor = new Color(1f, 1f, 1f, 0.15f);
            GUI.contentColor = Luminance(color) > 0.55f ? Color.black : Color.white;
            var clicked = GUI.Button(rect, new GUIContent(glyph, tooltip), style);
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;

            if (rect.Contains(Event.current.mousePosition) && !string.IsNullOrEmpty(tooltip))
                _hoverTooltip = tooltip;

            return clicked;
        }

        void EnsureWhiteTex()
        {
            if (_whiteTex != null) return;
            _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
            _whiteTex.hideFlags = HideFlags.HideAndDontSave;
        }

        static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        static string RoomTooltip(RoomTypeSO room, bool canBuild)
        {
            var lines = $"{room.displayName}";
            if (!canBuild)
                lines += $"\nLocked — needs {room.requiredStars}★";
            lines += $"\n{RoomEconomyFormat.CostLine(room)}";
            lines += $"\n{RoomEconomyFormat.IncomeLine(room)}";
            var upkeep = RoomEconomyFormat.UpkeepLine(room);
            if (upkeep != null)
                lines += $"\n{upkeep}";
            lines += $"\nSize {room.size.x}×{room.size.y}";
            return lines;
        }

        static string FamilyGlyph(BuildFamily family) => family switch
        {
            BuildFamily.Office => "Of",
            BuildFamily.Hotel => "Ht",
            BuildFamily.Condo => "Co",
            BuildFamily.Shops => "Sh",
            BuildFamily.Utility => "Ut",
            BuildFamily.Transit => "Tr",
            _ => "?"
        };

        static string SubgroupGlyph(BuildSubgroup subgroup) => subgroup switch
        {
            BuildSubgroup.Food => "Fd",
            BuildSubgroup.Retail => "Rt",
            _ => "?"
        };

        static Color FamilyColor(BuildFamily family) => family switch
        {
            BuildFamily.Office => new Color(0.35f, 0.55f, 0.85f),
            BuildFamily.Hotel => new Color(0.62f, 0.35f, 0.85f),
            BuildFamily.Condo => new Color(0.35f, 0.75f, 0.45f),
            BuildFamily.Shops => new Color(0.9f, 0.6f, 0.25f),
            BuildFamily.Utility => new Color(0.45f, 0.7f, 0.75f),
            BuildFamily.Transit => new Color(0.7f, 0.7f, 0.35f),
            _ => Color.gray
        };

        static string RoomGlyph(RoomTypeSO room)
        {
            if (room == null) return "?";
            if (room.isElevatorShaft) return "El";
            if (room.isStairs) return "St";
            if (room.isParkingRamp) return "Rm";
            if (!string.IsNullOrEmpty(room.id))
            {
                if (room.id.Contains("premium")) return "P" + FamilyGlyph(room.ResolvedBuildFamily())[0];
                if (room.id.Contains("housekeeping")) return "Hk";
                if (room.id.Contains("maintenance")) return "Mn";
                if (room.id.Contains("security")) return "Sc";
                if (room.id.Contains("restaurant")) return "Rn";
                if (room.id.Contains("research")) return "Lb";
                if (room.id.Contains("conference")) return "Cf";
                if (room.id.Contains("fine")) return "Fn";
                if (room.id.Contains("fast")) return "FF";
                if (room.id.Contains("retail")) return "Rt";
                if (room.id.Contains("parking_ramp") || room.id == ParkingStalls.RampId) return "Rm";
                if (room.id.Contains("parking")) return "Pk";
                if (room.id.Contains("valet")) return "Va";
            }

            var shortName = ShortLabel(room.displayName);
            if (shortName.Length <= 2) return shortName;
            if (shortName.StartsWith("Prem")) return "P" + shortName[shortName.Length - 1];
            return shortName.Substring(0, 2);
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

        static IEnumerable<string> ElevatorTrafficLines(ElevatorShaftRuntime shaft)
        {
            if (shaft == null) yield break;
            yield return $"Passengers today: {shaft.PassengersToday} (avg wait {shaft.AvgWaitToday:0.#}m)";
            yield return $"Passengers yesterday: {shaft.PassengersYesterday} (avg wait {shaft.AvgWaitYesterday:0.#}m)";
            yield return $"Avg passengers (7d): {shaft.AveragePassengersLast7Days:0.#}";
            yield return $"Avg wait (7d): {shaft.AverageWaitLast7Days:0.#}m";
        }

        IEnumerable<string> ConferenceSelectionLines(RoomInstance room)
        {
            if (room?.Type == null)
                yield break;

            var id = room.Type.id;
            if (id != ConferenceSystem.ConferenceId && id != ConferenceSystem.EventHallId)
                yield break;

            var conference = simulation?.Conference;
            if (conference == null)
                yield break;

            if (id == ConferenceSystem.ConferenceId)
            {
                var officeWorkers = EconomySystem.CountOfficeWorkers(simulation?.Agents?.Agents);
                var stars = simulation?.Stars?.CurrentStars ?? 0;
                var climateMult = simulation?.Climate?.SpendMultiplier ?? 1f;
                var estimate = conference.ComputeDailyMeetingsForHall(
                    room,
                    build.Grid,
                    officeWorkers,
                    stars,
                    climateMult);
                yield return $"Est. daily meetings: ${estimate:N0}";
                yield return $"Office workers counted: {officeWorkers}";
            }

            if (room.Dirty || room.CleanWorkRemaining > 0f)
            {
                yield return $"Needs cleaning: ~{room.CleanWorkRemaining:0} maid-min left";
            }

            if (room.RepairJobsRemaining > 0)
            {
                var mins = room.RepairJobMinutes > 0f
                    ? room.RepairJobMinutes
                    : RoomConditionRules.RepairMinutesPerChunk;
                yield return room.RepairJobsRemaining > 1
                    ? $"Needs repair: {room.RepairJobsRemaining} shifts × {mins:0}m"
                    : $"Needs repair: 1 handyman × {mins:0}m";
            }

            if (id == ConferenceSystem.EventHallId)
            {
                yield return $"Open hours: 8:00–22:00";
                var capacity = room.Type.eventCapacity > 0
                    ? room.Type.eventCapacity
                    : room.Size.x * room.Size.y * 5;
                var hotelGuests = 0;
                if (simulation?.Agents?.Agents != null)
                {
                    foreach (var agent in simulation.Agents.Agents)
                    {
                        if (agent != null &&
                            agent.Role == AgentRole.HotelGuest &&
                            agent.Phase != AgentPhase.Outside)
                            hotelGuests++;
                    }
                }

                var stars = simulation?.Stars?.CurrentStars ?? 0;
                var climateMult = simulation?.Climate?.SpendMultiplier ?? 1f;
                var estLump = ConferenceSystem.MajorEventLumpPayout(
                    hotelGuests,
                    stars,
                    capacity,
                    climateMult);
                yield return $"Est. event booking (if hosted): ${estLump:N0}";
                if (conference.Active?.Phase == MajorEventPhase.Live && conference.IsHallBooked(room))
                {
                    yield return $"Live event credit (start): ${conference.LiveLumpPayout:N0}";
                    var daily = Mathf.RoundToInt(
                        conference.LiveLumpPayout * ConferenceSystem.EventDailyWhileLiveMult);
                    yield return $"While live (+/day after start): ${daily:N0}";
                }
            }

            if (conference.IsHallBooked(room))
            {
                var active = conference.Active;
                var name = string.IsNullOrEmpty(active?.Name) ? "Event" : active.Name;
                var endDay = active != null ? active.EndDayIndex : -1;
                yield return endDay >= 0
                    ? $"Booked: {name} through day {endDay}"
                    : $"Booked: {name}";
            }
        }

        float DrawStaffStepper(
            float cx,
            float cy,
            float inner,
            float btnH,
            float row,
            GUIStyle label)
        {
            var room = build.SelectedRoom;
            if (room == null) return cy;

            GUI.Label(new Rect(cx, cy, inner, row), $"Staff ({room.StaffedWorkers}/4)", label);
            cy += row;

            const float gap = 4f;
            const int maxStaff = 4;
            var bw = (inner - gap * maxStaff) / (maxStaff + 1);
            for (var i = 0; i <= maxStaff; i++)
            {
                var count = i;
                var active = room.StaffedWorkers == count;
                var rect = new Rect(cx + i * (bw + gap), cy, bw, btnH);
                if (GUI.Toggle(rect, active, count.ToString(), GUI.skin.button) && !active)
                    build.TrySetStaffedWorkers(count);
            }

            cy += btnH + 4f;
            return cy;
        }

        float DrawResearchSelection(
            float cx,
            float cy,
            float inner,
            float btnH,
            float row,
            GUIStyle label)
        {
            var research = simulation?.Research;
            if (research == null || build.Grid == null)
                return cy;

            SyncResearchPick(research);

            var pool = EconomySystem.CountResearcherPool(build.Grid);
            var labs = EconomySystem.CountNonBrokenResearchLabs(build.Grid);
            var climate = simulation.Climate;
            var climateName = climate?.Name ?? "—";
            var climateMult = climate?.SpendMultiplier ?? 1f;
            var idleDay = ResearchCatalog.IdlePerLabPerDay * labs;
            var activeDay = research.IsRunning && !research.IsPaused
                ? ResearchCatalog.ActivePerDay
                : 0;

            GUI.Label(new Rect(cx, cy, inner, row), $"Researchers in pool: {pool}", label);
            cy += row;

            var branches = (ResearchBranch[])System.Enum.GetValues(typeof(ResearchBranch));
            const float levelGap = 3f;
            var nameW = Mathf.Min(118f, inner * 0.42f);
            var levelW = (inner - nameW - levelGap * 2f) / 3f;

            foreach (var branch in branches)
            {
                GUI.Label(
                    new Rect(cx, cy, nameW, btnH),
                    ResearchCatalog.BranchDisplayName(branch),
                    label);

                for (var level = 1; level <= ResearchCatalog.MaxLevel; level++)
                {
                    var rect = new Rect(
                        cx + nameW + (level - 1) * (levelW + levelGap),
                        cy,
                        levelW,
                        btnH);
                    var picked = _researchPickBranch == branch && _researchPickLevel == level;
                    var caption = ResearchLevelCaption(research, branch, level);
                    var wasEnabled = GUI.enabled;
                    var locked = !research.IsComplete(branch, level) && !research.CanStart(branch, level);
                    GUI.enabled = wasEnabled && !locked;
                    if (GUI.Toggle(rect, picked, caption, GUI.skin.button) && !picked && !locked)
                    {
                        _researchPickBranch = branch;
                        _researchPickLevel = level;
                    }

                    GUI.enabled = wasEnabled;
                }

                cy += btnH + 2f;
            }

            cy += 2f;
            var pickBranch = _researchPickBranch;
            var pickLevel = _researchPickLevel;
            var pickComplete = research.IsComplete(pickBranch, pickLevel);
            var isPickActive = research.ActiveBranch == pickBranch && research.ActiveLevel == pickLevel;
            var canStart = research.CanStart(pickBranch, pickLevel);

            var effect = ResearchCatalog.LevelEffectSummary(pickBranch, pickLevel);
            if (!string.IsNullOrEmpty(effect))
            {
                var effectH = row * 2.2f;
                GUI.Label(
                    new Rect(cx, cy, inner, effectH),
                    $"Effect: {effect}",
                    label);
                cy += effectH + 2f;
            }

            const float actionGap = 4f;
            var actionW = (inner - actionGap) * 0.5f;
            var startEnabled = canStart && !(isPickActive && !research.IsPaused);
            var pauseEnabled = isPickActive && !research.IsPaused;

            var prev = GUI.enabled;
            GUI.enabled = prev && startEnabled;
            if (GUI.Button(new Rect(cx, cy, actionW, btnH), "Start") && startEnabled)
                research.TryStart(pickBranch, pickLevel);
            GUI.enabled = prev && pauseEnabled;
            if (GUI.Button(new Rect(cx + actionW + actionGap, cy, actionW, btnH), "Pause") && pauseEnabled)
                research.Pause();
            GUI.enabled = prev;
            cy += btnH + 4f;

            if (pickComplete)
            {
                GUI.Label(new Rect(cx, cy, inner, row), "Selected tech: complete ✓", label);
                cy += row;
            }
            else
            {
                var eta = research.EstimateEtaMinutes(pickBranch, pickLevel, pool);
                var est = research.EstimateRemainingCost(
                    pickBranch, pickLevel, pool, labs, climateMult);
                GUI.Label(new Rect(cx, cy, inner, row), $"ETA: {FormatResearchEta(eta)}", label);
                cy += row;
                GUI.Label(new Rect(cx, cy, inner, row), $"Est. remaining $: ${est:N0}", label);
                cy += row;
            }

            GUI.Label(new Rect(cx, cy, inner, row), $"Idle/day: ${idleDay:N0}", label);
            cy += row;
            GUI.Label(new Rect(cx, cy, inner, row), $"Active/day: ${activeDay:N0}", label);
            cy += row;
            GUI.Label(
                new Rect(cx, cy, inner, row),
                $"Climate: {climateName} ×{climateMult:0.00}",
                label);
            cy += row;

            if (research.IsRunning && research.IsPaused)
            {
                GUI.Label(new Rect(cx, cy, inner, row), "Paused — progress decaying", label);
                cy += row;
            }

            GUI.Label(
                new Rect(cx, cy, inner, row * 2f),
                "Estimate at current climate & staff; actual burn changes if climate shifts.",
                label);
            cy += row * 2f + 4f;
            return cy;
        }

        void SyncResearchPick(ResearchSystem research)
        {
            var pickOk =
                research.IsComplete(_researchPickBranch, _researchPickLevel) ||
                research.CanStart(_researchPickBranch, _researchPickLevel) ||
                (research.ActiveBranch == _researchPickBranch &&
                 research.ActiveLevel == _researchPickLevel);
            if (pickOk)
                return;

            if (research.IsRunning && research.ActiveBranch.HasValue)
            {
                _researchPickBranch = research.ActiveBranch.Value;
                _researchPickLevel = research.ActiveLevel;
                return;
            }

            foreach (ResearchBranch branch in System.Enum.GetValues(typeof(ResearchBranch)))
            {
                for (var level = 1; level <= ResearchCatalog.MaxLevel; level++)
                {
                    if (!research.CanStart(branch, level)) continue;
                    _researchPickBranch = branch;
                    _researchPickLevel = level;
                    return;
                }
            }
        }

        static string ResearchLevelCaption(ResearchSystem research, ResearchBranch branch, int level)
        {
            var roman = level switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                _ => level.ToString()
            };

            if (research.IsComplete(branch, level))
                return $"{roman} ✓";
            if (!research.CanStart(branch, level))
                return $"{roman} locked";

            var pct = research.GetProgressPercent(branch, level);
            if (pct > 0.05f ||
                (research.ActiveBranch == branch && research.ActiveLevel == level))
                return $"{roman} {pct:0}%";
            return roman;
        }

        static string FormatResearchEta(float etaMinutes)
        {
            if (float.IsInfinity(etaMinutes))
                return "∞ (need researchers)";
            if (etaMinutes <= 0f)
                return "—";

            var totalMinutes = Mathf.CeilToInt(etaMinutes);
            var days = totalMinutes / (24 * 60);
            var hours = totalMinutes % (24 * 60) / 60;
            var mins = totalMinutes % 60;
            if (days > 0)
                return $"{days}d {hours}h";
            if (hours > 0)
                return $"{hours}h {mins}m";
            return $"{mins}m";
        }

        void DrawTimeSpeedButtons(float x, float y, float width, float height)
        {
            if (simulation?.Clock == null) return;

            var labels = new[] { "||", "1x", "2x", "5x", "10x", "60x" };
            var speeds = new[] { 0f, 1f, 2f, 5f, 10f, 60f };
            const float gap = 3f;
            // Weight later buttons slightly wider so "10x" / "60x" are not clipped.
            var weights = new[] { 0.85f, 0.9f, 0.9f, 0.9f, 1.2f, 1.25f };
            var weightSum = 0f;
            foreach (var w in weights)
                weightSum += w;
            var unit = (width - gap * (labels.Length - 1)) / weightSum;
            var clock = simulation.Clock;
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(1, 1, 1, 1)
            };

            var cursor = x;
            for (var i = 0; i < labels.Length; i++)
            {
                var active = i == 0
                    ? clock.Paused
                    : !clock.Paused && Mathf.Approximately(clock.MinutesPerRealSecond, speeds[i]);
                var buttonWidth = unit * weights[i];
                var rect = new Rect(cursor, y, buttonWidth, height);
                if (GUI.Toggle(rect, active, labels[i], style) && !active)
                    simulation.SetSpeedPreset(speeds[i], paused: i == 0);
                cursor += buttonWidth + gap;
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
