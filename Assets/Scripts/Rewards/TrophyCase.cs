namespace BuildATower
{
    public sealed class TrophyCase
    {
        readonly TrophyMetal[] _metalBySubject = new TrophyMetal[SubjectCount];

        static int SubjectCount => System.Enum.GetValues(typeof(ChallengeSubject)).Length;

        public TrophyCase()
        {
            for (var i = 0; i < _metalBySubject.Length; i++)
                _metalBySubject[i] = TrophyMetal.None;
        }

        public TrophyMetal GetMetal(ChallengeSubject subject) => _metalBySubject[(int)subject];

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
