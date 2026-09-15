using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// ~10 Hz ambient scorer: camera visibility × activity × bias → AmbientMixer → TowerAudio.
    /// </summary>
    public sealed class TowerAudioDriver : MonoBehaviour
    {
        public const float TickIntervalSeconds = 0.1f;
        public const float ViewportExpand = 0.08f;
        public const float ElevatorMovingActivityFloor = 0.8f;
        public const int ActivityAgentCap = 3;

        BuildController _build;
        TowerSimulation _simulation;
        float _accum;
        RoomInstance _lastSelected;

        readonly List<(SoundProfile profile, float weight)> _roomWeights = new();
        readonly Dictionary<int, int> _agentsPerRoom = new();

        /// <summary>
        /// Ensure a driver exists on the tower host (BuildController / simulation GameObject).
        /// </summary>
        public static TowerAudioDriver Ensure(MonoBehaviour host = null)
        {
            var existing = FindAnyObjectByType<TowerAudioDriver>();
            if (existing != null)
                return existing;

            TowerAudio.Ensure();

            GameObject go = null;
            if (host != null)
                go = host.gameObject;
            else
            {
                var build = FindAnyObjectByType<BuildController>();
                if (build != null)
                    go = build.gameObject;
                else if (TowerAudio.Instance != null)
                    go = TowerAudio.Instance.gameObject;
            }

            if (go == null)
            {
                go = new GameObject(nameof(TowerAudioDriver));
                DontDestroyOnLoad(go);
            }

            var driver = go.GetComponent<TowerAudioDriver>();
            if (driver == null)
                driver = go.AddComponent<TowerAudioDriver>();
            return driver;
        }

        /// <summary>
        /// Empty rooms are nearly silent so outdoor birds/breeze can lead;
        /// 1–3+ agents lerp toward 1.0 (cap at 3).
        /// </summary>
        public static float ActivityFromAgentCount(int agentCount)
        {
            if (agentCount <= 0)
                return 0.08f;

            float t = Mathf.Clamp01(agentCount / (float)ActivityAgentCap);
            return Mathf.Lerp(0.45f, 1f, t);
        }

        /// <summary>
        /// Visibility from cell centers via WorldToViewportPoint.
        /// 0 if none in slightly expanded viewport; otherwise max cell score with slight center bias.
        /// </summary>
        public static float ComputeRoomVisibility(Camera cam, RoomInstance room, float expand = ViewportExpand)
        {
            if (cam == null || room == null)
                return 0f;

            float best = 0f;
            var any = false;

            for (var dy = 0; dy < room.Size.y; dy++)
            for (var dx = 0; dx < room.Size.x; dx++)
            {
                var world = new Vector3(
                    room.Origin.x + dx + 0.5f,
                    room.Origin.y + dy + 0.5f,
                    0f);
                var vp = cam.WorldToViewportPoint(world);
                if (vp.z < 0f)
                    continue;

                if (vp.x < -expand || vp.x > 1f + expand ||
                    vp.y < -expand || vp.y > 1f + expand)
                    continue;

                any = true;

                float cx = Mathf.Clamp01(vp.x);
                float cy = Mathf.Clamp01(vp.y);
                float distFromCenter = Vector2.Distance(new Vector2(cx, cy), new Vector2(0.5f, 0.5f));
                float vis = 1f - Mathf.Clamp01(distFromCenter / 0.75f);

                if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
                    vis *= 0.4f;

                if (vis > best)
                    best = vis;
            }

            return any ? Mathf.Clamp01(best) : 0f;
        }

        void Awake()
        {
            ResolveRefs();
            TowerAudio.Ensure();
        }

        void Update()
        {
            _accum += Time.unscaledDeltaTime;
            if (_accum < TickIntervalSeconds)
                return;
            _accum = 0f;
            Tick();
        }

        void Tick()
        {
            ResolveRefs();
            var audio = TowerAudio.Ensure();
            if (_build?.Grid == null)
            {
                audio.TickAmbient(new List<(SoundProfile, float)>
                {
                    (SoundProfile.Outdoor, AmbientMixer.OutdoorFullWeight)
                });
                return;
            }

            SyncSelection(audio);

            var cam = Camera.main;
            var rooms = _build.Grid.Rooms;
            var agents = _simulation?.Agents?.Agents;
            var elevators = _simulation?.Elevators;

            _agentsPerRoom.Clear();
            if (agents != null)
            {
                for (var i = 0; i < agents.Count; i++)
                {
                    var agent = agents[i];
                    if (agent == null)
                        continue;
                    if (!_build.Grid.TryGetRoomAt(agent.Cell, out var room) || room == null)
                        continue;
                    _agentsPerRoom.TryGetValue(room.InstanceId, out var n);
                    _agentsPerRoom[room.InstanceId] = n + 1;
                }
            }

            _roomWeights.Clear();
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room?.Type == null)
                    continue;

                var profile = SoundProfileMap.ForRoom(room.Type);
                if (profile == SoundProfile.None ||
                    profile == SoundProfile.Build ||
                    profile == SoundProfile.Outdoor)
                    continue;

                float visibility = ComputeRoomVisibility(cam, room);
                if (visibility <= 0f)
                    continue;

                _agentsPerRoom.TryGetValue(room.InstanceId, out var agentCount);
                float activity = ActivityFromAgentCount(agentCount);

                if (profile == SoundProfile.Elevator &&
                    elevators != null &&
                    IsElevatorCarMoving(elevators, room.InstanceId))
                {
                    activity = Mathf.Max(activity, ElevatorMovingActivityFloor);
                }

                float score = AmbientMixer.ScoreRoom(visibility, activity, profile);
                if (score > 0f)
                    _roomWeights.Add((profile, score));
            }

            // Empty / early tower: birds + breeze. Fades as interior beds take over.
            var towerEnergy = AmbientMixer.SumWeights(_roomWeights);
            var outdoor = AmbientMixer.OutdoorWeight(towerEnergy);
            if (outdoor > 0.01f)
                _roomWeights.Add((SoundProfile.Outdoor, outdoor));

            SoundProfile? selectedBoost = null;
            if (audio.SelectedProfile != SoundProfile.None &&
                audio.SelectedProfile != SoundProfile.Outdoor)
                selectedBoost = audio.SelectedProfile;

            var ranked = AmbientMixer.RankProfiles(_roomWeights, selectedBoost);
            audio.TickAmbient(ranked);
        }

        void SyncSelection(TowerAudio audio)
        {
            var selected = _build != null ? _build.SelectedRoom : null;
            if (ReferenceEquals(selected, _lastSelected))
                return;
            _lastSelected = selected;
            audio.SetSelectedRoom(selected);
        }

        static bool IsElevatorCarMoving(ElevatorSystem elevators, int roomInstanceId)
        {
            var shaft = elevators.FindByRoomId(roomInstanceId);
            return shaft?.Car != null && shaft.Car.State == ElevatorCarState.Moving;
        }

        void ResolveRefs()
        {
            if (_build == null)
                _build = GetComponent<BuildController>() ?? FindAnyObjectByType<BuildController>();
            if (_simulation == null)
                _simulation = GetComponent<TowerSimulation>() ?? FindAnyObjectByType<TowerSimulation>();
        }
    }
}
