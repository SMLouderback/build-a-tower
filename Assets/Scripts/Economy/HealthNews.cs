using System.Collections.Generic;

namespace BuildATower
{
    public static class HealthNews
    {
        static readonly (float Line, string Text)[] OpsThresholds =
        {
            (0.08f, "Illness is nearing 10%. New hotel stays will slow."),
            (0.10f, "10% of tenant rooms are sick. New hotel stays are slowing."),
            (0.13f, "Illness is nearing 15%. Office leasing will slow."),
            (0.15f, "15% of tenant rooms are sick. Office leasing is slowing."),
            (0.18f, "Illness is nearing 20%. Condo sales will slow."),
            (0.20f, "20% of tenant rooms are sick. Condo sales are slowing."),
            (0.23f, "Illness is nearing 25%. The city can quarantine the tower."),
        };

        const string QuarantineOnText =
            "City quarantine. No one enters or leaves. New stays, leases, and sales are stopped.";
        const string QuarantineLiftedText =
            "Quarantine lifted. The tower is open to new tenants and street traffic.";

        public static IReadOnlyList<TowerNewsItem> Collect(
            float previousShare,
            float newShare,
            bool wasQuarantined,
            bool quarantined)
        {
            if (wasQuarantined && !quarantined)
            {
                return new[]
                {
                    OpsItem(TowerNewsCategory.MajorEvent, QuarantineLiftedText),
                };
            }

            var items = new List<TowerNewsItem>();

            for (var i = 0; i < OpsThresholds.Length; i++)
            {
                var threshold = OpsThresholds[i];
                if (previousShare < threshold.Line && newShare >= threshold.Line)
                    items.Add(OpsItem(TowerNewsCategory.OpsSerious, threshold.Text));
            }

            if (!wasQuarantined && quarantined)
                items.Add(OpsItem(TowerNewsCategory.MajorEvent, QuarantineOnText));

            return items;
        }

        static TowerNewsItem OpsItem(TowerNewsCategory category, string text) =>
            new TowerNewsItem
            {
                Category = category,
                Text = text,
            };
    }
}
