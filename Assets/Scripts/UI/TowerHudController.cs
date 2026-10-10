using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public enum BuildMenuTool
    {
        Select,
        Lobby,
        SkyLobby,
        Scaffold,
        Bulldoze
    }

    /// <summary>
    /// Progressive IMGUI HUD: core strip + accordion sections + pictorial 2-column build menu.
    /// Keep the Game tab Scale at 1x (or Scale to Fit). Zoom &gt; 1x crops the HUD.
    /// </summary>
    public sealed class TowerHudController : MonoBehaviour
    {
        [SerializeField] BuildController build;
        [SerializeField] TowerSimulation simulation;
        [SerializeField] StarCelebrationController celebration;
        [SerializeField] List<RoomTypeSO> placeableRooms = new();
        [SerializeField] RoomTypeSO stairsRoom;
        [SerializeField] RoomTypeSO elevatorRoom;
        [SerializeField] RoomTypeSO expressElevatorRoom;
        [SerializeField] RoomTypeSO serviceElevatorRoom;

        public IEnumerable<RoomTypeSO> EnumerateKnownRoomTypes()
        {
            if (placeableRooms != null)
            {
                foreach (var room in placeableRooms)
                    yield return room;
            }

            yield return stairsRoom;
            yield return elevatorRoom;
            yield return expressElevatorRoom;
            yield return serviceElevatorRoom;
        }

        [SerializeField] float edgeGapPixels = 12f;

        public const float MenuIconSize = 44f;
        public const float MenuIconGap = 4f;
        public const int MenuStripColumns = 2;

        const float IconSize = MenuIconSize;
        const float IconGap = MenuIconGap;
        const int BuildToolIconCount = 5;

        /// <summary>Legacy single-variant Office / Hotel / Condo replaced by luxury catalog.</summary>
        static readonly HashSet<string> LegacyMenuRoomIds = new(StringComparer.Ordinal)
        {
            "office",
            "hotel",
            "hotel_single",
            "condo",
        };

        static bool IsLegacyMenuRoom(RoomTypeSO room) =>
            room != null && LegacyMenuRoomIds.Contains(room.id);

        public static string MenuIconIdForFamily(BuildFamily family) => family switch
        {
            BuildFamily.Office => "family_office",
            BuildFamily.Hotel => "family_hotel",
            BuildFamily.Condo => "family_condo",
            BuildFamily.Shops => "family_shops",
            BuildFamily.Leisure => "family_leisure",
            BuildFamily.Utility => "family_utility",
            BuildFamily.Transit => "family_transit",
            _ => null
        };

        public static string MenuIconIdForSubgroup(BuildSubgroup subgroup) => subgroup switch
        {
            BuildSubgroup.Food => "subgroup_food",
            BuildSubgroup.Retail => "subgroup_retail",
            _ => null
        };

        public static string MenuIconIdForRoom(RoomTypeSO room)
        {
            if (room == null || string.IsNullOrEmpty(room.id))
                return string.Empty;
            if (room.id == "metro_station")
                return "transit_metro";
            return room.id;
        }

        public static string MenuIconIdForTool(BuildMenuTool tool) => tool switch
        {
            BuildMenuTool.Select => "tool_select",
            BuildMenuTool.Lobby => "tool_lobby",
            BuildMenuTool.SkyLobby => "tool_sky_lobby",
            BuildMenuTool.Scaffold => "tool_scaffold",
            BuildMenuTool.Bulldoze => "tool_bulldoze",
            _ => null
        };

        public static Rect MenuStripIconRect(float cx, float cy, int index) =>
            IconRect(cx, cy, index, MenuStripColumns);

        const string BuildDockPrefsKey = "bat.buildDock";
        const string BuildInfoPrefsKey = "bat.buildInfo";
        const float BuildDockGripHeight = 24f;
        const float BuildPanelPad = 8f;
        const float BuildPopoutGap = 4f;

        Rect _panelRect;
        Rect _dockRect;
        Rect _infoRect;
        Rect _popoutRect;
        Rect _topBarRect;
        Rect _goalsDropdownRect;
        Rect _infoDropdownRect;
        Rect _mapsDropdownRect;
        Rect _researchDropdownRect;
        Rect _mapsGraphRect;
        Rect _mapsLegendRect;
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
        bool _mapsOpen;
        bool _researchOpen;
        BuildFamily? _expandedFamily;
        BuildSubgroup? _expandedShopSubgroup;
        bool _buildLayoutInitialized;
        bool _dockDragging;
        bool _infoDragging;
        Vector2 _dockDragOffset;
        Vector2 _infoDragOffset;
        Vector2 _infoScroll;
        float _lastBuildInfoTopY = 64f;
        TowerMapController _mapController;

        // Maps Graph metric toggles (shared chart).
        bool _graphShowClimate = true;
        bool _graphShowSpend;
        bool _graphShowVacancy;
        bool _graphShowPopulation = true;
        bool _graphShowIncome = true;
        bool _graphShowLosses = true;
        bool _graphShowSavings = true;
        bool _graphShowStars = true;

        ResearchBranch _researchPickBranch = ResearchBranch.Marketing;
        int _researchPickLevel = 1;

        Texture2D _whiteTex;
        string _hoverTooltip;
        readonly TowerNewsHud _newsHud = new();

        enum PauseUiState
        {
            Playing,
            Paused,
            Options,
            Account,
            Feedback,
            Load,
            ConfirmQuit
        }

        const string TowerSceneName = "TowerSandbox";
        const string FeedbackFacebookUrl = "https://www.facebook.com/EScapeMProd";

        PauseUiState _pauseUi = PauseUiState.Playing;
        float _speedBeforePause = 1f;
        bool _clockPausedBeforeMenu;
        PauseSaveService _pauseSave;
        LocalSaveRepository _localSaves;
        bool _gridDirtyBound;
        string _pauseSaveMessage;
        string _pauseLoadMessage;
        string _pauseAccountMessage;
        string _pauseFeedbackNote;
        string _pauseFeedbackName;
        string _pauseFeedbackEmail;
        string _pauseFeedbackStatus;
        Vector2 _pauseLoadScroll;
        LocalSaveMenuPresenter _pauseLoadPresenter;
        IReadOnlyList<LocalSaveSummary> _pauseLoadSummaries;
        IReadOnlyList<CloudSlotSummary> _pauseCloudSlots = Array.Empty<CloudSlotSummary>();
        ICloudSlotSource _cloudSlots;
        Func<SyncStatus> _syncStatusProvider;
        Action _loadTowerSceneOverride;
        PlaytestFeedbackClient _feedbackClient;

        public Rect PanelScreenRect => _panelRect;
        public Rect TopBarScreenRect => _topBarRect;

        /// <summary>True when the Esc pause / quit confirm overlay is open.</summary>
        public bool IsEscPauseOpen => _pauseUi != PauseUiState.Playing;

        public bool IsLocalSaveDirty => PauseSaveOrNull?.IsDirty ?? true;

        public string LeaveTowerConfirmMessage =>
            PauseSaveOrNull?.QuitWarning ?? PauseSaveService.DirtyQuitWarning;

        public void ConfigureLocalSave(PauseSaveService pauseSave)
        {
            _pauseSave = pauseSave ?? throw new ArgumentNullException(nameof(pauseSave));
            _pauseSave.SyncFromSession();
            SubscribeGridDirty();
        }

        public void ConfigurePauseLoad(
            LocalSaveMenuPresenter presenter,
            LocalSaveRepository repository = null,
            Action loadTowerScene = null)
        {
            _pauseLoadPresenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            if (_cloudSlots != null)
                _pauseLoadPresenter.ConfigureCloudSlots(_cloudSlots);
            if (repository != null)
                _localSaves = repository;
            if (loadTowerScene != null)
                _loadTowerSceneOverride = loadTowerScene;
        }

        public void ConfigureCloudSlots(ICloudSlotSource cloudSlots)
        {
            _cloudSlots = cloudSlots;
            if (_pauseLoadPresenter != null)
                _pauseLoadPresenter.ConfigureCloudSlots(cloudSlots);
        }

        public void ConfigureSyncStatus(Func<SyncStatus> syncStatusProvider)
        {
            _syncStatusProvider = syncStatusProvider;
        }

        public void ConfigureFeedback(PlaytestFeedbackClient feedbackClient)
        {
            _feedbackClient = feedbackClient;
        }

        public IReadOnlyList<CloudSlotSummary> PauseCloudSlots => _pauseCloudSlots;
        public string PauseFeedbackStatusText => _pauseFeedbackStatus;

        public async System.Threading.Tasks.Task<IReadOnlyList<CloudSlotSummary>> RefreshPauseCloudSlots()
        {
            EnsurePauseLoadPresenter();
            _pauseCloudSlots = await _pauseLoadPresenter.RefreshCloudSlots();
            return _pauseCloudSlots;
        }

        public string SaveFromPause()
        {
            var service = EnsurePauseSave();
            if (service == null)
            {
                GameSession.MarkSaveDirty();
                _pauseSaveMessage = "The tower is not ready to save.";
                return _pauseSaveMessage;
            }

            _pauseSaveMessage = service.Save();
            return _pauseSaveMessage;
        }

        public void BindPauseSave(SaveCoordinator coordinator, LocalSaveRepository repository = null)
        {
            if (repository != null)
                _localSaves = repository;
            ConfigureLocalSave(new PauseSaveService(coordinator));
        }

        public bool TryLoadPausedTower(string saveId, out string errorMessage)
        {
            EnsurePauseLoadPresenter();
            return _pauseLoadPresenter.TryPrepareLoad(saveId, out errorMessage);
        }

        public string SavePausedTower() => SaveFromPause();

        public bool IsLatestLocalSaveComplete => !IsLocalSaveDirty;

        public string PauseQuitWarningText => LeaveTowerConfirmMessage;

        public bool HasRestoreFallbackNotice => GameSession.HasRestoreFallbackNotice;

        public string RestoreFallbackNoticeText => GameSession.RestoreFallbackNotice;

        public void OpenPauseFeedbackForTests() => OpenPauseFeedback();

        public void SetPauseFeedbackDraftForTests(string note, string name = null, string email = null)
        {
            _pauseFeedbackNote = note;
            _pauseFeedbackName = name;
            _pauseFeedbackEmail = email;
        }

        public void DismissRestoreFallbackNotice()
        {
            GameSession.ConsumeRestoreFallbackNotice();
        }

        /// <summary>When true, world build input should be ignored.</summary>
        public bool BlocksWorldInput
        {
            get
            {
                var celeb = ResolveCelebration();
                return IsEscPauseOpen
                       || GameSession.HasRestoreFallbackNotice
                       || (celeb != null && celeb.IsActive);
            }
        }

        /// <summary>Wired by <see cref="TowerSimulation"/> when it creates the controller at runtime.</summary>
        public void BindCelebration(StarCelebrationController controller)
        {
            if (controller != null)
                celebration = controller;
        }

        /// <summary>True when the GUI point (IMGUI / flipped Y) is over the top bar, info/goals/maps dropdown, graph, or side panel.</summary>
        public bool ContainsGuiPoint(Vector2 guiPoint) =>
            GameSession.HasRestoreFallbackNotice ||
            _topBarRect.Contains(guiPoint) ||
            _dockRect.Contains(guiPoint) ||
            _infoRect.Contains(guiPoint) ||
            (_expandedFamily.HasValue && _popoutRect.Contains(guiPoint)) ||
            (_goalsOpen && _goalsDropdownRect.Contains(guiPoint)) ||
            (_infoPanel != TopInfoPanel.None && _infoDropdownRect.Contains(guiPoint)) ||
            (_mapsOpen && _mapsDropdownRect.Contains(guiPoint)) ||
            (_researchOpen && _researchDropdownRect.Contains(guiPoint)) ||
            (_mapsGraphRect.width > 0f && _mapsGraphRect.Contains(guiPoint)) ||
            (_mapsLegendRect.width > 0f && _mapsLegendRect.Contains(guiPoint)) ||
            _newsHud.ContainsGuiPoint(guiPoint);

        void Awake()
        {
            // Domain-reload off can keep stale keyed icons; always rebuild on play.
            MenuIconArt.ResetCache();
            SeasonHudArt.ResetCache();
            if (simulation == null && build != null)
                simulation = build.GetComponent<TowerSimulation>();
            ResolveCelebration();
            EnsureElevatorAndCatalog();
            GameSession.EnsureDefault();
            SubscribeGridDirty();
        }

        void OnEnable()
        {
            SubscribeGridDirty();
            _buildLayoutInitialized = false;
        }

        void OnDisable()
        {
            if (build != null && _gridDirtyBound)
            {
                build.GridChanged -= OnGridChangedForSave;
                _gridDirtyBound = false;
            }
            _dockDragging = false;
            _infoDragging = false;
        }

        public void ResetBuildMenuLayout()
        {
            HudFloatingPanel.ClearRect(BuildDockPrefsKey);
            HudFloatingPanel.ClearRect(BuildInfoPrefsKey);
            RestoreDefaultBuildMenuLayout();
            _expandedFamily = null;
            _expandedShopSubgroup = null;
        }

        void EnsureBuildMenuLayout(float infoTopY)
        {
            _lastBuildInfoTopY = infoTopY;
            if (_buildLayoutInitialized)
                return;

            _dockRect = HudFloatingPanel.TryLoadRect(BuildDockPrefsKey, out var dock)
                ? HudFloatingPanel.SoftClamp(dock, Screen.width, Screen.height)
                : DefaultDockRect();
            _infoRect = HudFloatingPanel.TryLoadRect(BuildInfoPrefsKey, out var info)
                ? HudFloatingPanel.SoftClamp(info, Screen.width, Screen.height)
                : DefaultInfoRect();
            _panelRect = _dockRect;
            _buildLayoutInitialized = true;
        }

        void RestoreDefaultBuildMenuLayout()
        {
            _dockRect = DefaultDockRect();
            _infoRect = DefaultInfoRect();
            _popoutRect = Rect.zero;
            _panelRect = _dockRect;
            _buildLayoutInitialized = true;
        }

        Rect DefaultDockRect() =>
            BuildMenuLayoutDefaults.DefaultDock(
                Screen.width,
                Screen.height,
                BuildToolIconCount,
                _catalog.Count,
                edgeGapPixels);

        void FitDockHeightToContent()
        {
            var needed = BuildMenuLayoutDefaults.DockContentHeight(BuildToolIconCount, _catalog.Count);
            if (_dockRect.height + 0.5f >= needed)
                return;
            _dockRect.height = needed;
            _dockRect = HudFloatingPanel.SoftClamp(_dockRect, Screen.width, Screen.height);
            _panelRect = _dockRect;
        }

        Rect DefaultInfoRect() =>
            BuildMenuLayoutDefaults.DefaultInfo(
                Screen.width,
                Screen.height,
                _lastBuildInfoTopY,
                edgeGapPixels);

        void SubscribeGridDirty()
        {
            if (_gridDirtyBound || build == null) return;
            build.GridChanged += OnGridChangedForSave;
            _gridDirtyBound = true;
        }

        void OnGridChangedForSave()
        {
            if (GameSession.PendingLoad != null) return;
            GameSession.MarkSaveDirty();
            EnsurePauseSave()?.MarkDirty();
        }

        PauseSaveService PauseSaveOrNull => _pauseSave;

        PauseSaveService EnsurePauseSave()
        {
            if (_pauseSave != null)
            {
                SubscribeGridDirty();
                return _pauseSave;
            }

            if (build == null || simulation == null)
                return null;

            _pauseSave = new PauseSaveService(
                new SaveCoordinator(_localSaves ??= LocalSaveRepository.CreateDefault(), build, simulation));
            SubscribeGridDirty();
            return _pauseSave;
        }

        void EnsurePauseLoadPresenter()
        {
            if (_pauseLoadPresenter != null) return;
            var repository = _localSaves ?? LocalSaveRepository.CreateDefault();
            _localSaves = repository;
            _pauseLoadPresenter = new LocalSaveMenuPresenter(
                repository,
                new SaveCoordinator(repository),
                _loadTowerSceneOverride
                ?? (() => UnityEngine.SceneManagement.SceneManager.LoadScene(TowerSceneName)),
                TimeZoneInfo.Local);
            if (_cloudSlots != null)
                _pauseLoadPresenter.ConfigureCloudSlots(_cloudSlots);
        }

        void Update()
        {
            if (build == null) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // Continue-only while celebration modal is showing; still allow Esc to close a
            // pre-existing pause menu when celebrations are only queued and waiting.
            var celeb = ResolveCelebration();
            if (celeb != null && celeb.IsModalOpen)
                return;

            HandlePauseEscape();
        }

        void HandlePauseEscape()
        {
            if (_pauseUi == PauseUiState.ConfirmQuit
                || _pauseUi == PauseUiState.Options
                || _pauseUi == PauseUiState.Load
                || _pauseUi == PauseUiState.Account
                || _pauseUi == PauseUiState.Feedback)
            {
                _pauseLoadMessage = null;
                _pauseAccountMessage = null;
                _pauseUi = PauseUiState.Paused;
                return;
            }

            if (_pauseUi == PauseUiState.Paused)
            {
                ResumeFromPause();
                return;
            }

            EnterPause();
        }

        void EnsureElevatorAndCatalog()
        {
            if (stairsRoom == null)
                stairsRoom = Resources.Load<RoomTypeSO>("Rooms/Stairs");
            if (elevatorRoom == null)
                elevatorRoom = Resources.Load<RoomTypeSO>("Rooms/ElevatorNormal");
            if (expressElevatorRoom == null)
                expressElevatorRoom = Resources.Load<RoomTypeSO>("Rooms/ElevatorExpress");
            if (serviceElevatorRoom == null)
                serviceElevatorRoom = Resources.Load<RoomTypeSO>("Rooms/ElevatorService");
            expressElevatorRoom ??= RoomTypeSO.CreateRuntimeElevator(
                "elevator_express", "Express Elevator", ElevatorShaftKind.Express, requiredStars: 3, buildCost: 12000);
            serviceElevatorRoom ??= RoomTypeSO.CreateRuntimeElevator(
                "elevator_service", "Service Elevator", ElevatorShaftKind.Service, requiredStars: 4, buildCost: 9000);
            if (serviceElevatorRoom != null)
                serviceElevatorRoom.allowBasement = true;

            _roomButtons.Clear();
            CollectMenuRoomButtons(
                _roomButtons,
                placeableRooms,
                stairsRoom,
                elevatorRoom,
                expressElevatorRoom,
                serviceElevatorRoom);
            _catalog = BuildCatalog.Group(_roomButtons);
        }

        /// <summary>Builds the HUD menu catalog the same way as <see cref="EnsureElevatorAndCatalog"/>.</summary>
        public static List<BuildCatalogFamily> BuildMenuCatalogForTests(IEnumerable<RoomTypeSO> scenePlaceableRooms)
        {
            var stairs = Resources.Load<RoomTypeSO>("Rooms/Stairs");
            var elevator = Resources.Load<RoomTypeSO>("Rooms/ElevatorNormal");
            var express = Resources.Load<RoomTypeSO>("Rooms/ElevatorExpress") ?? RoomTypeSO.CreateRuntimeElevator(
                "elevator_express", "Express Elevator", ElevatorShaftKind.Express, requiredStars: 3, buildCost: 12000);
            var service = Resources.Load<RoomTypeSO>("Rooms/ElevatorService") ?? RoomTypeSO.CreateRuntimeElevator(
                "elevator_service", "Service Elevator", ElevatorShaftKind.Service, requiredStars: 4, buildCost: 9000);
            var buttons = new List<RoomTypeSO>();
            CollectMenuRoomButtons(buttons, scenePlaceableRooms, stairs, elevator, express, service);
            return BuildCatalog.Group(buttons);
        }

        static void CollectMenuRoomButtons(
            List<RoomTypeSO> buttons,
            IEnumerable<RoomTypeSO> scenePlaceableRooms,
            RoomTypeSO stairs,
            RoomTypeSO elevator,
            RoomTypeSO expressElevator,
            RoomTypeSO serviceElevator)
        {
            foreach (var room in scenePlaceableRooms)
            {
                if (IsLegacyMenuRoom(room)) continue;
                TryAddRoomButton(buttons, room);
            }

            if (stairs != null && !buttons.Contains(stairs))
            {
                buttons.RemoveAll(r => r != null && r.id == "stairs");
                buttons.Add(stairs);
            }

            if (elevator != null && !buttons.Contains(elevator))
            {
                buttons.RemoveAll(r => r != null && r.id == "elevator_normal");
                buttons.Add(elevator);
            }

            if (expressElevator != null && !buttons.Contains(expressElevator))
            {
                buttons.RemoveAll(r => r != null && r.id == "elevator_express");
                buttons.Add(expressElevator);
            }

            if (serviceElevator != null && !buttons.Contains(serviceElevator))
            {
                buttons.RemoveAll(r => r != null && r.id == "elevator_service");
                buttons.Add(serviceElevator);
            }

            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/CondoStudio"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/CondoAlcove"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/CondoBase"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/CondoMidStandard"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/CondoMidLoft"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/CondoMidFamily"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/CondoUpperStandard"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/CondoUpperCorner"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/CondoUpperPenthouse"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/HotelBase"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/HotelAccessible"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/HotelMidStandard"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/HotelMidExtended"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/HotelStudio"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/HotelJuniorSuite"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/HotelUpperStandard"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/HotelUpperKing"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/HotelUpperSuite"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/OfficeMicro"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/OfficeStudio"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/OfficeBase"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/OfficeMidStandard"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/OfficeMidClinic"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/OfficeMidTeam"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/OfficeUpperStandard"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/OfficeUpperCorner"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/OfficeUpperFloor"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ShopFastFood"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ShopRestaurant"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ShopRetail"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ShopFineDining"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ShopTacoCounter"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ShopChickenShack"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ShopMexicanRestaurant"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ShopGagGifts"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ShopShoeStore"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ShopDepartmentStore"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureGym"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureSpa"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisurePool"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureBowling"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureTheater"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureCasino"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureNightclub"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureChapel"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureCathedral"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureAtrium"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/Housekeeping"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/Maintenance"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ServiceMail"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ServiceRecycling"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ServiceLoadingDock"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/SecurityPost"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ResearchLab"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/Conference"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/EventHall"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ParkingUnderground"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/MetroStation"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/Valet"));
            TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/ParkingRamp"));
        }

        static void TryAddRoomButton(List<RoomTypeSO> buttons, RoomTypeSO room)
        {
            if (room != null && !room.isLobby && !IsLegacyMenuRoom(room) && !buttons.Contains(room))
                buttons.Add(room);
        }

        void AddRoomButton(RoomTypeSO room) => TryAddRoomButton(_roomButtons, room);

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

            var buildInfoTopY = gap + newsStripHeight + topBarHeight + 6f;
            EnsureBuildMenuLayout(buildInfoTopY);
            DrawBuildDock(iconStyle, title, label);
            DrawFamilyPopout(iconStyle, title, label, stars);
            DrawBuildInfoPanel(title, label, stars, agents, row, btnH);

            DrawHoverTooltip(label);
            DrawRestoreFallbackOverlay(title, label);
            DrawPauseOverlay(title, label);
        }

        void EnterPause()
        {
            if (_pauseUi != PauseUiState.Playing) return;
            // Continue-only while celebration modal owns pause; skip Esc pause overlay.
            if (ResolveCelebration()?.IsModalOpen == true) return;
            if (simulation?.Clock != null)
            {
                _speedBeforePause = simulation.Clock.MinutesPerRealSecond;
                _clockPausedBeforeMenu = simulation.Clock.Paused;
            }
            else
            {
                _speedBeforePause = 1f;
                _clockPausedBeforeMenu = false;
            }

            simulation?.SetSpeedPreset(_speedBeforePause, paused: true);
            _pauseSaveMessage = null;
            _pauseUi = PauseUiState.Paused;
        }

        void ResumeFromPause()
        {
            if (_clockPausedBeforeMenu)
                simulation?.SetSpeedPreset(_speedBeforePause, paused: true);
            else
                simulation?.SetSpeedPreset(Mathf.Max(0.01f, _speedBeforePause), paused: false);
            _pauseSaveMessage = null;
            _pauseUi = PauseUiState.Playing;
        }

        void ReturnToMainMenu()
        {
            ClearMapsMode();
            _pauseSaveMessage = null;
            _pauseUi = PauseUiState.Playing;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        void ClearMapsMode()
        {
            _mapsOpen = false;
            var maps = EnsureMapController();
            if (maps != null && maps.Mode != TowerMapMode.Off)
                maps.SetMode(TowerMapMode.Off);
            _mapsGraphRect = Rect.zero;
            _mapsLegendRect = Rect.zero;
            _mapsDropdownRect = Rect.zero;
        }

        TowerMapController EnsureMapController()
        {
            if (_mapController != null) return _mapController;
            if (build != null)
                _mapController = build.GetComponent<TowerMapController>();
            if (_mapController == null)
                _mapController = FindAnyObjectByType<TowerMapController>();
            return _mapController;
        }

        /// <summary>
        /// Lazy-resolve so a controller added after HUD Awake (e.g. sibling/child via
        /// <c>EnsureCelebrationController</c>) is still found for Esc gating / BlocksWorldInput.
        /// </summary>
        StarCelebrationController ResolveCelebration()
        {
            if (celebration != null) return celebration;
            if (simulation != null)
                celebration = simulation.GetComponent<StarCelebrationController>();
            if (celebration == null && build != null)
                celebration = build.GetComponent<StarCelebrationController>();
            if (celebration == null)
                celebration = GetComponent<StarCelebrationController>() ??
                              FindAnyObjectByType<StarCelebrationController>();
            return celebration;
        }

        void DrawRestoreFallbackOverlay(GUIStyle title, GUIStyle label)
        {
            if (!GameSession.HasRestoreFallbackNotice) return;

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            const float panelW = 420f;
            const float panelH = 200f;
            var panel = new Rect(
                (Screen.width - panelW) * 0.5f,
                (Screen.height - panelH) * 0.5f,
                panelW,
                panelH);
            GUI.Box(panel, GUIContent.none);

            var cx = panel.x + 20f;
            var cy = panel.y + 16f;
            var inner = panelW - 40f;
            GUI.Label(new Rect(cx, cy, inner, 28f), "Could not restore save", title);
            cy += 36f;
            GUI.Label(new Rect(cx, cy, inner, 72f), GameSession.RestoreFallbackNotice, label);
            cy += 84f;
            if (GUI.Button(new Rect(cx, cy, inner, 32f), "OK"))
                DismissRestoreFallbackNotice();
        }

        void DrawPauseOverlay(GUIStyle title, GUIStyle label)
        {
            if (_pauseUi == PauseUiState.Playing) return;
            if (GameSession.HasRestoreFallbackNotice) return;

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            var panelW = _pauseUi == PauseUiState.Load || _pauseUi == PauseUiState.Feedback ? 440f : 360f;
            var hasSaveMessage = _pauseUi == PauseUiState.Paused && !string.IsNullOrEmpty(_pauseSaveMessage);
            float panelH = _pauseUi switch
            {
                PauseUiState.ConfirmQuit => 184f,
                PauseUiState.Options => 360f,
                PauseUiState.Account => 260f,
                PauseUiState.Feedback => 440f,
                PauseUiState.Load => 360f,
                _ => hasSaveMessage ? 488f : 456f
            };
            var panel = new Rect(
                (Screen.width - panelW) * 0.5f,
                (Screen.height - panelH) * 0.5f,
                panelW,
                panelH);
            GUI.Box(panel, GUIContent.none);

            var cx = panel.x + 20f;
            var cy = panel.y + 16f;
            var inner = panelW - 40f;
            const float btnH = 32f;

            if (_pauseUi == PauseUiState.ConfirmQuit)
            {
                GUI.Label(new Rect(cx, cy, inner, 48f), LeaveTowerConfirmMessage, label);
                cy += 56f;
                if (GUI.Button(new Rect(cx, cy, (inner - 8f) * 0.5f, btnH), "Yes"))
                    ReturnToMainMenu();
                if (GUI.Button(new Rect(cx + (inner - 8f) * 0.5f + 8f, cy, (inner - 8f) * 0.5f, btnH), "No"))
                    _pauseUi = PauseUiState.Paused;
                return;
            }

            if (_pauseUi == PauseUiState.Options)
            {
                DrawPauseOptions(cx, cy, inner, btnH, title, label);
                return;
            }

            if (_pauseUi == PauseUiState.Load)
            {
                DrawPauseLoad(cx, cy, inner, btnH, title, label);
                return;
            }

            if (_pauseUi == PauseUiState.Account)
            {
                DrawPauseAccount(cx, cy, inner, btnH, title, label);
                return;
            }

            if (_pauseUi == PauseUiState.Feedback)
            {
                DrawPauseFeedback(cx, cy, inner, btnH, title, label);
                return;
            }

            GUI.Label(new Rect(cx, cy, inner, 28f), "Paused", title);
            cy += 36f;
            GUI.Label(new Rect(cx, cy, inner, 24f), "Cloud: " + CurrentSyncStatusText(), label);
            cy += 28f;
            if (hasSaveMessage)
            {
                GUI.Label(new Rect(cx, cy, inner, 24f), _pauseSaveMessage, label);
                cy += 28f;
            }

            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Save Game"))
                SavePausedTower();
            cy += btnH + 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Load Game"))
                OpenPauseLoad();
            cy += btnH + 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Account"))
                _pauseUi = PauseUiState.Account;
            cy += btnH + 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Feedback"))
                OpenPauseFeedback();
            cy += btnH + 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Resume"))
                ResumeFromPause();
            cy += btnH + 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Options"))
                _pauseUi = PauseUiState.Options;
            cy += btnH + 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Reset layout"))
                ResetBuildMenuLayout();
            cy += btnH + 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Main Menu"))
                _pauseUi = PauseUiState.ConfirmQuit;
        }

        void OpenPauseFeedback()
        {
            _pauseFeedbackStatus = string.Empty;
            _pauseUi = PauseUiState.Feedback;
        }

        void OpenPauseLoad()
        {
            _pauseLoadMessage = null;
            _pauseLoadScroll = Vector2.zero;
            EnsurePauseLoadPresenter();
            _pauseLoadSummaries = _pauseLoadPresenter?.RefreshLocalSaves()
                                  ?? Array.Empty<LocalSaveSummary>();
            _ = RefreshPauseCloudSlots();
            _pauseUi = PauseUiState.Load;
        }

        void DrawPauseLoad(float cx, float cy, float inner, float btnH, GUIStyle title, GUIStyle label)
        {
            GUI.Label(new Rect(cx, cy, inner, 28f), "Load Game", title);
            cy += 36f;

            if (!string.IsNullOrEmpty(_pauseLoadMessage))
            {
                GUI.Label(new Rect(cx, cy, inner, 40f), _pauseLoadMessage, label);
                cy += 44f;
            }

            var listH = 180f;
            var listRect = new Rect(cx, cy, inner, listH);
            var summaries = _pauseLoadSummaries ?? Array.Empty<LocalSaveSummary>();
            var cloudSlots = _pauseCloudSlots ?? Array.Empty<CloudSlotSummary>();
            var visibleCloudCount = 0;
            for (var i = 0; i < cloudSlots.Count; i++)
                if (cloudSlots[i] != null && cloudSlots[i].Occupied)
                    visibleCloudCount++;
            var contentH = Mathf.Max(listH, (summaries.Count + visibleCloudCount) * 56f + 8f);
            _pauseLoadScroll = GUI.BeginScrollView(
                listRect,
                _pauseLoadScroll,
                new Rect(0f, 0f, inner - 20f, contentH));
            var rowY = 4f;
            if (summaries.Count == 0 && visibleCloudCount == 0)
            {
                GUI.Label(new Rect(0f, rowY, inner - 24f, 24f), "No local saves found.", label);
            }
            else
            {
                for (var i = 0; i < summaries.Count; i++)
                {
                    var row = _pauseLoadPresenter.PresentRow(summaries[i]);
                    var rowRect = new Rect(0f, rowY, inner - 24f, 48f);
                    var caption = string.IsNullOrEmpty(row.MetaText)
                        ? row.TowerName + "  " + row.StatusMessage
                        : row.TowerName + "  " + row.MetaText;
                    if (row.CanLoadCurrent)
                    {
                        if (GUI.Button(rowRect, caption))
                        {
                            if (_pauseLoadPresenter.TryPrepareLoad(row.SaveId, out var error))
                                return;
                            _pauseLoadMessage = error;
                        }
                    }
                    else
                    {
                        GUI.Label(rowRect, caption, label);
                    }

                    rowY += 52f;
                }

                for (var i = 0; i < cloudSlots.Count; i++)
                {
                    var slot = cloudSlots[i];
                    if (slot == null || !slot.Occupied)
                        continue;

                    var row = _pauseLoadPresenter.PresentCloudRow(slot);
                    var caption = row.TowerName + "  " + row.MetaText;
                    if (GUI.Button(new Rect(0f, rowY, inner - 24f, 48f), caption))
                    {
                        _pauseLoadMessage = "Cloud slot "
                                            + slot.SlotId
                                            + " will replace the current tower after loading. Save locally first if needed.";
                    }

                    rowY += 52f;
                }
            }

            GUI.EndScrollView();
            cy += listH + 12f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Back"))
            {
                _pauseLoadMessage = null;
                _pauseUi = PauseUiState.Paused;
            }
        }

        void DrawPauseAccount(float cx, float cy, float inner, float btnH, GUIStyle title, GUIStyle label)
        {
            GUI.Label(new Rect(cx, cy, inner, 28f), "Account", title);
            cy += 36f;
            var signedIn = !string.IsNullOrWhiteSpace(GameSession.CurrentAccountId);
            var account = signedIn
                ? "Signed in. Cloud slots are available after email verification."
                : "Sign in from the main menu to use three cloud save slots.";
            GUI.Label(new Rect(cx, cy, inner, 48f), account, label);
            cy += 56f;
            GUI.Label(new Rect(cx, cy, inner, 28f), "Cloud: " + CurrentSyncStatusText(), label);
            cy += 36f;
            if (!string.IsNullOrEmpty(_pauseAccountMessage))
            {
                GUI.Label(new Rect(cx, cy, inner, 36f), _pauseAccountMessage, label);
                cy += 44f;
            }

            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Back"))
            {
                _pauseAccountMessage = null;
                _pauseUi = PauseUiState.Paused;
            }
        }

        void DrawPauseFeedback(float cx, float cy, float inner, float btnH, GUIStyle title, GUIStyle label)
        {
            GUI.Label(new Rect(cx, cy, inner, 28f), "Feedback", title);
            cy += 36f;

            GUI.Label(new Rect(cx, cy, inner, 20f), "Note", label);
            cy += 22f;
            _pauseFeedbackNote = GUI.TextArea(new Rect(cx, cy, inner, 84f), _pauseFeedbackNote ?? string.Empty);
            cy += 92f;

            GUI.Label(new Rect(cx, cy, 56f, 24f), "Name", label);
            _pauseFeedbackName = GUI.TextField(new Rect(cx + 64f, cy, inner - 64f, 24f), _pauseFeedbackName ?? string.Empty);
            cy += 32f;

            GUI.Label(new Rect(cx, cy, 56f, 24f), "Email", label);
            _pauseFeedbackEmail = GUI.TextField(new Rect(cx + 64f, cy, inner - 64f, 24f), _pauseFeedbackEmail ?? string.Empty);
            cy += 30f;

            GUI.Label(new Rect(cx, cy, inner, 36f), "Optional — only if you want a reply.", label);
            cy += 40f;

            if (!string.IsNullOrEmpty(_pauseFeedbackStatus))
            {
                GUI.Label(new Rect(cx, cy, inner, 24f), _pauseFeedbackStatus, label);
                cy += 32f;
            }

            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Send"))
                _ = TrySendPauseFeedback();
            cy += btnH + 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Message on Facebook"))
                Application.OpenURL(FeedbackFacebookUrl);
            cy += btnH + 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Back"))
                _pauseUi = PauseUiState.Paused;
        }

        public async System.Threading.Tasks.Task TrySendPauseFeedback(
            System.Threading.CancellationToken cancellationToken = default)
        {
            var result = await FeedbackClient.SendAsync(
                new PlaytestFeedbackDraft
                {
                    Message = _pauseFeedbackNote,
                    Name = _pauseFeedbackName,
                    Email = _pauseFeedbackEmail,
                    Version = Application.version
                },
                cancellationToken);

            SetPauseFeedbackStatus(result.Kind);
            if (result.Kind == PlaytestFeedbackSendKind.Sent)
                _pauseFeedbackNote = string.Empty;
        }

        PlaytestFeedbackClient FeedbackClient => _feedbackClient ?? (_feedbackClient = new PlaytestFeedbackClient());

        void SetPauseFeedbackStatus(PlaytestFeedbackSendKind kind)
        {
            switch (kind)
            {
                case PlaytestFeedbackSendKind.Sent:
                    _pauseFeedbackStatus = "Thanks — sent.";
                    break;
                case PlaytestFeedbackSendKind.RejectedEmpty:
                    _pauseFeedbackStatus = "Write a note first.";
                    break;
                case PlaytestFeedbackSendKind.RejectedEmail:
                    _pauseFeedbackStatus = "Enter a valid email or leave it blank.";
                    break;
                case PlaytestFeedbackSendKind.RateLimited:
                    _pauseFeedbackStatus = "Too many tries. Wait a minute.";
                    break;
                default:
                    _pauseFeedbackStatus = "Couldn't send.";
                    break;
            }
        }

        string CurrentSyncStatusText()
        {
            var status = _syncStatusProvider == null ? SyncStatus.Synced : _syncStatusProvider();
            return status.ToString();
        }

        void DrawPauseOptions(float cx, float cy, float inner, float btnH, GUIStyle title, GUIStyle label)
        {
            GUI.Label(new Rect(cx, cy, inner, 28f), "Options", title);
            cy += 36f;

            var audio = TowerAudio.Ensure();
            var buses = audio.Buses;
            if (buses == null)
            {
                if (GUI.Button(new Rect(cx, cy, inner, btnH), "Back"))
                    _pauseUi = PauseUiState.Paused;
                return;
            }

            bool mute = GUI.Toggle(new Rect(cx, cy, inner, 22f), buses.MasterMute, " Master Mute", label);
            if (mute != buses.MasterMute)
            {
                buses.MasterMute = mute;
                PersistAudioBuses(audio);
            }

            cy += 28f;
            cy = DrawVolumeSlider(cx, cy, inner, "Master", buses.Master, label, v =>
            {
                buses.Master = v;
                PersistAudioBuses(audio);
            });
            cy = DrawVolumeSlider(cx, cy, inner, "SFX", buses.Sfx, label, v =>
            {
                buses.Sfx = v;
                PersistAudioBuses(audio);
            });
            cy = DrawVolumeSlider(cx, cy, inner, "Ambience", buses.Ambience, label, v =>
            {
                buses.Ambience = v;
                PersistAudioBuses(audio);
            });
            cy = DrawVolumeSlider(cx, cy, inner, "Music", buses.Music, label, v =>
            {
                buses.Music = v;
                PersistAudioBuses(audio);
            });

            cy += 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Reset layout"))
                ResetBuildMenuLayout();
            cy += btnH + 8f;
            if (GUI.Button(new Rect(cx, cy, inner, btnH), "Back"))
                _pauseUi = PauseUiState.Paused;
        }

        static float DrawVolumeSlider(
            float cx,
            float cy,
            float inner,
            string caption,
            float value,
            GUIStyle label,
            System.Action<float> onChanged)
        {
            GUI.Label(new Rect(cx, cy, inner, 18f), $"{caption}: {Mathf.RoundToInt(value * 100f)}%", label);
            cy += 18f;
            float next = GUI.HorizontalSlider(new Rect(cx, cy, inner, 16f), value, 0f, 1f);
            if (!Mathf.Approximately(next, value))
                onChanged(next);
            return cy + 22f;
        }

        static void PersistAudioBuses(TowerAudio audio)
        {
            if (audio?.Buses == null) return;
            audio.Buses.Save();
            audio.ApplyVolumes();
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
            _mapsDropdownRect = Rect.zero;
            _researchDropdownRect = Rect.zero;
            _mapsGraphRect = Rect.zero;
            _mapsLegendRect = Rect.zero;
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

            x = DrawStarTrack(x, y, lineH, stars != null ? stars.CurrentStars : 0);

            var clockText = simulation?.Clock != null ? simulation.Clock.FormatHud() : "—";
            DrawChip(clockText, 150f);

            // Season tile (icon only; no weather caption). Tooltip names the season.
            {
                const float seasonTile = 34f; // fits the 36px bar with a 1px inset
                var season = SeasonHudArt.Resolve(simulation);
                var tileRect = new Rect(x, barTopY + (barH - seasonTile) * 0.5f, seasonTile, seasonTile);
                if (SeasonHudArt.TryGetTexture(season, out var seasonTex) && seasonTex != null)
                    GUI.DrawTexture(tileRect, seasonTex, ScaleMode.StretchToFill, true);
                GUI.Label(tileRect, new GUIContent(string.Empty, SeasonHudArt.DisplayName(season)));
                x += seasonTile + 10f;
            }

            var climateName = simulation?.Climate?.Name ?? "—";
            DrawChip(climateName, 78f);

            DrawChip(GameSession.Difficulty.ToString(), 88f);

            // Reserve space for right-cluster Menu/Maps/Research/Info/Goals buttons.
            var clusterW = 56f + 8f; // Menu
            clusterW += 64f + 8f; // Maps
            var researchLabs = build.Grid != null ? EconomySystem.CountResearchLabs(build.Grid) : 0;
            if (researchLabs >= 1)
                clusterW += 210f + 8f; // Research status caption (matches button max)
            if (economyUnlocked) clusterW += 64f + 8f + 56f + 8f;
            if (goalsUnlocked) clusterW += 64f + 8f + 72f;
            else if (economyUnlocked) clusterW = Mathf.Max(56f + 8f + 64f + 8f, clusterW - 8f);

            var speedWidth = 320f;
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
            const float mapsW = 64f;
            const float btnGap = 8f;

            var cursor = right;
            const float menuW = 56f;
            cursor -= menuW;
            if (GUI.Button(new Rect(cursor, y, menuW, lineH), "Menu", barButton))
            {
                // Celebration modal is Continue-only — do not open Esc pause/quit overlay.
                if (_pauseUi == PauseUiState.Playing &&
                    ResolveCelebration()?.IsModalOpen != true)
                    EnterPause();
            }
            cursor -= btnGap;

            if (goalsUnlocked)
            {
                cursor -= goalsW;
                var goalsRect = new Rect(cursor, y, goalsW, lineH);
                var goalsArrow = _goalsOpen ? "▼" : "▶";
                if (GUI.Button(goalsRect, $"{goalsArrow} Goals", barButton))
                {
                    _goalsOpen = !_goalsOpen;
                    if (_goalsOpen)
                        _researchOpen = false;
                }
                cursor -= btnGap;
            }

            if (goalsUnlocked)
            {
                cursor -= towerW;
                var towerRect = new Rect(cursor, y, towerW, lineH);
                var towerOpen = _infoPanel == TopInfoPanel.Tower;
                var towerArrow = towerOpen ? "▼" : "▶";
                if (GUI.Button(towerRect, $"{towerArrow} Tower", barButton))
                {
                    _infoPanel = towerOpen ? TopInfoPanel.None : TopInfoPanel.Tower;
                    if (_infoPanel != TopInfoPanel.None)
                        _researchOpen = false;
                }
                cursor -= btnGap;
            }

            if (economyUnlocked)
            {
                cursor -= elevW;
                var elevRect = new Rect(cursor, y, elevW, lineH);
                var elevOpen = _infoPanel == TopInfoPanel.Elev;
                var elevArrow = elevOpen ? "▼" : "▶";
                if (GUI.Button(elevRect, $"{elevArrow} Elev", barButton))
                {
                    _infoPanel = elevOpen ? TopInfoPanel.None : TopInfoPanel.Elev;
                    if (_infoPanel != TopInfoPanel.None)
                        _researchOpen = false;
                }
                cursor -= btnGap;

                cursor -= shopsW;
                var shopsRect = new Rect(cursor, y, shopsW, lineH);
                var shopsOpen = _infoPanel == TopInfoPanel.Shops;
                var shopsArrow = shopsOpen ? "▼" : "▶";
                if (GUI.Button(shopsRect, $"{shopsArrow} Shops", barButton))
                {
                    _infoPanel = shopsOpen ? TopInfoPanel.None : TopInfoPanel.Shops;
                    if (_infoPanel != TopInfoPanel.None)
                        _researchOpen = false;
                }
                cursor -= btnGap;
            }

            cursor -= mapsW;
            var mapsRect = new Rect(cursor, y, mapsW, lineH);
            var mapsArrow = _mapsOpen ? "▼" : "▶";
            if (GUI.Button(mapsRect, $"{mapsArrow} Maps", barButton))
            {
                _mapsOpen = !_mapsOpen;
                if (_mapsOpen)
                    _researchOpen = false;
            }

            var researchLabs = build.Grid != null ? EconomySystem.CountResearchLabs(build.Grid) : 0;
            if (researchLabs < 1)
                _researchOpen = false;
            else
            {
                var caption = ResearchHudPanel.StatusCaption(simulation?.Research);
                var researchArrow = _researchOpen ? "▼" : "▶";
                var researchLabel = $"{researchArrow} {caption}";
                var researchW = Mathf.Clamp(
                    barButton.CalcSize(new GUIContent(researchLabel)).x + 14f,
                    118f,
                    210f);
                cursor -= btnGap;
                cursor -= researchW;
                var researchRect = new Rect(cursor, y, researchW, lineH);
                if (GUI.Button(researchRect, researchLabel, barButton))
                {
                    _researchOpen = !_researchOpen;
                    if (_researchOpen)
                    {
                        _goalsOpen = false;
                        _mapsOpen = false;
                        _infoPanel = TopInfoPanel.None;
                    }
                }
            }

            if (!economyUnlocked && _infoPanel is TopInfoPanel.Shops or TopInfoPanel.Elev)
                _infoPanel = TopInfoPanel.None;
            if (!goalsUnlocked && _infoPanel == TopInfoPanel.Tower)
                _infoPanel = TopInfoPanel.None;

            if (_infoPanel != TopInfoPanel.None)
                DrawInfoDropdown(right, gap, barTopY, barH, barWidth, wrapLabel, agents, population, averageStress);

            if (_mapsOpen)
                DrawMapsDropdown(right, barTopY, barH, barWidth, barButton);

            DrawMapsOverlays(gap, barTopY, barH, barWidth, wrapLabel);

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
                if (_mapsOpen)
                    goalsX = Mathf.Max(gap, goalsX - Mathf.Min(220f, barWidth) - 8f);
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

            if (_researchOpen && researchLabs >= 1)
                DrawResearchDropdown(right, barTopY, barH, barWidth, wrapLabel);
        }

        void DrawResearchDropdown(
            float right,
            float barTopY,
            float barH,
            float barWidth,
            GUIStyle wrapLabel)
        {
            const float pad = 8f;
            const float row = 20f;
            const float btnH = 22f;
            var dropW = Mathf.Min(360f, barWidth);
            // Full drawer: pool + 5 branches + effect + Start/Pause + ETA/costs + climate + notes.
            var dropH = Mathf.Min(420f, Mathf.Max(120f, Screen.height - (barTopY + barH) - 8f));
            _researchDropdownRect = new Rect(right - dropW, barTopY + barH, dropW, dropH);
            GUI.Box(_researchDropdownRect, GUIContent.none);

            var research = simulation?.Research;
            if (research == null || build.Grid == null)
            {
                _researchOpen = false;
                return;
            }

            var cx = _researchDropdownRect.x + pad;
            var cy = _researchDropdownRect.y + pad;
            var inner = dropW - pad * 2f;
            ResearchHudPanel.Draw(
                cx,
                cy,
                inner,
                btnH,
                row,
                research,
                build.Grid,
                simulation.Climate,
                ref _researchPickBranch,
                ref _researchPickLevel,
                wrapLabel);
        }

        void DrawMapsDropdown(
            float right,
            float barTopY,
            float barH,
            float barWidth,
            GUIStyle barButton)
        {
            var maps = EnsureMapController();
            var mode = maps != null ? maps.Mode : TowerMapMode.Off;
            var modes = new[]
            {
                TowerMapMode.Off,
                TowerMapMode.Graph,
                TowerMapMode.Crime,
                TowerMapMode.Noise,
                TowerMapMode.Traffic,
                TowerMapMode.Economic
            };

            var rowH = 22f;
            var pad = 6f;
            var subH = 0f;
            if (mode == TowerMapMode.Traffic) subH = 44f;
            else if (mode == TowerMapMode.Economic) subH = 44f;

            var dropW = Mathf.Min(260f, barWidth);
            var dropH = pad * 2f + modes.Length * rowH + subH + 4f;
            // Sit under Maps button cluster (left of Menu).
            _mapsDropdownRect = new Rect(right - dropW - 56f - 8f, barTopY + barH, dropW, dropH);
            EnsureWhiteTex();
            GUI.DrawTexture(
                _mapsDropdownRect,
                _whiteTex,
                ScaleMode.StretchToFill,
                false,
                0f,
                new Color(0.14f, 0.15f, 0.18f, 0.96f),
                0f,
                0f);

            var ly = _mapsDropdownRect.y + pad;
            var lx = _mapsDropdownRect.x + 6f;
            var innerW = dropW - 12f;

            foreach (var entry in modes)
            {
                var label = entry == TowerMapMode.Off ? "Off" : entry.ToString();
                var selected = mode == entry;
                DrawMapsChoiceButton(
                    new Rect(lx, ly, innerW, rowH - 2f),
                    label,
                    selected,
                    barButton);
                if (GUI.Button(new Rect(lx, ly, innerW, rowH - 2f), GUIContent.none, GUIStyle.none))
                {
                    if (maps != null)
                        maps.SetMode(entry);
                    mode = entry;
                }

                ly += rowH;
            }

            if (maps == null) return;

            if (mode == TowerMapMode.Traffic)
            {
                GUI.Label(
                    new Rect(lx, ly, innerW, 16f),
                    "Traffic window (click one):",
                    barButton);
                ly += 18f;
                var half = (innerW - 4f) * 0.5f;
                var todayOn = maps.TrafficWindow == TrafficMapWindow.Today;
                DrawMapsChoiceButton(new Rect(lx, ly, half, 22f), "Today", todayOn, barButton);
                if (GUI.Button(new Rect(lx, ly, half, 22f), GUIContent.none, GUIStyle.none))
                {
                    maps.TrafficWindow = TrafficMapWindow.Today;
                    maps.RebuildAndPaint();
                }

                DrawMapsChoiceButton(
                    new Rect(lx + half + 4f, ly, half, 22f),
                    "30-day Avg",
                    !todayOn,
                    barButton);
                if (GUI.Button(new Rect(lx + half + 4f, ly, half, 22f), GUIContent.none, GUIStyle.none))
                {
                    maps.TrafficWindow = TrafficMapWindow.Average30;
                    maps.RebuildAndPaint();
                }
            }
            else if (mode == TowerMapMode.Economic)
            {
                GUI.Label(
                    new Rect(lx, ly, innerW, 16f),
                    "Economic view (click one):",
                    barButton);
                ly += 18f;
                var third = (innerW - 8f) / 3f;
                var view = maps.EconomicView;
                DrawMapsSegOption(
                    lx, ly, third, "Profit", view == EconomicMapView.Profit, barButton,
                    () =>
                    {
                        maps.EconomicView = EconomicMapView.Profit;
                        maps.RebuildAndPaint();
                    });
                DrawMapsSegOption(
                    lx + third + 4f, ly, third, "Demand", view == EconomicMapView.Demand, barButton,
                    () =>
                    {
                        maps.EconomicView = EconomicMapView.Demand;
                        maps.RebuildAndPaint();
                    });
                DrawMapsSegOption(
                    lx + (third + 4f) * 2f, ly, third, "Blend", view == EconomicMapView.Blend, barButton,
                    () =>
                    {
                        maps.EconomicView = EconomicMapView.Blend;
                        maps.RebuildAndPaint();
                    });
            }
        }

        void DrawMapsSegOption(
            float x,
            float y,
            float w,
            string label,
            bool selected,
            GUIStyle barButton,
            System.Action onClick)
        {
            DrawMapsChoiceButton(new Rect(x, y, w, 22f), label, selected, barButton);
            if (GUI.Button(new Rect(x, y, w, 22f), GUIContent.none, GUIStyle.none))
                onClick?.Invoke();
        }

        void DrawMapsChoiceButton(Rect rect, string label, bool selected, GUIStyle barButton)
        {
            EnsureWhiteTex();
            var fill = selected
                ? new Color(0.28f, 0.48f, 0.78f, 1f)
                : new Color(0.22f, 0.23f, 0.26f, 1f);
            var outline = selected
                ? new Color(0.75f, 0.88f, 1f, 1f)
                : new Color(0.45f, 0.47f, 0.52f, 1f);
            GUI.DrawTexture(rect, _whiteTex, ScaleMode.StretchToFill, false, 0f, fill, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);

            var style = new GUIStyle(barButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = selected ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = selected ? Color.white : new Color(0.82f, 0.84f, 0.88f) }
            };
            var text = selected ? $"● {label}" : label;
            GUI.Label(rect, text, style);
        }

        void DrawMapsOverlays(
            float gap,
            float barTopY,
            float barH,
            float barWidth,
            GUIStyle wrapLabel)
        {
            var maps = EnsureMapController();
            if (maps == null) return;

            if (maps.Mode == TowerMapMode.Graph)
                DrawMapsGraphPanel(gap, barTopY, barH, barWidth, wrapLabel, maps);

            if (maps.Mode is TowerMapMode.Crime or TowerMapMode.Noise or TowerMapMode.Traffic
                or TowerMapMode.Economic)
                DrawMapsLegend(gap, barTopY, barH, wrapLabel, maps);
        }

        void DrawMapsGraphPanel(
            float gap,
            float barTopY,
            float barH,
            float barWidth,
            GUIStyle wrapLabel,
            TowerMapController maps)
        {
            var history = maps.Analytics.DayHistory;
            var stars = maps.Analytics.StarEvents;

            // Large analytics panel sits below the top bar; the build dock now floats over the world.
            var panelX = gap;
            var panelY = barTopY + barH + 4f;
            var panelRight = Screen.width - gap;
            if (_dockRect.width > 0f && _dockRect.x > Screen.width * 0.5f)
                panelRight = Mathf.Min(panelRight, _dockRect.x - gap);
            var panelW = Mathf.Max(240f, panelRight - panelX);
            var panelH = Mathf.Max(280f, Screen.height - panelY - gap);

            var candidate = new Rect(panelX, panelY, panelW, panelH);
            if (_infoRect.width > 0f && candidate.Overlaps(_infoRect))
            {
                var insetX = Mathf.Max(gap, _infoRect.xMax + 8f);
                var insetW = panelRight - insetX;
                if (insetW >= 320f)
                {
                    panelX = insetX;
                    panelW = insetW;
                }
                else
                {
                    panelY = _infoRect.yMax + 4f;
                    panelW = Mathf.Max(240f, panelRight - panelX);
                    panelH = Mathf.Max(220f, Screen.height - panelY - gap);
                }
            }

            _mapsGraphRect = new Rect(panelX, panelY, panelW, panelH);
            EnsureWhiteTex();
            // Solid panel so tower tiles do not wash out the chart.
            GUI.DrawTexture(
                _mapsGraphRect,
                _whiteTex,
                ScaleMode.StretchToFill,
                false,
                0f,
                new Color(0.12f, 0.13f, 0.16f, 0.97f),
                0f,
                0f);
            GUI.DrawTexture(
                new Rect(panelX, panelY, panelW, 2f),
                _whiteTex,
                ScaleMode.StretchToFill,
                false,
                0f,
                new Color(0.55f, 0.6f, 0.7f, 1f),
                0f,
                0f);

            var titleStyle = new GUIStyle(wrapLabel) { fontStyle = FontStyle.Bold };
            var pad = 10f;
            var y = panelY + 8f;

            const float closeW = 88f;
            const float closeH = 28f;
            var closeRect = new Rect(panelX + panelW - pad - closeW, panelY + 6f, closeW, closeH);
            var closeStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };
            if (GUI.Button(closeRect, "Close", closeStyle))
            {
                maps.SetMode(TowerMapMode.Off);
                _mapsOpen = false;
                _mapsGraphRect = Rect.zero;
                return;
            }

            GUI.Label(
                new Rect(panelX + pad, y, panelW - closeW - pad * 3f, 22f),
                "Tower Analytics · last 90 midnights",
                titleStyle);
            y += 24f;
            var climateName = simulation?.Climate?.Name ?? "—";
            GUI.Label(
                new Rect(panelX + pad, y, panelW - pad * 2f, 18f),
                $"Climate: {climateName}",
                wrapLabel);
            y += 20f;

            // Metric toggles — same chart space, on/off per series.
            var toggleH = 22f;
            var tx = panelX + pad;
            var toggleRowY = y;
            void MetricToggle(ref bool on, string label, Color swatch, float width)
            {
                if (tx + width > panelX + panelW - pad)
                {
                    tx = panelX + pad;
                    toggleRowY += toggleH + 4f;
                }

                var r = new Rect(tx, toggleRowY, width, toggleH);
                var prev = GUI.color;
                GUI.color = new Color(swatch.r, swatch.g, swatch.b, on ? 1f : 0.35f);
                EnsureWhiteTex();
                GUI.DrawTexture(new Rect(r.x + 4f, r.y + 6f, 10f, 10f), _whiteTex);
                GUI.color = prev;
                var text = on ? $"● {label}" : $"○ {label}";
                if (GUI.Button(new Rect(r.x + 16f, r.y, width - 16f, toggleH), text))
                    on = !on;
                tx += width + 6f;
            }

            MetricToggle(ref _graphShowClimate, "Climate", new Color(0.45f, 0.75f, 1f), 88f);
            MetricToggle(ref _graphShowSpend, "Spend ×", new Color(0.55f, 0.9f, 0.55f), 88f);
            MetricToggle(ref _graphShowVacancy, "Vacancy", new Color(1f, 0.75f, 0.4f), 88f);
            MetricToggle(ref _graphShowPopulation, "Population", new Color(0.75f, 0.55f, 1f), 100f);
            MetricToggle(ref _graphShowIncome, "Income", new Color(0.25f, 0.85f, 0.4f), 88f);
            MetricToggle(ref _graphShowLosses, "Losses", new Color(0.95f, 0.3f, 0.25f), 80f);
            MetricToggle(ref _graphShowSavings, "Savings", new Color(1f, 0.85f, 0.25f), 88f);
            MetricToggle(ref _graphShowStars, "★ Stars", new Color(1f, 0.95f, 0.55f), 88f);
            y = toggleRowY + toggleH + 8f;

            // Reserve gutters for axis labels inside the panel.
            const float yAxisW = 58f;
            const float xAxisH = 22f;
            var plotOuter = new Rect(
                panelX + pad,
                y,
                panelW - pad * 2f,
                panelY + panelH - y - pad - 18f);
            var chart = new Rect(
                plotOuter.x + yAxisW,
                plotOuter.y,
                Mathf.Max(80f, plotOuter.width - yAxisW),
                Mathf.Max(60f, plotOuter.height - xAxisH));

            EnsureWhiteTex();
            GUI.DrawTexture(
                chart,
                _whiteTex,
                ScaleMode.StretchToFill,
                false,
                0f,
                new Color(0.08f, 0.09f, 0.12f, 1f),
                0f,
                0f);

            if (history == null || history.Count == 0)
            {
                GUI.Label(
                    new Rect(chart.x + 12f, chart.y + 12f, chart.width - 24f, 40f),
                    "No midnight samples yet — advance time past a day roll.",
                    wrapLabel);
                return;
            }

            var n = history.Count;
            // Horizontal grid
            for (var g = 0; g <= 4; g++)
            {
                var gy = chart.y + chart.height * (g / 4f);
                GUI.DrawTexture(
                    new Rect(chart.x, gy, chart.width, 1f),
                    _whiteTex,
                    ScaleMode.StretchToFill,
                    false,
                    0f,
                    new Color(1f, 1f, 1f, 0.08f),
                    0f,
                    0f);
            }

            // Vertical grid (day ticks)
            for (var g = 0; g <= 4; g++)
            {
                var gx = chart.x + chart.width * (g / 4f);
                GUI.DrawTexture(
                    new Rect(gx, chart.y, 1f, chart.height),
                    _whiteTex,
                    ScaleMode.StretchToFill,
                    false,
                    0f,
                    new Color(1f, 1f, 1f, 0.06f),
                    0f,
                    0f);
            }

            DrawGraphAxes(chart, history, wrapLabel);

            if (_graphShowStars && stars != null)
                DrawStarMarkers(chart, history, stars, wrapLabel);

            void DrawMetric(bool on, Color color, System.Func<TowerDaySample, float> pick)
            {
                if (!on) return;
                DrawNormalizedSeries(chart, history, pick, color);
            }

            DrawMetric(_graphShowClimate, new Color(0.45f, 0.75f, 1f), s => s.ClimateStep / 4f);
            DrawMetric(_graphShowSpend, new Color(0.55f, 0.9f, 0.55f), s => Mathf.Clamp01((s.SpendMult - 0.6f) / 0.8f));
            DrawMetric(_graphShowVacancy, new Color(1f, 0.75f, 0.4f), s => s.Vacancy);
            DrawMetric(_graphShowPopulation, new Color(0.75f, 0.55f, 1f), s => s.Population);
            DrawMetric(_graphShowIncome, new Color(0.25f, 0.85f, 0.4f), s => s.DailyIncome);
            DrawMetric(_graphShowLosses, new Color(0.95f, 0.3f, 0.25f), s => s.DailyExpense);
            DrawMetric(_graphShowSavings, new Color(1f, 0.85f, 0.25f), s => s.Savings);

            // Latest values strip
            var last = history[n - 1];
            var footer =
                $"Day {last.DayIndex}  ·  Pop {last.Population}  ·  In ${last.DailyIncome:N0}  ·  Loss ${last.DailyExpense:N0}  ·  Save ${last.Savings:N0}  ·  {last.Stars}★";
            GUI.Label(
                new Rect(panelX + pad, panelY + panelH - 16f, panelW - pad * 2f, 14f),
                footer,
                wrapLabel);
        }

        void DrawGraphAxes(
            Rect chart,
            System.Collections.Generic.IReadOnlyList<TowerDaySample> history,
            GUIStyle wrapLabel)
        {
            if (history == null || history.Count == 0) return;

            var firstDay = history[0].DayIndex;
            var lastDay = history[history.Count - 1].DayIndex;
            var tiny = new GUIStyle(wrapLabel) { fontSize = Mathf.Max(10, wrapLabel.fontSize - 1) };

            // X-axis: day labels (always when history exists).
            GUI.Label(new Rect(chart.x, chart.yMax + 2f, 70f, 16f), $"Day {firstDay}", tiny);
            if (lastDay != firstDay)
            {
                var mid = (firstDay + lastDay) / 2;
                GUI.Label(
                    new Rect(chart.x + chart.width * 0.5f - 28f, chart.yMax + 2f, 70f, 16f),
                    $"Day {mid}",
                    tiny);
                GUI.Label(
                    new Rect(chart.xMax - 70f, chart.yMax + 2f, 70f, 16f),
                    $"Day {lastDay}",
                    tiny);
            }

            GUI.Label(
                new Rect(chart.x + chart.width * 0.5f - 16f, chart.yMax + 14f, 40f, 14f),
                "Day",
                tiny);

            // Y-axis: depends on which value series are selected (stars are markers only).
            var moneyOn = (_graphShowIncome ? 1 : 0) + (_graphShowLosses ? 1 : 0) + (_graphShowSavings ? 1 : 0);
            var otherCount =
                (_graphShowClimate ? 1 : 0) +
                (_graphShowSpend ? 1 : 0) +
                (_graphShowVacancy ? 1 : 0) +
                (_graphShowPopulation ? 1 : 0);
            var valueSeries = moneyOn + otherCount;

            if (valueSeries == 0)
            {
                GUI.Label(new Rect(chart.x - 56f, chart.y + chart.height * 0.5f - 8f, 54f, 16f), "—", tiny);
                return;
            }

            string FormatY(float t01, float max, string kind)
            {
                var v = t01 * max;
                return kind switch
                {
                    "money" => AbbreviateAxisMoney(v),
                    "pop" => Mathf.RoundToInt(v).ToString(),
                    "climate" => ClimateAxisLabel(t01),
                    "spend" => $"{0.6f + t01 * 0.8f:0.00}×",
                    "vacancy" => $"{Mathf.RoundToInt(t01 * 100f)}%",
                    _ => $"{Mathf.RoundToInt(t01 * 100f)}%"
                };
            }

            // Single series → absolute units. Mixed → relative % + scale note.
            string kind;
            float max;
            string axisTitle;
            if (valueSeries == 1 && moneyOn == 1)
            {
                kind = "money";
                max = MaxOfEnabledMoney(history);
                axisTitle = _graphShowIncome && !_graphShowLosses && !_graphShowSavings ? "Income $"
                    : _graphShowLosses && !_graphShowIncome && !_graphShowSavings ? "Losses $"
                    : _graphShowSavings && !_graphShowIncome && !_graphShowLosses ? "Savings $"
                    : "$";
            }
            else if (valueSeries == 1 && _graphShowPopulation)
            {
                kind = "pop";
                max = MaxOf(history, s => s.Population);
                axisTitle = "Pop";
            }
            else if (valueSeries == 1 && _graphShowClimate)
            {
                kind = "climate";
                max = 1f;
                axisTitle = "Climate";
            }
            else if (valueSeries == 1 && _graphShowSpend)
            {
                kind = "spend";
                max = 1f;
                axisTitle = "Spend";
            }
            else if (valueSeries == 1 && _graphShowVacancy)
            {
                kind = "vacancy";
                max = 1f;
                axisTitle = "Vacancy";
            }
            else if (moneyOn > 0 && otherCount == 0)
            {
                // Multiple money series still self-normalize per line; show relative + note.
                kind = "rel";
                max = 1f;
                axisTitle = "Rel %";
            }
            else
            {
                kind = "rel";
                max = 1f;
                axisTitle = "Rel %";
            }

            for (var g = 0; g <= 4; g++)
            {
                var t = 1f - g / 4f; // top = max
                var gy = chart.y + chart.height * (g / 4f) - 7f;
                string label;
                if (kind == "rel")
                    label = $"{Mathf.RoundToInt(t * 100f)}%";
                else
                    label = FormatY(t, Mathf.Max(0.0001f, max), kind);

                GUI.Label(new Rect(chart.x - 56f, gy, 54f, 14f), label, tiny);
            }

            GUI.Label(new Rect(chart.x - 56f, chart.y - 14f, 54f, 14f), axisTitle, tiny);

            if (kind == "rel")
            {
                GUI.Label(
                    new Rect(chart.x, chart.y - 14f, chart.width, 14f),
                    BuildRelativeScaleNote(history),
                    tiny);
            }
        }

        string BuildRelativeScaleNote(System.Collections.Generic.IReadOnlyList<TowerDaySample> history)
        {
            var parts = new System.Collections.Generic.List<string>(6);
            if (_graphShowClimate) parts.Add("Climate 0–4");
            if (_graphShowSpend) parts.Add("Spend 0.6–1.4×");
            if (_graphShowVacancy) parts.Add($"Vacancy max {MaxOf(history, s => s.Vacancy) * 100f:0}%");
            if (_graphShowPopulation) parts.Add($"Pop max {Mathf.RoundToInt(MaxOf(history, s => s.Population))}");
            if (_graphShowIncome) parts.Add($"In max {AbbreviateAxisMoney(MaxOf(history, s => s.DailyIncome))}");
            if (_graphShowLosses) parts.Add($"Loss max {AbbreviateAxisMoney(MaxOf(history, s => s.DailyExpense))}");
            if (_graphShowSavings) parts.Add($"Save max {AbbreviateAxisMoney(MaxOf(history, s => s.Savings))}");
            if (parts.Count == 0) return string.Empty;
            return "Each line = own max · " + string.Join(" · ", parts);
        }

        float MaxOfEnabledMoney(System.Collections.Generic.IReadOnlyList<TowerDaySample> history)
        {
            var max = 0.0001f;
            if (_graphShowIncome) max = Mathf.Max(max, MaxOf(history, s => s.DailyIncome));
            if (_graphShowLosses) max = Mathf.Max(max, MaxOf(history, s => s.DailyExpense));
            if (_graphShowSavings) max = Mathf.Max(max, MaxOf(history, s => s.Savings));
            return max;
        }

        static float MaxOf(
            System.Collections.Generic.IReadOnlyList<TowerDaySample> history,
            System.Func<TowerDaySample, float> pick)
        {
            var max = 0.0001f;
            if (history == null) return max;
            for (var i = 0; i < history.Count; i++)
            {
                var v = pick(history[i]);
                if (v > max) max = v;
            }

            return max;
        }

        static string AbbreviateAxisMoney(float v)
        {
            var n = Mathf.Abs(v);
            if (n >= 1_000_000f) return $"${v / 1_000_000f:0.#}M";
            if (n >= 10_000f) return $"${v / 1000f:0.#}k";
            return $"${Mathf.RoundToInt(v):N0}";
        }

        static string ClimateAxisLabel(float t01)
        {
            var step = Mathf.Clamp(Mathf.RoundToInt(t01 * 4f), 0, 4);
            return step switch
            {
                0 => "Rec",
                1 => "Slow",
                2 => "Norm",
                3 => "Str",
                _ => "Boom"
            };
        }

        void DrawNormalizedSeries(
            Rect chart,
            System.Collections.Generic.IReadOnlyList<TowerDaySample> history,
            System.Func<TowerDaySample, float> pick,
            Color color)
        {
            if (history == null || history.Count == 0) return;
            var n = history.Count;
            var max = 0.0001f;
            for (var i = 0; i < n; i++)
            {
                var v = pick(history[i]);
                if (v > max) max = v;
            }

            var step = chart.width / Mathf.Max(1, n - 1);
            float X(int i) => chart.x + (n == 1 ? chart.width * 0.5f : i * step);
            float Y(float raw) => chart.yMax - 2f - Mathf.Clamp01(raw / max) * (chart.height - 4f);

            EnsureWhiteTex();
            for (var i = 0; i < n; i++)
            {
                var py = Y(pick(history[i]));
                var px = X(i);
                GUI.DrawTexture(
                    new Rect(px - 1.5f, py - 1.5f, 3f, 3f),
                    _whiteTex,
                    ScaleMode.StretchToFill,
                    false,
                    0f,
                    color,
                    0f,
                    0f);
                if (i > 0)
                {
                    var px0 = X(i - 1);
                    var py0 = Y(pick(history[i - 1]));
                    DrawChartSegment(px0, py0, px, py, color);
                }
            }
        }

        void DrawChartSegment(float x0, float y0, float x1, float y1, Color color)
        {
            EnsureWhiteTex();
            var dx = x1 - x0;
            var dy = y1 - y0;
            var len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.5f) return;
            var steps = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
            for (var s = 0; s <= steps; s++)
            {
                var t = s / (float)steps;
                var px = Mathf.Lerp(x0, x1, t);
                var py = Mathf.Lerp(y0, y1, t);
                GUI.DrawTexture(
                    new Rect(px - 1f, py - 1f, 2f, 2f),
                    _whiteTex,
                    ScaleMode.StretchToFill,
                    false,
                    0f,
                    color,
                    0f,
                    0f);
            }
        }

        void DrawStarMarkers(
            Rect chart,
            System.Collections.Generic.IReadOnlyList<TowerDaySample> history,
            System.Collections.Generic.IReadOnlyList<StarEarnEvent> events,
            GUIStyle wrapLabel)
        {
            if (history == null || history.Count == 0 || events == null || events.Count == 0)
                return;

            var firstDay = history[0].DayIndex;
            var lastDay = history[history.Count - 1].DayIndex;
            var span = Mathf.Max(1, lastDay - firstDay);
            EnsureWhiteTex();
            var markerColor = new Color(1f, 0.95f, 0.55f, 0.85f);

            foreach (var ev in events)
            {
                if (ev.DayIndex < firstDay || ev.DayIndex > lastDay) continue;
                var t = (ev.DayIndex - firstDay) / (float)span;
                var px = chart.x + t * chart.width;
                GUI.DrawTexture(
                    new Rect(px - 1f, chart.y, 2f, chart.height),
                    _whiteTex,
                    ScaleMode.StretchToFill,
                    false,
                    0f,
                    markerColor,
                    0f,
                    0f);
                GUI.Label(
                    new Rect(px + 2f, chart.y + 2f, 36f, 16f),
                    $"{ev.Stars}★",
                    wrapLabel);
            }
        }

        void DrawMapsLegend(float gap, float barTopY, float barH, GUIStyle wrapLabel, TowerMapController maps)
        {
            var isProfit = maps.Mode == TowerMapMode.Economic &&
                           maps.EconomicView == EconomicMapView.Profit;

            var title = maps.Mode switch
            {
                TowerMapMode.Crime => "Crime",
                TowerMapMode.Noise => "Noise",
                TowerMapMode.Traffic => maps.TrafficWindow == TrafficMapWindow.Today
                    ? "Traffic · Today"
                    : "Traffic · 30-day Avg",
                TowerMapMode.Economic => maps.EconomicView switch
                {
                    EconomicMapView.Profit => "Economic · Profit",
                    EconomicMapView.Demand => "Economic · Demand",
                    _ => "Economic · Blend"
                },
                _ => maps.Mode.ToString()
            };

            var meaning = isProfit
                ? "Red = loss · grey = break-even · green = profit (scaled to today’s tower)"
                : maps.Mode switch
                {
                    TowerMapMode.Crime => "Blue = low risk · red = high risk",
                    TowerMapMode.Noise => "Blue = quiet · red = louder / bother",
                    TowerMapMode.Traffic => "Blue = light · red = busy",
                    TowerMapMode.Economic => "Blue = low stress · red = high stress",
                    _ => "Blue = low · red = high"
                };

            var panelH = 68f;
            var w = 320f;
            var legendY = barTopY + barH + 4f;
            var maxX = Mathf.Max(gap, Screen.width - gap - w);
            var legendX = Mathf.Clamp(Mathf.Max(gap, _infoRect.xMax + 8f), gap, maxX);
            _mapsLegendRect = new Rect(legendX, legendY, w, panelH);
            var collidesWithInfo = _infoRect.width > 0f && _mapsLegendRect.Overlaps(_infoRect);
            var collidesWithRightDock = _dockRect.width > 0f &&
                                        _dockRect.x > Screen.width * 0.5f &&
                                        _mapsLegendRect.Overlaps(_dockRect);
            if (collidesWithInfo || collidesWithRightDock)
            {
                var belowX = Mathf.Clamp(_infoRect.width > 0f ? _infoRect.x : gap, gap, maxX);
                var belowY = _infoRect.width > 0f ? _infoRect.yMax + 4f : legendY;
                _mapsLegendRect = HudFloatingPanel.SoftClamp(
                    new Rect(belowX, belowY, w, panelH),
                    Screen.width,
                    Screen.height);
            }
            GUI.Box(_mapsLegendRect, GUIContent.none);

            var pad = 8f;
            var y = _mapsLegendRect.y + 4f;
            GUI.Label(
                new Rect(_mapsLegendRect.x + pad, y, w - pad * 2f, 16f),
                title,
                wrapLabel);
            y += 16f;
            GUI.Label(
                new Rect(_mapsLegendRect.x + pad, y, w - pad * 2f, 14f),
                meaning,
                wrapLabel);
            y += 15f;

            const int swatches = 20;
            var barX = _mapsLegendRect.x + pad;
            var barW = w - pad * 2f;
            var swW = barW / swatches;
            var barY = y;
            var barHgt = 10f;
            for (var i = 0; i < swatches; i++)
            {
                Color c;
                if (isProfit)
                {
                    // −1 … 0 … +1 across the bar
                    var signed = (i / (float)(swatches - 1)) * 2f - 1f;
                    if (!HeatmapColors.TryProfitColor(signed, out c))
                        c = HeatmapColors.Grey;
                }
                else
                {
                    var t = i / (float)(swatches - 1);
                    c = t <= 0.001f ? HeatmapColors.Grey : HeatmapColors.RiskColor(t);
                }

                var prev = GUI.color;
                GUI.color = new Color(c.r, c.g, c.b, 1f);
                GUI.DrawTexture(new Rect(barX + i * swW, barY, swW + 0.5f, barHgt), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            y = barY + barHgt + 2f;
            var labelStyle = wrapLabel;
            if (isProfit)
            {
                GUI.Label(new Rect(barX, y, barW * 0.4f, 14f), "−100 loss", labelStyle);
                GUI.Label(new Rect(barX + barW * 0.42f, y, barW * 0.16f, 14f), "0", labelStyle);
                GUI.Label(new Rect(barX + barW * 0.55f, y, barW * 0.45f, 14f), "+100 profit", labelStyle);
            }
            else
            {
                GUI.Label(new Rect(barX, y, 40f, 14f), "0", labelStyle);
                GUI.Label(new Rect(barX + barW - 36f, y, 36f, 14f), "100", labelStyle);
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
                    var demand = simulation?.ShopDemand;
                    if (demand != null)
                    {
                        var snapshot = demand.Snapshot;
                        var families = new[]
                        {
                            ShopDemandFamily.Food,
                            ShopDemandFamily.Retail
                        };
                        var tiers = new[]
                        {
                            ShopDemandTier.Budget,
                            ShopDemandTier.Mid,
                            ShopDemandTier.Premium
                        };
                        foreach (var family in families)
                        foreach (var tier in tiers)
                            lines.Add(ShopDemandFormat.PoolLine(
                                family,
                                tier,
                                snapshot.Pool(family, tier)));
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

        void DrawBuildDock(GUIStyle iconStyle, GUIStyle title, GUIStyle label)
        {
            FitDockHeightToContent();
            _dockRect = HudFloatingPanel.SoftClamp(_dockRect, Screen.width, Screen.height);
            _panelRect = _dockRect;
            GUI.Box(_dockRect, GUIContent.none);

            var grip = new Rect(_dockRect.x, _dockRect.y, _dockRect.width, BuildDockGripHeight);
            var resetW = 48f;
            var dragGrip = new Rect(grip.x, grip.y, Mathf.Max(24f, grip.width - resetW - 6f), grip.height);
            var wasDragging = _dockDragging;
            if (HudFloatingPanel.DragGrip(
                    dragGrip,
                    ref _dockRect,
                    Screen.width,
                    Screen.height,
                    ref _dockDragging,
                    ref _dockDragOffset) &&
                wasDragging &&
                !_dockDragging)
                HudFloatingPanel.SaveRect(BuildDockPrefsKey, _dockRect);

            EnsureWhiteTex();
            GUI.DrawTexture(
                grip,
                _whiteTex,
                ScaleMode.StretchToFill,
                false,
                0f,
                new Color(0.18f, 0.16f, 0.13f, 0.92f),
                0f,
                0f);
            GUI.Label(new Rect(grip.x + 8f, grip.y + 3f, grip.width - resetW - 12f, 18f), "Build", title);
            if (GUI.Button(new Rect(grip.xMax - resetW - 4f, grip.y + 3f, resetW, 18f), "Reset"))
                ResetBuildMenuLayout();

            var cx = _dockRect.x + BuildPanelPad;
            var cy = _dockRect.y + BuildDockGripHeight + BuildPanelPad;
            cy = DrawToolIcons(cx, cy, iconStyle);
            cy += 2f;
            DrawFamilyIcons(cx, cy, iconStyle);
        }

        void DrawFamilyPopout(GUIStyle iconStyle, GUIStyle title, GUIStyle label, StarSystem stars)
        {
            var active = ActiveCatalogFamily();
            if (active == null)
            {
                _popoutRect = Rect.zero;
                return;
            }

            if (active.Family == BuildFamily.Shops &&
                !_expandedShopSubgroup.HasValue &&
                active.Subgroups.Count > 0)
                _expandedShopSubgroup = active.Subgroups[0].Subgroup;

            var popoutSize = PopoutSize(active);
            var side = BuildMenuPopoutLayout.ChooseSide(_dockRect, popoutSize, Screen.width, BuildPopoutGap);
            _popoutRect = HudFloatingPanel.SoftClamp(
                BuildMenuPopoutLayout.Place(_dockRect, popoutSize, side, BuildPopoutGap),
                Screen.width,
                Screen.height);

            GUI.Box(_popoutRect, GUIContent.none);
            var cx = _popoutRect.x + BuildPanelPad;
            var cy = _popoutRect.y + BuildPanelPad;
            var inner = _popoutRect.width - BuildPanelPad * 2f;
            GUI.Label(new Rect(cx, cy, inner, 20f), active.Label, title);
            cy += 24f;

            if (active.Family == BuildFamily.Shops)
            {
                cy = DrawIconRow(
                    cy,
                    active.Subgroups.Count,
                    MenuStripColumns,
                    i =>
                    {
                        var subgroup = active.Subgroups[i];
                        var selected = _expandedShopSubgroup == subgroup.Subgroup;
                        if (DrawPictureButton(
                                IconRect(cx, cy, i, MenuStripColumns),
                                MenuIconIdForSubgroup(subgroup.Subgroup),
                                SubgroupGlyph(subgroup.Subgroup),
                                $"{active.Label} -> {subgroup.Label}",
                                FamilyColor(BuildFamily.Shops),
                                selected,
                                enabled: true,
                                iconStyle))
                            _expandedShopSubgroup = subgroup.Subgroup;
                    });

                foreach (var subgroup in active.Subgroups)
                {
                    if (subgroup.Subgroup != _expandedShopSubgroup) continue;
                    DrawRoomIconGrid(cx, cy, iconStyle, subgroup.Rooms, stars);
                    return;
                }
            }
            else
            {
                DrawRoomIconGrid(cx, cy, iconStyle, active.Rooms, stars);
            }
        }

        void DrawBuildInfoPanel(
            GUIStyle title,
            GUIStyle label,
            StarSystem stars,
            AgentSystem agents,
            float row,
            float btnH)
        {
            _infoRect = HudFloatingPanel.SoftClamp(_infoRect, Screen.width, Screen.height);
            GUI.Box(_infoRect, GUIContent.none);

            var grip = new Rect(_infoRect.x, _infoRect.y, _infoRect.width, BuildDockGripHeight);
            var wasDragging = _infoDragging;
            if (HudFloatingPanel.DragGrip(
                    grip,
                    ref _infoRect,
                    Screen.width,
                    Screen.height,
                    ref _infoDragging,
                    ref _infoDragOffset) &&
                wasDragging &&
                !_infoDragging)
                HudFloatingPanel.SaveRect(BuildInfoPrefsKey, _infoRect);

            EnsureWhiteTex();
            GUI.DrawTexture(
                grip,
                _whiteTex,
                ScaleMode.StretchToFill,
                false,
                0f,
                new Color(0.18f, 0.16f, 0.13f, 0.92f),
                0f,
                0f);
            GUI.Label(new Rect(grip.x + 8f, grip.y + 3f, grip.width - 16f, 18f), "Info", title);

            var cx = _infoRect.x + BuildPanelPad;
            var cy = _infoRect.y + BuildDockGripHeight + BuildPanelPad;
            var inner = _infoRect.width - BuildPanelPad * 2f;
            var viewRect = new Rect(
                cx,
                cy,
                inner,
                Mathf.Max(1f, _infoRect.height - BuildDockGripHeight - BuildPanelPad * 2f));
            var bodyInner = Mathf.Max(1f, inner - 18f);
            var contentRect = new Rect(0f, 0f, bodyInner, 1200f);
            _infoScroll = GUI.BeginScrollView(viewRect, _infoScroll, contentRect);
            if (build.SelectedRoom != null)
            {
                DrawSelectedRoomInfo(0f, 0f, bodyInner, row, btnH, label, stars, agents);
                GUI.EndScrollView();
                return;
            }

            DrawCatalogPickInfo(0f, 0f, bodyInner, row, label);
            GUI.EndScrollView();
        }

        void DrawSelectedRoomInfo(
            float cx,
            float cy,
            float inner,
            float row,
            float btnH,
            GUIStyle label,
            StarSystem stars,
            AgentSystem agents)
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

            var starsNow = stars?.CurrentStars ?? simulation?.Stars?.CurrentStars ?? 0;
            foreach (var line in RoomEconomyFormat.SelectedUnitLines(
                         build.SelectedRoom,
                         agents?.Agents,
                         simulation?.Economy,
                         simulation?.ShopDemand,
                         ShopDemandFormat.CountOpenShopsInPool(
                             build.Grid?.Rooms,
                             build.SelectedRoom?.Type),
                         starsNow,
                         simulation?.Climate?.SpendMultiplier ?? 1f,
                         simulation?.MacroEconomy?.LivingPulseMult ?? 1f,
                         simulation?.MacroEconomy?.CommercialPulseMult ?? 1f))
            {
                GUI.Label(new Rect(cx, cy, inner, row), line, label);
                cy += row;
            }

            var fitWarn = RoomEconomyFormat.FloorFitWarningOrNull(
                build.SelectedRoom?.Type,
                build.SelectedRoom?.Origin.y ?? 0,
                starsNow);
            if (fitWarn != null)
            {
                GUI.Label(new Rect(cx, cy, inner, row), fitWarn, label);
                cy += row;
            }

            foreach (var line in ConferenceSelectionLines(build.SelectedRoom))
            {
                GUI.Label(new Rect(cx, cy, inner, row), line, label);
                cy += row;
            }

            if (PricePricing.IsPricedRoom(build.SelectedRoom?.Type))
                cy = DrawPriceTierButtons(cx, cy, inner, btnH, row, label, stars);

            if (BuildController.IsStaffedServiceRoom(build.SelectedRoom?.Type))
                cy = DrawStaffStepper(cx, cy, inner, btnH, row, label);

            if (build.SelectedRoom?.Type?.id == EconomySystem.ResearchId)
            {
                GUI.Label(
                    new Rect(cx, cy, inner, row * 2f),
                    "Use Research in the top bar to start or pause projects.",
                    label);
                cy += row * 2f;
            }

            var elevStatus = build.GetElevatorStatusText();
            if (elevStatus == null) return;

            GUI.Label(new Rect(cx, cy, inner, row), $"Elevator: {elevStatus}", label);
            cy += row;
            var simElev = simulation?.Elevators?.FindByRoomId(build.SelectedRoom.InstanceId);
            if (simElev != null)
            {
                foreach (var line in ElevatorTrafficLines(simElev))
                {
                    GUI.Label(new Rect(cx, cy, inner, row), line, label);
                    cy += row;
                }
            }

            var inMaint = simElev != null && simElev.InMaintenance;
            var maintLabel = inMaint ? "Exit Maintenance" : "Enter Maintenance";
            if (GUI.Button(new Rect(cx, cy, inner, btnH), maintLabel))
                build.TrySetSelectedElevatorMaintenance(!inMaint);
        }

        void DrawCatalogPickInfo(float cx, float cy, float inner, float row, GUIStyle label)
        {
            var room = build.CurrentTool == BuildTool.PlaceRoom ? build.SelectedRoomType : null;
            var starsNow = simulation?.Stars?.CurrentStars ?? 0;
            if (room != null)
            {
                GUI.Label(new Rect(cx, cy, inner, row), room.displayName, label);
                cy += row;
                foreach (var economyLine in SelectedEconomyLines(room))
                {
                    GUI.Label(new Rect(cx, cy, inner, row), economyLine, label);
                    cy += row;
                }

                if (build.HoverCell.HasValue)
                {
                    var ghostWarn = RoomEconomyFormat.FloorFitWarningOrNull(
                        room, build.HoverCell.Value.y, starsNow);
                    if (ghostWarn != null)
                    {
                        GUI.Label(new Rect(cx, cy, inner, row), ghostWarn, label);
                        cy += row;
                    }
                }

                GUI.Label(new Rect(cx, cy, inner, row), $"Size {room.size.x}x{room.size.y}", label);
                cy += row;
            }
            else if (build.CurrentTool != BuildTool.Select)
            {
                GUI.Label(new Rect(cx, cy, inner, row), build.CurrentTool.ToString(), label);
                cy += row;
            }
            else
            {
                GUI.Label(new Rect(cx, cy, inner, row), "Select a tool or room.", label);
                cy += row;
            }

            var help = string.IsNullOrEmpty(build.HelpText) ? "Pick an icon to start building." : build.HelpText;
            var helpHeight = Mathf.Clamp(label.CalcHeight(new GUIContent(help), inner), row, row * 4f);
            GUI.Label(new Rect(cx, cy + 4f, inner, helpHeight), help, label);

            if (!build.HoverCell.HasValue) return;
            var c = build.HoverCell.Value;
            var floorLabel = c.y > 0 ? c.y.ToString() : c.y < 0 ? $"B{-c.y}" : "G";
            GUI.Label(new Rect(cx, cy + helpHeight + 8f, inner, row), $"Cell: ({c.x}, floor {floorLabel})", label);
        }

        float DrawFamilyIcons(float cx, float cy, GUIStyle iconStyle)
        {
            return DrawIconRow(
                cy,
                _catalog.Count,
                MenuStripColumns,
                i =>
                {
                    var family = _catalog[i];
                    var selected = _expandedFamily == family.Family;
                    var tip = $"{family.Label}\nClick to {(selected ? "collapse" : "expand")}";
                    if (DrawPictureButton(
                            IconRect(cx, cy, i, MenuStripColumns),
                            MenuIconIdForFamily(family.Family),
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
        }

        BuildCatalogFamily ActiveCatalogFamily()
        {
            if (!_expandedFamily.HasValue)
                return null;

            foreach (var family in _catalog)
                if (family.Family == _expandedFamily)
                    return family;
            return null;
        }

        static Vector2 PopoutSize(BuildCatalogFamily active)
        {
            var count = active.Family == BuildFamily.Shops
                ? active.Subgroups.Count
                : active.Rooms.Count;
            if (active.Family == BuildFamily.Shops)
            {
                var maxRooms = 0;
                foreach (var subgroup in active.Subgroups)
                    maxRooms = Mathf.Max(maxRooms, subgroup.Rooms.Count);
                count += maxRooms;
            }

            var rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)MenuStripColumns));
            var width = BuildPanelPad * 2f
                        + MenuStripColumns * MenuIconSize
                        + (MenuStripColumns - 1) * MenuIconGap;
            var height = BuildPanelPad * 2f
                         + 24f
                         + rows * MenuIconSize
                         + Mathf.Max(0, rows - 1) * MenuIconGap
                         + 8f;
            return new Vector2(width, height);
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

        float DrawRoomIconGrid(
            float cx,
            float cy,
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
                cy,
                count,
                MenuStripColumns,
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
                    if (DrawPictureButton(
                            IconRect(cx, cy, i, MenuStripColumns),
                            MenuIconIdForRoom(room),
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

        float DrawToolIcons(float cx, float cy, GUIStyle iconStyle)
        {
            var tools = new (BuildMenuTool id, string glyph, string tip, System.Action onClick, bool selected, Color color)[]
            {
                (BuildMenuTool.Select, "Sel", "Selector\nClick rooms on the tower to inspect them.",
                    () => build.SelectTool(),
                    build.CurrentTool == BuildTool.Select,
                    new Color(0.55f, 0.55f, 0.6f)),
                (BuildMenuTool.Lobby, "Lob", "Extend Lobby\nDrag to widen the lobby on floor G.",
                    () => build.SelectLobbyTool(),
                    build.CurrentTool == BuildTool.PlaceRoom &&
                    build.SelectedRoomType != null &&
                    build.SelectedRoomType.isLobby,
                    new Color(0.75f, 0.65f, 0.35f)),
                (BuildMenuTool.SkyLobby, "Sky", "Sky Lobby\nTransfer floor: ≥15 up, ≥15 apart from other lobbies.",
                    () => build.SelectSkyLobbyTool(),
                    build.CurrentTool == BuildTool.PlaceRoom &&
                    build.SelectedRoomType != null &&
                    build.SelectedRoomType.isSkyLobby,
                    new Color(0.72f, 0.78f, 0.92f)),
                (BuildMenuTool.Scaffold, "Sc", "Scaffold ($750)\nClick or drag to place walkable structural fill.",
                    () => build.SelectScaffoldTool(),
                    build.CurrentTool == BuildTool.Scaffold,
                    new Color(0.76f, 0.62f, 0.40f)),
                (BuildMenuTool.Bulldoze, "X", "Bulldoze\nDemolish a non-lobby room (grace refund if eligible).",
                    () => build.SetTool(BuildTool.Bulldoze),
                    build.CurrentTool == BuildTool.Bulldoze,
                    new Color(0.75f, 0.3f, 0.28f))
            };

            return DrawIconRow(
                cy,
                tools.Length,
                MenuStripColumns,
                i =>
                {
                    var tool = tools[i];
                    if (DrawPictureButton(
                            IconRect(cx, cy, i, MenuStripColumns),
                            MenuIconIdForTool(tool.id),
                            tool.glyph,
                            tool.tip,
                            tool.color,
                            tool.selected,
                            enabled: true,
                            iconStyle))
                    {
                        _expandedFamily = null;
                        _expandedShopSubgroup = null;
                        tool.onClick();
                    }
                });
        }

        float DrawIconRow(
            float cy,
            int count,
            int columns,
            System.Action<int> drawIndex)
        {
            if (count <= 0) return cy;
            var cols = Mathf.Max(1, columns);
            for (var i = 0; i < count; i++)
                drawIndex(i);

            var rows = Mathf.CeilToInt(count / (float)cols);
            return cy + rows * (IconSize + IconGap) + 4f;
        }

        static Rect IconRect(float cx, float cy, int index, int columns)
        {
            var cols = Mathf.Max(1, columns);
            var col = index % cols;
            var row = index / cols;
            return new Rect(
                cx + col * (IconSize + IconGap),
                cy + row * (IconSize + IconGap),
                IconSize,
                IconSize);
        }

        bool DrawPictureButton(
            Rect rect,
            string iconId,
            string fallbackGlyph,
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

            // Peek whether an icon exists so we can use a neutral plate (not loud family tint)
            // behind transparent keyed corners.
            Texture2D preview = null;
            var willDrawIcon = !string.IsNullOrEmpty(iconId) &&
                               MenuIconArt.TryGetTexture(iconId, out preview) &&
                               preview != null;
            if (willDrawIcon)
            {
                fill = selected
                    ? new Color(0.22f, 0.22f, 0.24f, enabled ? 1f : 0.45f)
                    : new Color(0.16f, 0.16f, 0.18f, enabled ? 1f : 0.45f);
            }

            GUI.DrawTexture(rect, _whiteTex, ScaleMode.StretchToFill, false, 0f, fill, 0f, 0f);

            var hasTex = TryDrawMenuIcon(rect, iconId, enabled);
            if (selected)
            {
                var outline = new Color(1f, 0.9f, 0.4f, 1f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
                GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), _whiteTex, ScaleMode.StretchToFill, false, 0f, outline, 0f, 0f);
            }

            GUI.backgroundColor = new Color(1f, 1f, 1f, 0f);
            GUI.contentColor = Luminance(color) > 0.55f ? Color.black : Color.white;
            var label = hasTex ? string.Empty : fallbackGlyph ?? string.Empty;
            var clicked = GUI.Button(rect, new GUIContent(label, tooltip), style);
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;

            if (rect.Contains(Event.current.mousePosition) && !string.IsNullOrEmpty(tooltip))
                _hoverTooltip = tooltip;

            return clicked;
        }

        static bool TryDrawMenuIcon(Rect rect, string iconId, bool enabled)
        {
            if (string.IsNullOrEmpty(iconId))
                return false;

            Texture2D tex = null;
            var hasTex = false;
            try
            {
                hasTex = MenuIconArt.TryGetTexture(iconId, out tex) && tex != null;
            }
            catch
            {
                return false;
            }

            if (!hasTex)
                return false;

            // Full-bleed cover texture — stretch into the button square.
            var inset = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            var tint = enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            GUI.DrawTexture(inset, tex, ScaleMode.StretchToFill, true, 0f, tint, 0f, 0f);
            return true;
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

        string RoomTooltip(RoomTypeSO room, bool canBuild)
        {
            var lines = $"{room.displayName}";
            if (!canBuild)
                lines += $"\nLocked — needs {room.requiredStars}★";
            lines += $"\n{RoomEconomyFormat.CostLine(room)}";
            lines += $"\n{RoomEconomyFormat.IncomeLine(room)}";
            var upkeep = RoomEconomyFormat.UpkeepLine(room);
            if (upkeep != null)
                lines += $"\n{upkeep}";
            var hotelLines = new List<string>();
            RoomEconomyFormat.AppendHotelSelectionLines(hotelLines, room);
            RoomEconomyFormat.AppendOfficeSelectionLines(hotelLines, room);
            RoomEconomyFormat.AppendCondoSelectionLines(hotelLines, room);
            foreach (var line in hotelLines)
                lines += $"\n{line}";
            lines += $"\nSize {room.size.x}×{room.size.y}";
            var floorY = build != null && build.HoverCell.HasValue
                ? build.HoverCell.Value.y
                : 0;
            var starsNow = simulation?.Stars?.CurrentStars ?? 0;
            var fitWarn = RoomEconomyFormat.FloorFitWarningOrNull(room, floorY, starsNow);
            if (fitWarn != null)
                lines += $"\n{fitWarn}";
            return lines;
        }

        static string FamilyGlyph(BuildFamily family) => family switch
        {
            BuildFamily.Office => "Of",
            BuildFamily.Hotel => "Ht",
            BuildFamily.Condo => "Co",
            BuildFamily.Shops => "Sh",
            BuildFamily.Leisure => "Le",
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
            BuildFamily.Leisure => new Color(0.25f, 0.65f, 0.70f),
            BuildFamily.Utility => new Color(0.45f, 0.7f, 0.75f),
            BuildFamily.Transit => new Color(0.7f, 0.7f, 0.35f),
            _ => Color.gray
        };

        static string RoomGlyph(RoomTypeSO room)
        {
            if (room == null) return "?";
            if (room.isElevatorShaft)
            {
                return room.id switch
                {
                    "elevator_express" => "Ex",
                    "elevator_service" => "Sv",
                    _ => "El"
                };
            }
            if (room.isStairs) return "St";
            if (room.isParkingRamp) return "Rm";
            if (!string.IsNullOrEmpty(room.id))
            {
                if (room.id.Contains("premium")) return "P" + FamilyGlyph(room.ResolvedBuildFamily())[0];
                if (room.id == "hotel_base") return "Ba";
                if (room.id == "hotel_accessible") return "Ac";
                if (room.id == "hotel_mid_standard") return "MS";
                if (room.id == "hotel_mid_extended") return "ME";
                if (room.id == "hotel_studio") return "Su";
                if (room.id == "hotel_junior_suite") return "Jr";
                if (room.id == "hotel_upper_standard") return "US";
                if (room.id == "hotel_upper_king") return "UK";
                if (room.id == "hotel_upper_suite") return "Up";
                if (room.id == "office_micro") return "Om";
                if (room.id == "office_studio") return "Os";
                if (room.id == "office_base") return "Ob";
                if (room.id == "office") return "Of";
                if (room.id == "office_premium") return "Op";
                if (room.id == "office_mid_standard") return "Mo";
                if (room.id == "office_mid_clinic") return "Cl";
                if (room.id == "office_mid_team") return "Tb";
                if (room.id == "office_upper_standard") return "Uo";
                if (room.id == "office_upper_corner") return "Uc";
                if (room.id == "office_upper_floor") return "Fl";
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

        void DrawTimeSpeedButtons(float x, float y, float width, float height)
        {
            if (simulation?.Clock == null) return;
            if (_pauseUi != PauseUiState.Playing) return;
            // Celebration pause snapshot must stay intact until Continue.
            if (ResolveCelebration()?.IsModalOpen == true) return;

            // minutesPerRealSecond: 360x ≈ 6 game hours/sec; 1d = 1440 = one game day/sec.
            var labels = new[] { "||", "1x", "5x", "60x", "360x", "1d" };
            var speeds = new[] { 0f, 1f, 5f, 60f, 360f, 1440f };
            const float gap = 3f;
            // Weight later buttons wider so "360x" / "1d" are not clipped.
            var weights = new[] { 0.8f, 0.85f, 0.85f, 1.05f, 1.25f, 1.0f };
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

            RoomEconomyFormat.AppendHotelSelectionLines(lines, type);
            RoomEconomyFormat.AppendOfficeSelectionLines(lines, type);
            RoomEconomyFormat.AppendCondoSelectionLines(lines, type);

            return lines;
        }

        static string ShortLabel(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "Room";
            if (displayName == "Hotel" || displayName == "Premium Hotel") return "Hotel";
            if (displayName.StartsWith("Retail")) return "Retail";
            if (displayName.StartsWith("Stairs")) return "Stairs";
            if (displayName.StartsWith("Elevator")) return "Elevator";
            if (displayName.StartsWith("Office") || displayName.EndsWith("Office") ||
                displayName.Contains("Suite") && displayName.Contains("Professional") ||
                displayName == "Team Bay" || displayName == "Corner Suite" || displayName == "Corporate Floor" ||
                displayName == "Micro Office" || displayName == "Studio Office" || displayName == "Small Office" ||
                displayName == "Mid Office" || displayName == "Upper Office")
            {
                if (displayName == "Micro Office") return "Micro";
                if (displayName == "Studio Office") return "Studio";
                if (displayName == "Small Office") return "Small";
                if (displayName == "Mid Office") return "Mid Ofc";
                if (displayName == "Professional Suite") return "Clinic";
                if (displayName == "Team Bay") return "Team";
                if (displayName == "Upper Office") return "Upper";
                if (displayName == "Corner Suite") return "Corner";
                if (displayName == "Corporate Floor") return "Corp";
                if (displayName.Contains("Premium")) return "Prem. Office";
                return "Office";
            }
            if (displayName.StartsWith("Condo") || displayName == "Studio" || displayName == "Alcove Studio" ||
                displayName == "One Bedroom" || displayName == "Mid Condo" || displayName == "Loft" ||
                displayName == "Family Condo" || displayName == "Upper Condo" || displayName == "Corner Condo" ||
                displayName == "Penthouse")
            {
                if (displayName == "Studio") return "Studio";
                if (displayName == "Alcove Studio") return "Alcove";
                if (displayName == "One Bedroom") return "1-Bed";
                if (displayName == "Mid Condo") return "Mid Condo";
                if (displayName == "Loft") return "Loft";
                if (displayName == "Family Condo") return "Family";
                if (displayName == "Upper Condo") return "Upper";
                if (displayName == "Corner Condo") return "Corner";
                if (displayName == "Penthouse") return "Penthouse";
                if (displayName.Contains("Premium")) return "Prem. Condo";
                return "Condo";
            }
            return displayName;
        }
    }
}
