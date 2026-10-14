using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class DollhouseContextArtTests
    {
        [TearDown]
        public void TearDown()
        {
            DollhouseContextArt.ResetForTests();
            RoomDollhouseArt.ResetForTests();
        }

        [Test]
        public void Interior_left_preserves_left_band_glass_pixels()
        {
            var type = Type("office_micro", BuildFamily.Office);
            var baseSprite = SpriteWithLeftWindowBand();
            var window = Pixel(baseSprite, 0, 4);

            var interiorLeft = DollhouseContextArt.GetSprite(
                type,
                leftExterior: false,
                rightExterior: true,
                AtriumStackRole.Solo,
                BuildFamily.Office,
                baseSprite);

            Assert.Less(ColorDistance(Pixel(interiorLeft, 0, 4), window), 0.001f);
        }

        [Test]
        public void Interior_left_preserves_dark_mirror_panes()
        {
            var type = Type("office_micro", BuildFamily.Office);
            var baseSprite = SpriteWithLeftDarkMirrorBand();
            var sourcePane = Pixel(baseSprite, 0, 4);

            var interiorLeft = DollhouseContextArt.GetSprite(
                type,
                leftExterior: false,
                rightExterior: true,
                AtriumStackRole.Solo,
                BuildFamily.Office,
                baseSprite);

            Assert.Less(ColorDistance(Pixel(interiorLeft, 0, 4), sourcePane), 0.002f);
        }

        [Test]
        public void Interior_left_does_not_rewrite_band_pixels()
        {
            var type = Type("office_micro", BuildFamily.Office);
            var baseSprite = SpriteWithLeftFurnitureInBand();
            var before = Pixel(baseSprite, 1, 4);

            var baked = DollhouseContextArt.GetSprite(
                type,
                leftExterior: false,
                rightExterior: true,
                AtriumStackRole.Solo,
                BuildFamily.Office,
                baseSprite);

            Assert.Less(ColorDistance(Pixel(baked, 1, 4), before), 0.001f);
        }

        [Test]
        public void Family_parameter_does_not_change_baked_pixels()
        {
            var type = Type("condo_base", BuildFamily.Condo);
            var baseSprite = SpriteWithAccentPatch();
            var beforeAccent = Pixel(baseSprite, 8, 5);

            var withFamily = DollhouseContextArt.GetSprite(
                type,
                leftExterior: true,
                rightExterior: true,
                AtriumStackRole.Solo,
                BuildFamily.Condo,
                baseSprite);

            DollhouseContextArt.ResetForTests();

            var withoutFamily = DollhouseContextArt.GetSprite(
                type,
                leftExterior: true,
                rightExterior: true,
                AtriumStackRole.Solo,
                BuildFamily.None,
                baseSprite);

            Assert.Less(ColorDistance(Pixel(withFamily, 8, 5), beforeAccent), 0.001f);
            Assert.Less(ColorDistance(Pixel(withoutFamily, 8, 5), beforeAccent), 0.001f);
        }

        [Test]
        public void Different_family_values_share_cache_entry()
        {
            var type = Type("hotel_base", BuildFamily.Hotel);
            var baseSprite = SpriteWithAccentPatch();

            var hotel = DollhouseContextArt.GetSprite(
                type,
                leftExterior: true,
                rightExterior: false,
                AtriumStackRole.Solo,
                BuildFamily.Hotel,
                baseSprite);
            var none = DollhouseContextArt.GetSprite(
                type,
                leftExterior: true,
                rightExterior: false,
                AtriumStackRole.Solo,
                BuildFamily.None,
                baseSprite);

            Assert.AreSame(hotel, none);
        }

        [Test]
        public void Same_arguments_return_cached_sprite_instance()
        {
            var type = Type("hotel_base", BuildFamily.Hotel);
            var baseSprite = SpriteWithAccentPatch();

            var first = DollhouseContextArt.GetSprite(
                type,
                leftExterior: true,
                rightExterior: false,
                AtriumStackRole.Solo,
                BuildFamily.Hotel,
                baseSprite);
            var second = DollhouseContextArt.GetSprite(
                type,
                leftExterior: true,
                rightExterior: false,
                AtriumStackRole.Solo,
                BuildFamily.Hotel,
                baseSprite);

            Assert.AreSame(first, second);
        }

        [Test]
        public void ResetForTests_clears_cache()
        {
            var type = Type("hotel_base", BuildFamily.Hotel);
            var baseSprite = SpriteWithAccentPatch();

            var first = DollhouseContextArt.GetSprite(
                type,
                leftExterior: true,
                rightExterior: false,
                AtriumStackRole.Solo,
                BuildFamily.Hotel,
                baseSprite);
            DollhouseContextArt.ResetForTests();
            var second = DollhouseContextArt.GetSprite(
                type,
                leftExterior: true,
                rightExterior: false,
                AtriumStackRole.Solo,
                BuildFamily.Hotel,
                baseSprite);

            Assert.AreNotSame(first, second);
        }

        [Test]
        public void Atrium_mid_uses_mid_leaf_when_available()
        {
            var type = Type("leisure_atrium", BuildFamily.Leisure, isAtrium: true);
            var baseSprite = SolidSprite(new Color(0.45f, 0.38f, 0.30f, 1f));
            var midSprite = SolidSprite(new Color(0.12f, 0.34f, 0.56f, 1f));
            RoomDollhouseArt.LoadSpriteForTests = path =>
                path == "Art/Dollhouse/atrium_8x3_mid" ? midSprite : null;

            var baked = DollhouseContextArt.GetSprite(
                type,
                leftExterior: true,
                rightExterior: true,
                AtriumStackRole.Mid,
                BuildFamily.Leisure,
                baseSprite);

            Assert.Less(
                ColorDistance(Pixel(baked, 4, 4), Pixel(midSprite, 4, 4)),
                ColorDistance(Pixel(baked, 4, 4), Pixel(baseSprite, 4, 4)));
        }

        [Test]
        public void Atrium_mid_falls_back_to_base_sprite_when_mid_leaf_is_missing()
        {
            var type = Type("leisure_atrium", BuildFamily.Leisure, isAtrium: true);
            var baseSprite = SolidSprite(new Color(0.45f, 0.38f, 0.30f, 1f));
            RoomDollhouseArt.LoadSpriteForTests = _ => null;

            var baked = DollhouseContextArt.GetSprite(
                type,
                leftExterior: true,
                rightExterior: true,
                AtriumStackRole.Mid,
                BuildFamily.Leisure,
                baseSprite);

            Assert.Less(ColorDistance(Pixel(baked, 4, 4), Pixel(baseSprite, 4, 4)), 0.001f);
        }

        static Sprite SpriteWithLeftWindowBand()
        {
            var tex = Texture(20, 10, new Color(0.52f, 0.43f, 0.34f, 1f));
            var window = new Color(0.55f, 0.85f, 0.95f, 1f);
            for (var y = 0; y < tex.height; y++)
            {
                tex.SetPixel(0, y, window);
                tex.SetPixel(1, y, window);
                tex.SetPixel(2, y, new Color(0.48f, 0.39f, 0.31f, 1f));
            }

            tex.Apply();
            return CreateSprite(tex);
        }

        static Sprite SpriteWithLeftDarkMirrorBand()
        {
            var wall = new Color(0.72f, 0.68f, 0.62f, 1f);
            var tex = Texture(20, 10, wall);
            var darkMirror = new Color(0.22f, 0.24f, 0.28f, 1f);
            for (var y = 0; y < tex.height; y++)
            {
                tex.SetPixel(0, y, darkMirror);
                tex.SetPixel(1, y, darkMirror);
            }

            tex.Apply();
            return CreateSprite(tex);
        }

        static Sprite SpriteWithLeftFurnitureInBand()
        {
            var wall = new Color(0.72f, 0.68f, 0.62f, 1f);
            var desk = new Color(0.55f, 0.32f, 0.18f, 1f);
            var tex = Texture(20, 10, wall);
            for (var y = 0; y < tex.height; y++)
            {
                tex.SetPixel(0, y, wall);
                tex.SetPixel(1, y, desk);
            }

            tex.Apply();
            return CreateSprite(tex);
        }

        static Sprite SpriteWithAccentPatch()
        {
            var tex = Texture(20, 10, new Color(0.50f, 0.43f, 0.34f, 1f));
            tex.SetPixel(8, 5, new Color(0.30f, 0.12f, 0.90f, 1f));
            tex.SetPixel(9, 5, new Color(0.34f, 0.10f, 0.82f, 1f));
            tex.Apply();
            return CreateSprite(tex);
        }

        static Sprite SolidSprite(Color color) => CreateSprite(Texture(20, 10, color));

        static Texture2D Texture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Sprite CreateSprite(Texture2D tex) =>
            Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, RoomDollhouseArt.PixelsPerUnit);

        static Color Pixel(Sprite sprite, int x, int y) => sprite.texture.GetPixel(x, y);

        static float ColorDistance(Color a, Color b)
        {
            var dr = a.r - b.r;
            var dg = a.g - b.g;
            var db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        static RoomTypeSO Type(string id, BuildFamily family, bool isAtrium = false)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.buildFamily = family;
            so.category = RoomCategory.Commercial;
            so.isAtrium = isAtrium;
            so.size = Vector2Int.one;
            return so;
        }
    }
}
