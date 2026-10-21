// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Redis;

/// <summary>
/// Redis Provider 选项。从配置节 <c>XiHan:Caching:Redis</c> 与绑定 <c>cache</c> 显式读取。
/// </summary>
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
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Redis 连接字符串不能为空。", nameof(value));
    }

    /// <summary>
    /// 键前缀；默认 <c>xihan:</c>。
    /// </summary>
    public string KeyPrefix { get; init; } = "xihan:";

    /// <summary>
    /// 未指定过期时的存活时间；默认 5 分钟。
    /// </summary>
    public TimeSpan DefaultTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "默认存活时间必须为正。");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 从配置与绑定读取：<c>Configuration</c>（配置优先于绑定值）、<c>KeyPrefix</c>、<c>DefaultTimeToLiveSeconds</c>。
    /// </summary>
    public static RedisCachingOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = configuration[$"{ConfigurationSection}:Configuration"]
            ?? (binding.TryGetValue(out var bound) ? bound.Value : null)
            ?? throw new ArgumentException($"没有 Redis 连接：设置 {ConfigurationSection}:Configuration 或提供绑定 cache。");

        var options = new RedisCachingOptions { Configuration = connection };
        if (configuration[$"{ConfigurationSection}:KeyPrefix"] is { } prefix)
        {
            options = options with { KeyPrefix = prefix };
        }

        var ttl = configuration[$"{ConfigurationSection}:DefaultTimeToLiveSeconds"];
        if (ttl is not null)
        {
            options = double.TryParse(ttl, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { DefaultTimeToLive = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:DefaultTimeToLiveSeconds 必须是正数，实际 '{ttl}'。");
        }

        return options;
    }
}
