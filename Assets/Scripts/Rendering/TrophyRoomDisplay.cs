using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BuildATower
{
    public static class TrophyRoomDisplay
    {
        public const string RoomTypeId = "service_trophy_room";
        public const string ResourceLeaf = "trophy_room_6x1";
        public const string TrophiesArtFolder = "Art/Trophies";

        /// <summary>On-screen medal chip, in source pixels. Far smaller than a gallery-modal pedestal.</summary>
        public const int ChipPixelSize = 20;

        public const int ChipSortingOrder = RoomDollhouseArt.SortingOrder + 1;

        /// <summary>
        /// Shelf-band anchors in room space (origin bottom-left, 1 = far edge).
        /// Index follows <see cref="ChallengeSubject"/> declaration order.
        /// </summary>
        public static readonly Vector2[] CutawayChipNormalizedCenters = BuildCutawayChipCenters();

        /// <summary>Set by <see cref="SyncView"/> so a paint during refresh uses that case.</summary>
        internal static TrophyCase CutawayPaintCase;

        public readonly struct PedestalSlot
        {
            public PedestalSlot(ChallengeSubject subject, TrophyMetal metal)
            {
                Subject = subject;
                Metal = metal;
                SpriteKey = MetalSpriteKey(subject, metal);
            }

            public ChallengeSubject Subject { get; }
            public TrophyMetal Metal { get; }
            public bool HasTrophy => Metal != TrophyMetal.None;
            public string SpriteKey { get; }
        }

        public static IReadOnlyList<PedestalSlot> BuildPedestals(TrophyCase trophyCase)
        {
            var subjects = (ChallengeSubject[])Enum.GetValues(typeof(ChallengeSubject));
            var slots = new PedestalSlot[subjects.Length];
            for (var i = 0; i < subjects.Length; i++)
            {
                var subject = subjects[i];
                var metal = trophyCase?.GetMetal(subject) ?? TrophyMetal.None;
                slots[i] = new PedestalSlot(subject, metal);
            }

            return slots;
        }

        public static string MetalSpriteKey(ChallengeSubject subject, TrophyMetal metal)
        {
            if (metal == TrophyMetal.None)
                return string.Empty;

            return "trophy_"
                   + subject.ToString().ToLower(CultureInfo.InvariantCulture)
                   + "_"
                   + metal.ToString().ToLower(CultureInfo.InvariantCulture);
        }

        public static string MetalSpriteResourcePath(ChallengeSubject subject, TrophyMetal metal) =>
            MetalSpriteResourcePath(MetalSpriteKey(subject, metal));

        public static string MetalSpriteResourcePath(string spriteKey) =>
            string.IsNullOrEmpty(spriteKey) ? string.Empty : TrophiesArtFolder + "/" + spriteKey;

        public static IEnumerable<RoomInstance> EnumeratePlacedRooms(TowerGrid grid)
        {
            if (grid?.Rooms == null)
                yield break;

            var rooms = grid.Rooms;
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room?.Type != null && room.Type.id == RoomTypeId)
                    yield return room;
            }
        }

        public static Vector2 RoomCutawayWorldCenter(RoomInstance room)
        {
            if (room == null)
                return Vector2.zero;

            var size = room.Size.x > 0 && room.Size.y > 0 ? room.Size : room.Type.size;
            return new Vector2(room.Origin.x + size.x * 0.5f, room.Origin.y + size.y * 0.5f);
        }

        public static Vector2 WorldToGuiPoint(Camera cam, Vector3 world)
        {
            if (cam == null)
                return Vector2.zero;

            var screen = cam.WorldToScreenPoint(world);
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        public static List<Vector2> CollectCutawayScreenAnchors(TowerGrid grid, Camera cam)
        {
            var anchors = new List<Vector2>();
            if (grid == null)
                return anchors;

            foreach (var room in EnumeratePlacedRooms(grid))
            {
                var world = RoomCutawayWorldCenter(room);
                anchors.Add(WorldToGuiPoint(cam, new Vector3(world.x, world.y, 0f)));
            }

            return anchors;
        }

        public static Vector2 ChipWorldPosition(RoomInstance room, int subjectIndex)
        {
            if (room == null ||
                subjectIndex < 0 ||
                subjectIndex >= CutawayChipNormalizedCenters.Length)
                return Vector2.zero;

            var size = room.Size.x > 0 && room.Size.y > 0
                ? room.Size
                : room.Type != null ? room.Type.size : Vector2Int.one;
            var center = CutawayChipNormalizedCenters[subjectIndex];
            return new Vector2(
                room.Origin.x + center.x * size.x,
                room.Origin.y + center.y * size.y);
        }

        public static Vector3 ChipScale(Sprite sprite)
        {
            if (sprite == null)
                return Vector3.one;

            var bounds = sprite.bounds.size;
            var target = ChipPixelSize / RoomDollhouseArt.PixelsPerUnit;
            var sx = bounds.x > 0.001f ? target / bounds.x : 1f;
            var sy = bounds.y > 0.001f ? target / bounds.y : 1f;
            var uniform = Mathf.Min(sx, sy);
            return new Vector3(uniform, uniform, 1f);
        }

        public static Sprite LoadChipSprite(string spriteKey)
        {
            if (string.IsNullOrEmpty(spriteKey))
                return null;
            if (ChipSprites.TryGetValue(spriteKey, out var cached) && cached != null)
                return cached;

            var tex = LoadChipTexture(MetalSpriteResourcePath(spriteKey));
            if (tex == null)
                return null;

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                RoomDollhouseArt.PixelsPerUnit);
            sprite.name = spriteKey;
            ChipSprites[spriteKey] = sprite;
            return sprite;
        }

        public static void SyncView(TowerGrid grid, TrophyCase trophyCase)
        {
            if (grid == null || trophyCase == null)
                return;

            var view = UnityEngine.Object.FindAnyObjectByType<TilemapTowerView>();
            if (view == null)
                return;

            CutawayPaintCase = trophyCase;
            try
            {
                foreach (var room in EnumeratePlacedRooms(grid))
                    view.PaintRoom(room);
            }
            finally
            {
                CutawayPaintCase = null;
            }
        }

        static Vector2[] BuildCutawayChipCenters()
        {
            var subjects = (ChallengeSubject[])Enum.GetValues(typeof(ChallengeSubject));
            var centers = new Vector2[subjects.Length];
            const float y = 0.35f;
            const float x0 = 0.08f;
            const float x1 = 0.92f;
            var last = Mathf.Max(1, subjects.Length - 1);
            for (var i = 0; i < subjects.Length; i++)
                centers[i] = new Vector2(Mathf.Lerp(x0, x1, i / (float)last), y);
            return centers;
        }

        static readonly Dictionary<string, Sprite> ChipSprites = new();

        static Texture2D LoadChipTexture(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var bytesAsset = Resources.Load<TextAsset>(path);
            var png = bytesAsset != null ? bytesAsset.bytes : null;
            if (png != null && png.Length >= 32)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = path
                };
                if (!tex.LoadImage(png, false))
                {
                    DestroyOwned(tex);
                    return null;
                }

                // Keep the shipped dark plate — color-keying brown shadows punched holes
                // in bronze/gold cups (same hue family as the backdrop).
                return tex;
            }

            return Resources.Load<Texture2D>(path);
        }

        public static void ResetChipCache()
        {
            foreach (var pair in ChipSprites)
            {
                if (pair.Value == null)
                    continue;
                var tex = pair.Value.texture;
                DestroyOwned(pair.Value);
                if (tex != null)
                    DestroyOwned(tex);
            }

            ChipSprites.Clear();
        }

        static void DestroyOwned(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    /// <summary>
    /// Tiny live medal chips over placed Trophy Rooms. One renderer per subject.
    /// </summary>
    public sealed class TrophyRoomChipLayer
    {
        readonly Transform _root;
        readonly Dictionary<int, SpriteRenderer[]> _chips = new();

        public TrophyRoomChipLayer(Transform parent)
        {
            var go = new GameObject("TrophyRoomChips");
            go.transform.SetParent(parent, false);
            _root = go.transform;
        }

        public void Apply(RoomInstance room, TrophyCase trophyCase)
        {
            if (room?.Type == null || room.Type.id != TrophyRoomDisplay.RoomTypeId)
                return;

            var slots = TrophyRoomDisplay.BuildPedestals(trophyCase);
            if (!_chips.TryGetValue(room.InstanceId, out var renderers) ||
                renderers == null ||
                renderers.Length != slots.Count)
            {
                Clear(room.InstanceId);
                renderers = new SpriteRenderer[slots.Count];
                for (var i = 0; i < slots.Count; i++)
                {
                    var go = new GameObject($"TrophyChip_{room.InstanceId}_{i}");
                    go.transform.SetParent(_root, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = TrophyRoomDisplay.ChipSortingOrder;
                    renderers[i] = sr;
                }

                _chips[room.InstanceId] = renderers;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var sr = renderers[i];
                if (sr == null)
                    continue;

                var slot = slots[i];
                if (!slot.HasTrophy)
                {
                    sr.enabled = false;
                    sr.sprite = null;
                    continue;
                }

                var sprite = TrophyRoomDisplay.LoadChipSprite(slot.SpriteKey);
                if (sprite == null)
                {
                    sr.enabled = false;
                    continue;
                }

                sr.enabled = true;
                sr.sprite = sprite;
                sr.color = Color.white;
                var world = TrophyRoomDisplay.ChipWorldPosition(room, i);
                sr.transform.position = new Vector3(world.x, world.y, 0f);
                sr.transform.localScale = TrophyRoomDisplay.ChipScale(sprite);
            }
        }

        public void Clear(int instanceId)
        {
            if (!_chips.TryGetValue(instanceId, out var renderers) || renderers == null)
                return;

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    UnityEngine.Object.Destroy(renderers[i].gameObject);
            }

            _chips.Remove(instanceId);
        }
    }
}
