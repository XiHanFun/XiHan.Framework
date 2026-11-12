// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;

namespace XiHan.Framework.Scheduling.Kubernetes.Leases;

/// <summary>
/// 对 API Server 的 Lease 读写
/// </summary>
/// <remarks>
/// GET / POST / PUT <c>/apis/coordination.k8s.io/v1/namespaces/{ns}/leases[/{name}]</c>，带 ServiceAccount 的 Bearer 令牌。409 翻译为 <see cref="SchedulingErrors.LeaseHeld"/>，其他失败翻译为 <see cref="SchedulingErrors.LeaseUnavailable"/>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KubernetesLeaseClient
{
    /// <summary>
    /// HTTP 客户端（已指向 API Server）
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// 选项
    /// </summary>
    private readonly KubernetesSchedulingOptions _options;

    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// <paramref name="http"/> 的 <see cref="HttpClient.BaseAddress"/> 为 API Server
    /// </remarks>
    public KubernetesLeaseClient(HttpClient http, KubernetesSchedulingOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Namespace = options.ResolveNamespace();
    }

    /// <summary>
    /// 命名空间
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// 扩展密钥用途「服务端认证」的 OID
    /// </summary>
    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    /// 按 Pod 内约定创建
    /// </summary>
    /// <remarks>
    /// API Server 地址、ServiceAccount 的 CA（存在时作为唯一信任根）与令牌。证书照常校验主机名，链要通到这个 CA、叶证书要有服务端认证用途（ADR-0201）
    /// </remarks>
    public static KubernetesLeaseClient CreateInCluster(KubernetesSchedulingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
        if (File.Exists(options.CertificateAuthorityPath))
        {
            // 集群 CA 不在系统信任里：只信任 ServiceAccount 挂载的 CA。换的是链策略而不是证书回调：
            // 平台照常校验主机名，只是信任根换成这个 CA；自己写回调只建链会丢掉主机名校验，集群 CA 签发的任何证书都能冒充 API Server
            var policy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = X509RevocationMode.NoCheck,
            };
            policy.CustomTrustStore.Add(X509CertificateLoader.LoadCertificateFromFile(options.CertificateAuthorityPath));
            // 叶证书要能用于服务端认证：同一个 CA 签发的客户端证书（kubelet 等）不能当 API Server 的证书
            policy.ApplicationPolicy.Add(new Oid(ServerAuthentication));
            handler.SslOptions.CertificateChainPolicy = policy;
        }

        return new KubernetesLeaseClient(new HttpClient(handler) { BaseAddress = options.ResolveApiServer(), Timeout = TimeSpan.FromSeconds(10) }, options);
    }

    /// <summary>
    /// 读取
    /// </summary>
    /// <remarks>
    /// 不存在为无值
    /// </remarks>
    internal async ValueTask<Result<Maybe<LeaseDocument>>> GetAsync(string name, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path(name));
        var response = await SendAsync(request, name, cancellationToken).ConfigureAwait(false);
        if (!response.TryGetValue(out var message))
        {
            return response.Error!;
        }

        using (message)
        {
            if (message.StatusCode == HttpStatusCode.NotFound)
            {
                return Result.Success<Maybe<LeaseDocument>>(default);
            }

            var document = await ReadAsync(message, name, cancellationToken).ConfigureAwait(false);
            return document.TryGetValue(out var lease) ? Result.Success(Maybe.Some(lease)) : document.Error!;
        }
    }

    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// 已存在 → <see cref="SchedulingErrors.LeaseHeld"/>
    /// </remarks>
    internal async ValueTask<Result<LeaseDocument>> CreateAsync(LeaseDocument lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        using var request = new HttpRequestMessage(HttpMethod.Post, Path(null)) { Content = JsonContent.Create(lease, KubernetesJsonContext.Default.LeaseDocument) };
        var response = await SendAsync(request, lease.Metadata.Name, cancellationToken).ConfigureAwait(false);
        if (!response.TryGetValue(out var message))
        {
            return response.Error!;
        }

        using (message)
        {
            return await ReadAsync(message, lease.Metadata.Name, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 整体替换（带 resourceVersion）
    /// </summary>
    /// <remarks>
    /// 版本过期或不存在 → <see cref="SchedulingErrors.LeaseHeld"/>
    /// </remarks>
    internal async ValueTask<Result<LeaseDocument>> ReplaceAsync(LeaseDocument lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        using var request = new HttpRequestMessage(HttpMethod.Put, Path(lease.Metadata.Name)) { Content = JsonContent.Create(lease, KubernetesJsonContext.Default.LeaseDocument) };
        var response = await SendAsync(request, lease.Metadata.Name, cancellationToken).ConfigureAwait(false);
        if (!response.TryGetValue(out var message))
        {
            return response.Error!;
        }

        using (message)
        {
            if (message.StatusCode == HttpStatusCode.NotFound)
            {
                return Held(lease.Metadata.Name);
            }

            return await ReadAsync(message, lease.Metadata.Name, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 资源路径
    /// </summary>
    private string Path(string? name) => $"/apis/coordination.k8s.io/v1/namespaces/{Uri.EscapeDataString(Namespace)}/leases" + (name is null ? string.Empty : "/" + Uri.EscapeDataString(name));

    /// <summary>
    /// 发送：加上令牌，连不上或超时翻译为不可达
    /// </summary>
    private async ValueTask<Result<HttpResponseMessage>> SendAsync(HttpRequestMessage request, string name, CancellationToken cancellationToken)
    {
        if (Token() is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            return await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(name, exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(name, exception.Message);
        }
    }

    /// <summary>
    /// 读取应答
    /// </summary>
    /// <remarks>
    /// 成功解析 Lease；409 为被占用；其余为不可达并带上 Status 的消息
    /// </remarks>
    private static async ValueTask<Result<LeaseDocument>> ReadAsync(HttpResponseMessage message, string name, CancellationToken cancellationToken)
    {
        if (message.StatusCode == HttpStatusCode.Conflict)
        {
            return Held(name);
        }

        if (!message.IsSuccessStatusCode)
        {
            string reason = $"HTTP {(int)message.StatusCode}";
            try
            {
                if (await message.Content.ReadFromJsonAsync(KubernetesJsonContext.Default.StatusDocument, cancellationToken).ConfigureAwait(false) is { Message: { Length: > 0 } detail })
                {
                    reason += ": " + detail;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // 不是 Status 对象也无妨，状态码已经说明问题
            }

            return Unavailable(name, reason);
        }

        var lease = await message.Content.ReadFromJsonAsync(KubernetesJsonContext.Default.LeaseDocument, cancellationToken).ConfigureAwait(false);
        return lease is null ? Unavailable(name, "empty response") : Result.Success(lease);
    }

    /// <summary>
    /// 当前令牌：显式给定的，否则每次从文件读（投射令牌会轮换）
    /// </summary>
    private string? Token()
    {
        if (_options.Token is not null)
        {
            return _options.Token;
        }

        return File.Exists(_options.TokenPath) ? File.ReadAllText(_options.TokenPath).Trim() : null;
    }

    /// <summary>
    /// 被占用
    /// </summary>
    private static Error Held(string name) => Error.Conflict(SchedulingErrors.LeaseHeld, SchedulingMessages.LeaseHeld, MessageArguments.Of(("resource", name)));

    /// <summary>
    /// 不可达
    /// </summary>
    private static Error Unavailable(string name, string reason) => Error.Unavailable(SchedulingErrors.LeaseUnavailable, SchedulingMessages.LeaseUnavailable, MessageArguments.Of(("resource", name), ("reason", reason)));
}
