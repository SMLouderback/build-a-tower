using UnityEngine;
using UnityEngine.Tilemaps;

namespace BuildATower
{
    /// <summary>
    /// Structure cutaway art: prefers hand-painted AI bytes under Resources (decoded via
    /// LoadImage → SetPixels, same path as procedural rooms — no pink import), with a rich
    /// procedural fallback. Lobby tiles share locked floor / crown / ceiling / edge bands.
    /// Elevator tiles are fully opaque.
    /// </summary>
    public static class StructureCutawayArt
    {
        public const string ResourcesRoot = "Art/Structure/";
        public const int CellPixels = 128;
        public const int StairsPixels = 256;
        public const int LobbyMidCount = 6;
        public const int LobbyPanCount = 5;
        public const int LobbyPanCells = 5;
        public const int LobbyPanWidthPixels = CellPixels * LobbyPanCells; // 640

        static readonly Color Wall = new(0.878f, 0.820f, 0.722f, 1f);
        // Prompt-locked structural bands (must match across all lobby mids).
        static readonly Color Crown = new(0.94f, 0.94f, 0.93f, 1f);      // white marble
        static readonly Color CrownHi = new(1.00f, 1.00f, 1.00f, 1f);
        static readonly Color CrownLo = new(0.82f, 0.82f, 0.80f, 1f);
        static readonly Color Floor = new(0.28f, 0.18f, 0.10f, 1f);      // dark walnut
        static readonly Color FloorHi = new(0.38f, 0.26f, 0.14f, 1f);
        static readonly Color FloorLo = new(0.20f, 0.12f, 0.07f, 1f);
        static readonly Color FloorBrass = new(0.72f, 0.58f, 0.28f, 1f);

        static readonly Color Shaft = new(0.145f, 0.155f, 0.180f, 1f);
        static readonly Color ShaftHi = new(0.185f, 0.195f, 0.220f, 1f);
        static readonly Color Rail = new(0.670f, 0.570f, 0.375f, 1f);
        static readonly Color RailDk = new(0.510f, 0.430f, 0.295f, 1f);
        static readonly Color Cable = new(0.360f, 0.360f, 0.385f, 1f);
        static readonly Color Machine = new(0.220f, 0.205f, 0.185f, 1f);
        static readonly Color Gear = new(0.590f, 0.510f, 0.355f, 1f);
        static readonly Color GearDk = new(0.420f, 0.360f, 0.250f, 1f);
        static readonly Color Pit = new(0.080f, 0.080f, 0.100f, 1f);
        static readonly Color Spring = new(0.630f, 0.590f, 0.510f, 1f);

        const int Edge = 18;
        // Prompt anchors: top 10% crown, bottom 15% floor (of CellPixels).
        const int CrownH = 13;  // ~10% of 128
        const int FloorH = 19;  // ~15% of 128
        const int BaseH = 0;    // floor band includes base look
        const int CeilH = 0;    // crown is the top band
        const int EdgeBlend = 6; // fade center art into shared edge columns

        static bool _attempted;
        static Tile[] _lobbyMids;
        static Tile[][] _lobbyPanTiles;
        static Tile _elevatorTop;
        static Tile _elevatorMid;
        static Tile _elevatorBottom;
        static Sprite _stairsSprite;
        static Color[] _lobbyShell;
        static int _stairsStarTier = -1;

        /// <summary>
        /// Map tower stars to stairs art: 0–1 basic, 2–3 mid, 4–5 luxury.
        /// Call when <see cref="StarSystem.CurrentStars"/> changes so overlays refresh.
        /// </summary>
        public static bool SetStarRating(int stars)
        {
            var tier = StarTierIndex(stars);
            if (_attempted && tier == _stairsStarTier && _stairsSprite != null)
                return false;
            _stairsStarTier = tier;
            if (_attempted)
                _stairsSprite = LoadOrBuildStairs();
            return true;
        }

        static int StarTierIndex(int stars)
        {
            if (stars >= 4) return 5;
            if (stars >= 2) return 3;
            return 1;
        }

        static string StairsResourceForTier(int tier) =>
            tier >= 5 ? "stairs_star_05" :
            tier >= 3 ? "stairs_star_03" :
            "stairs_star_01";

        public static int FloorDiv(int a, int b)
        {
            var q = a / b;
            var r = a % b;
            if (r != 0 && (r > 0) != (b > 0)) q--;
            return q;
        }

        public static int PositiveMod(int a, int b)
        {
            var m = a % b;
            return m < 0 ? m + b : m;
        }

        public static int LobbyPanIndex(int cellX) =>
            PositiveMod(FloorDiv(cellX, LobbyPanCells), LobbyPanCount);

        public static int LobbySliceIndex(int cellX) =>
            PositiveMod(cellX, LobbyPanCells);

        /// <summary>Legacy mid hashing — used only when panorama assets are missing.</summary>
        public static int LobbyVariantIndex(int cellX)
        {
            // One variant per cell (shared crown/floor keep continuity). Reconstruct
            // a short left-neighbor chain so hash collisions never stamp duplicates.
            const int window = 8;
            var prev = HashLobbyVariant(cellX - window);
            for (var x = cellX - window + 1; x <= cellX; x++)
            {
                var cur = HashLobbyVariant(x);
                if (cur == prev)
                    cur = (cur + 1) % LobbyMidCount;
                prev = cur;
            }

            return prev;
        }

        static int HashLobbyVariant(int cellX)
        {
            unchecked
            {
                var h = (uint)(cellX * 73856093);
                h ^= h >> 13;
                h *= 1274126177u;
                return (int)(h % LobbyMidCount);
            }
        }

        public static bool TryLobbyTile(int cellX, out Tile tile)
        {
            EnsureLoaded();
            tile = null;
            if (_lobbyPanTiles != null)
            {
                var p = LobbyPanIndex(cellX);
                var s = LobbySliceIndex(cellX);
                if ((uint)p < (uint)_lobbyPanTiles.Length &&
                    _lobbyPanTiles[p] != null &&
                    (uint)s < (uint)_lobbyPanTiles[p].Length &&
                    _lobbyPanTiles[p][s] != null)
                {
                    tile = _lobbyPanTiles[p][s];
                    return true;
                }
            }

            if (_lobbyMids == null || _lobbyMids.Length == 0) return false;
            var i = LobbyVariantIndex(cellX);
            if (i < 0 || i >= _lobbyMids.Length || _lobbyMids[i] == null) return false;
            tile = _lobbyMids[i];
            return true;
        }

        public static bool TryElevatorTile(int cellY, int minY, int maxY, out Tile tile)
        {
            EnsureLoaded();
            tile = null;
            if (cellY == maxY)
            {
                tile = _elevatorTop;
                return tile != null;
            }

            if (cellY == minY)
            {
                tile = _elevatorBottom;
                return tile != null;
            }

            tile = _elevatorMid;
            return tile != null;
        }

        public static bool TryStairsSprite(out Sprite sprite)
        {
            EnsureLoaded();
            sprite = _stairsSprite;
            return sprite != null;
        }

        public static void ResetCache()
        {
            _attempted = false;
            _lobbyMids = null;
            _lobbyPanTiles = null;
            _elevatorTop = null;
            _elevatorMid = null;
            _elevatorBottom = null;
            _stairsSprite = null;
            _lobbyShell = null;
            _stairsStarTier = -1;
        }

        static void EnsureLoaded()
        {
            if (_attempted) return;
            _attempted = true;

            if (_stairsStarTier < 0)
                _stairsStarTier = 1;

            _lobbyShell = null;
            _lobbyPanTiles = new Tile[LobbyPanCount][];
            var anyPan = false;
            Color[] firstPanPx = null;
            for (var p = 0; p < LobbyPanCount; p++)
            {
                var name = $"lobby_pan_{p + 1:00}";
                var panPx = TryLoadLobbyPanPixels(name);
                if (panPx == null)
                {
                    _lobbyPanTiles[p] = null;
                    continue;
                }

                anyPan = true;
                firstPanPx ??= panPx;
                if (_lobbyShell == null)
                {
                    _lobbyShell = ExtractCellFromPan(panPx, 0);
                    FillLobbyWhiteEdgeBars(_lobbyShell);
                }

                _lobbyPanTiles[p] = new Tile[LobbyPanCells];
                for (var s = 0; s < LobbyPanCells; s++)
                {
                    var cell = ExtractCellFromPan(panPx, s);
                    // Panoramas are already continuous — do NOT LockLobbyStructure.
                    // That stamps shared L/R edge columns (pillars) onto every cell
                    // and clips paintings / windows / furniture mid-image.
                    FillLobbyWhiteEdgeBars(cell);
                    ForceOpaque(cell);
                    _lobbyPanTiles[p][s] = MakeTile($"{name}_s{s}", cell, FilterMode.Bilinear);
                }
            }

            // Fill any missing pan slots from the first successful pan so segment
            // indices never paint empty when other pans loaded.
            if (anyPan && firstPanPx != null)
            {
                for (var p = 0; p < LobbyPanCount; p++)
                {
                    if (_lobbyPanTiles[p] != null) continue;
                    _lobbyPanTiles[p] = new Tile[LobbyPanCells];
                    for (var s = 0; s < LobbyPanCells; s++)
                    {
                        var cell = ExtractCellFromPan(firstPanPx, s);
                        FillLobbyWhiteEdgeBars(cell);
                        ForceOpaque(cell);
                        _lobbyPanTiles[p][s] = MakeTile($"lobby_pan_fill_s{s}", cell, FilterMode.Bilinear);
                    }
                }
            }

            _lobbyMids = null;
            if (!anyPan)
            {
                _lobbyMids = new Tile[LobbyMidCount];
                for (var i = 0; i < LobbyMidCount; i++)
                {
                    var name = $"lobby_mid_{i + 1:00}";
                    var px = TryLoadLobbyCellPixels(name);
                    if (px == null)
                    {
                        if (_lobbyShell == null)
                        {
                            _lobbyShell = new Color[CellPixels * CellPixels];
                            PaintLobbyShell(_lobbyShell);
                        }

                        px = (Color[])_lobbyShell.Clone();
                        PaintLobbyCenterFallback(px, i);
                    }
                    else if (_lobbyShell == null)
                    {
                        FillLobbyWhiteEdgeBars(px);
                        _lobbyShell = (Color[])px.Clone();
                    }

                    LockLobbyStructure(px);
                    FillLobbyWhiteEdgeBars(px);
                    ForceOpaque(px);
                    _lobbyMids[i] = MakeTile(name, px, FilterMode.Bilinear);
                }
            }

            if (_lobbyShell == null)
            {
                _lobbyShell = new Color[CellPixels * CellPixels];
                PaintLobbyShell(_lobbyShell);
            }

            _elevatorMid = MakeElevatorTile("elevator_mid", PaintElevatorMid, true);
            _elevatorTop = MakeElevatorTile("elevator_top", PaintElevatorTop, true);
            _elevatorBottom = MakeElevatorTile("elevator_bottom", PaintElevatorBottom, true);
            _stairsSprite = LoadOrBuildStairs();
        }

        static Tile MakeElevatorTile(string name, System.Action<Color[]> fallback, bool forceOpaque)
        {
            var solid = new Color[CellPixels * CellPixels];
            fallback(solid); // always start from opaque procedural shaft

            var ai = TryLoadCellPixels(name);
            if (ai != null)
                CompositeElevatorAi(solid, ai);

            if (forceOpaque)
                ForceOpaque(solid);
            return MakeTile(name, solid, FilterMode.Bilinear);
        }

        /// <summary>
        /// Keep solid dark shaft everywhere; only stamp AI detail where the AI pixel is
        /// dark enough (prevents light/sky bleed that reads as translucency).
        /// </summary>
        static void CompositeElevatorAi(Color[] solid, Color[] ai)
        {
            for (var i = 0; i < solid.Length; i++)
            {
                var c = ai[i];
                var lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                if (lum > 0.42f) continue; // too light — keep solid shaft
                // Prefer AI detail but never lighter than the shaft base.
                var baseLum = solid[i].r * 0.3f + solid[i].g * 0.59f + solid[i].b * 0.11f;
                if (lum > baseLum + 0.08f) continue;
                c.a = 1f;
                solid[i] = c;
            }
        }

        static Color[] TryLoadCellPixels(string fileName)
        {
            // Prefer .bytes TextAsset (raw PNG) — avoids sprite-importer color quirks.
            var bytesAsset = Resources.Load<TextAsset>(ResourcesRoot + fileName);
            byte[] png = bytesAsset != null ? bytesAsset.bytes : null;

            if (png == null || png.Length < 32)
            {
                var tex = Resources.Load<Texture2D>(ResourcesRoot + fileName);
                if (tex == null) return null;
                try
                {
                    png = tex.EncodeToPNG();
                }
                catch (UnityException)
                {
                    return null;
                }
            }

            if (png == null || png.Length < 32) return null;

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!decoded.LoadImage(png, false))
                return null;

            var resized = ResizeToCell(decoded);
            Object.Destroy(decoded);
            return resized;
        }

        static Color[] TryLoadLobbyCellPixels(string fileName)
        {
            var bytesAsset = Resources.Load<TextAsset>(ResourcesRoot + fileName);
            byte[] png = bytesAsset != null ? bytesAsset.bytes : null;

            if (png == null || png.Length < 32)
            {
                var tex = Resources.Load<Texture2D>(ResourcesRoot + fileName);
                if (tex == null) return null;
                try
                {
                    png = tex.EncodeToPNG();
                }
                catch (UnityException)
                {
                    return null;
                }
            }

            if (png == null || png.Length < 32) return null;

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!decoded.LoadImage(png, false))
                return null;

            var resized = ResizeLobbyToCell(decoded);
            Object.Destroy(decoded);
            return resized;
        }

        static Color[] TryLoadLobbyPanPixels(string fileName)
        {
            var bytesAsset = Resources.Load<TextAsset>(ResourcesRoot + fileName);
            byte[] png = bytesAsset != null ? bytesAsset.bytes : null;

            if (png == null || png.Length < 32)
            {
                var tex = Resources.Load<Texture2D>(ResourcesRoot + fileName);
                if (tex == null) return null;
                try
                {
                    png = tex.EncodeToPNG();
                }
                catch (UnityException)
                {
                    return null;
                }
            }

            if (png == null || png.Length < 32) return null;

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!decoded.LoadImage(png, false))
                return null;

            var resized = ResizeLobbyToPan(decoded);
            Object.Destroy(decoded);
            return resized;
        }

        static Color[] ExtractCellFromPan(Color[] panPx, int slice)
        {
            var cell = new Color[CellPixels * CellPixels];
            var x0 = Mathf.Clamp(slice, 0, LobbyPanCells - 1) * CellPixels;
            for (var y = 0; y < CellPixels; y++)
            for (var x = 0; x < CellPixels; x++)
                cell[y * CellPixels + x] = panPx[y * LobbyPanWidthPixels + x0 + x];
            return cell;
        }

        static Color[] ResizeToCell(Texture2D src)
        {
            var srcPx = src.GetPixels();
            var sw = src.width;
            var sh = src.height;
            FindContentRect(srcPx, sw, sh, out var minX, out var minY, out var maxX, out var maxY);
            return SampleToCell(src, sw, sh, minX, minY, maxX, maxY);
        }

        static Color[] ResizeLobbyToCell(Texture2D src)
        {
            var srcPx = src.GetPixels();
            var sw = src.width;
            var sh = src.height;
            FindLobbyContentRect(srcPx, sw, sh, out var minX, out var minY, out var maxX, out var maxY);
            return SampleToCell(src, sw, sh, minX, minY, maxX, maxY);
        }

        static Color[] ResizeLobbyToPan(Texture2D src)
        {
            var srcPx = src.GetPixels();
            var sw = src.width;
            var sh = src.height;
            FindLobbyContentRect(srcPx, sw, sh, out var minX, out var minY, out var maxX, out var maxY);
            var cw = Mathf.Max(1, maxX - minX + 1);
            var ch = Mathf.Max(1, maxY - minY + 1);
            var px = new Color[LobbyPanWidthPixels * CellPixels];
            for (var y = 0; y < CellPixels; y++)
            for (var x = 0; x < LobbyPanWidthPixels; x++)
            {
                var c = src.GetPixelBilinear(
                    (minX + (x + 0.5f) / LobbyPanWidthPixels * cw) / sw,
                    (minY + (y + 0.5f) / CellPixels * ch) / sh);
                if (c.r > 0.55f && c.b > 0.55f && c.g < c.r - 0.12f && c.g < c.b - 0.08f)
                    c = Wall;
                c.a = 1f;
                px[y * LobbyPanWidthPixels + x] = c;
            }

            return px;
        }

        static Color[] SampleToCell(
            Texture2D src, int sw, int sh, int minX, int minY, int maxX, int maxY)
        {
            var cw = Mathf.Max(1, maxX - minX + 1);
            var ch = Mathf.Max(1, maxY - minY + 1);

            var px = new Color[CellPixels * CellPixels];
            for (var y = 0; y < CellPixels; y++)
            for (var x = 0; x < CellPixels; x++)
            {
                var c = src.GetPixelBilinear(
                    (minX + (x + 0.5f) / CellPixels * cw) / sw,
                    (minY + (y + 0.5f) / CellPixels * ch) / sh);
                if (c.r > 0.55f && c.b > 0.55f && c.g < c.r - 0.12f && c.g < c.b - 0.08f)
                    c = Wall;
                c.a = 1f;
                px[y * CellPixels + x] = c;
            }

            return px;
        }

        /// <summary>
        /// Drop solid black/white generator plates so crown/floor aren't empty padding.
        /// </summary>
        static void FindContentRect(
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
                var c = px[y * w + x];
                var lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                if (c.a < 0.08f) continue;
                if (lum < 0.06f) continue; // black plate
                // Generator plates / white edge bars (lobby assets often have these).
                if (lum > 0.88f && Mathf.Abs(c.r - c.g) < 0.05f && Mathf.Abs(c.g - c.b) < 0.05f)
                    continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            if (maxX < minX)
            {
                minX = 0;
                minY = 0;
                maxX = w - 1;
                maxY = h - 1;
            }
        }

        /// <summary>
        /// Lobby PNGs bake thick white (and a 1px grey) plate bands. Drop whole
        /// plate rows from the edges, then tight content bounds.
        /// </summary>
        static void FindLobbyContentRect(
            Color[] px, int w, int h,
            out int minX, out int minY, out int maxX, out int maxY)
        {
            bool RowIsPlate(int y)
            {
                var plate = 0;
                for (var x = 0; x < w; x++)
                {
                    var c = px[y * w + x];
                    var lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    var chroma = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) -
                                 Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    if (c.a < 0.08f) { plate++; continue; }
                    if (chroma > 0.06f) continue;
                    // White / near-white plate, flat mid-grey strip, or black letterbox.
                    if (lum > 0.86f || lum < 0.08f || (lum > 0.48f && lum < 0.62f))
                        plate++;
                }

                return plate > w * 0.80f;
            }

            var y0 = 0;
            var y1 = h - 1;
            while (y0 < y1 && RowIsPlate(y0)) y0++;
            while (y1 > y0 && RowIsPlate(y1)) y1--;

            minX = w;
            minY = y1 + 1;
            maxX = -1;
            maxY = y0 - 1;
            for (var y = y0; y <= y1; y++)
            for (var x = 0; x < w; x++)
            {
                var c = px[y * w + x];
                var lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                if (c.a < 0.08f) continue;
                if (lum < 0.06f) continue;
                if (lum > 0.92f && Mathf.Abs(c.r - c.g) < 0.04f && Mathf.Abs(c.g - c.b) < 0.04f)
                    continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            if (maxX < minX)
            {
                minX = 0;
                minY = y0;
                maxX = w - 1;
                maxY = y1;
            }
        }

        /// <summary>
        /// Safety net: replace any remaining solid white rows near top/bottom
        /// with the nearest lobby content row (plate bleed after downsample).
        /// </summary>
        static void FillLobbyWhiteEdgeBars(Color[] px)
        {
            bool RowMostlyPlate(int y)
            {
                var plate = 0;
                for (var x = 0; x < CellPixels; x++)
                {
                    var c = px[y * CellPixels + x];
                    var lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    var chroma = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) -
                                 Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    if (chroma > 0.06f) continue;
                    if (lum > 0.86f || lum < 0.08f || (lum > 0.48f && lum < 0.62f))
                        plate++;
                }

                return plate > CellPixels * 0.80f;
            }

            // Top band (Unity y high): fill plate rows down to first content.
            var topContent = -1;
            for (var y = CellPixels - 1; y >= CellPixels * 2 / 3; y--)
            {
                if (!RowMostlyPlate(y))
                {
                    topContent = y;
                    break;
                }
            }

            // Also wipe plate rows that sit just inside a thin non-plate rim.
            if (topContent < 0)
                topContent = CellPixels - 1;
            for (var y = CellPixels - 1; y > topContent; y--)
            for (var x = 0; x < CellPixels; x++)
                px[y * CellPixels + x] = px[topContent * CellPixels + x];

            for (var y = topContent; y >= CellPixels * 2 / 3; y--)
            {
                if (!RowMostlyPlate(y)) continue;
                var srcY = y - 1;
                while (srcY >= 0 && RowMostlyPlate(srcY)) srcY--;
                if (srcY < 0) break;
                for (var x = 0; x < CellPixels; x++)
                    px[y * CellPixels + x] = px[srcY * CellPixels + x];
            }

            var botContent = -1;
            for (var y = 0; y <= CellPixels / 3; y++)
            {
                if (!RowMostlyPlate(y))
                {
                    botContent = y;
                    break;
                }
            }

            if (botContent < 0)
                botContent = 0;
            for (var y = 0; y < botContent; y++)
            for (var x = 0; x < CellPixels; x++)
                px[y * CellPixels + x] = px[botContent * CellPixels + x];

            for (var y = botContent; y <= CellPixels / 3; y++)
            {
                if (!RowMostlyPlate(y)) continue;
                var srcY = y + 1;
                while (srcY < CellPixels && RowMostlyPlate(srcY)) srcY++;
                if (srcY >= CellPixels) break;
                for (var x = 0; x < CellPixels; x++)
                    px[y * CellPixels + x] = px[srcY * CellPixels + x];
            }
        }

        static void ForceOpaque(Color[] px)
        {
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                c.a = 1f;
                px[i] = c;
            }
        }

        static Tile MakeTile(string name, Color[] pixels, FilterMode filter = FilterMode.Bilinear)
        {
            var tex = new Texture2D(CellPixels, CellPixels, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            tex.SetPixels(pixels);
            tex.Apply();

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, CellPixels, CellPixels),
                new Vector2(0.5f, 0.5f),
                CellPixels);
            tile.color = Color.white;
            tile.name = name;
            return tile;
        }

        static void Set(Color[] px, int x, int y, Color c)
        {
            if ((uint)x >= CellPixels || (uint)y >= CellPixels) return;
            c.a = 1f;
            px[y * CellPixels + x] = c;
        }

        static void PaintLobbyShell(Color[] px)
        {
            // Bottom 15%: dark walnut floor + brass accent line (identical on every tile).
            for (var y = 0; y < FloorH; y++)
            for (var x = 0; x < CellPixels; x++)
            {
                var v = (x * 3 + y * 5) % 11;
                if (x < Edge || x >= CellPixels - Edge)
                    v = (y * 5) % 11;
                var c = v < 3 ? FloorHi : v > 8 ? FloorLo : Floor;
                if (y == FloorH - 2)
                    c = FloorBrass;
                if (y == FloorH - 1)
                    c = FloorLo;
                Set(px, x, y, c);
            }

            // Middle wall (cream) — edges stay plain for seamless joins.
            var wallLo = FloorH;
            var wallHi = CellPixels - CrownH;
            for (var y = wallLo; y < wallHi; y++)
            for (var x = 0; x < CellPixels; x++)
                Set(px, x, y, Wall);

            // Top 10%: white marble crown molding (identical on every tile).
            for (var y = wallHi; y < CellPixels; y++)
            for (var x = 0; x < CellPixels; x++)
            {
                var t = (y - wallHi) / (float)Mathf.Max(1, CrownH - 1);
                var c = t < 0.25f ? CrownLo : t > 0.75f ? CrownHi : Crown;
                Set(px, x, y, c);
            }

            // Mirror L→R edges so any adjacency seams.
            for (var y = 0; y < CellPixels; y++)
            for (var i = 0; i < Edge; i++)
                Set(px, CellPixels - 1 - i, y, px[y * CellPixels + i]);
        }

        static void LockLobbyStructure(Color[] px)
        {
            var wallLo = FloorH;
            var wallHi = CellPixels - CrownH;
            for (var y = 0; y < wallLo; y++)
            for (var x = 0; x < CellPixels; x++)
                px[y * CellPixels + x] = _lobbyShell[y * CellPixels + x];

            for (var y = wallHi; y < CellPixels; y++)
            for (var x = 0; x < CellPixels; x++)
                px[y * CellPixels + x] = _lobbyShell[y * CellPixels + x];

            for (var y = wallLo; y < wallHi; y++)
            for (var i = 0; i < Edge; i++)
            {
                px[y * CellPixels + i] = _lobbyShell[y * CellPixels + i];
                px[y * CellPixels + (CellPixels - 1 - i)] =
                    _lobbyShell[y * CellPixels + (CellPixels - 1 - i)];
            }

            // Soft blend just inside the shared edges so variant art doesn't
            // hard-cut against the seam columns.
            for (var y = wallLo; y < wallHi; y++)
            for (var i = 0; i < EdgeBlend; i++)
            {
                var t = (i + 1) / (float)(EdgeBlend + 1);
                var lx = Edge + i;
                var rx = CellPixels - 1 - Edge - i;
                px[y * CellPixels + lx] = Color.Lerp(
                    _lobbyShell[y * CellPixels + lx], px[y * CellPixels + lx], t);
                px[y * CellPixels + rx] = Color.Lerp(
                    _lobbyShell[y * CellPixels + rx], px[y * CellPixels + rx], t);
            }
        }

        // Minimal fallbacks if AI bytes missing — still 64px with shared shell.
        static void PaintLobbyCenterFallback(Color[] px, int variant)
        {
            var wallLo = FloorH;
            var wallHi = CellPixels - CrownH;
            var mid = CellPixels / 2;
            var wood = new Color(0.27f, 0.20f, 0.14f, 1f);
            var glow = new Color(0.92f, 0.80f, 0.52f, 1f);
            switch (variant)
            {
                case 0:
                    for (var y = wallLo + 2; y < wallHi - 2; y++)
                    for (var x = mid - 8; x <= mid + 8; x++)
                    {
                        if (x < Edge || x >= CellPixels - Edge) continue;
                        var frame = x == mid - 8 || x == mid + 8 || y == wallLo + 2 || y == wallHi - 3;
                        Set(px, x, y, frame ? wood : glow);
                    }
                    break;
                case 1:
                    for (var x = mid - 14; x <= mid + 14; x++)
                    {
                        if (x < Edge || x >= CellPixels - Edge) continue;
                        Set(px, x, wallLo + 3, wood);
                        Set(px, x, wallLo + 4, wood);
                    }
                    break;
                default:
                    for (var y = wallLo; y < wallHi; y++)
                    {
                        Set(px, mid - 1, y, CrownLo);
                        Set(px, mid, y, Wall);
                        Set(px, mid + 1, y, CrownLo);
                    }
                    break;
            }
        }

        static void PaintElevatorMid(Color[] px)
        {
            for (var y = 0; y < CellPixels; y++)
            for (var x = 0; x < CellPixels; x++)
                Set(px, x, y, (x + y) % 17 == 0 ? ShaftHi : Shaft);

            var railL0 = CellPixels * 6 / 64;
            var railL1 = CellPixels * 10 / 64;
            var railR0 = CellPixels - CellPixels * 11 / 64;
            var railR1 = CellPixels - CellPixels * 7 / 64;
            var cable0 = CellPixels / 2 - 1;
            var cable1 = CellPixels / 2;
            for (var y = 0; y < CellPixels; y++)
            {
                for (var x = railL0; x <= railL1; x++)
                    Set(px, x, y, x == railL0 + 1 || x == railL0 + 2 ? Rail : RailDk);
                for (var x = railR0; x <= railR1; x++)
                    Set(px, x, y, x == railR1 - 2 || x == railR1 - 1 ? Rail : RailDk);
                Set(px, cable0, y, Cable);
                Set(px, cable1, y, Cable);
            }
        }

        static void PaintElevatorTop(Color[] px)
        {
            PaintElevatorMid(px);
            var machineH = CellPixels * 16 / 64;
            for (var y = CellPixels - machineH; y < CellPixels; y++)
            for (var x = 3; x < CellPixels - 3; x++)
                Set(px, x, y, Machine);
            var g1 = CellPixels * 22 / 64;
            var g2 = CellPixels * 42 / 64;
            PaintDisc(px, g1, CellPixels - CellPixels * 8 / 64, CellPixels * 7 / 64, Gear);
            PaintDisc(px, g1, CellPixels - CellPixels * 8 / 64, CellPixels * 3 / 64, GearDk);
            PaintDisc(px, g2, CellPixels - CellPixels * 7 / 64, CellPixels * 6 / 64, Gear);
            PaintDisc(px, g2, CellPixels - CellPixels * 7 / 64, CellPixels * 2 / 64, GearDk);
            for (var x = 4; x < CellPixels - 4; x++)
            {
                Set(px, x, CellPixels - machineH - 1, RailDk);
                Set(px, x, CellPixels - machineH, Rail);
            }
        }

        static void PaintElevatorBottom(Color[] px)
        {
            PaintElevatorMid(px);
            var pitH = CellPixels * 14 / 64;
            for (var y = 0; y < pitH; y++)
            for (var x = CellPixels * 12 / 64; x < CellPixels - CellPixels * 12 / 64; x++)
                Set(px, x, y, Pit);
            var s0 = CellPixels * 20 / 64;
            var s1 = CellPixels * 42 / 64;
            for (var i = 0; i < 12; i++)
            {
                Set(px, s0 + i % 2, 2 + i, Spring);
                Set(px, s0 + 1 + i % 2, 2 + i, Spring);
                Set(px, s1 + i % 2, 2 + i, Spring);
                Set(px, s1 + 1 + i % 2, 2 + i, Spring);
            }
        }

        static void PaintDisc(Color[] px, int cx, int cy, int r, Color c)
        {
            var r2 = r * r;
            for (var y = cy - r; y <= cy + r; y++)
            for (var x = cx - r; x <= cx + r; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                if (dx * dx + dy * dy <= r2)
                    Set(px, x, y, c);
            }
        }

        static Sprite LoadOrBuildStairs()
        {
            if (_stairsStarTier < 0)
                _stairsStarTier = 1;

            var px = TryLoadStairsPixels(StairsResourceForTier(_stairsStarTier));
            if (px == null && _stairsStarTier != 1)
                px = TryLoadStairsPixels("stairs_star_01");
            if (px == null)
                px = TryLoadStairsPixels("stairs_2x2");
            if (px == null)
                return BuildStairsFallback();

            // Crop transparent padding and pin pivot to the bottom tread so the
            // stairs sit on the floor of the 2×2 build box.
            FindStairsContent(px, out var minX, out var minY, out var maxX, out var maxY);
            var cw = Mathf.Max(1, maxX - minX + 1);
            var ch = Mathf.Max(1, maxY - minY + 1);
            var cropped = new Color[cw * ch];
            for (var y = 0; y < ch; y++)
            for (var x = 0; x < cw; x++)
                cropped[y * cw + x] = px[(minY + y) * StairsPixels + (minX + x)];

            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "stairs_" + _stairsStarTier
            };
            tex.SetPixels(cropped);
            tex.Apply();
            // PPU so cropped art naturally covers ~2 world units on the long side.
            var ppu = Mathf.Max(cw, ch) * 0.5f;
            // Bottom-left pivot: overlay pins lower-left to the lower floor and
            // scales toward the upper-right floor line.
            return Sprite.Create(
                tex,
                new Rect(0, 0, cw, ch),
                new Vector2(0f, 0f),
                ppu);
        }

        static void FindStairsContent(
            Color[] px, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = StairsPixels;
            minY = StairsPixels;
            maxX = -1;
            maxY = -1;
            for (var y = 0; y < StairsPixels; y++)
            for (var x = 0; x < StairsPixels; x++)
            {
                if (px[y * StairsPixels + x].a < 0.08f) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            if (maxX < minX)
            {
                minX = 0;
                minY = 0;
                maxX = StairsPixels - 1;
                maxY = StairsPixels - 1;
                return;
            }

            // Raise the bottom crop to the first row that looks like a tread
            // (enough opaque pixels), ignoring sparse fringe under the stairs.
            const int minOpaque = 8;
            for (var y = minY; y <= maxY; y++)
            {
                var count = 0;
                for (var x = minX; x <= maxX; x++)
                {
                    if (px[y * StairsPixels + x].a >= 0.08f) count++;
                }

                if (count >= minOpaque)
                {
                    minY = y;
                    break;
                }
            }
        }

        static Color[] TryLoadStairsPixels(string fileName)
        {
            var bytesAsset = Resources.Load<TextAsset>(ResourcesRoot + fileName);
            byte[] png = bytesAsset != null ? bytesAsset.bytes : null;
            if (png == null)
            {
                var tex = Resources.Load<Texture2D>(ResourcesRoot + fileName);
                if (tex == null) return null;
                try { png = tex.EncodeToPNG(); }
                catch (UnityException) { return null; }
            }

            if (png == null || png.Length < 32) return null;
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!decoded.LoadImage(png, false)) return null;

            var srcW = decoded.width;
            var srcH = decoded.height;
            var srcPx = decoded.GetPixels();
            // Key baked checkerboard / white plates before downsample.
            KeyStairsBackground(srcPx, srcW, srcH);

            decoded.SetPixels(srcPx);
            decoded.Apply(false, false);

            var px = new Color[StairsPixels * StairsPixels];
            for (var y = 0; y < StairsPixels; y++)
            for (var x = 0; x < StairsPixels; x++)
            {
                // Nearest sample — bilinear softens thin rails into see-through gaps.
                var sx = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / StairsPixels * srcW), 0, srcW - 1);
                var sy = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / StairsPixels * srcH), 0, srcH - 1);
                var c = srcPx[sy * srcW + sx];
                if (c.a < 0.08f)
                    c = Color.clear;
                else
                    c.a = 1f;
                px[y * StairsPixels + x] = c;
            }

            Object.Destroy(decoded);
            return px;
        }

        /// <summary>
        /// Stair PNGs often bake a checkerboard or solid black plate instead of alpha.
        /// Flood-fill plate from the border; avoid eating thin rail/newel pixels.
        /// </summary>
        static void KeyStairsBackground(Color[] px, int w, int h)
        {
            bool IsPlate(Color c)
            {
                if (c.a < 0.08f) return true;
                var lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                var max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                var min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                var chroma = max - min;
                if (chroma > 0.07f) return false; // gold / warm materials
                if (lum < 0.10f) return true;     // black plate (4–5★)
                if (lum > 0.82f) return true;     // white / near-white plate
                // Checker mid-grey only (avoid dark/mid rail steel ~0.25–0.50).
                if (lum > 0.58f && lum < 0.78f && chroma < 0.035f) return true;
                return false;
            }

            var visit = new bool[w * h];
            var q = new System.Collections.Generic.Queue<int>();
            void TryEnq(int x, int y)
            {
                if ((uint)x >= w || (uint)y >= h) return;
                var i = y * w + x;
                if (visit[i] || !IsPlate(px[i])) return;
                visit[i] = true;
                q.Enqueue(i);
            }

            for (var x = 0; x < w; x++)
            {
                TryEnq(x, 0);
                TryEnq(x, h - 1);
            }
            for (var y = 0; y < h; y++)
            {
                TryEnq(0, y);
                TryEnq(w - 1, y);
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

            // Enclosed pure black / pure white pockets (between spindles), not stone/rail.
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a < 0.08f) continue;
                var lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                var max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                var min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                var chroma = max - min;
                if (chroma > 0.05f) continue;
                if (lum < 0.10f || lum > 0.92f)
                    px[i] = Color.clear;
            }

            // Heal single-pixel holes in rails/newels caused by plate fringe.
            HealStairHoles(px, w, h);
        }

        static void HealStairHoles(Color[] px, int w, int h)
        {
            var copy = (Color[])px.Clone();
            for (var y = 1; y < h - 1; y++)
            for (var x = 1; x < w - 1; x++)
            {
                var i = y * w + x;
                if (copy[i].a >= 0.08f) continue;
                var count = 0;
                var r = 0f;
                var g = 0f;
                var b = 0f;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var n = copy[(y + dy) * w + (x + dx)];
                    if (n.a < 0.08f) continue;
                    count++;
                    r += n.r;
                    g += n.g;
                    b += n.b;
                }

                // Only fill gaps tightly surrounded by structure (missing rail pixels).
                if (count >= 5)
                    px[i] = new Color(r / count, g / count, b / count, 1f);
            }
        }

        static Sprite BuildStairsFallback()
        {
            var px = new Color[StairsPixels * StairsPixels];
            for (var i = 0; i < px.Length; i++)
                px[i] = Color.clear;

            const int steps = 14;
            for (var i = 0; i < steps; i++)
            {
                var t = i / (float)(steps - 1);
                var x0 = 3 + (int)(t * (StairsPixels - 14));
                var y0 = 3 + (int)(t * (StairsPixels - 14));
                for (var dx = 0; dx < 7; dx++)
                {
                    Set64(px, x0 + dx, y0, new Color(0.50f, 0.51f, 0.54f, 1f));
                    Set64(px, x0 + dx, y0 + 1, new Color(0.40f, 0.41f, 0.44f, 1f));
                    Set64(px, x0 + dx, y0 + 2, new Color(0.32f, 0.33f, 0.36f, 1f));
                }
                Set64(px, x0 + 2, y0 + 6, new Color(0.55f, 0.56f, 0.58f, 0.95f));
                if (i % 2 == 0)
                {
                    for (var by = 1; by <= 5; by++)
                        Set64(px, x0 + 2, y0 + by, new Color(0.48f, 0.49f, 0.51f, 0.85f));
                }
            }

            var tex = new Texture2D(StairsPixels, StairsPixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "stairs_2x2_proc"
            };
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(
                tex,
                new Rect(0, 0, StairsPixels, StairsPixels),
                new Vector2(0f, 0f),
                StairsPixels * 0.5f);
        }

        static void Set64(Color[] px, int x, int y, Color c)
        {
            if ((uint)x >= StairsPixels || (uint)y >= StairsPixels) return;
            px[y * StairsPixels + x] = c;
        }
    }
}
