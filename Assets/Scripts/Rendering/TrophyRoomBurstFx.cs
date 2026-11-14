using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Screen-space light burst and stars from placed trophy rooms when a subject metal upgrades.
    /// </summary>
    public static class TrophyRoomBurstFx
    {
        const float DurationSeconds = 0.95f;
        const int StarsPerRoom = 14;

        struct Star
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
        }

        struct BurstWave
        {
            public Vector2 Anchor;
            public float Age;
            public Color FlashColor;
            public List<Star> Stars;
        }

        static readonly List<BurstWave> Waves = new();
        static float _globalAge;
        static bool _active;

        public static bool IsPlaying => _active;

        /// <summary>EditMode: bursts started since last reset.</summary>
        public static int BurstsRequested { get; private set; }

        /// <summary>EditMode: anchors in the most recent <see cref="Play"/> call.</summary>
        public static int LastBurstAnchorCount { get; private set; }

        public static void ResetForTests()
        {
            Waves.Clear();
            _active = false;
            _globalAge = 0f;
            BurstsRequested = 0;
            LastBurstAnchorCount = 0;
        }

        public static void HandleTrophyUpgrade(TowerGrid grid, Camera cam, ChallengeSubject subject, TrophyMetal metal)
        {
            if (metal == TrophyMetal.None)
                return;

            var anchors = TrophyRoomDisplay.CollectCutawayScreenAnchors(grid, cam);
            if (anchors.Count == 0)
                return;

            Play(anchors, metal);
        }

        public static void Play(IReadOnlyList<Vector2> screenAnchors, TrophyMetal metal)
        {
            if (screenAnchors == null || screenAnchors.Count == 0 || metal == TrophyMetal.None)
                return;

            BurstsRequested++;
            LastBurstAnchorCount = screenAnchors.Count;
            var flash = MetalFlashColor(metal);
            var rng = new System.Random(unchecked((int)(Time.unscaledTime * 1000f) ^ (int)metal ^ screenAnchors.Count));

            for (var r = 0; r < screenAnchors.Count; r++)
            {
                var anchor = screenAnchors[r];
                var wave = new BurstWave
                {
                    Anchor = anchor,
                    Age = 0f,
                    FlashColor = flash,
                    Stars = new List<Star>(StarsPerRoom)
                };

                for (var i = 0; i < StarsPerRoom; i++)
                {
                    var angle = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                    var speed = 80f + (float)rng.NextDouble() * 140f;
                    wave.Stars.Add(new Star
                    {
                        Pos = anchor,
                        Vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                        Life = 0f,
                        MaxLife = 0.35f + (float)rng.NextDouble() * 0.45f,
                        Size = Mathf.Lerp(10f, 18f, (float)rng.NextDouble()),
                        Color = flash
                    });
                }

                Waves.Add(wave);
            }

            _active = true;
            _globalAge = 0f;
        }

        public static void Advance(float unscaledDeltaTime)
        {
            if (!_active)
                return;

            _globalAge += unscaledDeltaTime;
            for (var w = Waves.Count - 1; w >= 0; w--)
            {
                var wave = Waves[w];
                wave.Age += unscaledDeltaTime;
                var stars = wave.Stars;
                for (var i = stars.Count - 1; i >= 0; i--)
                {
                    var star = stars[i];
                    star.Life += unscaledDeltaTime;
                    star.Pos += star.Vel * unscaledDeltaTime;
                    star.Vel *= 1f - unscaledDeltaTime * 2.2f;
                    stars[i] = star;
                    if (star.Life >= star.MaxLife)
                        stars.RemoveAt(i);
                }

                Waves[w] = wave;
                if (wave.Age >= DurationSeconds && stars.Count == 0)
                    Waves.RemoveAt(w);
            }

            if (Waves.Count == 0)
                _active = false;
        }

        public static void Draw(GUIStyle glyphStyle)
        {
            if (!_active || glyphStyle == null)
                return;
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;

            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;

            for (var w = 0; w < Waves.Count; w++)
            {
                var wave = Waves[w];
                var flashU = Mathf.Clamp01(wave.Age / 0.22f);
                var flashOut = wave.Age > 0.18f ? Mathf.Clamp01((0.45f - wave.Age) / 0.27f) : 1f;
                var flashAlpha = (1f - flashU) * flashOut * 0.55f;
                if (flashAlpha > 0.01f)
                {
                    GUI.color = new Color(wave.FlashColor.r, wave.FlashColor.g, wave.FlashColor.b, flashAlpha);
                    var glow = 36f + flashU * 28f;
                    GUI.Label(
                        new Rect(wave.Anchor.x - glow * 0.5f, wave.Anchor.y - glow * 0.5f, glow, glow),
                        "✦",
                        glyphStyle);
                }

                var stars = wave.Stars;
                for (var i = 0; i < stars.Count; i++)
                {
                    var star = stars[i];
                    var u = Mathf.Clamp01(star.Life / star.MaxLife);
                    var alpha = (1f - u) * 0.95f;
                    GUI.color = new Color(star.Color.r, star.Color.g, star.Color.b, alpha);
                    var size = star.Size * (0.85f + u * 0.35f);
                    GUI.Label(
                        new Rect(star.Pos.x - size * 0.5f, star.Pos.y - size * 0.5f, size, size),
                        "★",
                        glyphStyle);
                }
            }

            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        static Color MetalFlashColor(TrophyMetal metal)
        {
            switch (metal)
            {
                case TrophyMetal.Bronze:
                    return new Color(0.82f, 0.52f, 0.28f);
                case TrophyMetal.Silver:
                    return new Color(0.86f, 0.88f, 0.92f);
                case TrophyMetal.Gold:
                    return new Color(1f, 0.84f, 0.28f);
                case TrophyMetal.Platinum:
                    return new Color(0.72f, 0.82f, 0.9f);
                case TrophyMetal.Diamond:
                    return new Color(0.88f, 0.96f, 1f);
                case TrophyMetal.Rainbow:
                    return new Color(0.62f, 0.82f, 1f);
                case TrophyMetal.Galactic:
                    return new Color(0.55f, 0.42f, 0.92f);
                default:
                    return Color.white;
            }
        }
    }
}
