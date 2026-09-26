using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BuildATower
{
    public sealed class AccountClient
    {
        readonly CloudSaveConfig config;
        readonly ICredentialStore credentials;
        readonly HttpClient http;
        string accessToken;

        public AccountClient(CloudSaveConfig config, ICredentialStore credentials)
            : this(config, credentials, null)
        {
        }

        public AccountClient(CloudSaveConfig config, ICredentialStore credentials, HttpClient http)
        {
            this.config = config ?? new CloudSaveConfig();
            this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            this.http = http ?? new HttpClient();
            if (this.http.BaseAddress == null)
                this.http.BaseAddress = new Uri(this.config.ApiBaseUrl);
        }

        public Task<CloudRegisterResult> Register(
            string email,
            string password,
            string inviteCode = null,
            CancellationToken cancellationToken = default)
        {
            return PostRegister(
                "/v1/auth/register",
                new RegisterRequestDto
                {
                    email = email,
                    password = password,
                    inviteCode = inviteCode
                },
                cancellationToken);
        }

        public Task<CloudAuthResult> Login(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            return PostAuth(
                "/v1/auth/login",
                new LoginRequestDto
                {
                    email = email,
                    password = password
                },
                cancellationToken);
        }

        public Task<CloudResult> Verify(string email, string token, CancellationToken cancellationToken = default)
        {
            return PostStatus(
                "/v1/auth/verify",
                new VerifyEmailRequestDto
                {
                    email = email,
                    token = token
                },
                cancellationToken);
        }

        public Task<CloudResult> Forgot(string email, CancellationToken cancellationToken = default)
        {
            return PostStatus(
                "/v1/auth/forgot",
                new ForgotPasswordRequestDto
                {
                    email = email
                },
                cancellationToken);
        }

        public Task<CloudResult> Reset(
            string email,
            string token,
            string newPassword,
            CancellationToken cancellationToken = default)
        {
            return PostStatus(
                "/v1/auth/reset",
                new ResetPasswordRequestDto
                {
                    email = email,
                    token = token,
                    newPassword = newPassword
                },
                cancellationToken);
        }

        public async Task<CloudResult> Logout(CancellationToken cancellationToken = default)
        {
            var accountId = GameSession.CurrentAccountId;
            string refreshToken = null;
            var hadRefresh = !string.IsNullOrEmpty(accountId)
                             && credentials.TryLoadRefresh(accountId, out refreshToken);

            CloudResult result = CloudResult.Succeeded();
            if (hadRefresh)
            {
                result = await PostStatus(
                    "/v1/auth/logout",
                    new RefreshRequestDto { refreshToken = refreshToken },
                    cancellationToken);
            }

            if (!string.IsNullOrEmpty(accountId))
                credentials.DeleteRefresh(accountId);

            accessToken = null;
            GameSession.ClearCurrentAccount();
            return result;
        }

        public async Task<CloudAuthResult> TryRefresh(CancellationToken cancellationToken = default)
        {
            var accountId = GameSession.CurrentAccountId;
            if (string.IsNullOrEmpty(accountId))
                return CloudAuthResult.Failed(CloudError.MissingCredentials, "No cloud account is signed in.");

            if (!credentials.TryLoadRefresh(accountId, out var refreshToken))
                return CloudAuthResult.Failed(CloudError.MissingCredentials, "No cloud refresh credential is stored.");

            return await PostAuth(
                "/v1/auth/refresh",
                new RefreshRequestDto { refreshToken = refreshToken },
                cancellationToken);
        }

        public async Task<CloudHttpResult> SendAuthorized(
            Func<HttpRequestMessage> requestFactory,
            CancellationToken cancellationToken = default)
        {
            if (requestFactory == null)
                throw new ArgumentNullException(nameof(requestFactory));

            if (string.IsNullOrEmpty(accessToken))
            {
                var refresh = await TryRefresh(cancellationToken);
                if (!refresh.Success)
                    return CloudHttpResult.Failed(refresh.Error, refresh.ErrorMessage);
            }

            var response = await SendWithBearer(requestFactory(), cancellationToken);
            if (response.Error == CloudError.Unauthorized)
            {
                var refresh = await TryRefresh(cancellationToken);
                if (!refresh.Success)
                    return CloudHttpResult.Failed(refresh.Error, refresh.ErrorMessage);

                response = await SendWithBearer(requestFactory(), cancellationToken);
            }

            return response;
        }

        async Task<CloudRegisterResult> PostRegister<TRequest>(
            string path,
            TRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                using (var response = await http.PostAsync(path, JsonContent(request), cancellationToken))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        return CloudRegisterResult.Failed(ToCloudError(response.StatusCode), ErrorMessage(response, body));

                    var dto = JsonUtility.FromJson<RegisterResponseDto>(body);
                    if (dto == null || string.IsNullOrEmpty(dto.userId))
                        return CloudRegisterResult.Failed(CloudError.InvalidResponse, "Register response was missing the user id.");

                    return CloudRegisterResult.Succeeded(dto.userId, dto.email, dto.emailConfirmed);
                }
            }
            catch (HttpRequestException)
            {
                return CloudRegisterResult.Failed(CloudError.Offline, "Cloud save API is unavailable.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CloudRegisterResult.Failed(CloudError.Offline, "Cloud save API request timed out.");
            }
        }

        async Task<CloudAuthResult> PostAuth<TRequest>(
            string path,
            TRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                using (var response = await http.PostAsync(path, JsonContent(request), cancellationToken))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        return CloudAuthResult.Failed(ToCloudError(response.StatusCode), ErrorMessage(response, body));

                    var dto = JsonUtility.FromJson<AuthResponseDto>(body);
                    if (dto == null
                        || string.IsNullOrEmpty(dto.accessToken)
                        || string.IsNullOrEmpty(dto.refreshToken))
                    {
                        return CloudAuthResult.Failed(CloudError.InvalidResponse, "Auth response was missing tokens.");
                    }

                    if (!TryReadAccountId(dto.accessToken, out var accountId))
                        return CloudAuthResult.Failed(CloudError.InvalidResponse, "Access token was missing the account id.");

                    accessToken = dto.accessToken;
                    GameSession.SetCurrentAccountId(accountId);
                    credentials.SaveRefresh(accountId, dto.refreshToken);
                    return CloudAuthResult.Succeeded(accountId, dto.accessToken, dto.expiresIn);
                }
            }
            catch (HttpRequestException)
            {
                return CloudAuthResult.Failed(CloudError.Offline, "Cloud save API is unavailable.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CloudAuthResult.Failed(CloudError.Offline, "Cloud save API request timed out.");
            }
        }

        async Task<CloudResult> PostStatus<TRequest>(
            string path,
            TRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                using (var response = await http.PostAsync(path, JsonContent(request), cancellationToken))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                        return CloudResult.Succeeded();

                    return CloudResult.Failed(ToCloudError(response.StatusCode), ErrorMessage(response, body));
                }
            }
            catch (HttpRequestException)
            {
                return CloudResult.Failed(CloudError.Offline, "Cloud save API is unavailable.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CloudResult.Failed(CloudError.Offline, "Cloud save API request timed out.");
            }
        }

        async Task<CloudHttpResult> SendWithBearer(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using (var response = await http.SendAsync(request, cancellationToken))
                {
                    var body = response.Content == null
                        ? string.Empty
                        : await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                        return CloudHttpResult.Succeeded(response.StatusCode, body);

                    return CloudHttpResult.Failed(
                        ToCloudError(response.StatusCode),
                        ErrorMessage(response, body),
                        response.StatusCode,
                        body);
                }
            }
            catch (HttpRequestException)
            {
                return CloudHttpResult.Failed(CloudError.Offline, "Cloud save API is unavailable.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CloudHttpResult.Failed(CloudError.Offline, "Cloud save API request timed out.");
            }
            finally
            {
                request.Dispose();
            }
        }

        static StringContent JsonContent<TRequest>(TRequest request)
        {
            return new StringContent(JsonUtility.ToJson(request), Encoding.UTF8, "application/json");
        }

        static CloudError ToCloudError(HttpStatusCode statusCode)
        {
            if (statusCode == HttpStatusCode.Unauthorized)
                return CloudError.Unauthorized;

            return CloudError.HttpError;
        }

        static string ErrorMessage(HttpResponseMessage response, string body)
        {
            var code = string.Empty;
            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    code = JsonUtility.FromJson<CloudErrorResponseDto>(body)?.code;
                }
                catch (ArgumentException)
                {
                    code = string.Empty;
                }
            }

            return string.IsNullOrEmpty(code)
                ? "Cloud save API returned HTTP " + (int)response.StatusCode + "."
                : "Cloud save API returned " + code + ".";
        }

        static bool TryReadAccountId(string jwt, out string accountId)
        {
            accountId = null;
            var parts = jwt.Split('.');
            if (parts.Length < 2)
                return false;

            try
            {
                var payloadBytes = Base64UrlDecode(parts[1]);
                var payloadJson = Encoding.UTF8.GetString(payloadBytes);
                var claims = JsonUtility.FromJson<JwtClaimsDto>(payloadJson);
                if (string.IsNullOrEmpty(claims?.sub))
                    return false;

                accountId = claims.sub;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static byte[] Base64UrlDecode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
            }

            return Convert.FromBase64String(padded);
        }

        [Serializable]
        sealed class RegisterRequestDto
        {
            public string email;
            public string password;
            public string inviteCode;
        }

        [Serializable]
        sealed class LoginRequestDto
        {
            public string email;
            public string password;
        }

        [Serializable]
        sealed class RefreshRequestDto
        {
            public string refreshToken;
        }

        [Serializable]
        sealed class VerifyEmailRequestDto
        {
            public string email;
            public string token;
        }

        [Serializable]
        sealed class ForgotPasswordRequestDto
        {
            public string email;
        }

        [Serializable]
        sealed class ResetPasswordRequestDto
        {
            public string email;
            public string token;
            public string newPassword;
        }

        [Serializable]
        sealed class RegisterResponseDto
        {
            public string userId;
            public string email;
            public bool emailConfirmed;
        }

        [Serializable]
        sealed class AuthResponseDto
        {
            public string accessToken;
            public string refreshToken;
            public int expiresIn;
        }

        [Serializable]
        sealed class CloudErrorResponseDto
        {
            public string code;
        }

        [Serializable]
        sealed class JwtClaimsDto
        {
            public string sub;
        }
    }
}
