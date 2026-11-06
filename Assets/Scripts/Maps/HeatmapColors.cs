using UnityEngine;

namespace BuildATower
{
    public enum HeatmapColorScale
    {
        Risk = 0,
        Profit = 1
    }

    /// <summary>
    /// Shared color math for Maps heatmaps: risk blue→red, profit red→grey→green.
    /// </summary>
    public static class HeatmapColors
    {
        /// <summary>Mid grey wash for room cells under active heatmap modes (zero / no data).</summary>
        public static readonly Color Grey = new Color(0.45f, 0.45f, 0.45f, 0.92f);

        const float ProfitDeadZone = 0.02f;

        /// <summary>
        /// Risk / stress ramp: blue (near 0, good) → red (near 1, bad). Alpha ~0.55–0.7.
        /// Caller should skip when score ≈ 0 so grey wash shows with no blue tint.
        /// </summary>
        public static Color RiskColor(float score01)
        {
            var t = score01 < 0f ? 0f : score01 > 1f ? 1f : score01;
            var a = Mathf.Lerp(0.55f, 0.7f, t);
            return Color.Lerp(
                new Color(0.12f, 0.35f, 0.92f, a),
                new Color(0.95f, 0.12f, 0.1f, a),
                t);
        }

        /// <summary>
        /// Profit ramp: red (losses) → green (profit). Returns false near zero (grey only).
        /// </summary>
        public static bool TryProfitColor(float signed01, out Color color)
        {
            if (Mathf.Abs(signed01) < ProfitDeadZone)
            {
                color = default;
                return false;
            }

            var t = signed01 < -1f ? -1f : signed01 > 1f ? 1f : signed01;
            if (t > 0f)
            {
                var a = Mathf.Lerp(0.55f, 0.7f, t);
                color = Color.Lerp(
                    new Color(0.45f, 0.45f, 0.45f, a),
                    new Color(0.15f, 0.75f, 0.25f, a),
                    t);
            }
            else
            {
                var u = -t;
                var a = Mathf.Lerp(0.55f, 0.7f, u);
                color = Color.Lerp(
                    new Color(0.45f, 0.45f, 0.45f, a),
                    new Color(0.95f, 0.12f, 0.1f, a),
                    u);
            }

            return true;
        }

        /// <summary>
        /// Tower-wide profit normalization to −1..+1.
        /// Only profits → do not invent a loss extreme; only losses → do not invent a profit extreme.
        /// </summary>
        public static float NormalizeTowerProfit(int net, int maxProfit, int maxLossAbs)
        {
            if (net > 0 && maxProfit > 0)
                return net / (float)maxProfit;
            if (net < 0 && maxLossAbs > 0)
                return -(Mathf.Abs(net) / (float)maxLossAbs);
            return 0f;
        }
    }
}
