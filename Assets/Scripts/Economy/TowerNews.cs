using System.Collections.Generic;

namespace BuildATower
{
    public enum TowerNewsCategory
    {
        MajorEvent,
        OpsSerious,
        Quirk
    }

    public sealed class TowerNewsItem
    {
        public TowerNewsCategory Category;
        public int Priority;
        public string Text;
        public int CreatedDayIndex;
        public int ExpireDayIndex;
    }

    /// <summary>
    /// Capped priority news feed for banner + ticker HUD (spec §7).
    /// </summary>
    public sealed class TowerNews
    {
        public const int MaxItems = 32;

        readonly List<TowerNewsItem> _items = new List<TowerNewsItem>();

        public IReadOnlyList<TowerNewsItem> Items => _items;

        public void Push(TowerNewsItem item)
        {
            if (item == null) return;

            _items.Add(item);
            while (_items.Count > MaxItems)
                _items.RemoveAt(0);
        }

        public void Prune(int currentDayIndex)
        {
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (currentDayIndex > _items[i].ExpireDayIndex)
                    _items.RemoveAt(i);
            }
        }

        public TowerNewsItem PeekBannerCandidate()
        {
            TowerNewsItem best = null;
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.Category != TowerNewsCategory.MajorEvent) continue;
                if (best == null || item.Priority > best.Priority)
                    best = item;
            }

            return best;
        }

        public IReadOnlyList<TowerNewsItem> TickerOrder()
        {
            var ordered = new List<TowerNewsItem>(_items.Count);
            AppendTickerGroup(ordered, seriousOnly: true);
            AppendTickerGroup(ordered, seriousOnly: false);
            return ordered;
        }

        void AppendTickerGroup(List<TowerNewsItem> dest, bool seriousOnly)
        {
            var start = dest.Count;
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var isQuirk = item.Category == TowerNewsCategory.Quirk;
                if (seriousOnly == isQuirk) continue;
                dest.Add(item);
            }

            // Stable insertion sort by Priority descending within the appended group.
            for (var i = start + 1; i < dest.Count; i++)
            {
                var key = dest[i];
                var j = i - 1;
                while (j >= start && dest[j].Priority < key.Priority)
                {
                    dest[j + 1] = dest[j];
                    j--;
                }

                dest[j + 1] = key;
            }
        }
    }
}
