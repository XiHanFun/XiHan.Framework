// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Nacos;

/// <summary>
/// Nacos Provider 的选项
/// </summary>
/// <remarks>
/// 配置节 <c>XiHan:Configuration:Nacos</c>：<c>ServerAddresses</c>（逗号或分号分隔；绑定 <c>nacos</c> 的主值优先）、<c>Group</c>、<c>Tenant</c>（Nacos 命名空间 ID）、<c>UserName</c> / <c>Password</c>（绑定的属性 <c>username</c> / <c>password</c> 优先）、<c>Format</c>、<c>LongPollTimeoutSeconds</c>、<c>RequestTimeoutSeconds</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record NacosOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Configuration:Nacos";

    /// <summary>
    /// 服务地址（绝对的 http / https URL），按序尝试
    /// </summary>
    /// <remarks>
    /// 至少一个。地址的路径是 Nacos 的上下文路径（<c>server.servlet.contextPath</c>，经网关转发时连同网关的前缀），接口路径接在它后面；没有路径时用缺省的 <c>/nacos</c>，<c>http://host:8848</c> 与 <c>http://host:8848/nacos</c> 等价
    /// </remarks>
    public required IReadOnlyList<string> ServerAddresses
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count == 0)
            {
                throw new ArgumentException("At least one Nacos server address is required.", nameof(value));
            }

            foreach (var address in value)
            {
                if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new ArgumentException($"The Nacos server address '{address}' must be an absolute http or https URL.", nameof(value));
                }
            }

            field = value;
        }
    }

    /// <summary>
    /// 配置分组
    /// </summary>
    /// <remarks>
    /// 默认 <c>DEFAULT_GROUP</c>
    /// </remarks>
    public string Group
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The group must not be empty.", nameof(value));
    } = "DEFAULT_GROUP";

    /// <summary>
    /// Nacos 命名空间 ID（租户）
    /// </summary>
    /// <remarks>
    /// 空表示公共命名空间
    /// </remarks>
    public string Tenant { get; init; } = string.Empty;

    /// <summary>
    /// 登录用户名
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Password"/> 一起给出时先登录取令牌。是登录凭据的身份那一半，打印选项时与 <see cref="Password"/> 一样遮蔽（ADR-0090）
    /// </remarks>
    public string? UserName { get; init; }

    /// <summary>
    /// 登录密码
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// 是否允许经非本机的 http 地址发送凭据
    /// </summary>
    /// <remarks>
    /// 默认否：配置了 <see cref="UserName"/> / <see cref="Password"/> 时，非回环的 <c>http</c> 地址让建客户端时抛出 <see cref="ArgumentException"/>，登录的口令与随后每个请求的令牌都是明文（ADR-0220）。只在链路另有保护（同机、服务网格的双向 TLS）时打开
    /// </remarks>
    public bool AllowInsecureHttp { get; init; }

    /// <summary>
    /// 配置内容的格式
    /// </summary>
    /// <remarks>
    /// 默认 JSON
    /// </remarks>
    public NacosConfigFormat Format { get; init; } = NacosConfigFormat.Json;

    /// <summary>
    /// 长轮询窗口
    /// </summary>
    /// <remarks>
    /// 默认 30 秒
    /// </remarks>
    public TimeSpan LongPollTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The long poll timeout must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 单次请求（取文档、登录）的超时
    /// </summary>
    /// <remarks>
    /// 默认 10 秒
    /// </remarks>
    public TimeSpan RequestTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The request timeout must be positive.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 从配置与绑定读取（RFC-0016「资源绑定与配置节」）
    /// </summary>
    /// <remarks>
    /// 地址：绑定 <c>nacos</c> 的主值优先于 <c>ServerAddresses</c>；用户名与密码：绑定的属性 <c>username</c> / <c>password</c> 各自优先于 <c>UserName</c> / <c>Password</c>。空白的主值与属性不算给出
    /// </remarks>
    public static NacosOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // 绑定来自部署环境（Service Binding），给出的地址与凭据逐项覆盖配置节里的同类键（RFC-0016「资源绑定与配置节」），
        // 分组、格式、超时等其余键照读配置节；空白的主值与属性不算给出
        var addresses = (binding.TryGetValue(out var bound) && !string.IsNullOrWhiteSpace(bound.Value) ? bound.Value : null)
            ?? configuration[$"{ConfigurationSection}:ServerAddresses"]
            ?? throw new ArgumentException($"No Nacos server address is configured: provide the 'nacos' binding or set {ConfigurationSection}:ServerAddresses.");
        var options = new NacosOptions
        {
            ServerAddresses = [.. addresses.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            UserName = BoundText(binding, "username") ?? Text(configuration, "UserName"),
            Password = BoundText(binding, "password") ?? Text(configuration, "Password"),
            Tenant = Text(configuration, "Tenant") ?? string.Empty,
        };
        if (Text(configuration, "Group") is { } group)
        {
            options = options with { Group = group };
        }

        if (Text(configuration, "Format") is { } format)
        {
            options = Enum.TryParse<NacosConfigFormat>(format, ignoreCase: true, out var parsed)
                ? options with { Format = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:Format must be 'Json' or 'Properties', but was '{format}'.");
        }

        if (Text(configuration, "LongPollTimeoutSeconds") is { } longPoll)
        {
            options = options with { LongPollTimeout = Seconds(longPoll, "LongPollTimeoutSeconds") };
        }

        if (Text(configuration, "RequestTimeoutSeconds") is { } request)
        {
            options = options with { RequestTimeout = Seconds(request, "RequestTimeoutSeconds") };
        }

        if (Text(configuration, "AllowInsecureHttp") is { } insecure)
        {
            options = options with
            {
                AllowInsecureHttp = bool.TryParse(insecure, out var allowed) ? allowed : throw new ArgumentException($"{ConfigurationSection}:AllowInsecureHttp must be 'true' or 'false', but was '{insecure}'."),
            };
        }

        return options;
    }

    /// <summary>
    /// 读绑定的一个非空属性
    /// </summary>
    /// <remarks>
    /// 没有绑定、没有该属性或属性空白为 <see langword="null"/>
    /// </remarks>
    private static string? BoundText(Maybe<BindingValue> binding, string key)
        => binding.TryGetValue(out var bound) && bound.Property(key).TryGetValue(out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// 读一个非空文本值
    /// </summary>
    /// <remarks>
    /// 缺失或空白为 <see langword="null"/>
    /// </remarks>
    private static string? Text(IConfiguration configuration, string name)
        => configuration[$"{ConfigurationSection}:{name}"] is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// 解析正数秒
    /// </summary>
    /// <remarks>
    /// 不合法抛出
    /// </remarks>
    private static TimeSpan Seconds(string text, string name)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0
            ? TimeSpan.FromSeconds(value)
            : throw new ArgumentException($"{ConfigurationSection}:{name} must be a positive number, but was '{text}'.");

    /// <summary>
    /// 打印成员，凭据以 <see cref="Redaction.Mask"/> 代替
    /// </summary>
    /// <remarks>
    /// 记录的 <c>ToString()</c> 经它输出：对象可能整个进日志与异常消息，用户名与口令不能明文出现（RFC-0014「选项里的凭据」，ADR-0090）。其余成员同编译器生成的写法；新加属性时在这里一并补上，架构测试 <c>CredentialRedactionTests</c> 会核对
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        // 凭据只打印遮蔽文本，null 仍打印为空：看得出有没有配置，看不到值
        builder.Append("ServerAddresses = ").Append(ServerAddresses)
            .Append(", Group = ").Append(Group)
            .Append(", Tenant = ").Append(Tenant)
            .Append(", UserName = ").Append(Redaction.Redact(UserName))
            .Append(", Password = ").Append(Redaction.Redact(Password))
            .Append(", Format = ").Append(Format)
            .Append(", LongPollTimeout = ").Append(LongPollTimeout)
            .Append(", RequestTimeout = ").Append(RequestTimeout)
            .Append(", AllowInsecureHttp = ").Append(AllowInsecureHttp);
        return true;
    }
}
