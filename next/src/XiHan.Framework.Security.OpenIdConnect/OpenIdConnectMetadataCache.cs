// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.OpenIdConnect;

/// <summary>
/// 签发者元数据的缓存：首次按需取发现文档与 JWKS，按 <see cref="OpenIdConnectOptions.RefreshInterval"/> 自动刷新，
/// 未知签名密钥时按需刷新但不快于 <see cref="OpenIdConnectOptions.MinimumRefreshInterval"/>；刷新失败沿用最后一份成功结果。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class OpenIdConnectMetadataCache : IDisposable
{
    /// <summary>
    /// 尚无任何元数据时，两次失败尝试之间的间隔；避免签发者故障期间每次认证都发起请求。
    /// </summary>
    public static readonly TimeSpan FailureRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly OpenIdConnectOptions _options;

    /// <summary>
    /// 取元数据的 HTTP 客户端。
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 发现文档地址。
    /// </summary>
    private readonly Uri _address;

    /// <summary>
    /// 同一时刻只允许一次取数；并发的调用方等待同一次结果。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 保护按需刷新时间戳的锁。
    /// </summary>
    private readonly Lock _refreshLock = new();

    /// <summary>
    /// 最后一份成功取得的元数据。
    /// </summary>
    private volatile OpenIdConnectMetadata? _current;

    /// <summary>
    /// 此刻之后才再次自动取数。
    /// </summary>
    private DateTimeOffset _syncAfter = DateTimeOffset.MinValue;

    /// <summary>
    /// 上一次按需刷新的时刻；空表示还没有过。
    /// </summary>
    private DateTimeOffset? _lastRequestedRefresh;

    /// <summary>
    /// 创建；<paramref name="http"/> 的生命周期由调用方掌握。
    /// </summary>
    public OpenIdConnectMetadataCache(OpenIdConnectOptions options, HttpClient http, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<OpenIdConnectMetadataCache>();
        _address = options.ResolveMetadataAddress();
    }

    /// <summary>
    /// 最后一份成功取得的元数据；尚未取得为空。
    /// </summary>
    public OpenIdConnectMetadata? Current => _current;

    /// <summary>
    /// 取元数据：缓存未到期直接返回；否则取数，失败时有缓存就沿用缓存，没有缓存返回 <c>SECURITY_UNAVAILABLE_003</c>。
    /// </summary>
    public ValueTask<Result<OpenIdConnectMetadata>> GetAsync(CancellationToken cancellationToken)
    {
        var current = _current;
        return current is not null && _clock.GetUtcNow() < _syncAfter
            ? ValueTask.FromResult(Result.Success(current))
            : new ValueTask<Result<OpenIdConnectMetadata>>(FetchAsync(force: false, cancellationToken));
    }

    /// <summary>
    /// 按需刷新（签名密钥未知时调用）：距上次按需刷新不足 <see cref="OpenIdConnectOptions.MinimumRefreshInterval"/> 时不取数，直接返回当前结果。
    /// </summary>
    public ValueTask<Result<OpenIdConnectMetadata>> RefreshAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        lock (_refreshLock)
        {
            // 未知 kid 可由任意调用方用伪造令牌触发；最短间隔把刷新频率限制在签发者可承受的范围
            if (_lastRequestedRefresh is { } last && now < last + _options.MinimumRefreshInterval)
            {
                var current = _current;
                return ValueTask.FromResult(current is not null ? Result.Success(current) : Unavailable());
            }

            _lastRequestedRefresh = now;
        }

        return new ValueTask<Result<OpenIdConnectMetadata>>(FetchAsync(force: true, cancellationToken));
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    /// <summary>
    /// 为选项创建默认 HTTP 客户端：连接池 5 分钟轮换，超时取 <see cref="OpenIdConnectOptions.BackchannelTimeout"/>。
    /// </summary>
    internal static HttpClient CreateHttpClient(OpenIdConnectOptions options)
        => new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = options.BackchannelTimeout };

    /// <summary>
    /// 在门内取数；<paramref name="force"/> 为假时先复查别的调用方是否已经取好。
    /// </summary>
    private async Task<Result<OpenIdConnectMetadata>> FetchAsync(bool force, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _current;
            var now = _clock.GetUtcNow();
            if (!force && current is not null && now < _syncAfter)
            {
                return Result.Success(current);
            }

            if (current is null && now < _syncAfter)
            {
                // 刚失败过且无缓存：退避期内直接报不可用，不再打签发者
                return Unavailable();
            }

            try
            {
                var metadata = await RetrieveAsync(cancellationToken).ConfigureAwait(false);
                _current = metadata;
                _syncAfter = _clock.GetUtcNow() + _options.RefreshInterval;
                LogLoaded(_logger, _address, metadata.Issuer, metadata.SigningKeys.Count);
                return Result.Success(metadata);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or ArgumentException or InvalidOperationException)
            {
                // 有缓存：沿用旧密钥，最短刷新间隔后再试；无缓存：短退避后再试
                if (current is not null)
                {
                    _syncAfter = _clock.GetUtcNow() + _options.MinimumRefreshInterval;
                    LogRefreshFailed(_logger, exception, _address, current.RetrievedAt);
                    return Result.Success(current);
                }

                _syncAfter = _clock.GetUtcNow() + FailureRetryDelay;
                LogLoadFailed(_logger, exception, _address);
                return Unavailable();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 取发现文档，再取其 <c>jwks_uri</c> 指向的密钥集。
    /// </summary>
    private async Task<OpenIdConnectMetadata> RetrieveAsync(CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await _http.GetStringAsync(_address, cancellationToken).ConfigureAwait(false));
        var issuer = document.RootElement.TryGetProperty("issuer", out var issuerElement) && issuerElement.ValueKind == JsonValueKind.String && issuerElement.GetString() is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"The discovery document at '{_address}' has no 'issuer'.");
        var jwksUri = document.RootElement.TryGetProperty("jwks_uri", out var jwksElement) && jwksElement.ValueKind == JsonValueKind.String && Uri.TryCreate(jwksElement.GetString(), UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException($"The discovery document at '{_address}' has no absolute 'jwks_uri'.");
        if (_options.RequireHttpsMetadata && jwksUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"The JWKS address '{jwksUri}' must use HTTPS.");
        }

        var keys = new JsonWebKeySet(await _http.GetStringAsync(jwksUri, cancellationToken).ConfigureAwait(false)).GetSigningKeys().ToArray<SecurityKey>();
        return keys.Length > 0
            ? new OpenIdConnectMetadata(issuer, jwksUri, keys, _clock.GetUtcNow())
            : throw new InvalidOperationException($"The JWKS at '{jwksUri}' contains no signing keys.");
    }

    /// <summary>
    /// 签发者不可用的失败结果。
    /// </summary>
    private Result<OpenIdConnectMetadata> Unavailable()
        => Result.Failure<OpenIdConnectMetadata>(Error.Unavailable(SecurityErrors.IssuerUnavailable, SecurityMessages.IssuerUnavailable, MessageArguments.Of(("authority", _options.Authority))));

    /// <summary>
    /// 记录元数据装载成功。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded OpenID Connect metadata from '{Address}': issuer '{Issuer}', {KeyCount} signing keys.")]
    private static partial void LogLoaded(ILogger logger, Uri address, string issuer, int keyCount);

    /// <summary>
    /// 记录刷新失败但沿用旧密钥。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to refresh OpenID Connect metadata from '{Address}'; keeping the signing keys retrieved at {RetrievedAt:O}.")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception, Uri address, DateTimeOffset retrievedAt);

    /// <summary>
    /// 记录首次装载失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load OpenID Connect metadata from '{Address}'; tokens cannot be validated until the issuer is reachable.")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception, Uri address);
}
