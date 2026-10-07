// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Web.Api.Middlewares;
using XiHan.Framework.Web.Api.Security.OpenApi;

namespace XiHan.Framework.Web.Api.Tests.Security;

public sealed class OpenApiNonceTests
{
    [Fact]
    public async Task InvalidSignature_DoesNotReserveNonce()
    {
        var options = CreateOptions();
        var client = new OpenApiSecurityClient { AccessKey = "test-key", SecretKey = "test-secret" };
        var middleware = CreateMiddleware(options);
        var clientStore = new TestClientStore(client);
        var badSignatureContext = CreateContext("same-nonce", "invalid-signature");

        await middleware.InvokeAsync(badSignatureContext, clientStore);
        await middleware.InvokeAsync(CreateContext("same-nonce", "invalid-signature"), clientStore);

        Assert.Equal(StatusCodes.Status401Unauthorized, badSignatureContext.Response.StatusCode);
        var retryContext = CreateSignedContext("same-nonce", client.SecretKey);
        await middleware.InvokeAsync(retryContext, clientStore);

        Assert.Equal(StatusCodes.Status204NoContent, retryContext.Response.StatusCode);
    }

    [Fact]
    public async Task ValidSignature_ReplayIsRejectedAndEndpointRunsOnce()
    {
        var options = CreateOptions();
        var client = new OpenApiSecurityClient { AccessKey = "test-key", SecretKey = "test-secret" };
        var endpointCalls = 0;
        var middleware = CreateMiddleware(options, () => endpointCalls++);
        var clientStore = new TestClientStore(client);
        var nonce = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var firstContext = CreateSignedContext(nonce, client.SecretKey, timestamp);
        var replayContext = CreateContext(nonce, firstContext.Request.Headers[OpenApiSecurityConstants.SignatureHeaderName]!);
        replayContext.Request.Headers[OpenApiSecurityConstants.TimestampHeaderName] = timestamp;

        await Task.WhenAll(
            middleware.InvokeAsync(firstContext, clientStore),
            middleware.InvokeAsync(replayContext, clientStore));

        Assert.Contains(
            new[] { firstContext.Response.StatusCode, replayContext.Response.StatusCode },
            statusCode => statusCode == StatusCodes.Status204NoContent);
        Assert.Contains(
            new[] { firstContext.Response.StatusCode, replayContext.Response.StatusCode },
            statusCode => statusCode == StatusCodes.Status409Conflict);
        Assert.Equal(1, endpointCalls);
    }

    [Fact]
    public async Task NonceStoreCapacityExceeded_ReturnsServiceUnavailable()
    {
        var client = new OpenApiSecurityClient { AccessKey = "test-key", SecretKey = "test-secret" };
        var endpointCalls = 0;
        var middleware = CreateMiddleware(
            CreateOptions(),
            () => endpointCalls++,
            new FixedNonceStore(OpenApiNonceAcquisitionResult.CapacityExceeded));
        var context = CreateSignedContext(Guid.NewGuid().ToString("N"), client.SecretKey);

        await middleware.InvokeAsync(context, new TestClientStore(client));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal(0, endpointCalls);
    }

    private static XiHanOpenApiSecurityOptions CreateOptions()
        => new()
        {
            IsEnabled = true,
            RequireContentSignature = false,
            DefaultEncryptionAlgorithm = "NONE",
            ProtectedPathPrefixes = ["/api"],
            IgnoredPathPrefixes = []
        };

    private static XiHanOpenApiSecurityMiddleware CreateMiddleware(
        XiHanOpenApiSecurityOptions options,
        Action? onNext = null,
        IOpenApiReplayNonceStore? replayNonceStore = null)
        => new(
            context =>
            {
                onNext?.Invoke();
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            NullLogger<XiHanOpenApiSecurityMiddleware>.Instance,
            new StaticOptionsMonitor<XiHanOpenApiSecurityOptions>(options),
            replayNonceStore);

    private static DefaultHttpContext CreateContext(string nonce, string signature)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/test";
        context.Request.Body = new MemoryStream();
        context.Request.ContentLength = 0;
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        context.Request.Headers[OpenApiSecurityConstants.AccessKeyHeaderName] = "test-key";
        context.Request.Headers[OpenApiSecurityConstants.TimestampHeaderName] = timestamp;
        context.Request.Headers[OpenApiSecurityConstants.NonceHeaderName] = nonce;
        context.Request.Headers[OpenApiSecurityConstants.SignatureHeaderName] = signature;
        return context;
    }

    private static DefaultHttpContext CreateSignedContext(string nonce, string secret, string? requestTimestamp = null)
    {
        var timestamp = requestTimestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var bodyHash = Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant();
        var canonical = string.Join('\n', "POST", "/api/test", string.Empty, bodyHash, timestamp, nonce);
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical)));
        var context = CreateContext(nonce, signature);
        context.Request.Headers[OpenApiSecurityConstants.TimestampHeaderName] = timestamp;
        return context;
    }

    private sealed class TestClientStore(OpenApiSecurityClient client) : IOpenApiSecurityClientStore
    {
        public Task<OpenApiSecurityClient?> FindByAccessKeyAsync(string accessKey, CancellationToken cancellationToken = default)
            => Task.FromResult<OpenApiSecurityClient?>(accessKey == client.AccessKey ? client : null);
    }

    private sealed class FixedNonceStore(OpenApiNonceAcquisitionResult result) : IOpenApiReplayNonceStore
    {
        public Task<OpenApiNonceAcquisitionResult> TryAcquireAsync(
            string accessKey,
            string nonce,
            DateTimeOffset expiresAt,
            int maxEntries,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
