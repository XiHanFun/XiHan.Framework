// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;

namespace XiHan.Framework.Scheduling.Kubernetes.Leases;

/// <summary>
/// 对 API Server 的 Lease 读写：GET / POST / PUT <c>/apis/coordination.k8s.io/v1/namespaces/{ns}/leases[/{name}]</c>，带 ServiceAccount 的 Bearer 令牌。
/// 409 翻译为 <see cref="SchedulingErrors.LeaseHeld"/>，其他失败翻译为 <see cref="SchedulingErrors.LeaseUnavailable"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KubernetesLeaseClient
{
    /// <summary>
    /// HTTP 客户端（已指向 API Server）。
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly KubernetesSchedulingOptions _options;

    /// <summary>
    /// 创建；<paramref name="http"/> 的 <see cref="HttpClient.BaseAddress"/> 为 API Server。
    /// </summary>
    public KubernetesLeaseClient(HttpClient http, KubernetesSchedulingOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Namespace = options.ResolveNamespace();
    }

    /// <summary>
    /// 命名空间。
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// 按 Pod 内约定创建：API Server 地址、ServiceAccount 的 CA（存在时作为唯一信任根）与令牌。
    /// </summary>
    public static KubernetesLeaseClient CreateInCluster(KubernetesSchedulingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
        if (File.Exists(options.CertificateAuthorityPath))
        {
            // 集群 CA 不在系统信任里：只信任 ServiceAccount 挂载的 CA
            var authority = X509CertificateLoader.LoadCertificateFromFile(options.CertificateAuthorityPath);
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, _) =>
            {
                if (certificate is null)
                {
                    return false;
                }

                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(authority);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return chain.Build(new X509Certificate2(certificate));
            };
        }

        return new KubernetesLeaseClient(new HttpClient(handler) { BaseAddress = options.ResolveApiServer(), Timeout = TimeSpan.FromSeconds(10) }, options);
    }

    /// <summary>
    /// 读取；不存在为无值。
    /// </summary>
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
    /// 创建；已存在 → <see cref="SchedulingErrors.LeaseHeld"/>。
    /// </summary>
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
    /// 整体替换（带 resourceVersion）；版本过期或不存在 → <see cref="SchedulingErrors.LeaseHeld"/>。
    /// </summary>
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
    /// 资源路径。
    /// </summary>
    private string Path(string? name) => $"/apis/coordination.k8s.io/v1/namespaces/{Uri.EscapeDataString(Namespace)}/leases" + (name is null ? string.Empty : "/" + Uri.EscapeDataString(name));

    /// <summary>
    /// 发送：加上令牌，连不上或超时翻译为不可达。
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
    /// 读取应答：成功解析 Lease；409 为被占用；其余为不可达并带上 Status 的消息。
    /// </summary>
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
    /// 当前令牌：显式给定的，否则每次从文件读（投射令牌会轮换）。
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
    /// 被占用。
    /// </summary>
    private static Error Held(string name) => Error.Conflict(SchedulingErrors.LeaseHeld, SchedulingMessages.LeaseHeld, MessageArguments.Of(("resource", name)));

    /// <summary>
    /// 不可达。
    /// </summary>
    private static Error Unavailable(string name, string reason) => Error.Unavailable(SchedulingErrors.LeaseUnavailable, SchedulingMessages.LeaseUnavailable, MessageArguments.Of(("resource", name), ("reason", reason)));
}
