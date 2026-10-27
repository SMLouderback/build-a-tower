namespace BuildATower
{
    public sealed class PlaytestConfig
    {
        public const string DefaultBaseUrl = "https://escapeproductions.biz";

        public PlaytestConfig()
            : this(DefaultBaseUrl)
        {
        }

        public PlaytestConfig(string baseUrl)
        {
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? DefaultBaseUrl
                : baseUrl.TrimEnd('/');
        }

        public string BaseUrl { get; }
    }
}
