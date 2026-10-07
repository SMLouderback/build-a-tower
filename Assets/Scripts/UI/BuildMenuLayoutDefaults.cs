using UnityEngine;

namespace BuildATower
{
    public static class BuildMenuLayoutDefaults
    {
        const float GripHeight = 24f;
        const float PanelPad = 8f;
        const float DefaultInfoHeight = 220f;
        const float DefaultInfoWidth = 260f;

        public static Rect DefaultDock(float screenW, float screenH, int itemCount, float gap = 12f)
        {
            var safeW = Mathf.Max(1f, screenW);
            var safeH = Mathf.Max(1f, screenH);
            var columns = Mathf.Max(1, TowerHudController.MenuStripColumns);
            var rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, itemCount) / (float)columns));
            var width = PanelPad * 2f
                        + columns * TowerHudController.MenuIconSize
                        + (columns - 1) * TowerHudController.MenuIconGap;
            var height = GripHeight
                         + PanelPad * 2f
                         + rows * TowerHudController.MenuIconSize
                         + Mathf.Max(0, rows - 1) * TowerHudController.MenuIconGap;
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
