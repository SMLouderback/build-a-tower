using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class MainMenuHeroLayoutTests
    {
        [Test]
        public void FitContain_ExactAspect_FillsView()
        {
            var fitted = MainMenuHeroLayout.FitContain(1920f, 1080f, 16f / 9f);
            Assert.AreEqual(0f, fitted.x, 0.01f);
            Assert.AreEqual(0f, fitted.y, 0.01f);
            Assert.AreEqual(1920f, fitted.width, 0.01f);
            Assert.AreEqual(1080f, fitted.height, 0.01f);
        }

        [Test]
        public void FitContain_WiderView_Pillarboxes()
        {
            var fitted = MainMenuHeroLayout.FitContain(2000f, 900f, 16f / 9f);
            Assert.Greater(fitted.x, 0f);
            Assert.AreEqual(0f, fitted.y, 0.01f);
            Assert.AreEqual(900f, fitted.height, 0.01f);
            Assert.AreEqual(900f * 16f / 9f, fitted.width, 0.01f);
            Assert.AreEqual(2000f, fitted.x * 2f + fitted.width, 0.01f);
        }

        [Test]
        public void FitContain_TallerView_Letterboxes()
        {
            var fitted = MainMenuHeroLayout.FitContain(1080f, 1920f, 16f / 9f);
            Assert.AreEqual(0f, fitted.x, 0.01f);
            Assert.Greater(fitted.y, 0f);
            Assert.AreEqual(1080f, fitted.width, 0.01f);
            Assert.AreEqual(1080f * 9f / 16f, fitted.height, 0.01f);
            Assert.AreEqual(1920f, fitted.y * 2f + fitted.height, 0.01f);
        }
    }
}
