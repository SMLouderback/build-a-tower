using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Minimal IMGUI banner + scrolling ticker for <see cref="TowerNews"/> (spec §7).
    /// </summary>
    public sealed class TowerNewsHud
    {
        public const float BannerSeconds = 6f;
        public const float BannerHeight = 28f;
        public const float TickerHeight = 18f;
        public const float StripGap = 2f;
        /// <summary>Pixels per second — kept slow so the same strip is not a blur of repeats.</summary>
        public const float TickerPixelsPerSecond = 16f;
        public const float TickerLoopGapPixels = 160f;
        const string ItemSeparator = "        ·        ";

        TowerNewsItem _bannerItem;
        TowerNewsItem _bannerHandled;
        float _bannerUntilRealtime;
        float _tickerScroll;
        Rect _bannerRect;
        Rect _tickerRect;

        public float OccupiedHeight
        {
            get
            {
                var h = TickerHeight + StripGap;
                if (IsBannerVisible)
                    h += BannerHeight + StripGap;
                return h;
            }
        }

        public bool IsBannerVisible =>
            _bannerItem != null && Time.realtimeSinceStartup < _bannerUntilRealtime;

        public bool ContainsGuiPoint(Vector2 guiPoint) =>
            (IsBannerVisible && _bannerRect.Contains(guiPoint)) ||
            _tickerRect.Contains(guiPoint);

        /// <summary>
        /// Prune feed, refresh banner/ticker. Draws above the top dashboard. Returns height used.
        /// </summary>
        public float Draw(
            TowerNews news,
            int dayIndex,
            float gap,
            float stripTopY,
            GUIStyle label,
            GUIStyle button)
        {
            _bannerRect = Rect.zero;
            _tickerRect = Rect.zero;
            if (news == null)
                return 0f;

            news.Prune(dayIndex);

            var candidate = news.PeekBannerCandidate();
            if (candidate != null && !ReferenceEquals(candidate, _bannerHandled))
            {
                _bannerItem = candidate;
                _bannerHandled = candidate;
                _bannerUntilRealtime = Time.realtimeSinceStartup + BannerSeconds;
            }

            if (_bannerItem != null && Time.realtimeSinceStartup >= _bannerUntilRealtime)
                _bannerItem = null;

            var y = stripTopY + StripGap;
            var width = Screen.width - gap * 2f;

            if (IsBannerVisible && _bannerItem != null)
            {
                _bannerRect = new Rect(gap, y, width, BannerHeight);
                GUI.Box(_bannerRect, GUIContent.none);
                var text = _bannerItem.Text ?? string.Empty;
                GUI.Label(
                    new Rect(_bannerRect.x + 8f, _bannerRect.y + 4f, width - 88f, BannerHeight - 8f),
                    text,
                    label);
                if (GUI.Button(
                        new Rect(_bannerRect.xMax - 72f, _bannerRect.y + 3f, 64f, BannerHeight - 6f),
                        "Dismiss",
                        button))
                {
                    _bannerItem = null;
                    _bannerUntilRealtime = 0f;
                }

                y += BannerHeight + StripGap;
            }

            _tickerRect = new Rect(gap, y, width, TickerHeight);
            GUI.Box(_tickerRect, GUIContent.none);
            DrawTicker(news.TickerOrder(), label);
            y += TickerHeight;

            return y - stripTopY;
        }

        void DrawTicker(IReadOnlyList<TowerNewsItem> items, GUIStyle label)
        {
            if (items == null || items.Count == 0)
            {
                GUI.Label(
                    new Rect(_tickerRect.x + 8f, _tickerRect.y + 1f, _tickerRect.width - 16f, TickerHeight - 2f),
                    "Tower News — quiet day",
                    label);
                return;
            }

            var sb = new StringBuilder(items.Count * 48);
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(ItemSeparator);
                sb.Append(items[i].Text);
            }

            var joined = sb.ToString();
            var content = new GUIContent(joined);
            var textWidth = Mathf.Max(_tickerRect.width, label.CalcSize(content).x + 40f);
            var loopWidth = textWidth + TickerLoopGapPixels;
            _tickerScroll += Time.unscaledDeltaTime * TickerPixelsPerSecond;
            if (_tickerScroll >= loopWidth)
                _tickerScroll %= loopWidth;

            var clip = _tickerRect;
            GUI.BeginGroup(clip);
            GUI.Label(
                new Rect(8f - _tickerScroll, 1f, textWidth, TickerHeight - 2f),
                joined,
                label);
            // Loop: second copy so the strip feels continuous (with a readable pause gap).
            GUI.Label(
                new Rect(8f - _tickerScroll + loopWidth, 1f, textWidth, TickerHeight - 2f),
                joined,
                label);
            GUI.EndGroup();
        }
    }
}
