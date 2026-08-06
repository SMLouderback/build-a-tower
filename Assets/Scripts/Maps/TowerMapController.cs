using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Rebuilds map scores from the live tower and paints the heatmap overlay.
    /// </summary>
    public sealed class TowerMapController : MonoBehaviour
    {
        [SerializeField] BuildController build;
        [SerializeField] TowerSimulation simulation;
        [SerializeField] TilemapTowerView view;
        [SerializeField] float rebuildSeconds = 1.5f;

        public TowerMapAnalytics Analytics { get; } = new();
        public TowerMapMode Mode { get; private set; } = TowerMapMode.Off;
        public TrafficMapWindow TrafficWindow { get; set; } = TrafficMapWindow.Today;
        public EconomicMapView EconomicView { get; set; } = EconomicMapView.Blend;

        float _rebuildTimer;
        int _lastArchivedDay = int.MinValue;

        void Awake()
        {
            if (build == null)
                build = FindAnyObjectByType<BuildController>();
            if (simulation == null)
                simulation = build != null
                    ? build.GetComponent<TowerSimulation>()
                    : FindAnyObjectByType<TowerSimulation>();
            if (view == null && build != null)
                view = build.GetComponent<TilemapTowerView>() ?? FindAnyObjectByType<TilemapTowerView>();
        }

        void Update()
        {
            if (Mode == TowerMapMode.Off || Mode == TowerMapMode.Graph)
                return;

            _rebuildTimer -= Time.unscaledDeltaTime;
            if (_rebuildTimer > 0f) return;
            _rebuildTimer = rebuildSeconds;
            RebuildAndPaint();
        }

        public void SetMode(TowerMapMode mode)
        {
            Mode = mode;
            if (mode == TowerMapMode.Off || mode == TowerMapMode.Graph)
            {
                view?.ClearHeatmap();
                return;
            }

            _rebuildTimer = 0f;
            RebuildAndPaint();
        }

        public void NotifyMidnight(int dayIndex, int climateStep, float spendMult, float demandProxy)
        {
            if (_lastArchivedDay != dayIndex)
            {
                Analytics.ArchiveTrafficDay();
                Analytics.RecordClimateSample(climateStep, spendMult, demandProxy);
                _lastArchivedDay = dayIndex;
            }
        }

        public void SampleAgentCell(Vector2Int cell, bool waiting)
        {
            if (waiting) Analytics.RecordWait(cell);
            else Analytics.RecordTraversal(cell);
        }

        public void RebuildAndPaint()
        {
            if (build?.Grid == null || view == null) return;
            RebuildScores();
            if (Mode is TowerMapMode.Off or TowerMapMode.Graph)
            {
                view.ClearHeatmap();
                return;
            }

            var roomCells = CollectRoomCells(build.Grid);
            var scale = Mode == TowerMapMode.Economic && EconomicView == EconomicMapView.Profit
                ? HeatmapColorScale.Profit
                : HeatmapColorScale.Risk;
            view.PaintHeatmap(roomCells, Analytics.EnumerateScores(Mode, EconomicView), scale);
        }

        void RebuildScores()
        {
            var grid = build.Grid;
            var agents = simulation?.Agents;
            var crime = simulation?.Crime;
            var economy = simulation?.Economy;
            var conference = simulation?.Conference;
            var clock = simulation?.Clock;
            var minute = clock != null ? clock.MinuteOfDay : 12 * 60;
            var stars = simulation?.Stars?.CurrentStars ?? 0;
            var climateOffset = simulation?.Climate?.ComfortTierOffset ?? 0;

            var capacity = new Dictionary<Vector2Int, float>();
            CollectTransitCapacityStress(grid, capacity);
            Analytics.RebuildTraffic(TrafficWindow, capacity);

            var crimeMap = new Dictionary<Vector2Int, float>();
            var noiseMap = new Dictionary<Vector2Int, float>();
            var demand = new Dictionary<Vector2Int, float>();

            // Pass 1: collect raw net per room for tower-wide profit extremes.
            var roomNets = new Dictionary<int, int>();
            var maxProfit = 0;
            var maxLossAbs = 0;
            if (economy != null)
            {
                foreach (var room in grid.Rooms)
                {
                    if (room?.Type == null) continue;
                    var income = economy.GetLastRoomIncome(room);
                    var expense = economy.GetLastRoomExpense(room);
                    var net = income - expense;
                    roomNets[room.InstanceId] = net;
                    if (net > maxProfit) maxProfit = net;
                    if (net < 0)
                    {
                        var lossAbs = -net;
                        if (lossAbs > maxLossAbs) maxLossAbs = lossAbs;
                    }
                }
            }

            var profit = new Dictionary<Vector2Int, float>();

            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null) continue;
                var occupied = RoomHasOccupant(room, agents);
                var eventBusy = IsEventOrConferenceBusy(room, conference);
                var crimeNear = crime != null && crime.AverageCrime > 40f;
                var emit = NoiseEmitterWeights.Emit(room.Type, occupied, crimeNear, eventBusy);
                var bother = NoiseEmitterWeights.ResidentialBotherFactor(room.Type, minute);

                float p = 0f, d = 0f;
                if (economy != null)
                {
                    roomNets.TryGetValue(room.InstanceId, out var net);
                    p = HeatmapColors.NormalizeTowerProfit(net, maxProfit, maxLossAbs);
                    d = DemandStress(room, agents, stars, climateOffset);
                }

                foreach (var cell in room.OccupiedCells())
                {
                    var traffic = Analytics.GetScore(TowerMapMode.Traffic, cell);
                    var criminal = CriminalProximity(agents, cell);
                    var patrol = PatrolCoverage(agents, cell);
                    var eventBoost = eventBusy ? 0.6f : 0f;
                    crimeMap[cell] = TowerMapAnalytics.CrimeScore(traffic, criminal, eventBoost, patrol);

                    var noise = emit * bother + traffic * 0.25f;
                    noiseMap[cell] = TowerMapAnalytics.Clamp01(noise);

                    profit[cell] = p;
                    demand[cell] = d;
                }
            }

            Analytics.SetCrimeScores(crimeMap);
            Analytics.SetNoiseScores(noiseMap);
            Analytics.SetEconomicScores(profit, demand);
        }

        static List<Vector2Int> CollectRoomCells(TowerGrid grid)
        {
            var set = new HashSet<Vector2Int>();
            if (grid == null) return new List<Vector2Int>();
            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null) continue;
                foreach (var cell in room.OccupiedCells())
                    set.Add(cell);
            }

            return new List<Vector2Int>(set);
        }

        void CollectTransitCapacityStress(TowerGrid grid, Dictionary<Vector2Int, float> into)
        {
            var research = simulation?.Research;
            var speedMult = ResearchEffects.ElevatorSpeedMultiplier(research);
            var waitScale = Mathf.Max(0.25f, ResearchEffects.ElevatorRoutingWaitWeightScale(research));
            // Higher elevator research → higher effective capacity → lower stress.
            var researchMult = speedMult / waitScale;

            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null) continue;
                if (room.Type.isElevatorShaft)
                {
                    var occ = CountAgentsOnRoom(room);
                    var cap = 10f * Mathf.Max(1, room.Size.y / 2f);
                    var stress = TowerMapAnalytics.TrafficCapacityStress(occ, cap, researchMult);
                    if (stress <= 0f) continue;
                    foreach (var cell in room.OccupiedCells())
                    {
                        into.TryGetValue(cell, out var cur);
                        into[cell] = Mathf.Max(cur, stress);
                    }
                }
                else if (room.Type.isStairs)
                {
                    var occ = CountAgentsOnRoom(room);
                    var stress = TowerMapAnalytics.TrafficCapacityStress(occ, 5f, researchMult);
                    if (stress <= 0f) continue;
                    foreach (var cell in room.OccupiedCells())
                    {
                        into.TryGetValue(cell, out var cur);
                        into[cell] = Mathf.Max(cur, stress);
                    }
                }
            }
        }

        static bool RoomHasOccupant(RoomInstance room, AgentSystem agents)
        {
            if (room == null || agents?.Agents == null) return false;
            foreach (var agent in agents.Agents)
            {
                if (agent == null) continue;
                if (agent.HomeRoom != null && agent.HomeRoom.InstanceId == room.InstanceId)
                    return true;
                if (agent.WorkplaceRoom != null && agent.WorkplaceRoom.InstanceId == room.InstanceId)
                    return true;
                if (agent.VisitTarget != null && agent.VisitTarget.InstanceId == room.InstanceId)
                    return true;
                if (RoomContainsCell(room, agent.Cell))
                    return true;
            }

            return false;
        }

        int CountAgentsOnRoom(RoomInstance room)
        {
            var agents = simulation?.Agents?.Agents;
            if (agents == null) return 0;
            var n = 0;
            foreach (var agent in agents)
            {
                if (agent == null) continue;
                if (RoomContainsCell(room, agent.Cell)) n++;
            }

            return n;
        }

        /// <summary>
        /// Office/hotel/condo residents assigned to this room (HomeRoom), plus office
        /// desk holders via WorkplaceRoom (condo in-tower jobs).
        /// </summary>
        public static int CountAssignedOccupants(RoomInstance room, AgentSystem agents)
        {
            if (room == null || agents?.Agents == null) return 0;
            var n = 0;
            var isOffice = room.Type != null && room.Type.category == RoomCategory.Office;
            foreach (var agent in agents.Agents)
            {
                if (agent == null) continue;
                if (agent.HomeRoom != null && agent.HomeRoom.InstanceId == room.InstanceId)
                {
                    n++;
                    continue;
                }

                if (isOffice &&
                    agent.WorkplaceRoom != null &&
                    agent.WorkplaceRoom.InstanceId == room.InstanceId)
                    n++;
            }

            return n;
        }

        public static float DemandStress(
            RoomInstance room,
            AgentSystem agents,
            int stars = 0,
            int climateOffset = 0)
        {
            if (room?.Type == null) return 0f;
            var max = room.Type.maxOccupants;
            if (max <= 0) return 0f;

            var occupants = CountAssignedOccupants(room, agents);
            if (occupants > max) occupants = max;

            var overprice = PricePricing.OverpriceSteps(room.PriceTier, stars, climateOffset);
            return TowerMapAnalytics.LivingDemandStress(
                room.Type.category,
                occupants,
                max,
                room.CondoSold,
                overprice);
        }

        /// <summary>Vacant seats / total capacity across Office, Hotel, and Condo rooms.</summary>
        public static float ComputeTowerVacancyPressure(TowerGrid grid, AgentSystem agents)
        {
            if (grid == null) return 0f;
            var capacity = 0;
            var filled = 0;
            foreach (var room in grid.Rooms)
            {
                if (room?.Type == null) continue;
                var cat = room.Type.category;
                if (cat is not (RoomCategory.Office or RoomCategory.Hotel or RoomCategory.Condo))
                    continue;
                var max = room.Type.maxOccupants;
                if (max <= 0) continue;
                capacity += max;
                if (cat == RoomCategory.Condo && !room.CondoSold)
                    continue;
                var occ = CountAssignedOccupants(room, agents);
                filled += occ > max ? max : occ;
            }

            return TowerMapAnalytics.TowerVacancyPressure(capacity - filled, capacity);
        }

        static bool RoomContainsCell(RoomInstance room, Vector2Int cell)
        {
            if (room == null) return false;
            foreach (var c in room.OccupiedCells())
            {
                if (c == cell) return true;
            }

            return false;
        }

        static float CriminalProximity(AgentSystem agents, Vector2Int cell)
        {
            if (agents?.Agents == null) return 0f;
            var best = 0f;
            foreach (var agent in agents.Agents)
            {
                if (agent == null || agent.Role != AgentRole.Criminal) continue;
                var d = Mathf.Abs(agent.Cell.x - cell.x) + Mathf.Abs(agent.Cell.y - cell.y);
                if (d == 0) best = 1f;
                else if (d == 1) best = Mathf.Max(best, 0.7f);
                else if (d <= 3) best = Mathf.Max(best, 0.35f);
            }

            return best;
        }

        static float PatrolCoverage(AgentSystem agents, Vector2Int cell)
        {
            if (agents?.Agents == null) return 0f;
            var best = 0f;
            foreach (var agent in agents.Agents)
            {
                if (agent == null || agent.Role != AgentRole.Security) continue;
                var d = Mathf.Abs(agent.Cell.x - cell.x) + Mathf.Abs(agent.Cell.y - cell.y);
                if (d <= 2) best = Mathf.Max(best, 1f);
                else if (d <= 5) best = Mathf.Max(best, 0.45f);
            }

            return best;
        }

        static bool IsEventOrConferenceBusy(RoomInstance room, ConferenceSystem conference)
        {
            if (room?.Type?.id == null) return false;
            if (conference?.Active == null || conference.Active.Phase != MajorEventPhase.Live)
                return false;

            if (conference.Active.BookedHallInstanceIds != null &&
                conference.Active.BookedHallInstanceIds.Contains(room.InstanceId))
                return true;

            // During a live major, conference halls are busy; idle (unbooked) event halls are not.
            var id = room.Type.id;
            return id.IndexOf("conference", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
