// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 内存缓存选项。从配置节 <c>XiHan:Caching:InMemory</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record InMemoryCachingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:InMemory";

    /// <summary>
    /// 每张表（字节存储、对象缓存各一张）的条目上限；默认 10000。
    /// </summary>
    public int MaxEntries
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The entry limit must be positive.");
    } = 10_000;

    /// <summary>
    /// 未指定过期时的存活时间；默认 5 分钟。
    /// </summary>
    public TimeSpan DefaultTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The default time to live must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 从配置读取：<c>MaxEntries</c>、<c>DefaultTimeToLiveSeconds</c>。
    /// </summary>
    public static InMemoryCachingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new InMemoryCachingOptions();
        var maxEntries = configuration[$"{ConfigurationSection}:MaxEntries"];
        if (maxEntries is not null)
        {
            options = int.TryParse(maxEntries, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
                ? options with { MaxEntries = value }
                : throw new ArgumentException($"{ConfigurationSection}:MaxEntries must be a positive integer, but was '{maxEntries}'.");
        }

        var ttl = configuration[$"{ConfigurationSection}:DefaultTimeToLiveSeconds"];
        if (ttl is not null)
        {
            options = double.TryParse(ttl, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { DefaultTimeToLive = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:DefaultTimeToLiveSeconds must be a positive number, but was '{ttl}'.");
        }

        return options;
    }
}
