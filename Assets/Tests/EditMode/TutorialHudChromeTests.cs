using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class TutorialHudChromeTests
    {
        [Test]
        public void IntroCardRect_is_centered()
        {
            var r = TutorialHudChrome.IntroCardRect(1920f, 1080f);
            Assert.AreEqual(TutorialHudChrome.IntroCardWidth, r.width, 0.01f);
            Assert.AreEqual(TutorialHudChrome.IntroCardHeight, r.height, 0.01f);
            Assert.AreEqual((1920f - TutorialHudChrome.IntroCardWidth) * 0.5f, r.x, 0.01f);
            Assert.AreEqual((1080f - TutorialHudChrome.IntroCardHeight) * 0.5f, r.y, 0.01f);
        }

        [Test]
        public void DockedCardRect_is_smaller_and_near_left_tools()
        {
            var intro = TutorialHudChrome.IntroCardRect(1920f, 1080f);
            var dock = TutorialHudChrome.DockedCardRect(1920f, 1080f);
            Assert.Less(dock.width, intro.width);
            Assert.Less(dock.height, intro.height);
            Assert.Less(dock.x, intro.x);
        }

        [Test]
        public void LerpCardRect_at_ends_matches_from_and_to()
        {
            var from = new Rect(10f, 20f, 100f, 50f);
            var to = new Rect(200f, 300f, 40f, 30f);
            var a = TutorialHudChrome.LerpCardRect(from, to, 0f);
            var b = TutorialHudChrome.LerpCardRect(from, to, 1f);
            Assert.AreEqual(from.x, a.x, 0.01f);
            Assert.AreEqual(from.width, a.width, 0.01f);
            Assert.AreEqual(to.x, b.x, 0.01f);
            Assert.AreEqual(to.width, b.width, 0.01f);
        }

        [Test]
        public void GateHoverTip_returns_empty_when_tips_disabled()
        {
            Assert.AreEqual(string.Empty, TutorialHudChrome.GateHoverTip("Lobby tip", false));
        }

        [Test]
        public void GateHoverTip_returns_tip_when_enabled()
        {
            Assert.AreEqual("Lobby tip", TutorialHudChrome.GateHoverTip("Lobby tip", true));
        }

        [Test]
        public void GateHoverTip_returns_empty_for_null_or_blank()
        {
            Assert.AreEqual(string.Empty, TutorialHudChrome.GateHoverTip(null, true));
            Assert.AreEqual(string.Empty, TutorialHudChrome.GateHoverTip("", true));
        }

        [Test]
        public void ResolveHoverTip_returns_empty_when_tips_off()
        {
            Assert.AreEqual(string.Empty, TutorialHudChrome.ResolveHoverTip("hover", "gui", false));
        }

        [Test]
        public void ResolveHoverTip_prefers_hover_over_gui_when_on()
        {
            Assert.AreEqual("hover", TutorialHudChrome.ResolveHoverTip("hover", "gui", true));
            Assert.AreEqual("gui", TutorialHudChrome.ResolveHoverTip(null, "gui", true));
            Assert.AreEqual("gui", TutorialHudChrome.ResolveHoverTip("", "gui", true));
        }

        [Test]
        public void PulseAlpha_stays_in_unit_range()
        {
            for (var t = 0f; t < 8f; t += 0.25f)
            {
                var a = TutorialHudChrome.PulseAlpha(t);
                Assert.GreaterOrEqual(a, 0f);
                Assert.LessOrEqual(a, 1f);
            }
        }
    }
}
