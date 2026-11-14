using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Soft parallax cloud bank in the sky. Coverage follows weather; tint follows
    /// <see cref="SkyCloudStyle"/> (puffy white / storm grey + sunrise–sunset warmth).
    /// </summary>
    public sealed class SkyCloudLayer : MonoBehaviour
    {
        public const string CloudResourcePath = "Art/Parallax/sky_cloud_puff";
        public const int SortingOrder = -220;
        public const int CloudCount = 7;
        public const float DriftTilesPerGameMinute = 0.045f;

        [SerializeField] Camera targetCamera;
        [SerializeField] TowerSimulation simulation;

        Transform _rig;
        SpriteRenderer[] _clouds;
        float[] _normX;
        float[] _normY;
        float[] _scale;
        float[] _phase;
        Sprite _sprite;
        float _drift;

        public bool HasRig => _rig != null;
        public int ActiveCloudCount
        {
            get
            {
                if (_clouds == null) return 0;
                var n = 0;
                for (var i = 0; i < _clouds.Length; i++)
                {
                    if (_clouds[i] != null && _clouds[i].enabled && _clouds[i].color.a > 0.01f)
                        n++;
                }

                return n;
            }
        }

        public static void EnsureInScene()
        {
            if (FindAnyObjectByType<SkyCloudLayer>() != null) return;
            new GameObject("SkyCloudLayer").AddComponent<SkyCloudLayer>();
        }

        void LateUpdate()
        {
            BindCamera();
            if (targetCamera == null) return;
            BuildRig();

            if (simulation == null)
                simulation = FindAnyObjectByType<TowerSimulation>();

            var clock = simulation != null ? simulation.Clock : null;
            var kind = simulation != null && simulation.Weather != null
                ? simulation.Weather.Kind
                : WeatherKind.Clear;
            var deltaGameMinutes = clock != null ? clock.LastTickGameMinutes : 0f;
            Apply(clock, kind, deltaGameMinutes);
        }

        public void Apply(GameClock clock, WeatherKind kind, float deltaGameMinutes)
        {
            BindCamera();
            if (targetCamera == null) return;
            BuildRig();

            var coverage = SkyCloudStyle.Coverage(kind);
            var opacity = SkyCloudStyle.Opacity(kind);
            var tint = SkyCloudStyle.ColorAt(clock, kind);
            _drift += Mathf.Max(0f, deltaGameMinutes) * DriftTilesPerGameMinute;

            var halfH = targetCamera.orthographicSize;
            var halfW = halfH * targetCamera.aspect;
            var cam = targetCamera.transform.position;
            // Sit in the upper sky, slightly behind parallax city (more negative sorting).
            var bandY = cam.y + halfH * 0.42f;
            var bandH = halfH * 0.55f;

            var show = coverage > 0.01f && opacity > 0.01f;
            for (var i = 0; i < _clouds.Length; i++)
            {
                var sr = _clouds[i];
                if (!show || i >= Mathf.CeilToInt(CloudCount * Mathf.Clamp01(coverage + 0.15f)))
                {
                    sr.enabled = false;
                    continue;
                }

                var x = cam.x + Mathf.Repeat(_normX[i] * halfW * 2.4f + _drift * (0.7f + _phase[i]), halfW * 2.4f)
                        - halfW * 1.2f;
                var y = bandY + (_normY[i] - 0.5f) * bandH;
                sr.enabled = true;
                sr.sprite = _sprite;
                sr.color = new Color(tint.r, tint.g, tint.b, opacity * (0.75f + 0.25f * _phase[i]));
                var bounds = _sprite.bounds.size;
                var targetW = halfW * (0.55f + 0.35f * _scale[i]);
                var sx = bounds.x > 0.01f ? targetW / bounds.x : 1f;
                // Slightly taller than a flat slab so feathered puffs read round, not squared.
                var sy = bounds.y > 0.01f ? (targetW * 0.40f) / bounds.y : 1f;
                sr.transform.position = new Vector3(x, y, 0f);
                sr.transform.localScale = new Vector3(sx, sy, 1f);
            }
        }

        void BindCamera()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        void BuildRig()
        {
            if (_rig != null) return;

            _sprite = LoadCloudSprite() ?? BuildProceduralCloudSprite();

            var go = new GameObject("SkyCloudRig");
            go.transform.SetParent(transform, false);
            _rig = go.transform;

            _clouds = new SpriteRenderer[CloudCount];
            _normX = new float[CloudCount];
            _normY = new float[CloudCount];
            _scale = new float[CloudCount];
            _phase = new float[CloudCount];

            for (var i = 0; i < CloudCount; i++)
            {
                var child = new GameObject($"Cloud_{i}");
                child.transform.SetParent(_rig, false);
                var sr = child.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortingOrder + (i % 3);
                sr.sprite = _sprite;
                sr.enabled = false;
                _clouds[i] = sr;
                _normX[i] = (i * 0.37f + 0.11f) % 1f;
                _normY[i] = 0.25f + (i % 5) * 0.12f;
                _scale[i] = 0.7f + (i % 4) * 0.12f;
                _phase[i] = 0.35f + (i % 3) * 0.2f;
            }
        }

        static Sprite LoadCloudSprite()
        {
            var bytesAsset = Resources.Load<TextAsset>(CloudResourcePath);
            byte[] png = bytesAsset != null ? bytesAsset.bytes : null;
            if (png == null || png.Length < 32)
            {
                var src = Resources.Load<Texture2D>(CloudResourcePath);
                if (src == null) return null;
                try { png = src.EncodeToPNG(); }
                catch (UnityException) { return SpriteFromTexture(src); }
            }

            if (png == null || png.Length < 32) return null;

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = CloudResourcePath
            };
            if (!decoded.LoadImage(png, false))
            {
                if (Application.isPlaying) Object.Destroy(decoded);
                else Object.DestroyImmediate(decoded);
                return null;
            }

            // Soft feather after a gentle key — hard magenta plates + morphological erode
            // were reading as squared-off cloud silhouettes on sky.
            var keyed = KeyCloudMagentaPlate(decoded, destroySource: true);
            keyed = FeatherCloudAlpha(keyed, radius: 7);
            return SpriteFromTexture(keyed);
        }

        /// <summary>
        /// Key hot magenta plate only (no near-magenta scrub / tight crop). Cloud art is
        /// bright white; aggressive fringe passes chew soft edges into blocky steps.
        /// </summary>
        public static Texture2D KeyCloudMagentaPlate(Texture2D source, bool destroySource)
        {
            if (source == null) return null;

            Color[] px;
            try
            {
                px = source.GetPixels();
            }
            catch (UnityException)
            {
                return source;
            }

            var w = source.width;
            var h = source.height;
            for (var i = 0; i < px.Length; i++)
            {
                if (MenuIconArt.IsHotMagenta(px[i]))
                    px[i] = Color.clear;
            }

            FindOpaqueBounds(px, w, h, out var minX, out var minY, out var maxX, out var maxY);
            if (maxX < minX || maxY < minY)
            {
                if (destroySource)
                {
                    if (Application.isPlaying) Object.Destroy(source);
                    else Object.DestroyImmediate(source);
                }

                return null;
            }

            // Pad so the later alpha feather has room without clipping the silhouette.
            const int pad = 10;
            minX = Mathf.Max(0, minX - pad);
            minY = Mathf.Max(0, minY - pad);
            maxX = Mathf.Min(w - 1, maxX + pad);
            maxY = Mathf.Min(h - 1, maxY + pad);
            var cw = maxX - minX + 1;
            var ch = maxY - minY + 1;
            var cropped = new Color[cw * ch];
            for (var y = 0; y < ch; y++)
            for (var x = 0; x < cw; x++)
                cropped[y * cw + x] = px[(minY + y) * w + (minX + x)];

            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = source.name
            };
            tex.SetPixels(cropped);
            tex.Apply(false, false);
            if (destroySource)
            {
                if (Application.isPlaying) Object.Destroy(source);
                else Object.DestroyImmediate(source);
            }

            return tex;
        }

        /// <summary>
        /// Soften hard keyed silhouettes: alpha falls off with distance to transparent sky.
        /// </summary>
        public static Texture2D FeatherCloudAlpha(Texture2D source, int radius)
        {
            if (source == null || radius <= 0)
                return source;

            var w = source.width;
            var h = source.height;
            var px = source.GetPixels();
            var alpha = new float[px.Length];
            for (var i = 0; i < px.Length; i++)
                alpha[i] = px[i].a;

            // Separable box blur on alpha (2 passes ≈ soft Gaussian) so stair-step
            // magenta masks blend into the sky instead of reading as squares.
            var tmp = new float[px.Length];
            var r = Mathf.Clamp(radius, 1, 24);
            BlurAlpha1D(alpha, tmp, w, h, r, horizontal: true);
            BlurAlpha1D(tmp, alpha, w, h, r, horizontal: false);
            BlurAlpha1D(alpha, tmp, w, h, r, horizontal: true);
            BlurAlpha1D(tmp, alpha, w, h, r, horizontal: false);

            for (var i = 0; i < px.Length; i++)
            {
                var a = Mathf.Clamp01(alpha[i]);
                if (a < 0.02f)
                {
                    px[i] = Color.clear;
                    continue;
                }

                // Mild ease keeps puffs round. Exterior bleed had clear RGB — tint white
                // so feathered rims stay cloud-colored on blue sky.
                var eased = a * a * (3f - 2f * a);
                if (px[i].a < 0.08f)
                    px[i] = new Color(1f, 1f, 1f, eased);
                else
                    px[i].a = eased;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = source.name
            };
            tex.SetPixels(px);
            tex.Apply(false, false);
            if (Application.isPlaying) Object.Destroy(source);
            else Object.DestroyImmediate(source);
            return tex;
        }

        static void BlurAlpha1D(float[] src, float[] dst, int w, int h, int radius, bool horizontal)
        {
            var span = radius * 2 + 1;
            if (horizontal)
            {
                for (var y = 0; y < h; y++)
                {
                    var row = y * w;
                    var sum = 0f;
                    for (var x = -radius; x <= radius; x++)
                        sum += src[row + Mathf.Clamp(x, 0, w - 1)];
                    for (var x = 0; x < w; x++)
                    {
                        dst[row + x] = sum / span;
                        var leave = src[row + Mathf.Clamp(x - radius, 0, w - 1)];
                        var enter = src[row + Mathf.Clamp(x + radius + 1, 0, w - 1)];
                        sum += enter - leave;
                    }
                }
            }
            else
            {
                for (var x = 0; x < w; x++)
                {
                    var sum = 0f;
                    for (var y = -radius; y <= radius; y++)
                        sum += src[Mathf.Clamp(y, 0, h - 1) * w + x];
                    for (var y = 0; y < h; y++)
                    {
                        dst[y * w + x] = sum / span;
                        var leave = src[Mathf.Clamp(y - radius, 0, h - 1) * w + x];
                        var enter = src[Mathf.Clamp(y + radius + 1, 0, h - 1) * w + x];
                        sum += enter - leave;
                    }
                }
            }
        }

        static void FindOpaqueBounds(
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
        }

        static Sprite SpriteFromTexture(Texture2D tex)
        {
            if (tex == null) return null;
            tex.filterMode = FilterMode.Bilinear;
            var ppu = Mathf.Max(1f, tex.height * 0.45f);
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                ppu,
                0,
                SpriteMeshType.FullRect);
        }

        static Sprite BuildProceduralCloudSprite()
        {
            const int w = 128;
            const int h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "sky_cloud_fallback"
            };
            var px = new Color[w * h];
            for (var i = 0; i < px.Length; i++)
                px[i] = Color.clear;

            StampBlob(px, w, h, 40, 28, 22, 16);
            StampBlob(px, w, h, 62, 30, 26, 18);
            StampBlob(px, w, h, 88, 26, 24, 15);
            StampBlob(px, w, h, 55, 18, 18, 12);
            tex.SetPixels(px);
            tex.Apply(false, false);
            return SpriteFromTexture(tex);
        }

        static void StampBlob(Color[] px, int w, int h, int cx, int cy, int rx, int ry)
        {
            for (var y = cy - ry; y <= cy + ry; y++)
            for (var x = cx - rx; x <= cx + rx; x++)
            {
                if ((uint)x >= w || (uint)y >= h) continue;
                var u = (x - cx) / (float)rx;
                var v = (y - cy) / (float)ry;
                var d = u * u + v * v;
                if (d > 1f) continue;
                var a = Mathf.Clamp01(1f - d);
                a = a * a;
                var i = y * w + x;
                px[i] = Color.Lerp(px[i], Color.white, a);
                px[i].a = Mathf.Max(px[i].a, a);
            }
        }
    }
}
