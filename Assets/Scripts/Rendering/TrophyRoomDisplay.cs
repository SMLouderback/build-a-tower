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

        public static void SyncView(TowerGrid grid, TrophyCase trophyCase)
        {
            if (grid == null || trophyCase == null)
                return;

            var view = UnityEngine.Object.FindAnyObjectByType<TilemapTowerView>();
            if (view == null)
                return;

            foreach (var room in EnumeratePlacedRooms(grid))
                view.PaintRoom(room);
        }
    }
}
