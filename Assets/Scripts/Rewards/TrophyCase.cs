using System;
using System.Collections.Generic;

namespace BuildATower
{
    public sealed class TrophyCase
    {
        readonly TrophyMetal[] _metalBySubject = new TrophyMetal[SubjectCount];
        readonly string[] _titleBySubject = new string[SubjectCount];
        readonly string[] _ribbonBySubject = new string[SubjectCount];

        static int SubjectCount => System.Enum.GetValues(typeof(ChallengeSubject)).Length;

        public TrophyCase()
        {
            for (var i = 0; i < _metalBySubject.Length; i++)
                _metalBySubject[i] = TrophyMetal.None;
        }

        public TrophyMetal GetMetal(ChallengeSubject subject) => _metalBySubject[(int)subject];

        public string GetTitle(ChallengeSubject subject) => _titleBySubject[(int)subject];

        public string GetRibbon(ChallengeSubject subject) => _ribbonBySubject[(int)subject];

        public TrophySnapshotV1[] CaptureSnapshot()
        {
            var trophies = new List<TrophySnapshotV1>();
            for (var i = 0; i < _metalBySubject.Length; i++)
            {
                var metal = _metalBySubject[i];
                var title = _titleBySubject[i];
                var ribbon = _ribbonBySubject[i];
                if (metal == TrophyMetal.None
                    && string.IsNullOrWhiteSpace(title)
                    && string.IsNullOrWhiteSpace(ribbon))
                    continue;

                trophies.Add(new TrophySnapshotV1
                {
                    subject = ((ChallengeSubject)i).ToString(),
                    metal = metal.ToString(),
                    title = title ?? string.Empty,
                    ribbon = ribbon ?? string.Empty
                });
            }

            return trophies.ToArray();
        }

        public void RestoreSnapshot(TrophySnapshotV1[] trophies)
        {
            for (var i = 0; i < _metalBySubject.Length; i++)
            {
                _metalBySubject[i] = TrophyMetal.None;
                _titleBySubject[i] = null;
                _ribbonBySubject[i] = null;
            }

            if (trophies == null)
                return;

            for (var i = 0; i < trophies.Length; i++)
            {
                var trophy = trophies[i];
                if (trophy == null
                    || !Enum.TryParse(trophy.subject, false, out ChallengeSubject subject)
                    || !Enum.IsDefined(typeof(ChallengeSubject), subject))
                    continue;

                if (!Enum.TryParse(trophy.metal, false, out TrophyMetal metal)
                    || !Enum.IsDefined(typeof(TrophyMetal), metal))
                    metal = TrophyMetal.None;

                var index = (int)subject;
                _metalBySubject[index] = metal;
                _titleBySubject[index] = string.IsNullOrWhiteSpace(trophy.title) ? null : trophy.title;
                _ribbonBySubject[index] = string.IsNullOrWhiteSpace(trophy.ribbon) ? null : trophy.ribbon;
            }
        }

        public void SetMetadata(ChallengeSubject subject, string title, string ribbon)
        {
            var index = (int)subject;
            if (!string.IsNullOrWhiteSpace(title))
                _titleBySubject[index] = title;
            if (!string.IsNullOrWhiteSpace(ribbon))
                _ribbonBySubject[index] = ribbon;
        }

        public bool HasAnyTrophy
        {
            get
            {
                for (var i = 0; i < _metalBySubject.Length; i++)
                {
                    if (_metalBySubject[i] != TrophyMetal.None)
                        return true;
                }

                return false;
            }
        }

        public (bool metalRaised, bool firstTrophyOnTower) ApplyClaimMetal(
            ChallengeSubject subject,
            TrophyMetal metal)
        {
            if (metal == TrophyMetal.None)
                return (false, false);

            var hadAny = HasAnyTrophy;
            var current = GetMetal(subject);
            if (!TrophyMetalCompare.IsHigher(metal, current))
                return (false, false);

            _metalBySubject[(int)subject] = metal;
            var firstTrophy = !hadAny;
            return (true, firstTrophy);
        }
    }
}
