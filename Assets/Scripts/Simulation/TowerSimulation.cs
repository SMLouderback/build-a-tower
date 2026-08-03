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
                research);
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
            if (agentView != null)
                agentView.Sync(_agents.Agents);
        }

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
                _climate?.ComfortTierOffset ?? 0);
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
                _economy.OnNewDay(
                    build.Grid,
                    _agents.Agents,
                    build.Wallet,
                    _stars.CurrentStars,
                    climateOffset,
                    _research,
                    climateSpendMult,
                    _conference);

                _conference?.TickDay(
                    day,
                    build.Grid,
                    CountHotelGuests(_agents.Agents),
                    _stars.CurrentStars,
                    climateSpendMult,
                    build.Wallet,
                    _news,
                    _conferenceRng);

                _agents.SyncEventVisitors(_conference, build.Grid, _clock);

                // §7.3: decay all incomplete stored progress except active running unpaused.
                _research?.TickDayDecay();

                if (day > 0 && day % StarSystem.QuarterDays == 0)
                    _stars.EvaluateQuarterly(build.Grid, _agents.AverageStress, _agents.Population);
                else
                    _stars.TryPromote(build.Grid, _agents.AverageStress, _agents.Population);
            }

            _lastDayIndex = _clock.DayIndex;
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
                if (agent != null && agent.Role == AgentRole.HotelGuest)
                    total++;
            }

            return total;
        }
    }
}
