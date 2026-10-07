using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildMenuLayoutResetTests
    {
        const string BuildDockPrefsKey = "bat.buildDock";
        const string BuildInfoPrefsKey = "bat.buildInfo";

        [TearDown]
        public void TearDown()
        {
            HudFloatingPanel.ClearRect(BuildDockPrefsKey);
            HudFloatingPanel.ClearRect(BuildInfoPrefsKey);
        }

        [Test]
        public void DefaultDock_right_aligns_xMax_within_gap_of_screen()
        {
            const float screenW = 1920f;
            const float screenH = 1080f;
            const float gap = 12f;
            const int itemCount = 12;

            var dock = BuildMenuLayoutDefaults.DefaultDock(screenW, screenH, itemCount, gap);

            Assert.LessOrEqual(Mathf.Abs(screenW - dock.xMax - gap), 0.01f);
            Assert.Greater(dock.width, 0f);
            Assert.Greater(dock.height, 0f);
        }

        [Test]
        public void DefaultInfo_places_upper_left_near_gap_and_topY()
        {
            const float screenW = 1920f;
            const float screenH = 1080f;
            const float topY = 88f;
            const float gap = 12f;

            var info = BuildMenuLayoutDefaults.DefaultInfo(screenW, screenH, topY, gap);

            Assert.AreEqual(gap, info.x, 0.01f);
            Assert.AreEqual(Mathf.Max(gap, topY), info.y, 0.01f);
        }

        [Test]
        public void Clear_build_menu_prefs_removes_dock_and_info_keys()
        {
            HudFloatingPanel.SaveRect(BuildDockPrefsKey, new Rect(100f, 200f, 120f, 300f));
            HudFloatingPanel.SaveRect(BuildInfoPrefsKey, new Rect(8f, 96f, 260f, 280f));
            Assert.IsTrue(HudFloatingPanel.TryLoadRect(BuildDockPrefsKey, out _));
            Assert.IsTrue(HudFloatingPanel.TryLoadRect(BuildInfoPrefsKey, out _));

            HudFloatingPanel.ClearRect(BuildDockPrefsKey);
            HudFloatingPanel.ClearRect(BuildInfoPrefsKey);

            Assert.IsFalse(HudFloatingPanel.TryLoadRect(BuildDockPrefsKey, out _));
            Assert.IsFalse(HudFloatingPanel.TryLoadRect(BuildInfoPrefsKey, out _));
        }

        [Test]
        public void ResetBuildMenuLayout_clears_dock_and_info_prefs()
        {
            HudFloatingPanel.SaveRect(BuildDockPrefsKey, new Rect(100f, 200f, 120f, 300f));
            HudFloatingPanel.SaveRect(BuildInfoPrefsKey, new Rect(8f, 96f, 260f, 280f));

            var go = new GameObject("BuildMenuLayoutResetTests_Hud");
            try
            {
                var hud = go.AddComponent<TowerHudController>();
                hud.ResetBuildMenuLayout();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            Assert.IsFalse(HudFloatingPanel.TryLoadRect(BuildDockPrefsKey, out _));
            Assert.IsFalse(HudFloatingPanel.TryLoadRect(BuildInfoPrefsKey, out _));
        }
    }
}
