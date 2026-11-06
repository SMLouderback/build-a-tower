using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildFamilyColorsTests
    {
        [Test]
        public void Office_matches_hud_palette()
        {
            ExpectRgb(BuildFamilyColors.For(BuildFamily.Office), 0.35f, 0.55f, 0.85f);
        }

        [Test]
        public void Hotel_matches_hud_palette()
        {
            ExpectRgb(BuildFamilyColors.For(BuildFamily.Hotel), 0.62f, 0.35f, 0.85f);
        }

        [Test]
        public void Condo_matches_hud_palette()
        {
            ExpectRgb(BuildFamilyColors.For(BuildFamily.Condo), 0.35f, 0.75f, 0.45f);
        }

        [Test]
        public void Shops_matches_hud_palette()
        {
            ExpectRgb(BuildFamilyColors.For(BuildFamily.Shops), 0.9f, 0.6f, 0.25f);
        }

        [Test]
        public void Leisure_matches_hud_palette()
        {
            ExpectRgb(BuildFamilyColors.For(BuildFamily.Leisure), 0.25f, 0.65f, 0.70f);
        }

        [Test]
        public void Utility_matches_hud_palette()
        {
            ExpectRgb(BuildFamilyColors.For(BuildFamily.Utility), 0.45f, 0.7f, 0.75f);
        }

        [Test]
        public void Transit_matches_hud_palette()
        {
            ExpectRgb(BuildFamilyColors.For(BuildFamily.Transit), 0.7f, 0.7f, 0.35f);
        }

        [Test]
        public void None_and_unknown_families_use_gray()
        {
            ExpectRgb(BuildFamilyColors.For(BuildFamily.None), Color.gray.r, Color.gray.g, Color.gray.b);
            ExpectRgb(BuildFamilyColors.For((BuildFamily)999), Color.gray.r, Color.gray.g, Color.gray.b);
        }

        static void ExpectRgb(Color c, float r, float g, float b)
        {
            const float tol = 1e-5f;
            Assert.AreEqual(r, c.r, tol);
            Assert.AreEqual(g, c.g, tol);
            Assert.AreEqual(b, c.b, tol);
            Assert.AreEqual(1f, c.a, tol);
        }
    }
}
