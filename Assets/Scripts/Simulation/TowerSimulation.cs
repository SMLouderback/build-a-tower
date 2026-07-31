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
        EconomySystem _economy;
        StarSystem _stars;
        int _lastDayIndex;
        bool _subscribed;

        public GameClock Clock => _clock;
        public AgentSystem Agents => _agents;
        public EconomySystem Economy => _economy;
        public StarSystem Stars => _stars;
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
            _economy = new EconomySystem();
            _stars = new StarSystem();
            _lastDayIndex = _clock.DayIndex;
            _clock.DayRolled += OnDayRolled;

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
                _clock.DayRolled -= OnDayRolled;
        }

        void Update()
        {
            if (build?.Grid == null || _clock == null || _agents == null) return;
            _clock.Tick(Time.deltaTime);
            _elevators.Tick(_clock.LastTickGameMinutes);
            _agents.Tick(
                _clock.LastTickGameMinutes,
                _clock,
                build.Grid,
                _stars?.CurrentStars ?? 0);
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
                _stars?.CurrentStars ?? 0);
            _stars?.TryPromote(build.Grid, _agents.AverageStress, _agents.Population);
        }

        void OnDayRolled()
        {
            if (build?.Grid == null || _agents == null || _economy == null || _stars == null)
                return;

            for (var day = _lastDayIndex + 1; day <= _clock.DayIndex; day++)
            {
                _economy.OnNewDay(build.Grid, _agents.Agents, build.Wallet, _stars.CurrentStars);

                if (day > 0 && day % StarSystem.QuarterDays == 0)
                    _stars.EvaluateQuarterly(build.Grid, _agents.AverageStress, _agents.Population);
                else
                    _stars.TryPromote(build.Grid, _agents.AverageStress, _agents.Population);
            }

            _lastDayIndex = _clock.DayIndex;
        }
    }
}
