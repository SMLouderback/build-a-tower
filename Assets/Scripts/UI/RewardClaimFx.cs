using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Screen-space burst of $ glyphs toward the bottom-bar savings chip on reward claim.
    /// Presentation-only; wallet credit happens before Play is called.
    /// </summary>
    public static class RewardClaimFx
    {
        const int MinGlyphs = 12;
        const int MaxGlyphs = 56;
        const float MinDuration = 0.8f;
        const float MaxDuration = 1.2f;

        struct Glyph
        {
            public Vector2 Start;
            public Vector2 End;
            public float Delay;
            public float Travel;
            public float Spin;
            public float Size;
        }

        static readonly List<Glyph> Glyphs = new();
        static float _age;
        static float _duration;
        static bool _active;

        public static bool IsPlaying => _active;

        public static int BurstGlyphCount(int cashPaid, int claimCount)
        {
            if (cashPaid <= 0 && claimCount <= 0)
                return 0;
            var scaled = 10 + Mathf.Max(0, claimCount) * 4 + Mathf.Min(24, Mathf.Max(0, cashPaid) / 2500);
            return Mathf.Clamp(scaled, MinGlyphs, MaxGlyphs);
        }

        public static float BurstDurationSeconds(int cashPaid, int claimCount)
        {
            if (cashPaid <= 0 && claimCount <= 0)
                return 0f;
            var extra = Mathf.Min(0.25f, Mathf.Max(0, cashPaid) / 100000f);
            var scaled = 0.85f + Mathf.Max(0, claimCount) * 0.04f + extra;
            return Mathf.Clamp(scaled, MinDuration, MaxDuration);
        }

        public static void Play(Rect targetChip, int cashPaid, int claimCount)
        {
            if (targetChip.width <= 0f || targetChip.height <= 0f)
                return;
            if (cashPaid <= 0 && claimCount <= 0)
                return;

            Glyphs.Clear();
            _age = 0f;
            _duration = BurstDurationSeconds(cashPaid, claimCount);
            _active = true;

            var count = BurstGlyphCount(cashPaid, claimCount);
            var center = targetChip.center;
            var rng = new System.Random(unchecked((int)(Time.unscaledTime * 1000f) ^ cashPaid ^ claimCount));

            for (var i = 0; i < count; i++)
            {
                var t = (float)i / Mathf.Max(1, count - 1);
                var erupt = i % 3 == 0;
                Vector2 start;
                if (erupt)
                {
                    start = center + new Vector2(
                        (float)(rng.NextDouble() * 2.0 - 1.0) * targetChip.width * 0.35f,
                        (float)(rng.NextDouble() * 2.0 - 1.0) * targetChip.height * 0.25f);
                }
                else
                {
                    var spread = targetChip.width * 2.2f + 40f;
                    start = new Vector2(
                        center.x + (float)(rng.NextDouble() * 2.0 - 1.0) * spread,
                        targetChip.yMin - 12f - (float)rng.NextDouble() * 120f);
                }

                var end = center + new Vector2(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * targetChip.width * 0.22f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * targetChip.height * 0.18f);

                Glyphs.Add(new Glyph
                {
                    Start = start,
                    End = end,
                    Delay = t * _duration * 0.35f,
                    Travel = Mathf.Lerp(0.38f, 0.62f, (float)rng.NextDouble()),
                    Spin = (float)(rng.NextDouble() * 2.0 - 1.0) * 18f,
                    Size = Mathf.Lerp(14f, 22f, (float)rng.NextDouble())
                });
            }
        }

        public static void Advance(float unscaledDeltaTime)
        {
            if (!_active)
                return;
            _age += unscaledDeltaTime;
            if (_age >= _duration + 0.15f)
            {
                Glyphs.Clear();
                _active = false;
            }
        }

        public static void Draw(GUIStyle glyphStyle)
        {
            if (!_active || glyphStyle == null || Glyphs.Count == 0)
                return;

            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;

            for (var i = 0; i < Glyphs.Count; i++)
            {
                var g = Glyphs[i];
                var local = _age - g.Delay;
                if (local <= 0f)
                    continue;

                var u = Mathf.Clamp01(local / g.Travel);
                var eased = 1f - (1f - u) * (1f - u);
                var pos = Vector2.Lerp(g.Start, g.End, eased);
                pos.y -= Mathf.Sin(u * Mathf.PI) * 18f;

                var fadeIn = Mathf.Clamp01(local / 0.08f);
                var fadeOut = local > g.Travel - 0.12f
                    ? Mathf.Clamp01((g.Travel - local) / 0.12f)
                    : 1f;
                var alpha = fadeIn * fadeOut * 0.95f;
                GUI.color = new Color(0.45f, 1f, 0.55f, alpha);

                var size = g.Size * (0.85f + eased * 0.2f);
                var rect = new Rect(pos.x - size * 0.5f, pos.y - size * 0.5f, size, size);
                GUIUtility.RotateAroundPivot(g.Spin * eased, pos);
                GUI.Label(rect, "$", glyphStyle);
                GUI.matrix = oldMatrix;
            }

            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }
    }
}
