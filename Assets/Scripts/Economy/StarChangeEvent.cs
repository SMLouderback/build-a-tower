namespace BuildATower
{
    public enum StarChangeKind
    {
        Promoted,
        Demoted
    }

    public readonly struct StarChangeEvent
    {
        public StarChangeKind Kind { get; }
        public int Stars { get; }

        public StarChangeEvent(StarChangeKind kind, int stars)
        {
            Kind = kind;
            Stars = stars;
        }
    }
}
