using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public sealed class PlaytestFeedbackClientTests
    {
        [Test]
        public async Task Empty_note_does_not_post()
        {
            var posted = false;
            var handler = new StubHandler(_ =>
            {
                posted = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });
            var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
            {
                BaseAddress = new Uri("https://escapeproductions.biz/")
            });

            var result = await client.SendAsync(new PlaytestFeedbackDraft { Message = "  ", Version = "1.0.1" });

            Assert.AreEqual(PlaytestFeedbackSendKind.RejectedEmpty, result.Kind);
            Assert.IsFalse(posted);
        }

        [Test]
        public async Task Invalid_email_does_not_post()
        {
            var posted = false;
            var handler = new StubHandler(_ =>
            {
                posted = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });
            var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
            {
                BaseAddress = new Uri("https://escapeproductions.biz/")
            });

            var result = await client.SendAsync(new PlaytestFeedbackDraft
            {
                Message = "Hi",
                Email = "not-an-email",
                Version = "1.0.1"
            });

            Assert.AreEqual(PlaytestFeedbackSendKind.RejectedEmail, result.Kind);
            Assert.IsFalse(posted);
        }

        [Test]
        public async Task Success_posts_json_and_returns_Sent()
        {
            HttpRequestMessage seen = null;
            var handler = new StubHandler(req =>
            {
                seen = req;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });
            var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
            {
                BaseAddress = new Uri("https://escapeproductions.biz/")
            });

            var result = await client.SendAsync(new PlaytestFeedbackDraft
            {
                Message = "Stairs feel slow",
                Name = "Pat",
                Email = "pat@example.com",
                Version = "1.0.1"
            });

            Assert.AreEqual(PlaytestFeedbackSendKind.Sent, result.Kind);
            Assert.IsNotNull(seen);
            Assert.AreEqual(HttpMethod.Post, seen.Method);
            Assert.AreEqual("/playtest/feedback", seen.RequestUri.AbsolutePath);
        }

        [Test]
        public async Task Offline_keeps_draft_message()
        {
            var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
            var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
            {
                BaseAddress = new Uri("https://escapeproductions.biz/")
            });
            var draft = new PlaytestFeedbackDraft { Message = "Hello", Version = "1.0.1" };

            var result = await client.SendAsync(draft);

            Assert.AreEqual(PlaytestFeedbackSendKind.FailedKeepDraft, result.Kind);
            Assert.AreEqual("Hello", draft.Message);
        }

        [Test]
        public async Task Rate_limit_maps_to_RateLimited()
        {
            var handler = new StubHandler(_ => new HttpResponseMessage((HttpStatusCode)429));
            var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
            {
                BaseAddress = new Uri("https://escapeproductions.biz/")
            });

            var result = await client.SendAsync(new PlaytestFeedbackDraft { Message = "Hi", Version = "1.0.1" });

            Assert.AreEqual(PlaytestFeedbackSendKind.RateLimited, result.Kind);
        }

        sealed class StubHandler : HttpMessageHandler
        {
            readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                this.respond = respond;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(respond(request));
            }
        }
    }
}
