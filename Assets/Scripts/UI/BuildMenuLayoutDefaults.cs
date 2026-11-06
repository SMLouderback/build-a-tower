using UnityEngine;

namespace BuildATower
{
    public static class BuildMenuLayoutDefaults
    {
        public const float GripHeight = 24f;
        public const float PanelPad = 8f;
        public const float SectionGap = 2f;
        public const float IconRowExtra = 4f;
        const float DefaultInfoHeight = 280f;
        const float DefaultInfoWidth = 260f;

        /// <summary>
        /// Height of one icon strip as drawn by <see cref="TowerHudController"/>'s DrawIconRow
        /// (rows of icon+gap, plus trailing padding).
        /// </summary>
        public static float IconStripHeight(int itemCount, int columns = -1)
        {
            if (itemCount <= 0) return 0f;
            var cols = columns > 0 ? columns : Mathf.Max(1, TowerHudController.MenuStripColumns);
            var rows = Mathf.CeilToInt(itemCount / (float)cols);
            return rows * (TowerHudController.MenuIconSize + TowerHudController.MenuIconGap) + IconRowExtra;
        }

        /// <summary>
        /// Full dock height: grip + pads + tool strip + section gap + family strip.
        /// Tools and families are separate 2-col grids (not one continuous grid).
        /// </summary>
        public static float DockContentHeight(int toolCount, int familyCount)
        {
            return GripHeight
                   + PanelPad
                   + IconStripHeight(toolCount)
                   + SectionGap
                   + IconStripHeight(familyCount)
                   + PanelPad;
        }

        public static Rect DefaultDock(float screenW, float screenH, int toolCount, int familyCount, float gap = 12f)
        {
            var safeW = Mathf.Max(1f, screenW);
            var safeH = Mathf.Max(1f, screenH);
            var columns = Mathf.Max(1, TowerHudController.MenuStripColumns);
            var width = PanelPad * 2f
                        + columns * TowerHudController.MenuIconSize
                        + (columns - 1) * TowerHudController.MenuIconGap;
            var height = DockContentHeight(toolCount, familyCount);
            var rect = new Rect(
                safeW - width - gap,
                Mathf.Max(gap, (safeH - height) * 0.5f),
                width,
                height);
            return HudFloatingPanel.SoftClamp(rect, safeW, safeH);
        }

        public static Rect DefaultInfo(float screenW, float screenH, float topY, float gap = 12f)
        {
            var safeW = Mathf.Max(1f, screenW);
            var safeH = Mathf.Max(1f, screenH);
            var width = Mathf.Min(DefaultInfoWidth, Mathf.Max(160f, safeW - gap * 2f));
            var height = Mathf.Min(DefaultInfoHeight, Mathf.Max(150f, safeH - topY - gap));
            var rect = new Rect(gap, Mathf.Max(gap, topY), width, height);
            return HudFloatingPanel.SoftClamp(rect, safeW, safeH);
        }
    }
}
