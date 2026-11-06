namespace BuildATower
{
    public sealed class TutorialHighlight
    {
        public TutorialHighlightTarget Target;
        public string Title;
        public string Body;

        public TutorialHighlight()
        {
        }

        public TutorialHighlight(TutorialHighlightTarget target, string title, string body)
        {
            Target = target;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }
    }
}
