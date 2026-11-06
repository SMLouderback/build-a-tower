using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class HudFloatingPanelTests
    {
        [Test]
        public void SoftClamp_keeps_min_visible_when_fully_off_right()
        {
            var panel = new Rect(2000f, 100f, 120f, 200f);
            var clamped = HudFloatingPanel.SoftClamp(panel, 800f, 600f, minVisible: 28f);
            Assert.LessOrEqual(clamped.x, 800f - 28f);
            Assert.GreaterOrEqual(clamped.xMax, 28f);
        }

        [Test]
        public void SoftClamp_allows_slight_overhang_when_grip_still_visible()
        {
            // 40px overhang, 80px still on-screen (≥ 28) → do not pull back
            var panel = new Rect(720f, 100f, 120f, 200f);
            var clamped = HudFloatingPanel.SoftClamp(panel, 800f, 600f, minVisible: 28f);
            Assert.AreEqual(720f, clamped.x, 0.01f);
        }

        [Test]
        public void SoftClamp_keeps_min_visible_when_fully_off_top_left()
        {
            var panel = new Rect(-500f, -400f, 100f, 80f);
            var clamped = HudFloatingPanel.SoftClamp(panel, 800f, 600f, 28f);
            Assert.GreaterOrEqual(clamped.xMax, 28f);
            Assert.GreaterOrEqual(clamped.yMax, 28f);
        }

        [Test]
        public void Prefs_round_trip_and_clear()
        {
            const string key = "bat.test.hudPanel";
            HudFloatingPanel.ClearRect(key);
            Assert.IsFalse(HudFloatingPanel.TryLoadRect(key, out _));

            var original = new Rect(12f, 34f, 100f, 200f);
            HudFloatingPanel.SaveRect(key, original);
            Assert.IsTrue(HudFloatingPanel.TryLoadRect(key, out var loaded));
            Assert.AreEqual(original.x, loaded.x, 0.01f);
            Assert.AreEqual(original.y, loaded.y, 0.01f);
            Assert.AreEqual(original.width, loaded.width, 0.01f);
            Assert.AreEqual(original.height, loaded.height, 0.01f);

            HudFloatingPanel.ClearRect(key);
            Assert.IsFalse(HudFloatingPanel.TryLoadRect(key, out _));
        }
    }
}
