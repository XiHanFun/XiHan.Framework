// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Database;

/// <summary>
/// 数据库授权选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Security:Database</c> 显式读取：<c>CacheSize</c>、<c>CacheTimeToLiveSeconds</c>（0 不过期）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DatabaseSecurityOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Security:Database";

    /// <summary>
    /// 授予缓存的条目上限（每个主体与租户的组合一条）。
    /// </summary>
    /// <remarks>
    /// 默认 10000，满时整体清空。
    /// </remarks>
    public int CacheSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The cache size must be positive.");
    } = 10_000;

    /// <summary>
    /// 缓存条目的存活时间。
    /// </summary>
    /// <remarks>
    /// 默认 30 秒，<see langword="null"/> 不过期（只靠失效与容量清空）。
    /// </remarks>
    public TimeSpan? CacheTimeToLive
    {
        get => field;
        init => field = value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The cache time to live must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static DatabaseSecurityOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new DatabaseSecurityOptions();
        if (configuration[$"{ConfigurationSection}:CacheSize"] is { } size)
        {
            options = int.TryParse(size, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 1
                ? options with { CacheSize = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:CacheSize must be a positive integer, but was '{size}'.");
        }

        if (configuration[$"{ConfigurationSection}:CacheTimeToLiveSeconds"] is { } ttl)
        {
            options = double.TryParse(ttl, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
                ? options with { CacheTimeToLive = seconds == 0 ? null : TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:CacheTimeToLiveSeconds must be a non-negative number, but was '{ttl}'.");
        }

        return options;
    }
}
