using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Owns day clock, stairs pathfinder, and agents. Auto-added beside BuildController.
    /// </summary>
    public sealed class TowerSimulation : MonoBehaviour
    {
        [SerializeField] BuildController build;
        [SerializeField] AgentView agentView;
        [SerializeField] float minutesPerRealSecond = 1f;
        [SerializeField] int startMinuteOfDay = 6 * 60;

        GameClock _clock;
        StairsPathfinder _pathfinder;
        AgentSystem _agents;
        bool _subscribed;

        public GameClock Clock => _clock;
        public AgentSystem Agents => _agents;
        public StairsPathfinder Pathfinder => _pathfinder;

        void Awake()
        {
            if (build == null)
                build = GetComponent<BuildController>() ?? FindAnyObjectByType<BuildController>();

            _clock = new GameClock(minutesPerRealSecond, startMinuteOfDay);
            _pathfinder = new StairsPathfinder();
            _agents = new AgentSystem(_pathfinder);

            if (agentView == null)
            {
                var viewGo = new GameObject("AgentView");
                viewGo.transform.SetParent(transform, false);
                agentView = viewGo.AddComponent<AgentView>();
            }
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
            if (build?.Grid == null || _pathfinder == null || _agents == null) return;
            _pathfinder.Rebuild(build.Grid);
            _agents.SyncHomes(build.Grid);
        }
    }
}
