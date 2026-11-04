using System;
using System.Collections.Generic;
using System.Globalization;

namespace BuildATower
{
    public static class TrophyRoomDisplay
    {
        public const string RoomTypeId = "service_trophy_room";
        public const string ResourceLeaf = "trophy_room_6x1";

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
    }
}
