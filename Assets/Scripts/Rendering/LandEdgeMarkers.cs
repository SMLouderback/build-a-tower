using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Non-interactive For Sale signposts at dirt parcel horizontal edges (visual only).
    /// </summary>
    public sealed class LandEdgeMarkers : MonoBehaviour
    {
        const int SortingOrder = 15;
        const string SignResourceRoot = "Art/Dirt/";
        const string SignLeaf = "for_sale_sign";

        static Sprite _cachedSignSprite;

        public static Vector3 LeftSignPosition() =>
            new(DirtBand.MinX - 0.5f, 0f, 0f);

        public static Vector3 RightSignPosition() =>
            new(DirtBand.MaxX + 0.5f, 0f, 0f);

        public void EnsureSigns()
        {
            if (transform.Find("ForSaleSignLeft") != null) return;

            SpawnSign("ForSaleSignLeft", LeftSignPosition(), flipX: false);
            SpawnSign("ForSaleSignRight", RightSignPosition(), flipX: true);
        }

        void SpawnSign(string objectName, Vector3 position, bool flipX)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            if (flipX)
                go.transform.localScale = new Vector3(-1f, 1f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSignSprite();
            renderer.sortingOrder = SortingOrder;
        }

        static Sprite GetSignSprite()
        {
            if (_cachedSignSprite != null) return _cachedSignSprite;
            _cachedSignSprite = TryLoadSignArt() ?? BuildProceduralSignSprite();
            return _cachedSignSprite;
        }

        static Sprite TryLoadSignArt()
        {
            var path = SignResourceRoot + SignLeaf;
            byte[] png = null;
            var ta = Resources.Load<TextAsset>(path);
            if (ta != null) png = ta.bytes;
            if (png == null || png.Length < 32)
            {
                var srcTex = Resources.Load<Texture2D>(path);
                if (srcTex == null) return null;
                try { png = srcTex.EncodeToPNG(); }
                catch (UnityException) { return null; }
            }

            if (png == null || png.Length < 32) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = SignLeaf
            };
            if (!tex.LoadImage(png, false)) return null;

            var ppu = tex.width > 0 ? (float)tex.width : 32f;
            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0f),
                ppu);
        }

        static Sprite BuildProceduralSignSprite()
        {
            const int w = 16;
            const int h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "for_sale_sign_fallback"
            };

            var wood = new Color(0.45f, 0.28f, 0.14f, 1f);
            var board = new Color(0.95f, 0.55f, 0.12f, 1f);
            var text = new Color(0.98f, 0.96f, 0.92f, 1f);
            var clear = Color.clear;

            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                Color c = clear;
                if (x >= 7 && x <= 8 && y < 14)
                    c = wood;
                else if (y >= 14 && y <= 28 && x >= 2 && x <= 13)
                    c = board;
                else if (y >= 18 && y <= 25 && x >= 4 && x <= 11)
                {
                    if (y == 18 || y == 25 || y == 21)
                        c = text;
                    else if (x == 4 || x == 11)
                        c = text;
                }

                tex.SetPixel(x, y, c);
            }

            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        }
    }
}
