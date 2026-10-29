// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Configuration.Abstractions;
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Nacos;

/// <summary>
/// Nacos 远程配置客户端。
/// </summary>
/// <remarks>
/// 命名空间即 <c>dataId</c>，版本是服务端给出的内容 MD5（响应头 <c>Content-MD5</c>）；<c>knownVersion</c> 非空时先用它长轮询监听，服务端报告变化才取整份文档。直接用 Nacos 的 HTTP 配置接口（v1，Nacos 2.x 兼容），有用户名密码时先登录取令牌并在到期前续；多个地址按序尝试。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class NacosConfigurationClient : IRemoteConfigurationClient, IDisposable
{
    /// <summary>
    /// 宿主里登记的 <c>HttpClient</c> 名。
    /// </summary>
    public const string HttpClientName = "xihan.configuration.nacos";

    /// <summary>
    /// 取配置的接口。
    /// </summary>
    private const string ConfigsPath = "/nacos/v1/cs/configs";

    /// <summary>
    /// 长轮询监听的接口。
    /// </summary>
    private const string ListenerPath = "/nacos/v1/cs/configs/listener";

    /// <summary>
    /// 登录接口。
    /// </summary>
    private const string LoginPath = "/nacos/v1/auth/login";

    /// <summary>
    /// 令牌提前续期的余量。
    /// </summary>
    private static readonly TimeSpan _tokenRenewMargin = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly NacosOptions _options;

    /// <summary>
    /// HTTP 客户端。
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 是否由本实例创建并在释放时关闭 HTTP 客户端。
    /// </summary>
    private readonly bool _ownsHttp;

    /// <summary>
    /// 保护登录的锁。
    /// </summary>
    private readonly SemaphoreSlim _loginGate = new(1, 1);

    /// <summary>
    /// 当前令牌。
    /// </summary>
    private string? _token;

    /// <summary>
    /// 令牌到期时刻。
    /// </summary>
    private DateTimeOffset _tokenExpiresAt;

    /// <summary>
    /// 创建（HTTP 客户端由外部持有，超时应为无限，由本类按请求控制）。
    /// </summary>
    public NacosConfigurationClient(NacosOptions options, HttpClient httpClient, TimeProvider clock)
        : this(options, httpClient, clock, ownsHttpClient: false)
    {
    }

    private NacosConfigurationClient(NacosOptions options, HttpClient httpClient, TimeProvider clock, bool ownsHttpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _ownsHttp = ownsHttpClient;
    }

    /// <summary>
    /// 自建 HTTP 客户端创建（配置构建期没有容器时用），释放时一并关闭。
    /// </summary>
    public static NacosConfigurationClient Create(NacosOptions options, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new NacosConfigurationClient(options, new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, clock ?? TimeProvider.System, ownsHttpClient: true);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<ConfigurationDocument>>> FetchAsync(string namespaceName, string? knownVersion, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);
        try
        {
            // 已知版本：先长轮询，窗口到期或被取消都当作未变化
            if (knownVersion is not null && !await ListenAsync(namespaceName, knownVersion, cancellationToken).ConfigureAwait(false))
            {
                return Result.Success(Maybe.None<ConfigurationDocument>());
            }

            var fetched = await GetContentAsync(namespaceName, cancellationToken).ConfigureAwait(false);
            if (!fetched.TryGetValue(out var content))
            {
                return Error.NotFound(ConfigurationErrors.NamespaceNotFound, ConfigurationMessages.NamespaceNotFound, MessageArguments.Of(("namespace", namespaceName)));
            }

            var version = content.Version;
            if (version == knownVersion)
            {
                return Result.Success(Maybe.None<ConfigurationDocument>());
            }

            IReadOnlyDictionary<string, string?> values;
            try
            {
                values = NacosDocumentParser.Parse(content.Text, _options.Format);
            }
            catch (FormatException exception)
            {
                return Error.Validation(ConfigurationErrors.DocumentInvalid, ConfigurationMessages.DocumentInvalid, MessageArguments.Of(("namespace", namespaceName), ("format", _options.Format.ToString()), ("reason", exception.Message)));
            }

            return Result.Success(Maybe.Some(new ConfigurationDocument(version, values)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Success(Maybe.None<ConfigurationDocument>());
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            // 超时表现为 OperationCanceledException（不是调用方的取消），与连接失败一样是不可达
            return Error.Unavailable(ConfigurationErrors.RemoteUnavailable, ConfigurationMessages.RemoteUnavailable, MessageArguments.Of(("namespace", namespaceName), ("reason", exception.Message)));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _loginGate.Dispose();
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    /// <summary>
    /// 取整份内容。
    /// </summary>
    /// <remarks>
    /// 不存在为无值。版本取响应头 <c>Content-MD5</c>（Nacos 1.x / 2.x 总是给出）；没有时退到内容的 SHA-256，此时监听接口认不出这个版本、每个窗口都会报告变化，退化成普通轮询但仍然正确。
    /// </remarks>
    private async Task<Maybe<(string Text, string Version)>> GetContentAsync(string dataId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(address => new HttpRequestMessage(HttpMethod.Get, ConfigsUri(address, dataId)), _options.RequestTimeout, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Maybe.None<(string, string)>();
        }

        await EnsureSuccessAsync(response, "fetch", cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var version = response.Headers.TryGetValues("Content-MD5", out var md5) ? md5.FirstOrDefault() : null;
        return Maybe.Some((text, string.IsNullOrWhiteSpace(version) ? Fingerprint(text) : version));
    }

    /// <summary>
    /// 长轮询监听：服务端在窗口内发现 MD5 变化就返回变化的条目，否则窗口到期返回空。
    /// </summary>
    private async Task<bool> ListenAsync(string dataId, string knownVersion, CancellationToken cancellationToken)
    {
        var listening = _options.Tenant.Length == 0
            ? $"{dataId}\u0002{_options.Group}\u0002{knownVersion}\u0001"
            : $"{dataId}\u0002{_options.Group}\u0002{knownVersion}\u0002{_options.Tenant}\u0001";
        var timeout = _options.LongPollTimeout + _options.RequestTimeout;
        using var response = await SendAsync(address =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, WithToken(new UriBuilder(address) { Path = ListenerPath }).Uri)
            {
                Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("Listening-Configs", listening)]),
            };
            request.Headers.TryAddWithoutValidation("Long-Pulling-Timeout", ((long)_options.LongPollTimeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
            return request;
        }, timeout, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "listen", cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(body);
    }

    /// <summary>
    /// 按地址顺序发送。
    /// </summary>
    /// <remarks>
    /// 连接失败换下一个地址，全部失败抛出最后一个异常；每次请求各自计超时。
    /// </remarks>
    private async Task<HttpResponseMessage> SendAsync(Func<Uri, HttpRequestMessage> build, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await EnsureTokenAsync(cancellationToken).ConfigureAwait(false);
        HttpRequestException? last = null;
        foreach (var address in _options.ServerAddresses)
        {
            using var request = build(new Uri(address));
            using var window = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            window.CancelAfter(timeout);
            try
            {
                return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, window.Token).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                last = exception;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 本地址超时：换下一个
                last = new HttpRequestException($"The request to '{address}' timed out after {timeout}.");
            }
        }

        throw last ?? new HttpRequestException("No Nacos server address is configured.");
    }

    /// <summary>
    /// 配置了用户名密码时取令牌，到期前一分钟续。
    /// </summary>
    private async Task EnsureTokenAsync(CancellationToken cancellationToken)
    {
        if (_options.Username is null || _options.Password is null)
        {
            return;
        }

        if (_token is not null && _clock.GetUtcNow() < _tokenExpiresAt - _tokenRenewMargin)
        {
            return;
        }

        await _loginGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 双检：等锁期间别的调用可能已经续好了
            if (_token is not null && _clock.GetUtcNow() < _tokenExpiresAt - _tokenRenewMargin)
            {
                return;
            }

            HttpRequestException? last = null;
            foreach (var address in _options.ServerAddresses)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new UriBuilder(address) { Path = LoginPath }.Uri)
                {
                    Content = new FormUrlEncodedContent([new("username", _options.Username), new("password", _options.Password)]),
                };
                using var window = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                window.CancelAfter(_options.RequestTimeout);
                try
                {
                    using var response = await _http.SendAsync(request, window.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"Logging in to Nacos at '{address}' failed with status {(int)response.StatusCode}.");
                    }

                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(window.Token).ConfigureAwait(false));
                    var token = json.RootElement.TryGetProperty("accessToken", out var tokenElement) ? tokenElement.GetString() : null;
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        throw new HttpRequestException($"Logging in to Nacos at '{address}' returned no access token.");
                    }

                    var ttl = json.RootElement.TryGetProperty("tokenTtl", out var ttlElement) && ttlElement.TryGetInt64(out var seconds) ? seconds : 18_000;
                    _token = token;
                    _tokenExpiresAt = _clock.GetUtcNow().AddSeconds(ttl);
                    return;
                }
                catch (HttpRequestException exception)
                {
                    last = exception;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    last = new HttpRequestException($"Logging in to Nacos at '{address}' timed out.");
                }
            }

            throw last ?? new HttpRequestException("No Nacos server address is configured.");
        }
        finally
        {
            _loginGate.Release();
        }
    }

    /// <summary>
    /// 非 2xx（404 除外，由调用方处理）视为不可达，把状态码与响应正文带进原因。
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException($"Nacos {action} failed with status {(int)response.StatusCode}: {body}");
    }

    /// <summary>
    /// 取配置的地址：<c>dataId</c>、<c>group</c>、可选的 <c>tenant</c> 与令牌。
    /// </summary>
    private Uri ConfigsUri(Uri address, string dataId)
    {
        var builder = new UriBuilder(address) { Path = ConfigsPath, Query = $"dataId={Uri.EscapeDataString(dataId)}&group={Uri.EscapeDataString(_options.Group)}" + (_options.Tenant.Length == 0 ? string.Empty : $"&tenant={Uri.EscapeDataString(_options.Tenant)}") };
        return WithToken(builder).Uri;
    }

    /// <summary>
    /// 有令牌时追加 <c>accessToken</c> 查询参数。
    /// </summary>
    private UriBuilder WithToken(UriBuilder builder)
    {
        if (_token is null)
        {
            return builder;
        }

        var query = builder.Query.TrimStart('?');
        builder.Query = (query.Length == 0 ? string.Empty : query + "&") + "accessToken=" + Uri.EscapeDataString(_token);
        return builder;
    }

    /// <summary>
    /// 服务端没有给出 MD5 时的内容指纹（SHA-256 小写十六进制）。
    /// </summary>
    private static string Fingerprint(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
