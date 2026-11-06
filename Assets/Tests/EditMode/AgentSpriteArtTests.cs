using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AgentSpriteArtTests
    {
        [TearDown]
        public void TearDown() => AgentSpriteArt.ResetForTests();

        [Test]
        public void DressTierFromWealth_maps_street_and_premium()
        {
            Assert.AreEqual(AgentDressTier.Basic, AgentSpriteArt.DressTierFromWealth(WealthBand.Street));
            Assert.AreEqual(AgentDressTier.Basic, AgentSpriteArt.DressTierFromWealth(WealthBand.Basic));
            Assert.AreEqual(AgentDressTier.Mid, AgentSpriteArt.DressTierFromWealth(WealthBand.Mid));
            Assert.AreEqual(AgentDressTier.Upper, AgentSpriteArt.DressTierFromWealth(WealthBand.Upper));
            Assert.AreEqual(AgentDressTier.Upper, AgentSpriteArt.DressTierFromWealth(WealthBand.Premium));
        }

        [TestCase(AgentRole.OfficeWorker, AgentGender.Male, WealthBand.Basic, "office_worker_male_basic")]
        [TestCase(AgentRole.HotelGuest, AgentGender.Female, WealthBand.Premium, "hotel_guest_female_upper")]
        [TestCase(AgentRole.CondoResident, AgentGender.Male, WealthBand.Mid, "condo_resident_male_mid")]
        [TestCase(AgentRole.StreetVisitor, AgentGender.Female, WealthBand.Street, "street_visitor_female_basic")]
        [TestCase(AgentRole.EventVisitor, AgentGender.Male, WealthBand.Mid, "event_visitor_male_mid")]
        [TestCase(AgentRole.Maid, AgentGender.Female, WealthBand.Premium, "maid_female_uniform")]
        [TestCase(AgentRole.Handyman, AgentGender.Male, WealthBand.Basic, "handyman_male_uniform")]
        [TestCase(AgentRole.Security, AgentGender.Female, WealthBand.Upper, "security_female_uniform")]
        [TestCase(AgentRole.Criminal, AgentGender.Male, WealthBand.Basic, "criminal_male")]
        public void ResolveSheetKey_matches_catalog(
            AgentRole role,
            AgentGender gender,
            WealthBand wealth,
            string expected)
        {
            Assert.AreEqual(expected, AgentSpriteArt.ResolveSheetKey(role, gender, wealth));
        }

        [Test]
        public void GetWalkFrame_slices_horizontal_strip()
        {
            AgentSpriteArt.LoadSpriteForTests = _ => DummyStrip(384, 256);

            var frame0 = AgentSpriteArt.GetWalkFrame("office_worker_male_basic", 0);
            var frame3 = AgentSpriteArt.GetWalkFrame("office_worker_male_basic", 3);

            Assert.IsNotNull(frame0);
            Assert.IsNotNull(frame3);
            Assert.AreEqual(new Rect(0f, 0f, 96f, 256f), frame0.rect);
            Assert.AreEqual(new Rect(288f, 0f, 96f, 256f), frame3.rect);
            Assert.AreEqual(new Vector2(0.5f, 0f), frame0.pivot);
        }

        [Test]
        public void ScaleForTargetHeight_targets_seventy_percent_cell()
        {
            AgentSpriteArt.LoadSpriteForTests = _ => DummyStrip(384, 256);
            var frame = AgentSpriteArt.GetWalkFrame("test", 0);
            var scale = AgentSpriteArt.ScaleForTargetHeight(frame);
            Assert.AreEqual(AgentSpriteArt.TargetHeightCells, scale * frame.bounds.size.y, 0.001f);
        }

        [Test]
        public void FootLiftFromPivot_reads_lowest_opaque_row()
        {
            var tex = new Texture2D(96, 256, TextureFormat.RGBA32, false);
            var pixels = new Color32[96 * 256];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);
            pixels[96 * 24 + 48] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();

            var frame = Sprite.Create(tex, new Rect(0f, 0f, 96f, 256f), new Vector2(0.5f, 0f), 128f);
            var lift = AgentSpriteArt.FootLiftFromPivot(frame);
            Assert.Greater(lift, 0.1f);
            Assert.AreEqual(lift, frame.bounds.min.y > 0.001f ? frame.bounds.min.y : 24f / 128f, 0.05f);
        }

        static Sprite DummyStrip(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(new Color[width * height]);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0f, 0f), 128f);
        }
    }
}
