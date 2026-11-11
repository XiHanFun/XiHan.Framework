// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Redis;

/// <summary>
/// Redis Provider 选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Caching:Redis</c> 与绑定 <c>cache</c> 显式读取（RFC-0016「资源绑定与配置节」）：绑定的主值覆盖 <c>Configuration</c>，绑定的属性 <c>username</c> / <c>password</c> 覆盖连接字符串里的 <c>user</c> / <c>password</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record RedisCachingOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:Redis";

    /// <summary>
    /// StackExchange.Redis 连接字符串
    /// </summary>
    public required string Configuration
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The Redis connection string cannot be empty.", nameof(value));
    }

    /// <summary>
    /// 覆盖 <see cref="Configuration"/> 里 <c>user</c> 的用户名（Redis 6 的 ACL 用户）
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时用连接字符串里的。<see cref="FromConfiguration"/> 从绑定 <c>cache</c> 的属性 <c>username</c> 取；连接时以 StackExchange.Redis 的选项设置覆盖，不改写连接字符串（密码里的逗号等字符不会把字符串拆错）。是登录凭据的身份那一半，打印选项时与 <see cref="Password"/> 一样遮蔽（ADR-0090）
    /// </remarks>
    public string? UserName { get; init; }

    /// <summary>
    /// 覆盖 <see cref="Configuration"/> 里 <c>password</c> 的密码
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时用连接字符串里的。<see cref="FromConfiguration"/> 从绑定 <c>cache</c> 的属性 <c>password</c> 取。不进日志与错误消息
    /// </remarks>
    public string? Password { get; init; }

    /// <summary>
    /// 键前缀
    /// </summary>
    /// <remarks>
    /// 默认 <c>xihan:</c>
    /// </remarks>
    public string KeyPrefix { get; init; } = "xihan:";

    /// <summary>
    /// 写入与删除后是否向变更频道发布通知
    /// </summary>
    /// <remarks>
    /// 默认开：其他实例的两级缓存据此失效 L1（ADR-0055）；发后不等，不增加写入的往返
    /// </remarks>
    public bool PublishChanges { get; init; } = true;

    /// <summary>
    /// 未指定过期时的存活时间
    /// </summary>
    /// <remarks>
    /// 默认 5 分钟
    /// </remarks>
    public TimeSpan DefaultTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The default time to live must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 健康项 <c>caching.redis.connection</c> 的重探间隔（也是单次探测的超时）
    /// </summary>
    /// <remarks>
    /// 默认 15 秒
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 从配置与绑定读取
    /// </summary>
    /// <remarks>
    /// <c>Configuration</c>（绑定 <c>cache</c> 的主值优先于配置，RFC-0016；绑定的属性 <c>username</c> / <c>password</c> 各自覆盖连接字符串里的 <c>user</c> / <c>password</c>）、<c>KeyPrefix</c>、<c>DefaultTimeToLiveSeconds</c>、<c>HealthProbeIntervalSeconds</c>
    /// </remarks>
    public static RedisCachingOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // 绑定来自部署环境（Service Binding），给出的连接与凭据逐项覆盖配置节里的同类键（RFC-0016「资源绑定与配置节」），其余键照读配置节；
        // 空白的主值与属性不算给出。连接两者都没有直接报错而不是静默退回
        var connection = (binding.TryGetValue(out var bound) && !string.IsNullOrWhiteSpace(bound.Value) ? bound.Value : null)
            ?? configuration[$"{ConfigurationSection}:Configuration"]
            ?? throw new ArgumentException($"No Redis connection is configured: provide the 'cache' binding or set {ConfigurationSection}:Configuration.");

        var options = new RedisCachingOptions
        {
            Configuration = connection,
            UserName = BoundText(binding, "username"),
            Password = BoundText(binding, "password"),
        };
        if (configuration[$"{ConfigurationSection}:KeyPrefix"] is { } prefix)
        {
            options = options with { KeyPrefix = prefix };
        }

        if (configuration[$"{ConfigurationSection}:PublishChanges"] is { } publish)
        {
            options = bool.TryParse(publish, out var enabled)
                ? options with { PublishChanges = enabled }
                : throw new ArgumentException($"{ConfigurationSection}:PublishChanges must be 'true' or 'false', but was '{publish}'.");
        }

        var ttl = configuration[$"{ConfigurationSection}:DefaultTimeToLiveSeconds"];
        if (ttl is not null)
        {
            options = double.TryParse(ttl, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { DefaultTimeToLive = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:DefaultTimeToLiveSeconds must be a positive number, but was '{ttl}'.");
        }

        var probe = configuration[$"{ConfigurationSection}:HealthProbeIntervalSeconds"];
        if (probe is not null)
        {
            options = double.TryParse(probe, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { HealthProbeInterval = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:HealthProbeIntervalSeconds must be a positive number, but was '{probe}'.");
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
    /// 打印成员，凭据以 <see cref="Redaction.Mask"/> 代替
    /// </summary>
    /// <remarks>
    /// 记录的 <c>ToString()</c> 经它输出：对象可能整个进日志与异常消息，连接字符串（可能含 <c>password=</c>）、用户名与口令不能明文出现（RFC-0014「选项里的凭据」，ADR-0090）。其余成员同编译器生成的写法；新加属性时在这里一并补上，架构测试 <c>CredentialRedactionTests</c> 会核对
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        // 凭据只打印遮蔽文本，null 仍打印为空：看得出有没有配置，看不到值
        builder.Append("Configuration = ").Append(Redaction.Redact(Configuration))
            .Append(", UserName = ").Append(Redaction.Redact(UserName))
            .Append(", Password = ").Append(Redaction.Redact(Password))
            .Append(", KeyPrefix = ").Append(KeyPrefix)
            .Append(", PublishChanges = ").Append(PublishChanges)
            .Append(", DefaultTimeToLive = ").Append(DefaultTimeToLive)
            .Append(", HealthProbeInterval = ").Append(HealthProbeInterval);
        return true;
    }
}
