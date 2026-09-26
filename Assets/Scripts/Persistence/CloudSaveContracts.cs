using System;
using System.Net;

namespace BuildATower
{
    [Serializable]
    public sealed class CloudSaveConfig
    {
        public const string DefaultApiBaseUrl = "https://api.escapeproductions.biz";

        public CloudSaveConfig()
            : this(DefaultApiBaseUrl)
        {
        }

        public CloudSaveConfig(string apiBaseUrl)
        {
            ApiBaseUrl = string.IsNullOrWhiteSpace(apiBaseUrl)
                ? DefaultApiBaseUrl
                : apiBaseUrl.TrimEnd('/');
        }

        public string ApiBaseUrl { get; }
    }

    public enum CloudError
    {
        None,
        Offline,
        Unauthorized,
        MissingCredentials,
        InvalidResponse,
        HttpError
    }

    public class CloudResult
    {
        protected CloudResult(CloudError error, string errorMessage)
        {
            Error = error;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool Success => Error == CloudError.None;
        public CloudError Error { get; }
        public string ErrorMessage { get; }

        public static CloudResult Succeeded()
        {
            return new CloudResult(CloudError.None, string.Empty);
        }

        public static CloudResult Failed(CloudError error, string errorMessage)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed cloud result must have an error.", nameof(error));

            return new CloudResult(error, errorMessage);
        }
    }

    public sealed class CloudAuthResult : CloudResult
    {
        CloudAuthResult(
            CloudError error,
            string errorMessage,
            string accountId,
            string accessToken,
            int expiresIn)
            : base(error, errorMessage)
        {
            AccountId = accountId;
            AccessToken = accessToken;
            ExpiresIn = expiresIn;
        }

        public string AccountId { get; }
        public string AccessToken { get; }
        public int ExpiresIn { get; }

        public static CloudAuthResult Succeeded(string accountId, string accessToken, int expiresIn)
        {
            return new CloudAuthResult(CloudError.None, string.Empty, accountId, accessToken, expiresIn);
        }

        public new static CloudAuthResult Failed(CloudError error, string errorMessage)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed auth result must have an error.", nameof(error));

            return new CloudAuthResult(error, errorMessage, null, null, 0);
        }
    }

    public sealed class CloudRegisterResult : CloudResult
    {
        CloudRegisterResult(
            CloudError error,
            string errorMessage,
            string userId,
            string email,
            bool emailConfirmed)
            : base(error, errorMessage)
        {
            UserId = userId;
            Email = email;
            EmailConfirmed = emailConfirmed;
        }

        public string UserId { get; }
        public string Email { get; }
        public bool EmailConfirmed { get; }

        public static CloudRegisterResult Succeeded(string userId, string email, bool emailConfirmed)
        {
            return new CloudRegisterResult(
                CloudError.None,
                string.Empty,
                userId,
                email,
                emailConfirmed);
        }

        public new static CloudRegisterResult Failed(CloudError error, string errorMessage)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed register result must have an error.", nameof(error));

            return new CloudRegisterResult(error, errorMessage, null, null, false);
        }
    }

    public sealed class CloudHttpResult : CloudResult
    {
        CloudHttpResult(CloudError error, string errorMessage, HttpStatusCode statusCode, string body)
            : base(error, errorMessage)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        public HttpStatusCode StatusCode { get; }
        public string Body { get; }

        public static CloudHttpResult Succeeded(HttpStatusCode statusCode, string body)
        {
            return new CloudHttpResult(CloudError.None, string.Empty, statusCode, body);
        }

        public new static CloudHttpResult Failed(CloudError error, string errorMessage)
        {
            if (error == CloudError.None)
                throw new ArgumentException("A failed HTTP result must have an error.", nameof(error));

            return new CloudHttpResult(error, errorMessage, 0, string.Empty);
        }
    }
}
