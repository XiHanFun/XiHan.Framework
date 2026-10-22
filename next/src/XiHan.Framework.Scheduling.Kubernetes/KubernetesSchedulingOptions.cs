// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;

namespace XiHan.Framework.Scheduling.Kubernetes;

/// <summary>
/// Kubernetes 领导选举选项。从配置节 <c>XiHan:Scheduling:Kubernetes</c> 显式读取：<c>Lease</c>、<c>Namespace</c>、<c>Holder</c>、<c>ApiServer</c>、<c>TokenPath</c>、<c>CertificateAuthorityPath</c>。
/// 没给的值按 Pod 内的约定解析：命名空间读 ServiceAccount 挂载或 <c>POD_NAMESPACE</c>，持有者读 <c>XIHAN_POD_NAME</c> / <c>HOSTNAME</c>，API Server 读 <c>KUBERNETES_SERVICE_HOST</c> / <c>PORT</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial record KubernetesSchedulingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Scheduling:Kubernetes";

    /// <summary>
    /// 调度器默认竞争的资源名；它映射到 <see cref="Lease"/>，其他资源映射到 <c>&lt;Lease&gt;-&lt;资源&gt;</c>。
    /// </summary>
    public const string DefaultResource = "scheduling.jobs";

    /// <summary>
    /// ServiceAccount 挂载目录。
    /// </summary>
    private const string ServiceAccountDirectory = "/var/run/secrets/kubernetes.io/serviceaccount";

    /// <summary>
    /// 调度器租约的 Lease 名（DNS 标签）；默认 <c>xihan-scheduler</c>。
    /// </summary>
    public string Lease
    {
        get => field;
        init => field = DnsLabel().IsMatch(value ?? string.Empty) ? value! : throw new ArgumentException($"Lease name '{value}' must be a DNS label.", nameof(value));
    } = "xihan-scheduler";

    /// <summary>
    /// 命名空间；为空时解析 ServiceAccount 挂载的 namespace 文件、环境变量 <c>POD_NAMESPACE</c>，最后 <c>default</c>。
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// 持有者标识；为空时解析环境变量 <c>XIHAN_POD_NAME</c>、<c>HOSTNAME</c>，最后机器名。
    /// </summary>
    public string? Holder { get; init; }

    /// <summary>
    /// API Server 地址；为空时由 <c>KUBERNETES_SERVICE_HOST</c> / <c>KUBERNETES_SERVICE_PORT</c> 拼出，没有时 <c>https://kubernetes.default.svc</c>。
    /// </summary>
    public Uri? ApiServer { get; init; }

    /// <summary>
    /// 直接给定的 Bearer 令牌（测试用）；为空时每次请求从 <see cref="TokenPath"/> 读取，以跟上投射令牌的轮换。
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// ServiceAccount 令牌文件。
    /// </summary>
    public string TokenPath { get; init; } = ServiceAccountDirectory + "/token";

    /// <summary>
    /// API Server 的 CA 证书文件；不存在时用系统信任。
    /// </summary>
    public string CertificateAuthorityPath { get; init; } = ServiceAccountDirectory + "/ca.crt";

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static KubernetesSchedulingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new KubernetesSchedulingOptions();
        if (section["Lease"] is { } lease && !string.IsNullOrWhiteSpace(lease))
        {
            options = options with { Lease = lease };
        }

        if (section["Namespace"] is { } ns && !string.IsNullOrWhiteSpace(ns))
        {
            options = options with { Namespace = ns };
        }

        if (section["Holder"] is { } holder && !string.IsNullOrWhiteSpace(holder))
        {
            options = options with { Holder = holder };
        }

        if (section["ApiServer"] is { } server && !string.IsNullOrWhiteSpace(server))
        {
            options = Uri.TryCreate(server, UriKind.Absolute, out var uri)
                ? options with { ApiServer = uri }
                : throw new ArgumentException($"{ConfigurationSection}:ApiServer must be an absolute URI, but was '{server}'.");
        }

        if (section["TokenPath"] is { } tokenPath && !string.IsNullOrWhiteSpace(tokenPath))
        {
            options = options with { TokenPath = tokenPath };
        }

        if (section["CertificateAuthorityPath"] is { } caPath && !string.IsNullOrWhiteSpace(caPath))
        {
            options = options with { CertificateAuthorityPath = caPath };
        }

        return options;
    }

    /// <summary>
    /// 解析命名空间。
    /// </summary>
    public string ResolveNamespace()
    {
        if (!string.IsNullOrWhiteSpace(Namespace))
        {
            return Namespace;
        }

        var file = ServiceAccountDirectory + "/namespace";
        if (File.Exists(file) && File.ReadAllText(file).Trim() is { Length: > 0 } mounted)
        {
            return mounted;
        }

        return Environment.GetEnvironmentVariable("POD_NAMESPACE") is { Length: > 0 } env ? env : "default";
    }

    /// <summary>
    /// 解析持有者标识。
    /// </summary>
    public string ResolveHolder()
        => !string.IsNullOrWhiteSpace(Holder) ? Holder
            : Environment.GetEnvironmentVariable("XIHAN_POD_NAME") is { Length: > 0 } pod ? pod
            : Environment.GetEnvironmentVariable("HOSTNAME") is { Length: > 0 } host ? host
            : Environment.MachineName;

    /// <summary>
    /// 解析 API Server 地址。
    /// </summary>
    public Uri ResolveApiServer()
    {
        if (ApiServer is not null)
        {
            return ApiServer;
        }

        var host = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
        if (string.IsNullOrWhiteSpace(host))
        {
            return new Uri("https://kubernetes.default.svc");
        }

        var port = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT");
        // IPv6 地址要加方括号
        var authority = host.Contains(':', StringComparison.Ordinal) ? "[" + host + "]" : host;
        return new Uri($"https://{authority}:{(string.IsNullOrWhiteSpace(port) ? "443" : port)}");
    }

    /// <summary>
    /// 资源对应的 Lease 名：默认资源用 <see cref="Lease"/>，其他资源追加成 <c>&lt;Lease&gt;-&lt;资源&gt;</c>（点与下划线换成连字符，截到 63 字符）。
    /// </summary>
    public string LeaseNameFor(JobName resource)
    {
        if (string.Equals(resource.Value, DefaultResource, StringComparison.Ordinal))
        {
            return Lease;
        }

        var name = Lease + "-" + resource.Value.Replace('.', '-').Replace('_', '-');
        return name.Length <= 63 ? name : name[..63].TrimEnd('-');
    }

    /// <summary>
    /// DNS 标签。
    /// </summary>
    [GeneratedRegex("^[a-z]([a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex DnsLabel();
}
