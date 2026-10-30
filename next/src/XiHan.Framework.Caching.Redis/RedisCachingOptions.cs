// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Redis;

/// <summary>
/// Redis Provider 选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Caching:Redis</c> 与绑定 <c>cache</c> 显式读取。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record RedisCachingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:Redis";

    /// <summary>
    /// StackExchange.Redis 连接字符串。
    /// </summary>
    public required string Configuration
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The Redis connection string cannot be empty.", nameof(value));
    }

    /// <summary>
    /// 键前缀。
    /// </summary>
    /// <remarks>
    /// 默认 <c>xihan:</c>。
    /// </remarks>
    public string KeyPrefix { get; init; } = "xihan:";

    /// <summary>
    /// 写入与删除后是否向变更频道发布通知。
    /// </summary>
    /// <remarks>
    /// 默认开：其他实例的两级缓存据此失效 L1（ADR-0055）；发后不等，不增加写入的往返。
    /// </remarks>
    public bool PublishChanges { get; init; } = true;

    /// <summary>
    /// 未指定过期时的存活时间。
    /// </summary>
    /// <remarks>
    /// 默认 5 分钟。
    /// </remarks>
    public TimeSpan DefaultTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The default time to live must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 健康项 <c>caching.redis.connection</c> 的重探间隔（也是单次探测的超时）。
    /// </summary>
    /// <remarks>
    /// 默认 15 秒。
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 从配置与绑定读取。
    /// </summary>
    /// <remarks>
    /// <c>Configuration</c>（配置优先于绑定值）、<c>KeyPrefix</c>、<c>DefaultTimeToLiveSeconds</c>、<c>HealthProbeIntervalSeconds</c>。
    /// </remarks>
    public static RedisCachingOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // 显式配置优先于宿主注入的绑定，便于本地覆盖；两者都没有直接报错而不是静默退回
        var connection = configuration[$"{ConfigurationSection}:Configuration"]
            ?? (binding.TryGetValue(out var bound) ? bound.Value : null)
            ?? throw new ArgumentException($"No Redis connection is configured: set {ConfigurationSection}:Configuration or provide the 'cache' binding.");

        var options = new RedisCachingOptions { Configuration = connection };
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
}
