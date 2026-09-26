using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public sealed class AccountClientTests
    {
        const string AccountId = "account-123";
        const string RefreshToken = "refresh-token-1";
        const string RotatedRefreshToken = "refresh-token-2";
        static readonly string LoginAccessToken = FakeJwt(AccountId, "login");
        static readonly string RefreshAccessToken = FakeJwt(AccountId, "refresh");

        MemoryCredentialStore credentials;

        [SetUp]
        public void SetUp()
        {
            GameSession.ResetForTests();
            credentials = new MemoryCredentialStore();
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.ResetForTests();
        }

        [Test]
        public async Task Login_posts_camel_case_json_stores_refresh_and_sets_current_account()
        {
            var handler = new StubHandler(request =>
            {
                Assert.AreEqual(HttpMethod.Post, request.Method);
                Assert.AreEqual("/v1/auth/login", request.RequestUri.AbsolutePath);
                var body = ReadBody(request);
                StringAssert.Contains("\"email\":\"player@example.com\"", body);
                StringAssert.Contains("\"password\":\"correct horse battery staple\"", body);

                return Json(HttpStatusCode.OK, AuthJson(LoginAccessToken, RefreshToken));
            });
            var client = CreateClient(handler);

            var result = await client.Login(
                "player@example.com",
                "correct horse battery staple");

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(AccountId, GameSession.CurrentAccountId);
            Assert.AreEqual(RefreshToken, credentials.Get(AccountId));
        }

        [Test]
        public async Task Logout_revokes_refresh_and_clears_session_and_credentials_without_deleting_local_files()
        {
            var saveRoot = Path.Combine(Path.GetTempPath(), "bat-account-client-" + Guid.NewGuid().ToString("N"));
            var saveFile = Path.Combine(saveRoot, "tower-a.batsave");
            Directory.CreateDirectory(saveRoot);
            File.WriteAllText(saveFile, "local save must stay");
            GameSession.SetCurrentAccountId(AccountId);
            credentials.SaveRefresh(AccountId, RefreshToken);
            var handler = new StubHandler(request =>
            {
                Assert.AreEqual(HttpMethod.Post, request.Method);
                Assert.AreEqual("/v1/auth/logout", request.RequestUri.AbsolutePath);
                StringAssert.Contains("\"refreshToken\":\"" + RefreshToken + "\"", ReadBody(request));
                return Json(HttpStatusCode.OK, "{\"status\":\"ok\"}");
            });
            var client = CreateClient(handler);

            try
            {
                var result = await client.Logout();

                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.IsNull(GameSession.CurrentAccountId);
                Assert.IsFalse(credentials.TryLoadRefresh(AccountId, out _));
                Assert.IsTrue(File.Exists(saveFile), "Logout must not delete local .batsave files.");
            }
            finally
            {
                if (Directory.Exists(saveRoot))
                    Directory.Delete(saveRoot, true);
            }
        }

        [Test]
        public async Task Authorized_request_that_gets_401_refreshes_once_and_retries_with_new_access_token()
        {
            var protectedAttempts = 0;
            var refreshAttempts = 0;
            var handler = new StubHandler(request =>
            {
                if (request.RequestUri.AbsolutePath == "/v1/auth/login")
                    return Json(HttpStatusCode.OK, AuthJson(LoginAccessToken, RefreshToken));

                if (request.RequestUri.AbsolutePath == "/v1/auth/refresh")
                {
                    refreshAttempts++;
                    StringAssert.Contains("\"refreshToken\":\"" + RefreshToken + "\"", ReadBody(request));
                    return Json(HttpStatusCode.OK, AuthJson(RefreshAccessToken, RotatedRefreshToken));
                }

                Assert.AreEqual("/v1/protected", request.RequestUri.AbsolutePath);
                protectedAttempts++;
                if (protectedAttempts == 1)
                {
                    Assert.AreEqual("Bearer", request.Headers.Authorization.Scheme);
                    Assert.AreEqual(LoginAccessToken, request.Headers.Authorization.Parameter);
                    return Json(HttpStatusCode.Unauthorized, "{\"code\":\"invalid_credentials\"}");
                }

                Assert.AreEqual("Bearer", request.Headers.Authorization.Scheme);
                Assert.AreEqual(RefreshAccessToken, request.Headers.Authorization.Parameter);
                return Json(HttpStatusCode.OK, "{\"status\":\"ok\"}");
            });
            var client = CreateClient(handler);
            Assert.IsTrue((await client.Login("player@example.com", "password")).Success);

            var response = await client.SendAuthorized(() => new HttpRequestMessage(HttpMethod.Get, "/v1/protected"));

            Assert.IsTrue(response.Success, response.ErrorMessage);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(2, protectedAttempts);
            Assert.AreEqual(1, refreshAttempts);
            Assert.AreEqual(RotatedRefreshToken, credentials.Get(AccountId));
        }

        [Test]
        public async Task Login_network_failure_returns_typed_offline_error()
        {
            var client = CreateClient(new StubHandler(_ => throw new HttpRequestException("No route to host.")));

            var result = await client.Login("player@example.com", "password");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CloudError.Offline, result.Error);
        }

        [Test]
        public async Task List_slots_gets_authorized_cloud_slot_summaries()
        {
            var handler = new StubHandler(request =>
            {
                if (request.RequestUri.AbsolutePath == "/v1/auth/login")
                    return Json(HttpStatusCode.OK, AuthJson(LoginAccessToken, RefreshToken));

                Assert.AreEqual(HttpMethod.Get, request.Method);
                Assert.AreEqual("/v1/saves", request.RequestUri.AbsolutePath);
                Assert.AreEqual(LoginAccessToken, request.Headers.Authorization.Parameter);
                return Json(
                    HttpStatusCode.OK,
                    "{\"slots\":[{\"slotId\":1,\"occupied\":true,\"revision\":7,\"towerName\":\"Cloud Harbor\",\"modifiedUtc\":\"2026-09-26T16:00:00Z\",\"deviceName\":\"Studio PC\",\"playMinutes\":42},{\"slotId\":2,\"occupied\":false}]}");
            });
            var client = CreateClient(handler);
            Assert.IsTrue((await client.Login("player@example.com", "password")).Success);

            var result = await client.ListSlots();

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(2, result.Slots.Count);
            Assert.IsTrue(result.Slots[0].Occupied);
            Assert.AreEqual(1, result.Slots[0].SlotId);
            Assert.AreEqual("Cloud Harbor", result.Slots[0].TowerName);
            Assert.AreEqual(7, result.Slots[0].Revision);
            Assert.AreEqual("Studio PC", result.Slots[0].DeviceName);
            Assert.IsFalse(result.Slots[1].Occupied);
        }

        AccountClient CreateClient(HttpMessageHandler handler)
        {
            return new AccountClient(
                new CloudSaveConfig("https://api.test"),
                credentials,
                new HttpClient(handler)
                {
                    BaseAddress = new Uri("https://api.test")
                });
        }

        static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        static string AuthJson(string accessToken, string refreshToken)
        {
            return "{\"accessToken\":\"" + accessToken + "\",\"refreshToken\":\"" + refreshToken + "\",\"expiresIn\":900}";
        }

        static string FakeJwt(string accountId, string jwtId)
        {
            return Base64Url("{\"alg\":\"none\"}")
                   + "."
                   + Base64Url("{\"sub\":\"" + accountId + "\",\"jti\":\"" + jwtId + "\"}")
                   + ".signature";
        }

        static string Base64Url(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        static string ReadBody(HttpRequestMessage request)
        {
            return request.Content == null
                ? string.Empty
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        sealed class StubHandler : HttpMessageHandler
        {
            readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                this.responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(responder(request));
            }
        }

        sealed class MemoryCredentialStore : ICredentialStore
        {
            readonly Dictionary<string, string> refreshTokens = new Dictionary<string, string>();

            public void SaveRefresh(string accountId, string refreshToken)
            {
                refreshTokens[accountId] = refreshToken;
            }

            public bool TryLoadRefresh(string accountId, out string refreshToken)
            {
                return refreshTokens.TryGetValue(accountId, out refreshToken);
            }

            public void DeleteRefresh(string accountId)
            {
                refreshTokens.Remove(accountId);
            }

            public string Get(string accountId)
            {
                return refreshTokens[accountId];
            }
        }
    }
}
