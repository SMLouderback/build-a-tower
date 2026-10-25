using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Screen-space star-promotion fireworks. Shells launch from behind the
    /// announcement banner and burst just outside it.
    /// </summary>
    public static class StarFireworks
    {
        const int MaxEmbers = 520;
        const float RocketGravity = 40f;
        const float EmberGravity = 520f;
        const float FadeSeconds = 0.9f;

        enum Kind
        {
            Rocket,
            Ember
        }

        struct Ember
        {
            public Kind Kind;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Width;
            public float Gravity;
            public float Delay;
            public Color Color;
        }

        static readonly List<Ember> Embers = new();
        static readonly Color[] Palette =
        {
            new Color(1f, 0.96f, 0.82f),
            new Color(1f, 0.82f, 0.32f),
            new Color(1f, 0.58f, 0.18f),
            new Color(1f, 0.72f, 0.42f)
        };

        static bool _playing;
        static bool _fading;
        static float _fade = 1f;
        static float _age;
        static float _nextVolley;
        static float _lastStep = -1f;
        static Rect _banner;
        static Texture2D _spark;
        static StarFireworksHost _host;

        public static bool IsPlaying => _playing;

        static float Presence => _playing ? 1f : (_fading ? Mathf.Clamp01(_fade) : 0f);

        public static void Play()
        {
            HardStop();
            _playing = true;
            _fade = 1f;
            _age = 0f;
            _nextVolley = 0f;
            _lastStep = -1f;
            EnsureHost();
            _host.BeginLoop();
        }

        /// <summary>
        /// Stops new shells and fades the bursts and their sound together.
        /// </summary>
        public static void Stop()
        {
            if (!_playing && !_fading)
                return;
            if (_fading && !_playing)
                return;
            _playing = false;
            _fading = true;
            _fade = 1f;
            EnsureHost();
        }

        public static void Tick()
        {
            if (_host == null)
                return;

            if (_fading)
            {
                _fade -= Time.unscaledDeltaTime / FadeSeconds;
                if (_fade <= 0f)
                {
                    HardStop();
                    return;
                }
            }

            if (_playing || _fading)
                _host.SetLevel(Presence);
        }

        static void HardStop()
        {
            _playing = false;
            _fading = false;
            _fade = 0f;
            Embers.Clear();
            if (_host != null)
                _host.EndLoop();
        }

        /// <summary>
        /// Draw after the dim scrim and before the banner so the panel hides the launch.
        /// </summary>
        public static void DrawBehindBanner(Rect banner)
        {
            if (Presence <= 0f || banner.width < 8f || banner.height < 8f)
                return;
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;

            _banner = banner;
            EnsureSpark();
            Step(banner);
            Render();
        }

        /// <summary>
        /// Keeps the bursts on screen after the banner closes, while they fade out.
        /// </summary>
        public static void DrawOrphanFade()
        {
            if (!_fading || _playing || Presence <= 0f)
                return;
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;

            EnsureSpark();
            Step(_banner.width > 1f ? _banner : new Rect(0f, 0f, 8f, 8f));
            Render();
        }

        static void Step(Rect banner)
        {
            var now = Time.unscaledTime;
            var dt = _lastStep < 0f ? 0.016f : Mathf.Clamp(now - _lastStep, 0f, 0.05f);
            _lastStep = now;
            if (dt <= 0f)
                return;

            _age += dt;
            if (_playing && _age >= _nextVolley && Embers.Count < MaxEmbers)
            {
                LaunchVolley(banner);
                _nextVolley = _age + Random.Range(1.45f, 2.05f);
            }

            for (var i = Embers.Count - 1; i >= 0; i--)
            {
                var ember = Embers[i];
                if (ember.Delay > 0f)
                {
                    ember.Delay -= dt;
                    Embers[i] = ember;
                    continue;
                }

                ember.Life -= dt;
                if (ember.Life <= 0f)
                {
                    if (ember.Kind == Kind.Rocket)
                        Burst(ember.Pos, ember.Color);
                    Embers.RemoveAt(i);
                    continue;
                }

                ember.Vel.y += ember.Gravity * dt;
                ember.Vel *= ember.Kind == Kind.Rocket ? 0.992f : 0.985f;
                ember.Pos += ember.Vel * dt;
                Embers[i] = ember;

                if (ember.Kind == Kind.Rocket &&
                    ember.MaxLife - ember.Life > 0.05f &&
                    Random.value < 0.55f &&
                    Embers.Count < MaxEmbers)
                {
                    Embers.Add(new Ember
                    {
                        Kind = Kind.Ember,
                        Pos = ember.Pos,
                        Vel = ember.Vel * 0.08f + Random.insideUnitCircle * 12f,
                        Life = Random.Range(0.18f, 0.38f),
                        MaxLife = 0.38f,
                        Width = 1.6f,
                        Gravity = EmberGravity * 0.35f,
                        Color = Color.Lerp(ember.Color, new Color(1f, 0.9f, 0.55f), 0.4f)
                    });
                }
            }
        }

        static void LaunchVolley(Rect banner)
        {
            var origin = banner.center;
            float[] angles = { -68f, -36f, -12f, 14f, 38f, 72f };
            for (var i = 0; i < angles.Length; i++)
            {
                var rad = angles[i] * Mathf.Deg2Rad;
                var speed = Random.Range(340f, 460f);
                var vel = new Vector2(Mathf.Sin(rad) * speed, -Mathf.Cos(rad) * speed);
                var exit = TimeToExit(banner, origin, vel);
                var color = Palette[i % Palette.Length];
                Embers.Add(new Ember
                {
                    Kind = Kind.Rocket,
                    Pos = origin,
                    Vel = vel,
                    Life = exit + Random.Range(0.06f, 0.16f),
                    MaxLife = exit + 0.16f,
                    Width = 2.2f,
                    Gravity = RocketGravity,
                    Delay = i * 0.08f,
                    Color = color
                });
            }
        }

        static void Burst(Vector2 pos, Color color)
        {
            if (Embers.Count >= MaxEmbers)
                return;

            var count = Random.Range(48, 68);
            for (var i = 0; i < count; i++)
            {
                if (Embers.Count >= MaxEmbers)
                    return;

                var ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.08f, 0.08f);
                var ring = (i % 3 == 0) ? Random.Range(40f, 90f) : Random.Range(120f, 260f);
                var vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ring;
                vel.y += Random.Range(-20f, 30f);
                var life = Random.Range(0.75f, 1.45f);
                Embers.Add(new Ember
                {
                    Kind = Kind.Ember,
                    Pos = pos + Random.insideUnitCircle * 3f,
                    Vel = vel,
                    Life = life,
                    MaxLife = life,
                    Width = Random.Range(1.4f, 2.6f),
                    Gravity = EmberGravity,
                    Color = Color.Lerp(Color.white, color, Random.Range(0.25f, 0.85f))
                });
            }
        }

        static void Render()
        {
            var prevColor = GUI.color;
            var prevMatrix = GUI.matrix;
            for (var i = 0; i < Embers.Count; i++)
            {
                var ember = Embers[i];
                var t = Mathf.Clamp01(ember.Life / Mathf.Max(0.001f, ember.MaxLife));
                var alpha = ember.Kind == Kind.Rocket
                    ? Mathf.Lerp(0.2f, 0.95f, t)
                    : t * t;
                var flicker = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * 48f + i);
                var color = ember.Color;
                color.a = alpha * flicker * Presence;
                var tail = ember.Pos - ember.Vel * (ember.Kind == Kind.Rocket ? 0.045f : 0.07f);
                DrawStreak(tail, ember.Pos, ember.Width, color);

                var head = color;
                head.a = Mathf.Min(1f, color.a + 0.25f);
                GUI.color = head;
                var s = ember.Width * (ember.Kind == Kind.Rocket ? 2.4f : 1.8f);
                GUI.DrawTexture(new Rect(ember.Pos.x - s * 0.5f, ember.Pos.y - s * 0.5f, s, s), _spark);
            }

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        static void DrawStreak(Vector2 tail, Vector2 head, float width, Color color)
        {
            var delta = head - tail;
            var len = delta.magnitude;
            if (len < 1f)
                return;

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var mid = (head + tail) * 0.5f;
            GUI.matrix = Matrix4x4.identity;
            GUIUtility.RotateAroundPivot(angle, mid);
            var glow = color;
            glow.a *= 0.35f;
            GUI.color = glow;
            GUI.DrawTexture(new Rect(mid.x - len * 0.5f, mid.y - width, len, width * 2f), _spark);
            GUI.color = color;
            GUI.DrawTexture(new Rect(mid.x - len * 0.5f, mid.y - width * 0.35f, len, width * 0.7f), _spark);
            GUI.matrix = Matrix4x4.identity;
        }

        static float TimeToExit(Rect rect, Vector2 origin, Vector2 velocity)
        {
            var t = float.PositiveInfinity;
            if (velocity.x > 1f)
                t = Mathf.Min(t, (rect.xMax - origin.x) / velocity.x);
            else if (velocity.x < -1f)
                t = Mathf.Min(t, (rect.xMin - origin.x) / velocity.x);
            if (velocity.y > 1f)
                t = Mathf.Min(t, (rect.yMax - origin.y) / velocity.y);
            else if (velocity.y < -1f)
                t = Mathf.Min(t, (rect.yMin - origin.y) / velocity.y);
            if (float.IsPositiveInfinity(t) || t < 0f)
                return 0.45f;
            return t;
        }

        static void EnsureSpark()
        {
            if (_spark != null)
                return;

            const int size = 32;
            _spark = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x - center) / center;
                var dy = (y - center) / center;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                var a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                _spark.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            _spark.Apply(false, true);
        }

        static void EnsureHost()
        {
            if (_host != null)
                return;

            var go = new GameObject("StarFireworksAudio")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _host = go.AddComponent<StarFireworksHost>();
        }

        sealed class StarFireworksHost : MonoBehaviour
        {
            AudioSource _source;
            AudioClip _clip;
            bool _missingLogged;

            void Update()
            {
                Tick();
            }

            void OnGUI()
            {
                DrawOrphanFade();
            }

            public void BeginLoop()
            {
                EnsureSource();
                if (_clip == null)
                {
                    _clip = Resources.Load<AudioClip>(TowerAudio.SfxFireworksPath);
                    if (_clip == null && !_missingLogged)
                    {
                        _missingLogged = true;
                        Debug.LogWarning("Missing fireworks clip at " + TowerAudio.SfxFireworksPath);
                    }
                }

                if (_clip == null || _source == null)
                    return;

                _source.clip = _clip;
                _source.loop = true;
                _source.spatialBlend = 0f;
                _source.pitch = 1f;
                SetLevel(1f);
                if (!_source.isPlaying)
                    _source.Play();
            }

            public void SetLevel(float level01)
            {
                if (_source == null || !_source.isPlaying)
                    return;

                var audio = TowerAudio.Ensure();
                var bus = audio != null && audio.Buses != null
                    ? audio.Buses.Effective(AudioBus.Sfx)
                    : 0f;
                _source.volume = Mathf.Clamp01(level01) * bus;
            }

            public void EndLoop()
            {
                if (_source == null)
                    return;
                _source.Stop();
                _source.volume = 0f;
            }

            void EnsureSource()
            {
                if (_source != null)
                    return;
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.loop = true;
                _source.spatialBlend = 0f;
            }
        }
    }
}
