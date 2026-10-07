using UnityEngine;

namespace BuildATower
{
    public enum PrecipStyle
    {
        None = 0,
        Rain = 1,
        Snow = 2
    }

    /// <summary>Placeholder precipitation look for a weather kind (no final art yet).</summary>
    public readonly struct PrecipProfile
    {
        public readonly PrecipStyle Style;
        /// <summary>0..1 share of the particle pool shown.</summary>
        public readonly float Density;
        /// <summary>Horizontal drift as a fraction of fall speed (negative = leftward).</summary>
        public readonly float Wind;
        public readonly bool Lightning;

        public PrecipProfile(PrecipStyle style, float density, float wind, bool lightning)
        {
            Style = style;
            Density = density;
            Wind = wind;
            Lightning = lightning;
        }
    }

    /// <summary>
    /// Camera-space weather overlay: placeholder rain streaks / snow flakes and a Thunderstorm
    /// lightning flash. Reads <see cref="TowerSimulation.Weather"/> every frame; never rolls weather.
    /// </summary>
    public sealed class WeatherFx : MonoBehaviour
    {
        const int PoolSize = 320;
        const float LightningDuration = 0.55f;

        [SerializeField] Camera targetCamera;
        [SerializeField] TowerSimulation simulation;
        [SerializeField] int sortingOrder = 900;

        /// <summary>Current lightning flash strength (0..1); read by <see cref="DayNightSkyController"/>.</summary>
        public static float LightningPulse01 { get; private set; }

        Transform _rig;
        SpriteRenderer[] _drops;
        Vector2[] _norm;        // per-drop position in 0..1 view space
        float[] _speed;         // per-drop speed multiplier
        SpriteRenderer _flash;
        PrecipStyle _style = PrecipStyle.None;
        float _intensity;
        float _wind;
        bool _lightningActive;
        float _nextLightningIn = 2f;
        float _sinceFlash = float.MaxValue;
        Sprite _pixel;

        // ---- Pure helpers (EditMode tested) ----

        public static PrecipProfile ProfileFor(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Rain:
                    return new PrecipProfile(PrecipStyle.Rain, 0.55f, -0.12f, false);
                case WeatherKind.Thunderstorm:
                    return new PrecipProfile(PrecipStyle.Rain, 1f, -0.25f, true);
                case WeatherKind.Snow:
                    return new PrecipProfile(PrecipStyle.Snow, 0.5f, 0.05f, false);
                case WeatherKind.Blizzard:
                    return new PrecipProfile(PrecipStyle.Snow, 1f, -0.9f, false);
                default:
                    return new PrecipProfile(PrecipStyle.None, 0f, 0f, false);
            }
        }

        /// <summary>
        /// Double-flicker flash envelope: 0 before/after, peaks near 1 at ~0.05s and ~0.2s.
        /// </summary>
        public static float LightningEnvelope(float secondsSinceFlash)
        {
            var t = secondsSinceFlash;
            if (t < 0f || t >= LightningDuration) return 0f;
            float first = Bump(t, 0.05f, 0.08f, 1f);
            float second = Bump(t, 0.22f, 0.14f, 0.7f);
            return Mathf.Clamp01(Mathf.Max(first, second));
        }

        static float Bump(float t, float center, float halfWidth, float peak) =>
            Mathf.Max(0f, 1f - Mathf.Abs(t - center) / halfWidth) * peak;

        // ---- Lifecycle ----

        void Start()
        {
            BindCamera();
            BuildRig();
        }

        void OnDestroy()
        {
            LightningPulse01 = 0f;
        }

        void LateUpdate()
        {
            BindCamera();
            if (targetCamera == null) return;
            BuildRig();

            if (simulation == null)
                simulation = FindAnyObjectByType<TowerSimulation>();

            var kind = simulation != null && simulation.Weather != null
                ? simulation.Weather.Kind
                : WeatherKind.Clear;
            var profile = ProfileFor(kind);
            var dt = Time.deltaTime;

            if (profile.Style != PrecipStyle.None && profile.Style != _style)
            {
                _style = profile.Style;
                ApplyStyle();
            }

            var target = profile.Style == PrecipStyle.None ? 0f : profile.Density;
            _intensity = Mathf.MoveTowards(_intensity, target, dt * 0.6f);
            _wind = Mathf.MoveTowards(_wind, profile.Wind, dt * 0.8f);
            if (_intensity <= 0f) _style = PrecipStyle.None;

            _lightningActive = profile.Lightning;
            TickLightning(dt);
            UpdateParticles(dt);
            UpdateFlash();
        }

        void TickLightning(float dt)
        {
            if (_lightningActive)
            {
                _nextLightningIn -= dt;
                if (_nextLightningIn <= 0f)
                {
                    _sinceFlash = 0f;
                    _nextLightningIn = Random.Range(4f, 11f);
                }
            }

            if (_sinceFlash < LightningDuration)
                _sinceFlash += dt;
            LightningPulse01 = LightningEnvelope(_sinceFlash);
        }

        void BindCamera()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        // ---- Rendering ----

        void BuildRig()
        {
            if (_rig != null) return;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = "weather_px"
            };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply(false, true);
            _pixel = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);

            var go = new GameObject("WeatherFxRig");
            go.transform.SetParent(transform, false);
            _rig = go.transform;

            _drops = new SpriteRenderer[PoolSize];
            _norm = new Vector2[PoolSize];
            _speed = new float[PoolSize];
            var rng = new System.Random(97);
            for (var i = 0; i < PoolSize; i++)
            {
                var d = new GameObject("Drop" + i);
                d.transform.SetParent(_rig, false);
                var sr = d.AddComponent<SpriteRenderer>();
                sr.sprite = _pixel;
                sr.sortingOrder = sortingOrder;
                sr.enabled = false;
                _drops[i] = sr;
                _norm[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                _speed[i] = 0.7f + (float)rng.NextDouble() * 0.6f;
            }

            var flashGo = new GameObject("LightningFlash");
            flashGo.transform.SetParent(_rig, false);
            _flash = flashGo.AddComponent<SpriteRenderer>();
            _flash.sprite = _pixel;
            _flash.sortingOrder = sortingOrder + 1;
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _flash.enabled = false;
        }

        void ApplyStyle()
        {
            if (_drops == null) return;
            var rain = _style == PrecipStyle.Rain;
            var color = rain
                ? new Color(0.72f, 0.80f, 0.95f, 0.55f)
                : new Color(1f, 1f, 1f, 0.85f);
            for (var i = 0; i < _drops.Length; i++)
                _drops[i].color = color;
        }

        void UpdateParticles(float dt)
        {
            if (_drops == null || targetCamera == null) return;
            if (!targetCamera.orthographic) return;

            var active = _style == PrecipStyle.None
                ? 0
                : Mathf.CeilToInt(_intensity * PoolSize);

            var camPos = targetCamera.transform.position;
            var halfH = targetCamera.orthographicSize;
            var halfW = halfH * targetCamera.aspect;
            var viewH = halfH * 2f;
            var viewW = halfW * 2f;
            var rain = _style == PrecipStyle.Rain;
            var fall = rain ? 2.1f : 0.28f;           // view-heights per second
            var rot = Mathf.Atan(_wind) * Mathf.Rad2Deg;

            for (var i = 0; i < _drops.Length; i++)
            {
                var sr = _drops[i];
                var on = i < active;
                if (sr.enabled != on) sr.enabled = on;
                if (!on) continue;

                var n = _norm[i];
                n.y -= fall * _speed[i] * dt;
                n.x += _wind * fall * _speed[i] * dt * (viewH / Mathf.Max(0.01f, viewW));
                if (n.y < 0f) n.y += 1f;
                if (n.x < 0f) n.x += 1f;
                if (n.x > 1f) n.x -= 1f;
                _norm[i] = n;

                var t = sr.transform;
                t.position = new Vector3(
                    camPos.x - halfW + n.x * viewW,
                    camPos.y - halfH + n.y * viewH,
                    0f);
                if (rain)
                {
                    // Longer thin streaks read better at high camera orthographic sizes.
                    t.localScale = new Vector3(0.025f, 0.55f * _speed[i], 1f);
                    t.rotation = Quaternion.Euler(0f, 0f, rot);
                }
                else
                {
                    var s = 0.09f + 0.07f * _speed[i];
                    t.localScale = new Vector3(s, s * 0.85f, 1f);
                    t.rotation = Quaternion.identity;
                }
            }
        }

        void UpdateFlash()
        {
            if (_flash == null || targetCamera == null || !targetCamera.orthographic) return;

            var pulse = LightningPulse01;
            var on = pulse > 0.001f;
            if (_flash.enabled != on) _flash.enabled = on;
            if (!on) return;

            var halfH = targetCamera.orthographicSize;
            var halfW = halfH * targetCamera.aspect;
            var cam = targetCamera.transform.position;
            _flash.transform.position = new Vector3(cam.x, cam.y, 0f);
            _flash.transform.localScale = new Vector3(halfW * 2.1f, halfH * 2.1f, 1f);
            _flash.color = new Color(1f, 1f, 1f, pulse * 0.28f);
        }

        public static void EnsureInScene()
        {
            if (FindAnyObjectByType<WeatherFx>() != null) return;
            new GameObject("WeatherFx").AddComponent<WeatherFx>();
        }
    }
}
