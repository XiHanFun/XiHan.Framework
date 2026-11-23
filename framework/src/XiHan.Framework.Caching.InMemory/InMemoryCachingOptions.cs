// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 内存缓存选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Caching:InMemory</c> 显式读取
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record InMemoryCachingOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:InMemory";

    /// <summary>
    /// 每张表（字节存储、对象缓存各一张）的条目上限
    /// </summary>
    /// <remarks>
    /// 默认 10000
    /// </remarks>
    public int MaxEntries
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The entry limit must be positive.");
    } = 10_000;

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
    /// 从配置读取：<c>MaxEntries</c>、<c>DefaultTimeToLiveSeconds</c>
    /// </summary>
    public static InMemoryCachingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new InMemoryCachingOptions();
        if (ConfigurationValues.ReadInt32(section, "MaxEntries", 1) is { } maxEntries)
        {
            options = options with { MaxEntries = maxEntries };
        }

        if (ConfigurationValues.ReadSeconds(section, "DefaultTimeToLiveSeconds") is { } timeToLive)
        {
            options = options with { DefaultTimeToLive = timeToLive };
        }

        return options;
    }
}
