using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// 2.5D parallax: mid city roofs + painted grass and transparent tree
    /// buffers (behind the tower).
    /// </summary>
    public sealed class ParallaxBackdrop : MonoBehaviour
    {
        enum PlateMode
        {
            City,
            GrassOpaque,
            TreesAlpha
        }

        [SerializeField] Camera targetCamera;
        [SerializeField] float midLag = 0.72f;
        [SerializeField] float grassLag = 0.97f;
        [SerializeField] float treeLag = 0.985f;
        [Tooltip("World Y where skyline building feet meet the dirt/lobby ground.")]
        [SerializeField] float groundY = 0f;
        [SerializeField] float midMaxHeight = 3.8f;
        [SerializeField] float grassMaxHeight = 1.15f;
        [SerializeField] float treeMaxHeight = 2.8f;
        [SerializeField] float midTargetWidth = 18f;
        [SerializeField] float vegTargetWidth = 22f;
        [SerializeField] float coverageWidth = 140f;

        Transform _rig;
        Transform _mid;
        Transform _grass;
        Transform _trees;
        SpriteRenderer[] _midRenderers;
        SpriteRenderer[] _grassRenderers;
        SpriteRenderer[] _treeRenderers;
        float _lastCamX;
        float _midOff;
        float _grassOff;
        float _treeOff;
        float _midTileW = 32f;
        float _grassTileW = 24f;
        float _treeTileW = 24f;

        void Start()
        {
            BindCamera();
            BuildRig();
        }

        void BindCamera()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        void BuildRig()
        {
            if (_rig != null) return;
            BindCamera();

            var go = new GameObject("ParallaxRig");
            go.transform.SetParent(transform, false);
            _rig = go.transform;

            _mid = SpawnStrip(
                _rig,
                "MidRoofs",
                sortingOrder: -180,
                resource: "Art/Parallax/mid_roofs",
                fallback: BuildMidFallback,
                maxH: midMaxHeight,
                targetW: midTargetWidth,
                mode: PlateMode.City,
                out _midRenderers,
                out _midTileW);

            _grass = SpawnStrip(
                _rig,
                "NearGrass",
                sortingOrder: -145,
                resource: "Art/Parallax/near_grass",
                fallback: BuildGrassFallback,
                maxH: grassMaxHeight,
                targetW: vegTargetWidth,
                mode: PlateMode.GrassOpaque,
                out _grassRenderers,
                out _grassTileW);

            _trees = SpawnStrip(
                _rig,
                "NearTrees",
                sortingOrder: -140,
                resource: "Art/Parallax/near_trees",
                fallback: BuildTreesFallback,
                maxH: treeMaxHeight,
                targetW: vegTargetWidth,
                mode: PlateMode.TreesAlpha,
                out _treeRenderers,
                out _treeTileW);

            if (targetCamera != null)
                _lastCamX = targetCamera.transform.position.x;
        }

        void LateUpdate()
        {
            BindCamera();
            if (targetCamera == null || _rig == null) return;

            var cam = targetCamera.transform.position;
            var dx = cam.x - _lastCamX;
            _lastCamX = cam.x;

            _midOff += -dx * midLag;
            _grassOff += -dx * grassLag;
            _treeOff += -dx * treeLag;
            _midOff = WrapOffset(_midOff, _midTileW);
            _grassOff = WrapOffset(_grassOff, _grassTileW);
            _treeOff = WrapOffset(_treeOff, _treeTileW);

            if (_mid != null) _mid.position = new Vector3(cam.x + _midOff, groundY + 0.15f, 0f);
            if (_grass != null) _grass.position = new Vector3(cam.x + _grassOff, groundY, 0f);
            if (_trees != null) _trees.position = new Vector3(cam.x + _treeOff, groundY, 0f);

            ApplyDaylightTint();
        }

        void ApplyDaylightTint()
        {
            var clock = FindAnyObjectByType<TowerSimulation>()?.Clock;
            var sky = DayNightSky.ColorAt(clock);
            var dayAmt = Mathf.Clamp01(
                Vector3.Dot(new Vector3(sky.r, sky.g, sky.b), new Vector3(0.3f, 0.5f, 0.2f)) /
                0.75f);
            var midTint = Color.Lerp(
                new Color(0.40f, 0.38f, 0.36f, 1f),
                new Color(1.08f, 1.04f, 0.98f, 1f),
                dayAmt);
            var vegTint = Color.Lerp(
                new Color(0.22f, 0.32f, 0.18f, 1f),
                new Color(0.55f, 0.72f, 0.38f, 1f),
                dayAmt);

            SetTint(_midRenderers, midTint);
            SetTint(_grassRenderers, vegTint);
            SetTint(_treeRenderers, vegTint);
        }

        static void SetTint(SpriteRenderer[] renderers, Color tint)
        {
            if (renderers == null) return;
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].color = tint;
        }

        static float WrapOffset(float x, float tileW)
        {
            var half = Mathf.Max(1f, tileW) * 0.5f;
            if (x > half) x -= tileW;
            if (x < -half) x += tileW;
            return x;
        }

        Transform SpawnStrip(
            Transform parent,
            string name,
            int sortingOrder,
            string resource,
            System.Func<Sprite> fallback,
            float maxH,
            float targetW,
            PlateMode mode,
            out SpriteRenderer[] renderers,
            out float tileWorldW)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;

            var sprite = resource != null ? LoadSprite(resource, mode) : null;
            if (sprite == null)
                sprite = fallback();

            var bw = Mathf.Max(0.01f, sprite.bounds.size.x);
            var bh = Mathf.Max(0.01f, sprite.bounds.size.y);
            var scale = Mathf.Min(maxH / bh, targetW / bw);
            tileWorldW = bw * scale;

            var tiles = Mathf.Max(3, Mathf.CeilToInt(coverageWidth / tileWorldW) + 2);
            if ((tiles & 1) == 0) tiles++;
            var half = tiles / 2;
            renderers = new SpriteRenderer[tiles];
            for (var i = 0; i < tiles; i++)
            {
                var idx = i - half;
                var tile = new GameObject(name + "_" + idx);
                tile.transform.SetParent(root.transform, false);
                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = Color.white;
                sr.sortingOrder = sortingOrder;
                tile.transform.localScale = new Vector3(scale, scale, 1f);
                tile.transform.localPosition = new Vector3(idx * tileWorldW, 0f, 0f);
                renderers[i] = sr;
            }

            return root.transform;
        }

        static Sprite LoadSprite(string path, PlateMode mode)
        {
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
            var runtime = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!runtime.LoadImage(png, false)) return null;

            var px = runtime.GetPixels();
            var w = runtime.width;
            var h = runtime.height;

            if (mode == PlateMode.City)
            {
                FloodClearSkyFromTop(px, w, h);
                FloodClearGroundBarFromBottom(px, w, h);
                RemoveSmallBlobs(px, w, h, maxPixels: Mathf.Max(40, w * h / 2500));
                FadeSeamEdges(px, w, h);
                ForceOpaqueWhereVisible(px);
            }
            else if (mode == PlateMode.GrassOpaque)
            {
                FadeSeamEdges(px, w, h);
                ForceOpaqueWhereVisible(px);
            }
            else // TreesAlpha
            {
                KeyTreeBackground(px, w, h);
                FadeSeamEdges(px, w, h);
                // Keep alpha — city/grass show through canopy gaps.
            }

            if (!TryContentBounds(px, w, h, out var minX, out var minY, out var maxX, out var maxY))
            {
                runtime.SetPixels(px);
                runtime.Apply(false, false);
                return Sprite.Create(
                    runtime,
                    new Rect(0, 0, w, h),
                    new Vector2(0.5f, 0f),
                    64f);
            }

            minX = Mathf.Max(0, minX - 1);
            minY = Mathf.Max(0, minY - 1);
            maxX = Mathf.Min(w - 1, maxX + 1);
            maxY = Mathf.Min(h - 1, maxY + 1);
            var cw = maxX - minX + 1;
            var ch = maxY - minY + 1;
            var cropped = new Color[cw * ch];
            for (var y = 0; y < ch; y++)
            for (var x = 0; x < cw; x++)
                cropped[y * cw + x] = px[(minY + y) * w + (minX + x)];

            var croppedTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            croppedTex.SetPixels(cropped);
            croppedTex.Apply(false, false);
            Object.Destroy(runtime);

            return Sprite.Create(
                croppedTex,
                new Rect(0, 0, cw, ch),
                new Vector2(0.5f, 0f),
                64f);
        }

        static void ForceOpaqueWhereVisible(Color[] px)
        {
            for (var i = 0; i < px.Length; i++)
            {
                if (px[i].a < 0.08f) continue;
                var c = px[i];
                c.a = 1f;
                px[i] = c;
            }
        }

        /// <summary>
        /// Drop generator white / near-white / black plates from tree strips.
        /// </summary>
        static void KeyTreeBackground(Color[] px, int w, int h)
        {
            bool IsPlate(Color c)
            {
                if (c.a < 0.08f) return true;
                var lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                var max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                var min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                var chroma = max - min;
                if (chroma > 0.14f) return false; // keep foliage / trunks
                if (lum > 0.80f) return true;
                if (lum < 0.08f) return true;
                return false;
            }

            FloodClear(px, w, h, IsPlate, seedTop: true, seedBottom: true);
            // Enclosed plate pockets (between canopies).
            for (var i = 0; i < px.Length; i++)
            {
                if (IsPlate(px[i]))
                    px[i] = Color.clear;
            }
        }

        static bool TryContentBounds(
            Color[] px, int w, int h,
            out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = w;
            minY = h;
            maxX = -1;
            maxY = -1;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (px[y * w + x].a < 0.08f) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            return maxX >= minX && maxY >= minY;
        }

        static void FloodClearSkyFromTop(Color[] px, int w, int h)
        {
            bool IsSky(Color c)
            {
                if (c.a < 0.05f) return true;
                var lum = c.r * 0.30f + c.g * 0.59f + c.b * 0.11f;
                var max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                var min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                var chroma = max - min;
                if (lum > 0.78f && chroma < 0.18f) return true;
                if (lum > 0.55f && c.b > c.r + 0.02f && c.b > 0.45f) return true;
                if (lum > 0.72f && chroma < 0.08f) return true;
                return false;
            }

            FloodClear(px, w, h, IsSky, seedTop: true, seedBottom: false);
        }

        static void FloodClearGroundBarFromBottom(Color[] px, int w, int h)
        {
            // Flat dark/grey bar baked under many skyline plates (the "grey box").
            bool IsBar(Color c)
            {
                if (c.a < 0.05f) return true;
                var lum = c.r * 0.30f + c.g * 0.59f + c.b * 0.11f;
                var max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                var min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                var chroma = max - min;
                return lum < 0.38f && chroma < 0.10f;
            }

            FloodClear(px, w, h, IsBar, seedTop: false, seedBottom: true);
        }

        static void FloodClear(
            Color[] px, int w, int h,
            System.Func<Color, bool> match,
            bool seedTop,
            bool seedBottom)
        {
            var visit = new bool[w * h];
            var q = new Queue<int>();
            void TryEnq(int x, int y)
            {
                if ((uint)x >= w || (uint)y >= h) return;
                var i = y * w + x;
                if (visit[i] || !match(px[i])) return;
                visit[i] = true;
                q.Enqueue(i);
            }

            for (var x = 0; x < w; x++)
            {
                if (seedTop)
                {
                    TryEnq(x, h - 1);
                    TryEnq(x, h - 2);
                }

                if (seedBottom)
                {
                    TryEnq(x, 0);
                    TryEnq(x, 1);
                }
            }

            while (q.Count > 0)
            {
                var i = q.Dequeue();
                px[i] = Color.clear;
                var x = i % w;
                var y = i / w;
                TryEnq(x + 1, y);
                TryEnq(x - 1, y);
                TryEnq(x, y + 1);
                TryEnq(x, y - 1);
            }
        }

        /// <summary>
        /// Drop floating specks / tiny disconnected fragments above the skyline.
        /// </summary>
        static void RemoveSmallBlobs(Color[] px, int w, int h, int maxPixels)
        {
            var visit = new bool[w * h];
            var q = new Queue<int>();
            var blob = new List<int>(256);

            for (var i = 0; i < px.Length; i++)
            {
                if (visit[i] || px[i].a < 0.08f) continue;
                blob.Clear();
                q.Enqueue(i);
                visit[i] = true;
                while (q.Count > 0)
                {
                    var cur = q.Dequeue();
                    blob.Add(cur);
                    var x = cur % w;
                    var y = cur / w;
                    for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var nx = x + dx;
                        var ny = y + dy;
                        if ((uint)nx >= w || (uint)ny >= h) continue;
                        var ni = ny * w + nx;
                        if (visit[ni] || px[ni].a < 0.08f) continue;
                        visit[ni] = true;
                        q.Enqueue(ni);
                    }
                }

                if (blob.Count <= maxPixels)
                {
                    for (var b = 0; b < blob.Count; b++)
                        px[blob[b]] = Color.clear;
                }
            }
        }

        /// <summary>
        /// Fade plate L/R edges so tiled seams don't show cut-off half-buildings.
        /// </summary>
        static void FadeSeamEdges(Color[] px, int w, int h)
        {
            var fade = Mathf.Clamp(w / 12, 24, 96);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < fade; x++)
                {
                    var t = x / (float)fade;
                    var i = y * w + x;
                    var c = px[i];
                    c.a *= t * t;
                    if (c.a < 0.08f) c = Color.clear;
                    px[i] = c;
                }

                for (var x = 0; x < fade; x++)
                {
                    var t = x / (float)fade;
                    var i = y * w + (w - 1 - x);
                    var c = px[i];
                    c.a *= t * t;
                    if (c.a < 0.08f) c = Color.clear;
                    px[i] = c;
                }
            }
        }

        static Sprite BuildMidFallback()
        {
            const int w = 512;
            const int h = 80;
            var px = new Color[w * h];
            for (var i = 0; i < px.Length; i++) px[i] = Color.clear;
            var rng = new System.Random(11);
            for (var b = 0; b < 28; b++)
            {
                var bx = rng.Next(0, w - 40);
                var bw = rng.Next(18, 48);
                var bh = rng.Next(24, 72);
                var c = new Color(0.55f, 0.48f, 0.40f, 1f);
                for (var y = 0; y < bh; y++)
                for (var x = bx; x < bx + bw && x < w; x++)
                    px[y * w + x] = Color.Lerp(c, Color.black, y / (float)bh * 0.35f);
            }

            return FromPixels(px, w, h, "mid_fb");
        }

        static Sprite BuildGrassFallback()
        {
            const int w = 640;
            const int h = 96;
            var px = new Color[w * h];
            var soil = new Color(0.22f, 0.16f, 0.10f, 1f);
            var grassA = new Color(0.30f, 0.46f, 0.20f, 1f);
            var grassB = new Color(0.38f, 0.56f, 0.24f, 1f);
            var grassC = new Color(0.24f, 0.38f, 0.16f, 1f);
            for (var y = 0; y < h; y++)
            {
                var t = y / (float)Mathf.Max(1, h - 1);
                for (var x = 0; x < w; x++)
                {
                    var wave = 0.5f + 0.5f * Mathf.Sin(x * 0.07f + y * 0.2f);
                    var c = Color.Lerp(grassC, Color.Lerp(grassA, grassB, wave), t);
                    if (y < 3) c = Color.Lerp(soil, c, y / 3f);
                    if (y > h - 4 && ((x + y * 3) % 5 == 0))
                        c = grassB;
                    px[y * w + x] = c;
                }
            }

            return FromPixels(px, w, h, "grass_fb");
        }

        static Sprite BuildTreesFallback()
        {
            const int w = 640;
            const int h = 160;
            var px = new Color[w * h];
            for (var i = 0; i < px.Length; i++) px[i] = Color.clear;

            var leafDeep = new Color(0.10f, 0.22f, 0.09f, 1f);
            var leafMid = new Color(0.16f, 0.32f, 0.12f, 1f);
            var leafLit = new Color(0.26f, 0.44f, 0.18f, 1f);
            var trunkDk = new Color(0.20f, 0.12f, 0.07f, 1f);
            var trunkMd = new Color(0.32f, 0.20f, 0.11f, 1f);
            var rng = new System.Random(23);
            const int baseY = 4;

            void PlotDisk(int cx, int cy, int rx, int ry, Color col, float soft = 0.85f)
            {
                for (var y = cy - ry; y <= cy + ry; y++)
                for (var x = cx - rx; x <= cx + rx; x++)
                {
                    if ((uint)x >= w || (uint)y >= h) continue;
                    var nx = (x - cx) / (float)Mathf.Max(1, rx);
                    var ny = (y - cy) / (float)Mathf.Max(1, ry);
                    var d = nx * nx + ny * ny;
                    if (d > 1f) continue;
                    var i = y * w + x;
                    if (d > soft && px[i].a > 0.1f && px[i].r + px[i].g < 0.6f)
                        continue;
                    var shade = Mathf.Lerp(1.1f, 0.75f, d);
                    var put = new Color(col.r * shade, col.g * shade, col.b * shade, 1f);
                    if (px[i].a < 0.1f || put.g >= px[i].g)
                        px[i] = put;
                }
            }

            void DrawTree(int cx, int by, int height, int spread)
            {
                var trunkH = Mathf.Max(8, height / 3);
                var trunkW = Mathf.Max(2, spread / 10);
                for (var y = by; y < by + trunkH; y++)
                for (var x = cx - trunkW; x <= cx + trunkW; x++)
                {
                    if ((uint)x >= w || (uint)y >= h) continue;
                    px[y * w + x] = ((x + y) & 1) == 0 ? trunkMd : trunkDk;
                }

                var cy = by + trunkH + spread / 3;
                PlotDisk(cx, cy, spread, (int)(spread * 0.85f), leafDeep, 0.9f);
                PlotDisk(cx - spread / 3, cy + spread / 5, spread * 2 / 3, spread * 2 / 3, leafMid, 0.88f);
                PlotDisk(cx + spread / 3, cy + spread / 6, spread * 2 / 3, spread * 2 / 3, leafMid, 0.88f);
                PlotDisk(cx, cy + spread / 2, spread / 2, spread / 2, leafLit, 0.86f);
            }

            for (var i = 0; i < 14; i++)
                DrawTree(rng.Next(12, w - 12), baseY, rng.Next(h / 2, h - 6), rng.Next(18, 30));
            for (var i = 0; i < 18; i++)
                DrawTree(rng.Next(8, w - 8), baseY, rng.Next(h / 3, (int)(h * 0.65f)), rng.Next(12, 22));
            for (var i = 0; i < 30; i++)
            {
                var cx = rng.Next(4, w - 4);
                var ry = rng.Next(5, 11);
                var rx = rng.Next(7, 16);
                PlotDisk(cx, baseY + ry / 3, rx, ry, leafMid, 0.92f);
            }

            return FromPixels(px, w, h, "trees_fb");
        }

        static Sprite FromPixels(Color[] px, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                name = name
            };
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
        }

        public static void EnsureInScene()
        {
            if (FindAnyObjectByType<ParallaxBackdrop>() != null) return;
            new GameObject("ParallaxBackdrop").AddComponent<ParallaxBackdrop>();
        }
    }
}
