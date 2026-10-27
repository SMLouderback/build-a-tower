using UnityEngine;

namespace BuildATower
{
    public enum PrecipStyle
    {
        None = 0,
        Rain = 1,
        Snow = 2
    }

    /// <summary>Precipitation look for a weather kind.</summary>
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
    /// Camera-space weather overlay: rain streaks / snow flakes clipped at street ground,
    /// plus a Thunderstorm lightning flash. Reads <see cref="TowerSimulation.Weather"/>;
    /// never rolls weather.
    /// </summary>
    public sealed class WeatherFx : MonoBehaviour
    {
        const int PoolSize = 420;
        const float LightningDuration = 0.55f;
        /// <summary>Street / lobby ground — precip never falls through dirt or basement.</summary>
        public const float GroundWorldY = 0f;

        [SerializeField] Camera targetCamera;
        [SerializeField] TowerSimulation simulation;
        [SerializeField] int sortingOrder = 900;
        [SerializeField] float groundWorldY = GroundWorldY;

        /// <summary>Current lightning flash strength (0..1); read by <see cref="DayNightSkyController"/>.</summary>
        public static float LightningPulse01 { get; private set; }

        Transform _rig;
        SpriteRenderer[] _drops;
        Vector2[] _norm;        // per-drop position in 0..1 view space
        float[] _speed;
        float[] _phase;
        float[] _size;
        SpriteRenderer _flash;
        PrecipStyle _style = PrecipStyle.None;
        float _intensity;
        float _wind;
        bool _lightningActive;
        float _nextLightningIn = 2f;
        float _sinceFlash = float.MaxValue;
        Sprite _rainSprite;
        Sprite _snowSprite;
        Sprite _pixel;
        float _swayTime;

        // ---- Pure helpers (EditMode tested) ----

        public static PrecipProfile ProfileFor(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Rain:
                    return new PrecipProfile(PrecipStyle.Rain, 0.55f, -0.12f, false);
                case WeatherKind.Thunderstorm:
                    return new PrecipProfile(PrecipStyle.Rain, 1f, -0.28f, true);
                case WeatherKind.Snow:
                    return new PrecipProfile(PrecipStyle.Snow, 0.55f, 0.04f, false);
                case WeatherKind.Blizzard:
                    return new PrecipProfile(PrecipStyle.Snow, 1f, -0.85f, false);
                default:
                    return new PrecipProfile(PrecipStyle.None, 0f, 0f, false);
            }
        }

        /// <summary>
        /// Lowest view-normalized Y (0 bottom … 1 top) that still sits on/above ground.
        /// </summary>
        public static float MinViewNormY(float cameraY, float orthoHalfHeight, float groundWorldY)
        {
            var viewH = orthoHalfHeight * 2f;
            if (viewH <= 0.0001f)
                return 1f;
            return Mathf.Clamp01((groundWorldY - cameraY + orthoHalfHeight) / viewH);
        }

        public static float WorldYFromNorm(float cameraY, float orthoHalfHeight, float normY) =>
            cameraY - orthoHalfHeight + Mathf.Clamp01(normY) * (orthoHalfHeight * 2f);

        public static bool ShouldHidePrecip(float minNormY) =>
            minNormY >= 0.99f;

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
            _swayTime += dt;

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

            _rainSprite = BuildRainStreakSprite();
            _snowSprite = BuildSnowFlakeSprite();
            _pixel = BuildSolidPixel();

            var go = new GameObject("WeatherFxRig");
            go.transform.SetParent(transform, false);
            _rig = go.transform;

            _drops = new SpriteRenderer[PoolSize];
            _norm = new Vector2[PoolSize];
            _speed = new float[PoolSize];
            _phase = new float[PoolSize];
            _size = new float[PoolSize];
            var rng = new System.Random(97);
            for (var i = 0; i < PoolSize; i++)
            {
                var d = new GameObject("Drop" + i);
                d.transform.SetParent(_rig, false);
                var sr = d.AddComponent<SpriteRenderer>();
                sr.sprite = _rainSprite;
                sr.sortingOrder = sortingOrder;
                sr.enabled = false;
                _drops[i] = sr;
                _norm[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                _speed[i] = 0.65f + (float)rng.NextDouble() * 0.7f;
                _phase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
                _size[i] = 0.7f + (float)rng.NextDouble() * 0.6f;
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
            var sprite = rain ? _rainSprite : _snowSprite;
            var color = rain
                ? new Color(0.78f, 0.86f, 0.98f, 0.62f)
                : new Color(0.96f, 0.98f, 1f, 0.88f);
            for (var i = 0; i < _drops.Length; i++)
            {
                _drops[i].sprite = sprite;
                _drops[i].color = color;
            }
        }

        void UpdateParticles(float dt)
        {
            if (_drops == null || targetCamera == null) return;
            if (!targetCamera.orthographic) return;

            var camPos = targetCamera.transform.position;
            var halfH = targetCamera.orthographicSize;
            var halfW = halfH * targetCamera.aspect;
            var viewH = halfH * 2f;
            var viewW = halfW * 2f;
            var minY = MinViewNormY(camPos.y, halfH, groundWorldY);
            var hideAll = _style == PrecipStyle.None || ShouldHidePrecip(minY);
            var active = hideAll ? 0 : Mathf.CeilToInt(_intensity * PoolSize);
            var rain = _style == PrecipStyle.Rain;
            // Fall rates in view-heights / sec (above-ground band only feels natural).
            var fall = rain ? 1.55f : 0.22f;
            var rot = Mathf.Atan(_wind) * Mathf.Rad2Deg;
            var skySpan = Mathf.Max(0.05f, 1f - minY);

            for (var i = 0; i < _drops.Length; i++)
            {
                var sr = _drops[i];
                var on = i < active;
                if (!on)
                {
                    if (sr.enabled) sr.enabled = false;
                    continue;
                }

                var n = _norm[i];
                // Keep particles seeded into the above-ground band.
                if (n.y < minY)
                    n.y = minY + Mathf.Repeat(_phase[i] + i * 0.017f, 1f) * skySpan;

                n.y -= fall * _speed[i] * dt * skySpan;
                var windPush = _wind * fall * _speed[i] * dt * (viewH / Mathf.Max(0.01f, viewW));
                if (!rain)
                    windPush += Mathf.Sin(_swayTime * (1.1f + _phase[i] * 0.15f) + _phase[i]) * 0.08f * dt;
                n.x += windPush;

                if (n.y < minY)
                {
                    // Hit the street — recycle into the sky instead of tunneling underground.
                    n.y = 1f - (float)(i % 7) * 0.01f;
                    n.x = (_norm[i].x + 0.37f * (i % 5 + 1)) % 1f;
                }

                if (n.x < 0f) n.x += 1f;
                if (n.x > 1f) n.x -= 1f;
                _norm[i] = n;

                var worldY = WorldYFromNorm(camPos.y, halfH, n.y);
                if (worldY < groundWorldY - 0.02f)
                {
                    sr.enabled = false;
                    continue;
                }

                // Soft fade as drops near the ground plane.
                var nearGround = Mathf.InverseLerp(groundWorldY + 1.2f, groundWorldY, worldY);
                var baseCol = rain
                    ? new Color(0.78f, 0.86f, 0.98f, 0.62f)
                    : new Color(0.96f, 0.98f, 1f, 0.88f);
                var a = baseCol.a * (1f - nearGround * 0.85f);
                sr.color = new Color(baseCol.r, baseCol.g, baseCol.b, a);
                sr.enabled = a > 0.02f;

                var swayX = rain
                    ? 0f
                    : Mathf.Sin(_swayTime * 1.4f + _phase[i]) * halfW * 0.012f * _size[i];
                var t = sr.transform;
                t.position = new Vector3(
                    camPos.x - halfW + n.x * viewW + swayX,
                    worldY,
                    0f);

                if (rain)
                {
                    var streakH = Mathf.Lerp(0.35f, 0.85f, _speed[i]) * _size[i];
                    // Keep streak length from poking through ground.
                    var maxH = Mathf.Max(0.08f, worldY - groundWorldY);
                    streakH = Mathf.Min(streakH, maxH);
                    t.localScale = new Vector3(0.035f * _size[i], streakH, 1f);
                    t.rotation = Quaternion.Euler(0f, 0f, rot);
                }
                else
                {
                    var s = (0.11f + 0.10f * _size[i]) * Mathf.Lerp(0.85f, 1.15f, _speed[i]);
                    t.localScale = new Vector3(s, s * 0.92f, 1f);
                    t.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_swayTime + _phase[i]) * 12f);
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

        static Sprite BuildSolidPixel()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                name = "weather_px"
            };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        }

        static Sprite BuildRainStreakSprite()
        {
            const int w = 8;
            const int h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "weather_rain_streak"
            };
            var px = new Color[w * h];
            for (var y = 0; y < h; y++)
            {
                var v = y / (float)(h - 1);
                // Bright head, soft tapering tail — reads as a motion-blurred drop.
                var along = Mathf.Pow(1f - v, 1.35f);
                for (var x = 0; x < w; x++)
                {
                    var u = (x + 0.5f) / w;
                    var radial = 1f - Mathf.Abs(u - 0.5f) * 2f;
                    radial = Mathf.Pow(Mathf.Max(0f, radial), 1.6f);
                    var a = along * radial;
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.15f), h);
        }

        static Sprite BuildSnowFlakeSprite()
        {
            const int s = 24;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "weather_snow_flake"
            };
            var px = new Color[s * s];
            var c = (s - 1) * 0.5f;
            for (var y = 0; y < s; y++)
            for (var x = 0; x < s; x++)
            {
                var dx = (x - c) / c;
                var dy = (y - c) / c;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                var soft = Mathf.Clamp01(1f - d);
                soft = soft * soft * (0.55f + 0.45f * soft);
                // Slight cross sparkle without hard edges.
                var arm = Mathf.Max(
                    Mathf.Exp(-Mathf.Abs(dx) * 7f) * Mathf.Exp(-Mathf.Abs(dy) * 2.2f),
                    Mathf.Exp(-Mathf.Abs(dy) * 7f) * Mathf.Exp(-Mathf.Abs(dx) * 2.2f));
                var a = Mathf.Clamp01(soft * 0.85f + arm * 0.35f);
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
