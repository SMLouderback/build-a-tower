using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BuildATower
{
    public sealed class PlaytestFeedbackClient
    {
        const string FeedbackPath = "/playtest/feedback";

        readonly PlaytestConfig config;
        readonly HttpClient http;

        public PlaytestFeedbackClient()
            : this(new PlaytestConfig(), null)
        {
        }

        public PlaytestFeedbackClient(PlaytestConfig config)
            : this(config, null)
        {
        }

        public PlaytestFeedbackClient(PlaytestConfig config, HttpClient http)
        {
            this.config = config ?? new PlaytestConfig();
            this.http = http ?? new HttpClient();
            if (this.http.BaseAddress == null)
                this.http.BaseAddress = new Uri(this.config.BaseUrl);
        }

        public async Task<PlaytestFeedbackSendResult> SendAsync(
            PlaytestFeedbackDraft draft,
            CancellationToken cancellationToken = default)
        {
            draft = draft ?? new PlaytestFeedbackDraft();
            var message = (draft.Message ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(message))
                return PlaytestFeedbackSendResult.FromKind(PlaytestFeedbackSendKind.RejectedEmpty);

            var email = (draft.Email ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(email) && !IsValidEmail(email))
                return PlaytestFeedbackSendResult.FromKind(PlaytestFeedbackSendKind.RejectedEmail);

            var request = new FeedbackRequestDto
            {
                message = message,
                name = (draft.Name ?? string.Empty).Trim(),
                email = email,
                version = (draft.Version ?? string.Empty).Trim()
            };

            try
            {
                using (var response = await http.PostAsync(FeedbackPath, JsonContent(request), cancellationToken))
                {
                    if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.OK)
                        return PlaytestFeedbackSendResult.FromKind(PlaytestFeedbackSendKind.Sent);

                    if ((int)response.StatusCode == 429)
                        return PlaytestFeedbackSendResult.FromKind(PlaytestFeedbackSendKind.RateLimited);

                    return PlaytestFeedbackSendResult.FromKind(PlaytestFeedbackSendKind.FailedKeepDraft);
                }
            }
            catch (HttpRequestException)
            {
                return PlaytestFeedbackSendResult.FromKind(PlaytestFeedbackSendKind.FailedKeepDraft);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return PlaytestFeedbackSendResult.FromKind(PlaytestFeedbackSendKind.FailedKeepDraft);
            }
        }

        static bool IsValidEmail(string email)
        {
            var at = email.IndexOf('@');
            if (at <= 0 || at >= email.Length - 1)
                return false;

            var dotAfterAt = email.IndexOf('.', at + 1);
            return dotAfterAt > at + 1 && dotAfterAt < email.Length - 1;
        }

        static StringContent JsonContent<TRequest>(TRequest request)
        {
            return new StringContent(JsonUtility.ToJson(request), Encoding.UTF8, "application/json");
        }

        [Serializable]
        sealed class FeedbackRequestDto
        {
            public string message;
            public string name;
            public string email;
            public string version;
        }
    }

    public sealed class PlaytestFeedbackDraft
    {
        public string Message;
        public string Name;
        public string Email;
        public string Version;
    }

    public enum PlaytestFeedbackSendKind
    {
        Sent,
        RejectedEmpty,
        RejectedEmail,
        FailedKeepDraft,
        RateLimited
    }

    public readonly struct PlaytestFeedbackSendResult
    {
        PlaytestFeedbackSendResult(PlaytestFeedbackSendKind kind)
        {
            Kind = kind;
        }

        public PlaytestFeedbackSendKind Kind { get; }

        public static PlaytestFeedbackSendResult FromKind(PlaytestFeedbackSendKind kind)
        {
            return new PlaytestFeedbackSendResult(kind);
        }
    }
}
