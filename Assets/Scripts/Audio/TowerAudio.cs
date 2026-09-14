using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Runtime audio façade: ambience voice pool, SFX one-shots, and bus levels.
    /// Driver wiring (build place / selection / ambient tick) is Task 5+.
    /// </summary>
    public sealed class TowerAudio : MonoBehaviour
    {
        public const float BuildSfxCooldownSeconds = 0.12f;
        /// <summary>Light per-shaft rate limit so rapid reopen edges do not stack ping/door.</summary>
        public const float ElevatorArrivalCooldownSeconds = 0.2f;
        public const int AmbienceVoiceCount = 4;

        public const string SfxBuildPlacePath = "Audio/Sfx/build_place";
        public const string SfxElevatorPingPath = "Audio/Sfx/elevator_ping";
        public const string SfxElevatorDoorPath = "Audio/Sfx/elevator_door";

        static readonly SoundProfile[] AmbienceProfiles =
        {
            SoundProfile.Office,
            SoundProfile.Restaurant,
            SoundProfile.Retail,
            SoundProfile.Condo,
            SoundProfile.Hotel,
            SoundProfile.Conference,
            SoundProfile.Event,
            SoundProfile.Elevator,
            SoundProfile.Parking,
            SoundProfile.Utility,
            SoundProfile.Lobby,
            SoundProfile.Stairs
        };

        public static TowerAudio Instance { get; private set; }

        public AudioBuses Buses { get; private set; }

        readonly Dictionary<SoundProfile, AudioClip> _ambienceClips = new();
        readonly HashSet<string> _loggedMissing = new();

        AudioSource[] _ambienceSources;
        AudioSource _sfxPrimary;
        AudioSource _sfxSecondary;

        AudioClip _buildPlace;
        AudioClip _elevatorPing;
        AudioClip _elevatorDoor;

        float _lastBuildSfxTime = float.NegativeInfinity;
        readonly Dictionary<int, float> _lastElevatorArrivalByShaft = new();
        float _lastAmbienceGain;
        SoundProfile _selectedProfile = SoundProfile.None;
        RoomInstance _selectedRoom;

        public SoundProfile SelectedProfile => _selectedProfile;
        public RoomInstance SelectedRoom => _selectedRoom;

        public static bool CanPlayBuildSfx(float now, float last, float cooldown = BuildSfxCooldownSeconds)
        {
            return now - last >= cooldown;
        }

        /// <summary>True only on Moving → DoorsOpen (not Idle same-floor open).</summary>
        public static bool ShouldPlayElevatorArrivalSfx(ElevatorCarState previousState)
        {
            return previousState == ElevatorCarState.Moving;
        }

        public static bool CanPlayElevatorArrivalSfx(
            float now,
            float last,
            float cooldown = ElevatorArrivalCooldownSeconds)
        {
            return now - last >= cooldown;
        }

        public static string AmbienceResourcePath(SoundProfile profile)
        {
            return "Audio/Ambience/" + profile.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Rescale a looping ambience voice when bus levels change between TickAmbient calls.
        /// Zero new gain silences immediately; zero previous gain leaves volume until next tick.
        /// </summary>
        public static float RescaleAmbienceVoice(float voiceVolume, float previousGain, float newGain)
        {
            if (newGain <= 0f) return 0f;
            if (previousGain <= 1e-6f) return voiceVolume;
            return voiceVolume * (newGain / previousGain);
        }

        /// <summary>
        /// Find an existing TowerAudio or create a DontDestroyOnLoad host.
        /// Safe to call before scene wiring (Task 5).
        /// </summary>
        public static TowerAudio Ensure()
        {
            if (Instance != null) return Instance;

            var existing = FindAnyObjectByType<TowerAudio>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject(nameof(TowerAudio));
            DontDestroyOnLoad(go);
            return go.AddComponent<TowerAudio>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Buses = new AudioBuses();
            Buses.Load();
            EnsureSources();
            LoadClips();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetSelectedRoom(RoomInstance room)
        {
            _selectedRoom = room;
            _selectedProfile = room?.Type != null
                ? SoundProfileMap.ForRoom(room.Type)
                : SoundProfile.None;
        }

        public void PlayBuildSfx()
        {
            var now = Time.unscaledTime;
            if (!CanPlayBuildSfx(now, _lastBuildSfxTime))
                return;

            if (!PlayOneShot(_buildPlace, SfxBuildPlacePath, _sfxPrimary))
                return;

            _lastBuildSfxTime = now;
        }

        public void PlayElevatorPing()
        {
            PlayOneShot(_elevatorPing, SfxElevatorPingPath, _sfxSecondary != null ? _sfxSecondary : _sfxPrimary);
        }

        public void PlayElevatorDoor()
        {
            PlayOneShot(_elevatorDoor, SfxElevatorDoorPath, _sfxSecondary != null ? _sfxSecondary : _sfxPrimary);
        }

        /// <summary>
        /// Ping + door once per stop, rate-limited per shaft id.
        /// </summary>
        public void PlayElevatorArrival(int shaftId)
        {
            var now = Time.unscaledTime;
            if (!_lastElevatorArrivalByShaft.TryGetValue(shaftId, out var last))
                last = float.NegativeInfinity;
            if (!CanPlayElevatorArrivalSfx(now, last))
                return;

            PlayElevatorPing();
            PlayElevatorDoor();
            _lastElevatorArrivalByShaft[shaftId] = now;
        }

        /// <summary>
        /// Immediately refresh ambience voice volumes after Options bus edits (mute/sliders).
        /// SFX one-shots already read <see cref="AudioBuses.Effective"/> at play time.
        /// </summary>
        public void ApplyVolumes()
        {
            EnsureSources();
            if (_ambienceSources == null) return;

            float newGain = Buses != null ? Buses.Effective(AudioBus.Ambience) : 0f;
            for (int i = 0; i < _ambienceSources.Length; i++)
            {
                var source = _ambienceSources[i];
                if (source == null) continue;
                source.volume = RescaleAmbienceVoice(source.volume, _lastAmbienceGain, newGain);
            }

            _lastAmbienceGain = newGain;
        }

        /// <summary>
        /// Assign ranked profile beds to the 4-voice pool.
        /// Volume = Buses.Effective(Ambience) × weight / sum(weights).
        /// </summary>
        public void TickAmbient(IList<(SoundProfile profile, float weight)> ranked)
        {
            EnsureSources();
            var ambGain = Buses != null ? Buses.Effective(AudioBus.Ambience) : 0f;

            float sum = 0f;
            if (ranked != null)
            {
                for (int i = 0; i < ranked.Count; i++)
                {
                    if (ranked[i].weight > 0f)
                        sum += ranked[i].weight;
                }
            }

            for (int v = 0; v < _ambienceSources.Length; v++)
            {
                var source = _ambienceSources[v];
                if (ranked == null || v >= ranked.Count || ranked[v].weight <= 0f || sum <= 0f)
                {
                    if (source.isPlaying)
                        source.Stop();
                    source.clip = null;
                    source.volume = 0f;
                    continue;
                }

                var (profile, weight) = ranked[v];
                if (!_ambienceClips.TryGetValue(profile, out var clip) || clip == null)
                {
                    LogMissingOnce(AmbienceResourcePath(profile));
                    if (source.isPlaying)
                        source.Stop();
                    source.clip = null;
                    source.volume = 0f;
                    continue;
                }

                float normalized = weight / sum;
                if (source.clip != clip)
                {
                    source.clip = clip;
                    source.loop = true;
                    if (!source.isPlaying)
                        source.Play();
                }
                else if (!source.isPlaying)
                {
                    source.Play();
                }

                source.volume = ambGain * normalized;
            }

            _lastAmbienceGain = ambGain;
        }

        void EnsureSources()
        {
            if (_ambienceSources != null && _ambienceSources.Length == AmbienceVoiceCount &&
                _sfxPrimary != null)
                return;

            _ambienceSources = new AudioSource[AmbienceVoiceCount];
            for (int i = 0; i < AmbienceVoiceCount; i++)
            {
                var child = new GameObject($"AmbienceVoice_{i}");
                child.transform.SetParent(transform, false);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;
                _ambienceSources[i] = src;
            }

            _sfxPrimary = gameObject.AddComponent<AudioSource>();
            _sfxPrimary.playOnAwake = false;
            _sfxPrimary.loop = false;
            _sfxPrimary.spatialBlend = 0f;

            var sfxGo = new GameObject("SfxSecondary");
            sfxGo.transform.SetParent(transform, false);
            _sfxSecondary = sfxGo.AddComponent<AudioSource>();
            _sfxSecondary.playOnAwake = false;
            _sfxSecondary.loop = false;
            _sfxSecondary.spatialBlend = 0f;
        }

        void LoadClips()
        {
            _ambienceClips.Clear();
            foreach (var profile in AmbienceProfiles)
            {
                var path = AmbienceResourcePath(profile);
                var clip = Resources.Load<AudioClip>(path);
                if (clip == null)
                {
                    LogMissingOnce(path);
                    continue;
                }

                _ambienceClips[profile] = clip;
            }

            _buildPlace = Resources.Load<AudioClip>(SfxBuildPlacePath);
            if (_buildPlace == null) LogMissingOnce(SfxBuildPlacePath);

            _elevatorPing = Resources.Load<AudioClip>(SfxElevatorPingPath);
            if (_elevatorPing == null) LogMissingOnce(SfxElevatorPingPath);

            _elevatorDoor = Resources.Load<AudioClip>(SfxElevatorDoorPath);
            if (_elevatorDoor == null) LogMissingOnce(SfxElevatorDoorPath);
        }

        bool PlayOneShot(AudioClip clip, string path, AudioSource source)
        {
            if (clip == null)
            {
                LogMissingOnce(path);
                return false;
            }

            if (source == null)
                return false;

            float gain = Buses != null ? Buses.Effective(AudioBus.Sfx) : 0f;
            source.PlayOneShot(clip, gain);
            return true;
        }

        void LogMissingOnce(string path)
        {
            if (!_loggedMissing.Add(path))
                return;
            Debug.LogWarning($"[TowerAudio] Missing clip Resources/{path} — voice skipped.");
        }
    }
}
