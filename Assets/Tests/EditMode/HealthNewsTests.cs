using System.Linq;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class HealthNewsTests
    {
        const string Near10Hotel = "Illness is nearing 10%. New hotel stays will slow.";
        const string TenHotel = "10% of tenant rooms are sick. New hotel stays are slowing.";
        const string Near15Office = "Illness is nearing 15%. Office leasing will slow.";
        const string FifteenOffice = "15% of tenant rooms are sick. Office leasing is slowing.";
        const string Near20Condo = "Illness is nearing 20%. Condo sales will slow.";
        const string TwentyCondo = "20% of tenant rooms are sick. Condo sales are slowing.";
        const string Near25Quarantine = "Illness is nearing 25%. The city can quarantine the tower.";
        const string QuarantineOn =
            "City quarantine. No one enters or leaves. New stays, leases, and sales are stopped.";
        const string QuarantineLifted =
            "Quarantine lifted. The tower is open to new tenants and street traffic.";

        [Test]
        public void Jump_from_zero_to_quarter_emits_all_threshold_ops_then_quarantine_major()
        {
            var items = HealthNews.Collect(0f, 0.25f, wasQuarantined: false, quarantined: true);

            Assert.AreEqual(8, items.Count);
            Assert.AreEqual(TowerNewsCategory.OpsSerious, items[0].Category);
            Assert.AreEqual(Near10Hotel, items[0].Text);
            Assert.AreEqual(TowerNewsCategory.OpsSerious, items[1].Category);
            Assert.AreEqual(TenHotel, items[1].Text);
            Assert.AreEqual(TowerNewsCategory.OpsSerious, items[2].Category);
            Assert.AreEqual(Near15Office, items[2].Text);
            Assert.AreEqual(TowerNewsCategory.OpsSerious, items[3].Category);
            Assert.AreEqual(FifteenOffice, items[3].Text);
            Assert.AreEqual(TowerNewsCategory.OpsSerious, items[4].Category);
            Assert.AreEqual(Near20Condo, items[4].Text);
            Assert.AreEqual(TowerNewsCategory.OpsSerious, items[5].Category);
            Assert.AreEqual(TwentyCondo, items[5].Text);
            Assert.AreEqual(TowerNewsCategory.OpsSerious, items[6].Category);
            Assert.AreEqual(Near25Quarantine, items[6].Text);
            Assert.AreEqual(TowerNewsCategory.MajorEvent, items[7].Category);
            Assert.AreEqual(QuarantineOn, items[7].Text);

            foreach (var item in items)
            {
                Assert.AreEqual(0, item.Priority);
                Assert.AreEqual(0, item.CreatedDayIndex);
                Assert.AreEqual(0, item.ExpireDayIndex);
            }
        }

        [Test]
        public void Stay_at_quarter_with_quarantine_already_on_emits_nothing()
        {
            var items = HealthNews.Collect(0.25f, 0.25f, wasQuarantined: true, quarantined: true);

            Assert.AreEqual(0, items.Count);
        }

        [Test]
        public void Dropping_quarantine_emits_lifted_major_line_only()
        {
            var items = HealthNews.Collect(0.25f, 0.05f, wasQuarantined: true, quarantined: false);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(TowerNewsCategory.MajorEvent, items[0].Category);
            Assert.AreEqual(QuarantineLifted, items[0].Text);
        }

        [Test]
        public void Upward_crossing_emits_each_threshold_at_most_once_per_call()
        {
            var items = HealthNews.Collect(0.07f, 0.11f, wasQuarantined: false, quarantined: false);

            Assert.AreEqual(2, items.Count);
            Assert.That(items.Select(i => i.Text), Is.EqualTo(new[] { Near10Hotel, TenHotel }));
            Assert.IsTrue(items.All(i => i.Category == TowerNewsCategory.OpsSerious));
        }

        [Test]
        public void Quarantine_turns_on_when_share_already_past_23_emits_major_only()
        {
            var items = HealthNews.Collect(0.24f, 0.26f, wasQuarantined: false, quarantined: true);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(TowerNewsCategory.MajorEvent, items[0].Category);
            Assert.AreEqual(QuarantineOn, items[0].Text);
        }
    }
}
