using System.Linq;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class TowerNewsTests
    {
        static TowerNewsItem Item(
            TowerNewsCategory category,
            int priority,
            string text,
            int createdDay = 0,
            int expireDay = 100) =>
            new TowerNewsItem
            {
                Category = category,
                Priority = priority,
                Text = text,
                CreatedDayIndex = createdDay,
                ExpireDayIndex = expireDay
            };

        [Test]
        public void MaxItems_is_32()
        {
            Assert.AreEqual(32, TowerNews.MaxItems);
        }

        [Test]
        public void Push_appends_items_in_order()
        {
            var news = new TowerNews();
            var a = Item(TowerNewsCategory.Quirk, 1, "a");
            var b = Item(TowerNewsCategory.OpsSerious, 2, "b");

            news.Push(a);
            news.Push(b);

            Assert.AreEqual(2, news.Items.Count);
            Assert.AreSame(a, news.Items[0]);
            Assert.AreSame(b, news.Items[1]);
        }

        [Test]
        public void Push_over_MaxItems_drops_oldest()
        {
            var news = new TowerNews();
            for (var i = 0; i < TowerNews.MaxItems; i++)
                news.Push(Item(TowerNewsCategory.Quirk, 0, $"i{i}"));

            var newest = Item(TowerNewsCategory.MajorEvent, 9, "newest");
            news.Push(newest);

            Assert.AreEqual(TowerNews.MaxItems, news.Items.Count);
            Assert.AreEqual("i1", news.Items[0].Text);
            Assert.AreSame(newest, news.Items[news.Items.Count - 1]);
            Assert.IsFalse(news.Items.Any(x => x.Text == "i0"));
        }

        [Test]
        public void Push_same_category_and_text_refreshes_instead_of_stacking()
        {
            var news = new TowerNews();
            var first = Item(TowerNewsCategory.OpsSerious, 5, "same", createdDay: 1, expireDay: 3);
            news.Push(first);
            news.Push(Item(TowerNewsCategory.OpsSerious, 9, "same", createdDay: 2, expireDay: 8));
            news.Push(Item(TowerNewsCategory.Quirk, 1, "same", createdDay: 2, expireDay: 5));

            Assert.AreEqual(2, news.Items.Count);
            Assert.AreSame(first, news.Items[0]);
            Assert.AreEqual(9, first.Priority);
            Assert.AreEqual(8, first.ExpireDayIndex);
            Assert.AreEqual(TowerNewsCategory.Quirk, news.Items[1].Category);
        }

        [Test]
        public void Prune_keeps_item_on_inclusive_expire_day()
        {
            var news = new TowerNews();
            var item = Item(TowerNewsCategory.OpsSerious, 1, "keep", createdDay: 1, expireDay: 5);
            news.Push(item);

            news.Prune(5);

            Assert.AreEqual(1, news.Items.Count);
            Assert.AreSame(item, news.Items[0]);
        }

        [Test]
        public void Prune_drops_item_after_expire_day()
        {
            var news = new TowerNews();
            news.Push(Item(TowerNewsCategory.OpsSerious, 1, "gone", createdDay: 1, expireDay: 5));
            news.Push(Item(TowerNewsCategory.Quirk, 0, "stay", createdDay: 1, expireDay: 10));

            news.Prune(6);

            Assert.AreEqual(1, news.Items.Count);
            Assert.AreEqual("stay", news.Items[0].Text);
        }

        [Test]
        public void PeekBannerCandidate_returns_null_when_no_major_event()
        {
            var news = new TowerNews();
            news.Push(Item(TowerNewsCategory.OpsSerious, 99, "ops"));
            news.Push(Item(TowerNewsCategory.Quirk, 50, "quirk"));

            Assert.IsNull(news.PeekBannerCandidate());
        }

        [Test]
        public void PeekBannerCandidate_returns_highest_priority_major_event()
        {
            var news = new TowerNews();
            var low = Item(TowerNewsCategory.MajorEvent, 1, "low");
            var high = Item(TowerNewsCategory.MajorEvent, 5, "high");
            var mid = Item(TowerNewsCategory.MajorEvent, 3, "mid");
            news.Push(low);
            news.Push(high);
            news.Push(mid);
            news.Push(Item(TowerNewsCategory.OpsSerious, 100, "ops"));

            Assert.AreSame(high, news.PeekBannerCandidate());
        }

        [Test]
        public void TickerOrder_puts_serious_categories_before_quirk()
        {
            var news = new TowerNews();
            var quirk = Item(TowerNewsCategory.Quirk, 99, "quirk");
            var ops = Item(TowerNewsCategory.OpsSerious, 1, "ops");
            var major = Item(TowerNewsCategory.MajorEvent, 1, "major");
            news.Push(quirk);
            news.Push(ops);
            news.Push(major);

            var ordered = news.TickerOrder();

            Assert.AreEqual(3, ordered.Count);
            Assert.AreSame(ops, ordered[0]);
            Assert.AreSame(major, ordered[1]);
            Assert.AreSame(quirk, ordered[2]);
        }

        [Test]
        public void TickerOrder_orders_by_priority_descending_with_stable_ties()
        {
            var news = new TowerNews();
            var a = Item(TowerNewsCategory.OpsSerious, 2, "a");
            var b = Item(TowerNewsCategory.MajorEvent, 2, "b");
            var c = Item(TowerNewsCategory.OpsSerious, 5, "c");
            var qLow = Item(TowerNewsCategory.Quirk, 1, "qLow");
            var qHigh = Item(TowerNewsCategory.Quirk, 3, "qHigh");
            news.Push(a);
            news.Push(b);
            news.Push(c);
            news.Push(qLow);
            news.Push(qHigh);

            var ordered = news.TickerOrder();

            Assert.AreEqual(new[] { "c", "a", "b", "qHigh", "qLow" }, ordered.Select(x => x.Text).ToArray());
        }
    }
}
