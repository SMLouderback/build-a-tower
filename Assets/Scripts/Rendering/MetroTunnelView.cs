using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Shared world-width subway strip at the lowest metro station floor, with a train
    /// looping along X. Spawned from <see cref="TowerSimulation"/> like <see cref="WeatherFx"/>.
    /// </summary>
    public sealed class MetroTunnelView : MonoBehaviour
    {
        public const string TunnelResourcePath = "Art/World/metro_tunnel";
        public const float TrainTilesPerSecond = 6f;
        public const float TrainCarTiles = 6f;
        /// <summary>Behind dollhouse rooms (5) so station plates cover the bore.</summary>
        public const int TunnelSortingOrder = 3;
        public const int TrainSortingOrder = 4;

        [SerializeField] TowerSimulation simulation;
        [SerializeField] BuildController build;

        SpriteRenderer _tunnel;
        SpriteRenderer _train;
        Sprite _tunnelSprite;
        Sprite _trainSprite;
        float _elapsed;

        public bool TunnelVisible => _tunnel != null && _tunnel.enabled;
        public bool TrainVisible => _train != null && _train.enabled;
        public float TunnelCenterX => _tunnel != null ? _tunnel.transform.position.x : 0f;
        public float TunnelCenterY => _tunnel != null ? _tunnel.transform.position.y : 0f;
        public float TunnelWidthTiles => _tunnel != null ? _tunnel.size.x : 0f;
        public float TrainCenterX => _train != null ? _train.transform.position.x : 0f;

        /// <summary>Bottom floor of the lowest station footprint (platform / tunnel level).</summary>
        public static bool TryTunnelFloorY(IReadOnlyList<RectInt> stations, out int floorY)
        {
            floorY = 0;
            if (stations == null || stations.Count == 0)
                return false;

            var y = stations[0].y;
            for (var i = 1; i < stations.Count; i++)
            {
                if (stations[i].y < y)
                    y = stations[i].y;
            }

            floorY = y;
            return true;
        }

        /// <summary>
        /// Inclusive cell span: the dirt-band world, widened if the grid extends past it.
        /// </summary>
        public static void WorldSpan(int gridMinX, int gridMaxX, out int minX, out int maxX)
        {
            minX = DirtBand.MinX;
            maxX = DirtBand.MaxX;
            if (gridMinX < minX)
                minX = gridMinX;
            if (gridMaxX > maxX)
                maxX = gridMaxX;
        }

        /// <summary>
        /// Train left edge relative to the tunnel's left. Loops across the span plus the
        /// car length so the car fully exits before it re-enters.
        /// </summary>
        public static float TrainOffsetX(float elapsedSeconds, float spanTiles, float tilesPerSecond, float carTiles)
        {
            var travel = Mathf.Max(0.01f, spanTiles + carTiles);
            var dist = Mathf.Repeat(Mathf.Max(0f, elapsedSeconds) * tilesPerSecond, travel);
            return dist - carTiles;
        }

        public static void EnsureInScene()
        {
            if (FindAnyObjectByType<MetroTunnelView>() != null)
                return;
            new GameObject("MetroTunnelView").AddComponent<MetroTunnelView>();
        }

        void LateUpdate()
        {
            if (simulation == null)
                simulation = FindAnyObjectByType<TowerSimulation>();
            if (build == null)
                build = FindAnyObjectByType<BuildController>();

            var metro = simulation != null ? simulation.Metro : null;
            var floorY = 0;
            var has = metro != null &&
                      metro.HasTunnel &&
                      TryTunnelFloorY(metro.Stations, out floorY);

            var grid = build != null ? build.Grid : null;
            WorldSpan(grid != null ? grid.MinX : 0, grid != null ? grid.MaxX : 0, out var minX, out var maxX);
            _elapsed += Time.deltaTime;
            Apply(has, floorY, minX, maxX, _elapsed);
        }

        public void Apply(bool hasTunnel, int floorY, int minX, int maxX, float elapsedSeconds)
        {
            EnsureRig();
            if (!hasTunnel || maxX < minX)
            {
                _tunnel.enabled = false;
                _train.enabled = false;
                return;
            }

            var width = maxX - minX + 1f;
            _tunnel.enabled = true;
            _tunnel.sprite = _tunnelSprite;
            _tunnel.drawMode = SpriteDrawMode.Tiled;
            _tunnel.size = new Vector2(width, 1f);
            _tunnel.transform.localScale = Vector3.one;
            _tunnel.transform.position = new Vector3(minX + width * 0.5f, floorY + 0.5f, 0f);

            var left = TrainOffsetX(elapsedSeconds, width, TrainTilesPerSecond, TrainCarTiles);
            var trainBounds = _trainSprite.bounds.size;
            var trainW = trainBounds.x > 0.01f ? trainBounds.x : 1f;
            var trainH = trainBounds.y > 0.01f ? trainBounds.y : 1f;
            _train.enabled = true;
            _train.sprite = _trainSprite;
            _train.drawMode = SpriteDrawMode.Simple;
            _train.transform.localScale = new Vector3(TrainCarTiles / trainW, 0.72f / trainH, 1f);
            _train.transform.position = new Vector3(
                minX + left + TrainCarTiles * 0.5f,
                floorY + 0.5f,
                0f);
        }

        void EnsureRig()
        {
            if (_tunnel != null && _train != null)
                return;

            _tunnelSprite ??= LoadTunnelSprite() ?? ProceduralTunnelSprite();
            _trainSprite ??= ProceduralTrainSprite();

            var tunnelGo = new GameObject("MetroTunnel");
            tunnelGo.transform.SetParent(transform, false);
            _tunnel = tunnelGo.AddComponent<SpriteRenderer>();
            _tunnel.sortingOrder = TunnelSortingOrder;
            _tunnel.sprite = _tunnelSprite;
            _tunnel.drawMode = SpriteDrawMode.Tiled;
            _tunnel.enabled = false;

            var trainGo = new GameObject("MetroTrain");
            trainGo.transform.SetParent(transform, false);
            _train = trainGo.AddComponent<SpriteRenderer>();
            _train.sortingOrder = TrainSortingOrder;
            _train.sprite = _trainSprite;
            _train.enabled = false;
        }

        static Sprite LoadTunnelSprite()
        {
            var tex = LoadTunnelTexture();
            if (tex == null)
                return null;

            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            var ppu = Mathf.Max(1f, tex.height);
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                ppu,
                0,
                SpriteMeshType.FullRect);
        }

        static Texture2D LoadTunnelTexture()
        {
            var bytesAsset = Resources.Load<TextAsset>(TunnelResourcePath);
            byte[] png = bytesAsset != null ? bytesAsset.bytes : null;
            if (png == null || png.Length < 32)
            {
                var src = Resources.Load<Texture2D>(TunnelResourcePath);
                if (src == null)
                    return null;
                try
                {
                    png = src.EncodeToPNG();
                }
                catch (UnityException)
                {
                    return src;
                }
            }

            if (png == null || png.Length < 32)
                return null;

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                name = TunnelResourcePath
            };
            if (!decoded.LoadImage(png, false))
            {
                DestroyObject(decoded);
                return null;
            }

            var keyed = RoomDollhouseArt.KeyCropMagentaPlate(decoded, destroySource: false);
            DestroyObject(decoded);
            return keyed;
        }

        static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        static Sprite ProceduralTunnelSprite()
        {
            const int w = 64;
            const int h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                name = "metro_tunnel_fallback"
            };
            var px = new Color[w * h];
            var concrete = new Color(0.45f, 0.43f, 0.40f, 1f);
            var stripe = new Color(0.82f, 0.75f, 0.62f, 1f);
            var rail = new Color(0.18f, 0.17f, 0.16f, 1f);
            var lamp = new Color(0.95f, 0.72f, 0.35f, 1f);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var c = concrete;
                if (y >= 14 && y <= 16)
                    c = stripe;
                if (y <= 6)
                    c = rail;
                if (y >= 20 && y <= 23 && (x % 16) >= 6 && (x % 16) <= 9)
                    c = lamp;
                px[y * w + x] = c;
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(
                tex,
                new Rect(0, 0, w, h),
                new Vector2(0.5f, 0.5f),
                h,
                0,
                SpriteMeshType.FullRect);
        }

        static Sprite ProceduralTrainSprite()
        {
            const int w = 96;
            const int h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "metro_train"
            };
            var px = new Color[w * h];
            var body = new Color(0.73f, 0.75f, 0.77f, 1f);
            var stripe = new Color(0.86f, 0.46f, 0.18f, 1f);
            var window = new Color(0.96f, 0.84f, 0.48f, 1f);
            var dark = new Color(0.12f, 0.13f, 0.14f, 1f);
            for (var i = 0; i < px.Length; i++)
                px[i] = Color.clear;

            for (var y = 6; y < 26; y++)
            for (var x = 4; x < w - 4; x++)
                px[y * w + x] = body;

            for (var y = 14; y <= 16; y++)
            for (var x = 4; x < w - 4; x++)
                px[y * w + x] = stripe;

            for (var x = 10; x < w - 10; x++)
            {
                var slot = (x - 10) % 14;
                if (slot >= 8)
                    continue;
                for (var y = 18; y <= 23; y++)
                    px[y * w + x] = window;
            }

            PaintDisc(px, w, h, 18, 6, 5, dark);
            PaintDisc(px, w, h, w - 18, 6, 5, dark);
            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(
                tex,
                new Rect(0, 0, w, h),
                new Vector2(0.5f, 0.5f),
                h,
                0,
                SpriteMeshType.FullRect);
        }

        static void PaintDisc(Color[] px, int w, int h, int cx, int cy, int r, Color color)
        {
            var r2 = r * r;
            for (var y = cy - r; y <= cy + r; y++)
            for (var x = cx - r; x <= cx + r; x++)
            {
                if ((uint)x >= w || (uint)y >= h)
                    continue;
                var dx = x - cx;
                var dy = y - cy;
                if (dx * dx + dy * dy <= r2)
                    px[y * w + x] = color;
            }
        }
    }
}
