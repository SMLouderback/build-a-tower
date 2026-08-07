using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Owns the clock, transit systems, and agents. Auto-added beside BuildController.
    /// </summary>
    public sealed class TowerSimulation : MonoBehaviour
    {
        public const int OpsDirtyHotelThreshold = 5;
        public const float OpsAvgCrimeThreshold = 35f;
        public const float OpsAvgStressThreshold = 40f;
        public const int QuirkEveryNDays = 2;

        static readonly string[] QuirkLines =
        {
            "Someone held an elevator for a sandwich run on floor 3.",
            "A lobby plant was renamed 'Steve' by anonymous vote.",
            "Office workers debated the ethics of microwave fish.",
            "A courier left a mysterious box labeled 'do not open until Friday'.",
            "Security clocked a raccoon as a VIP visitor. Briefly.",
            "The coffee machine on 7 declared war on oat milk. Again.",
            "Lost-and-found now claims three left shoes and one right sock.",
            "A meeting was postponed because nobody could find the clicker.",
            "Night cleaning found a sticky note that just said 'trust the stairs'.",
            "Two guests argued about which floor has the best window glare.",
            "Facilities booked a ladder for a lightbulb that was already fine.",
            "Someone rated the lobby scent 'aggressively citrus' in a survey.",
            "An office pool started on which elevator arrives last.",
            "A whiteboard still says 'synergy' from last quarter. Nobody erased it.",
            "The gift shop sold out of tiny tower magnets before lunch.",
            "A guest asked if the stairs count as cardio. Staff said yes.",
            "Maintenance hummed an elevator jingle that does not exist.",
            "A vendor delivered 40 chairs and one suspiciously heavy box of air.",
            "Floor 2's vending machine dispensed two bags of chips for the price of one.",
            "Anonymous tip: the best wifi is mysteriously near the janitor closet.",
            "A tourist asked which floor has the 'main character energy'. Staff pointed at Lobby.",
            "Someone laminated a meme and hung it in the copy room. It has tenure now.",
            "The revolving door collected three umbrellas and one strongly worded note.",
            "Break-room fridge trial continues: unlabeled soup enters day four. Nobody blinks.",
            "A delivery drone buzzed the atrium. Security wrote 'sky raccoon?' on the log.",
            "Floor directory stickers keep migrating overnight. Suspect: interns or ghosts."
        };

        static readonly string[] OpsDirtyHotelLines =
        {
            "Housekeeping backlog: dirty rooms may miss afternoon check-ins.",
            "Housekeeping is swamped — turnovers are slipping into the evening.",
            "Too many dirty hotel rooms; front desk is delaying some arrivals."
        };

        static readonly string[] OpsCrimeLines =
        {
            "Crime spike across the tower — tenants are on edge.",
            "Security reports elevated incidents; guests are uneasy.",
            "Average crime is high — patrol coverage may be thin."
        };

        static readonly string[] OpsStressLines =
        {
            "Elevator waits and crowding are stressing tenants.",
            "Transit delays are wearing on the tower — stress is climbing.",
            "Crowded lobbies and slow rides are fraying nerves."
        };

        int _lastQuirkIndex = -1;

        [SerializeField] BuildController build;
        [SerializeField] AgentView agentView;
        [SerializeField] ElevatorView elevatorView;
        [SerializeField] float minutesPerRealSecond = 1f;
        [SerializeField] int startMinuteOfDay = 6 * 60;

        GameClock _clock;
        StairsPathfinder _pathfinder;
        ElevatorSystem _elevators;
        TransitRouter _router;
        AgentSystem _agents;
        CrimeSystem _crime;
        EconomySystem _economy;
        ResearchSystem _research;
        ConferenceSystem _conference;
        TowerNews _news;
        StarSystem _stars;
        MarketClimate _climate;
        readonly System.Random _climateRng = new();
        readonly System.Random _conferenceRng = new();
        readonly System.Random _newsRng = new();
        readonly List<int> _patrolFloors = new();
        readonly List<int> _criminalFloors = new();
        int _lastDayIndex;
        bool _subscribed;

        public GameClock Clock => _clock;
        public AgentSystem Agents => _agents;
        public CrimeSystem Crime => _crime;
        public EconomySystem Economy => _economy;
        public ResearchSystem Research => _research;
        public ConferenceSystem Conference => _conference;
        public TowerNews News => _news;
        public StarSystem Stars => _stars;
        public MarketClimate Climate => _climate;
        public StairsPathfinder Pathfinder => _pathfinder;
        public ElevatorSystem Elevators => _elevators;
        public TransitRouter Router => _router;

        const float AgentSampleIntervalGameMinutes = 2f;
        float _agentSampleAccumulator;
        TowerMapController _mapController;

        public void SetSpeedPreset(float minutesPerRealSecond, bool paused)
        {
            _clock.Paused = paused;
            if (!paused)
                _clock.MinutesPerRealSecond = minutesPerRealSecond;
        }

        void Awake()
        {
            if (build == null)
                build = GetComponent<BuildController>() ?? FindAnyObjectByType<BuildController>();

            EnsureDayNightSkyController();

            _clock = new GameClock(minutesPerRealSecond, startMinuteOfDay);
            _elevators = new ElevatorSystem();
            _pathfinder = new StairsPathfinder();
            _router = new TransitRouter(_pathfinder, _elevators);
            _agents = new AgentSystem(_router);
            _crime = new CrimeSystem();
            _economy = new EconomySystem();
            _research = new ResearchSystem();
            _conference = new ConferenceSystem();
            _news = new TowerNews();
            _stars = new StarSystem();
            _climate = new MarketClimate();
            _lastDayIndex = _clock.DayIndex;
            _clock.DayRolled += OnDayRolled;
            _clock.MonthRolled += OnMonthRolled;

            if (agentView == null)
            {
                var viewGo = new GameObject("AgentView");
                viewGo.transform.SetParent(transform, false);
                agentView = viewGo.AddComponent<AgentView>();
            }

            if (elevatorView == null)
            {
                var viewGo = new GameObject("ElevatorView");
                viewGo.transform.SetParent(transform, false);
                elevatorView = viewGo.AddComponent<ElevatorView>();
            }
            elevatorView.Bind(_elevators);
        }

        void OnEnable() => TrySubscribe();

        void Start()
        {
            TrySubscribe();
            OnGridChanged();
        }

        void OnDisable()
        {
            if (build != null && _subscribed)
            {
                build.GridChanged -= OnGridChanged;
                _subscribed = false;
            }
        }

        void OnDestroy()
        {
            if (_clock != null)
            {
                _clock.DayRolled -= OnDayRolled;
                _clock.MonthRolled -= OnMonthRolled;
            }
        }

        void Update()
        {
            if (build?.Grid == null || _clock == null || _agents == null) return;
            _clock.Tick(Time.deltaTime);
            var research = _research;
            _router?.SetWaitWeightScale(ResearchEffects.ElevatorRoutingWaitWeightScale(research));
            _elevators.Tick(
                _clock.LastTickGameMinutes,
                ResearchEffects.ElevatorSpeedMultiplier(research));
            _agents.Tick(
                _clock.LastTickGameMinutes,
                _clock,
                build.Grid,
                _stars?.CurrentStars ?? 0,
                _climate,
                _crime,
                research,
                _conference);
            _agents.CollectFloorsForRole(AgentRole.Security, _patrolFloors);
            _agents.CollectFloorsForRole(AgentRole.Criminal, _criminalFloors);
            _crime.Tick(
                _clock.LastTickGameMinutes,
                CrimeFloorLoads.ShopLoadByFloor(build.Grid),
                CrimeFloorLoads.HotelLoadByFloor(build.Grid, _agents.Agents),
                CountStaffedSecurity(build.Grid),
                _patrolFloors,
                _criminalFloors,
                ResearchEffects.CrimeSuppressionMultiplier(research));
            research?.TickProgress(
                _clock.LastTickGameMinutes,
                EconomySystem.CountResearcherPool(build.Grid));
            SampleAgentsForMaps(_clock.LastTickGameMinutes);
            if (agentView != null)
                agentView.Sync(_agents.Agents);
        }

        void SampleAgentsForMaps(float gameMinutes)
        {
            if (gameMinutes <= 0f || _agents?.Agents == null) return;
            _agentSampleAccumulator += gameMinutes;
            if (_agentSampleAccumulator < AgentSampleIntervalGameMinutes) return;
            _agentSampleAccumulator = 0f;

            var maps = EnsureMapController();
            if (maps == null) return;

            foreach (var agent in _agents.Agents)
            {
                if (agent == null) continue;
                if (agent.Phase == AgentPhase.Outside) continue;

                if (agent.Phase == AgentPhase.WaitingAtElevator)
                    maps.SampleAgentCell(agent.Cell, waiting: true);
                else if (agent.Phase is AgentPhase.Moving or AgentPhase.Riding)
                    maps.SampleAgentCell(agent.Cell, waiting: false);
            }
        }

        TowerMapController EnsureMapController()
        {
            if (_mapController != null) return _mapController;
            _mapController = GetComponent<TowerMapController>() ??
                             FindAnyObjectByType<TowerMapController>();
            return _mapController;
        }

        static float DemandProxyFromVacancy(TowerGrid grid, AgentSystem agents) =>
            TowerMapController.ComputeTowerVacancyPressure(grid, agents);

        void TrySubscribe()
        {
            if (_subscribed || build == null) return;
            build.GridChanged += OnGridChanged;
            _subscribed = true;
        }

        void OnGridChanged()
        {
            if (build?.Grid == null || _router == null || _agents == null) return;
            _router.Rebuild(build.Grid);
            _agents.SyncHomes(
                build.Grid,
                room => _economy?.TrySellCondo(room, build.Wallet),
                _stars?.CurrentStars ?? 0,
                _climate?.ComfortTierOffset ?? 0,
                _crime?.AverageCrime ?? 0f);
            _stars?.TryPromote(build.Grid, _agents.AverageStress, _agents.Population);
        }

        void OnMonthRolled()
        {
            _climate?.OnMonthRolled(_climateRng);
        }

        void OnDayRolled()
        {
            if (build?.Grid == null || _agents == null || _economy == null || _stars == null)
                return;

            var climateOffset = _climate?.ComfortTierOffset ?? 0;
            var climateSpendMult = _climate?.SpendMultiplier ?? 1f;
            for (var day = _lastDayIndex + 1; day <= _clock.DayIndex; day++)
            {
                // Event schedule first so pending lump / daily credits land in OnNewDay.
                _conference?.TickDay(
                    day,
                    build.Grid,
                    CountHotelGuests(_agents.Agents),
                    _stars.CurrentStars,
                    climateSpendMult,
                    build.Wallet,
                    _news,
                    _conferenceRng);

                _economy.OnNewDay(
                    build.Grid,
                    _agents.Agents,
                    build.Wallet,
                    _stars.CurrentStars,
                    climateOffset,
                    _research,
                    climateSpendMult,
                    _conference);

                _elevators?.ArchiveDay();

                _agents.SyncEventVisitors(_conference, build.Grid, _clock);

                PushOpsAndQuirkNews(
                    day,
                    build.Grid,
                    _agents.AverageStress,
                    _crime?.AverageCrime ?? 0f);

                // §7.3: decay all incomplete stored progress except active running unpaused.
                _research?.TickDayDecay();

                if (day > 0 && day % StarSystem.QuarterDays == 0)
                    _stars.EvaluateQuarterly(build.Grid, _agents.AverageStress, _agents.Population);
                else
                    _stars.TryPromote(build.Grid, _agents.AverageStress, _agents.Population);

                EnsureMapController()?.NotifyMidnight(
                    day,
                    _climate?.Step ?? MarketClimate.Normal,
                    climateSpendMult,
                    DemandProxyFromVacancy(build.Grid, _agents),
                    _agents.Population,
                    _economy.LastIncome,
                    _economy.LastExpense,
                    build.Wallet.Balance,
                    _stars.CurrentStars);
            }

            _lastDayIndex = _clock.DayIndex;
        }

        void PushOpsAndQuirkNews(int dayIndex, TowerGrid grid, float averageStress, float averageCrime)
        {
            if (_news == null) return;

            var dirtyHotels = CountDirtyHotels(grid);
            if (dirtyHotels >= OpsDirtyHotelThreshold)
                PushOps(dayIndex, PickLine(OpsDirtyHotelLines), priority: 8);

            if (averageCrime >= OpsAvgCrimeThreshold)
                PushOps(dayIndex, PickLine(OpsCrimeLines), priority: 9);

            if (averageStress >= OpsAvgStressThreshold)
                PushOps(dayIndex, PickLine(OpsStressLines), priority: 7);

            PushHolidayNews(dayIndex);

            if (dayIndex > 0 && dayIndex % QuirkEveryNDays == 0 && QuirkLines.Length > 0)
            {
                var line = PickQuirkLine();
                _news.Push(new TowerNewsItem
                {
                    Category = TowerNewsCategory.Quirk,
                    Priority = 1,
                    Text = line,
                    CreatedDayIndex = dayIndex,
                    ExpireDayIndex = dayIndex + 4
                });
            }
        }

        void PushHolidayNews(int dayIndex)
        {
            var date = GameClock.DateForDayIndex(dayIndex);
            var matches = HolidayNewsCatalog.MatchesFor(date);
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (match.Lines == null || match.Lines.Length == 0) continue;
                var line = PickLine(match.Lines);
                if (string.IsNullOrEmpty(line)) continue;
                _news.Push(new TowerNewsItem
                {
                    Category = TowerNewsCategory.Quirk,
                    Priority = 3,
                    Text = line,
                    CreatedDayIndex = dayIndex,
                    ExpireDayIndex = dayIndex + 3
                });
            }
        }

        string PickLine(string[] lines)
        {
            if (lines == null || lines.Length == 0) return string.Empty;
            return lines[_newsRng.Next(0, lines.Length)];
        }

        string PickQuirkLine()
        {
            if (QuirkLines.Length == 1)
                return QuirkLines[0];

            var index = _newsRng.Next(0, QuirkLines.Length);
            // Avoid immediate repeat of the last quirk when the pool is larger than one.
            if (index == _lastQuirkIndex)
                index = (index + 1 + _newsRng.Next(0, QuirkLines.Length - 1)) % QuirkLines.Length;
            _lastQuirkIndex = index;
            return QuirkLines[index];
        }

        void PushOps(int dayIndex, string text, int priority)
        {
            _news.Push(new TowerNewsItem
            {
                Category = TowerNewsCategory.OpsSerious,
                Priority = priority,
                Text = text,
                CreatedDayIndex = dayIndex,
                ExpireDayIndex = dayIndex + 5
            });
        }

        static int CountDirtyHotels(TowerGrid grid)
        {
            if (grid == null) return 0;

            var total = 0;
            foreach (var room in grid.Rooms)
            {
                if (room == null || ReferenceEquals(room.Type, null)) continue;
                if (room.Type.category != RoomCategory.Hotel) continue;
                if (room.Dirty)
                    total++;
            }

            return total;
        }

        static int CountStaffedSecurity(TowerGrid grid)
        {
            if (grid == null) return 0;

            var total = 0;
            foreach (var room in grid.Rooms)
            {
                if (room?.Type?.id != "service_security") continue;
                if (room.IsBroken) continue;
                total += room.StaffedWorkers;
            }

            return total;
        }

        static int CountHotelGuests(IReadOnlyList<Agent> agents)
        {
            if (agents == null) return 0;

            var total = 0;
            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                // Match star Population: only guests currently staying in the tower.
                if (agent != null &&
                    agent.Role == AgentRole.HotelGuest &&
                    agent.Phase != AgentPhase.Outside)
                    total++;
            }

            return total;
        }

        /// <summary>
        /// Scene wiring preferred; this covers play from scenes that omit the component.
        /// </summary>
        static void EnsureDayNightSkyController()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (cam.GetComponent<DayNightSkyController>() != null) return;
            cam.gameObject.AddComponent<DayNightSkyController>();
        }
    }
}
