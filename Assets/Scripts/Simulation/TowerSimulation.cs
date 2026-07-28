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
        bool _subscribed;

        public GameClock Clock => _clock;
        public AgentSystem Agents => _agents;
        public StairsPathfinder Pathfinder => _pathfinder;
        public ElevatorSystem Elevators => _elevators;
        public TransitRouter Router => _router;

        void Awake()
        {
            if (build == null)
                build = GetComponent<BuildController>() ?? FindAnyObjectByType<BuildController>();

            _clock = new GameClock(minutesPerRealSecond, startMinuteOfDay);
            _elevators = new ElevatorSystem();
            _pathfinder = new StairsPathfinder();
            _router = new TransitRouter(_pathfinder, _elevators);
            _agents = new AgentSystem(_router);

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

        void Update()
        {
            if (build?.Grid == null || _clock == null || _agents == null) return;
            _clock.Tick(Time.deltaTime);
            _elevators.Tick(_clock.LastTickGameMinutes);
            _agents.Tick(Time.deltaTime, _clock, build.Grid);
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
            _agents.SyncHomes(build.Grid);
        }
    }
}
