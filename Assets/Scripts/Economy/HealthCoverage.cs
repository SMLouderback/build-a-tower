using System;
using System.Collections.Generic;

namespace BuildATower
{
    public readonly struct CoverageAssignment
    {
        public CoverageAssignment(int needInstanceId, int facilityInstanceId, bool streetBonus)
        {
            NeedInstanceId = needInstanceId;
            FacilityInstanceId = facilityInstanceId;
            StreetBonus = streetBonus;
        }

        public int NeedInstanceId { get; }
        public int FacilityInstanceId { get; }
        public bool StreetBonus { get; }
    }

    public static class HealthCoverage
    {
        public const int TrashHorizontalRange = 20;
        public const int TrashVerticalRange = 3;
        public const int PublicHorizontalRange = 12;
        public const int PublicVerticalRange = 2;
        public const int PublicCapacity = 3;
        public const int OfficeHorizontalRange = 16;
        public const int OfficeVerticalRange = 1;
        public const int OfficeCapacity = 4;

        const string TrashId = "service_trash";
        const string PublicRestroomId = "service_public_restroom";
        const string OfficeRestroomId = "service_office_restroom";

        public static bool TrashCovers(int floor, int originX, IReadOnlyList<RoomInstance> rooms)
        {
            if (rooms == null)
                return false;

            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room?.Type == null || room.Type.id != TrashId || room.IsBroken || !room.Powered)
                    continue;

                if (WithinRange(originX, floor, room.Origin.x, room.Origin.y, TrashHorizontalRange, TrashVerticalRange))
                    return true;
            }

            return false;
        }

        public static IReadOnlyList<CoverageAssignment> AssignPublicRestrooms(IReadOnlyList<RoomInstance> rooms) =>
            Assign(
                rooms,
                PublicRestroomId,
                PublicHorizontalRange,
                PublicVerticalRange,
                PublicCapacity,
                IsPublicNeedRoom,
                StreetBonusForPublicNeed);

        public static IReadOnlyList<CoverageAssignment> AssignOfficeRestrooms(IReadOnlyList<RoomInstance> rooms) =>
            Assign(
                rooms,
                OfficeRestroomId,
                OfficeHorizontalRange,
                OfficeVerticalRange,
                OfficeCapacity,
                IsOfficeNeedRoom,
                _ => false);

        static IReadOnlyList<CoverageAssignment> Assign(
            IReadOnlyList<RoomInstance> rooms,
            string facilityTypeId,
            int horizontalRange,
            int verticalRange,
            int capacity,
            Func<RoomInstance, bool> isNeedRoom,
            Func<RoomInstance, bool> streetBonusForNeed)
        {
            if (rooms == null || rooms.Count == 0)
                return Array.Empty<CoverageAssignment>();

            var facilities = new List<RoomInstance>();
            var needs = new List<RoomInstance>();

            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room?.Type == null)
                    continue;

                if (room.Type.id == facilityTypeId)
                {
                    if (!room.IsBroken && !room.Dirty && room.Powered)
                        facilities.Add(room);
                    continue;
                }

                if (isNeedRoom(room))
                    needs.Add(room);
            }

            if (facilities.Count == 0 || needs.Count == 0)
                return Array.Empty<CoverageAssignment>();

            needs.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));

            var remaining = new Dictionary<int, int>(facilities.Count);
            for (var i = 0; i < facilities.Count; i++)
                remaining[facilities[i].InstanceId] = capacity;

            var assignments = new List<CoverageAssignment>();

            for (var n = 0; n < needs.Count; n++)
            {
                var need = needs[n];
                RoomInstance bestFacility = null;
                var bestScore = int.MaxValue;
                var bestFacilityId = int.MaxValue;

                for (var f = 0; f < facilities.Count; f++)
                {
                    var facility = facilities[f];
                    if (!remaining.TryGetValue(facility.InstanceId, out var left) || left <= 0)
                        continue;

                    var dx = Math.Abs(facility.Origin.x - need.Origin.x);
                    var dy = Math.Abs(facility.Origin.y - need.Origin.y);
                    if (dx > horizontalRange || dy > verticalRange)
                        continue;

                    var score = dy * 1000 + dx;
                    if (score > bestScore)
                        continue;
                    if (score == bestScore && facility.InstanceId >= bestFacilityId)
                        continue;

                    bestScore = score;
                    bestFacilityId = facility.InstanceId;
                    bestFacility = facility;
                }

                if (bestFacility == null)
                    continue;

                remaining[bestFacility.InstanceId]--;
                assignments.Add(new CoverageAssignment(
                    need.InstanceId,
                    bestFacility.InstanceId,
                    streetBonusForNeed(need)));
            }

            return assignments;
        }

        static bool IsPublicNeedRoom(RoomInstance room)
        {
            var type = room.Type;
            if (type == null)
                return false;
            if (type.id == PublicRestroomId || type.id == OfficeRestroomId)
                return false;
            if (type.isLobby || type.isSkyLobby)
                return true;

            var family = type.ResolvedBuildFamily();
            if (family == BuildFamily.Leisure)
                return true;
            if (family == BuildFamily.Shops || type.buildSubgroup == BuildSubgroup.Food)
                return true;

            return false;
        }

        static bool IsOfficeNeedRoom(RoomInstance room)
        {
            var type = room.Type;
            if (type == null || type.category != RoomCategory.Office)
                return false;
            return type.luxuryBand is LuxuryBand.Base or LuxuryBand.Mid;
        }

        static bool StreetBonusForPublicNeed(RoomInstance room)
        {
            var type = room.Type;
            if (type == null)
                return false;
            if (type.buildSubgroup == BuildSubgroup.Food)
                return true;
            return type.ResolvedBuildFamily() == BuildFamily.Shops;
        }

        static bool WithinRange(int needX, int needY, int facilityX, int facilityY, int horizontalRange, int verticalRange) =>
            Math.Abs(facilityX - needX) <= horizontalRange &&
            Math.Abs(facilityY - needY) <= verticalRange;
    }
}
