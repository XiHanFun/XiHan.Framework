// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 两级缓存的选项
/// </summary>
/// <remarks>
/// 配置节 <c>XiHan:Caching:Hybrid</c>：<c>LocalCapacity</c>（L1 条目上限，默认 10000）、<c>LocalTimeToLiveSeconds</c>（L1 存活时间上限，默认 30 秒）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record HybridCachingOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:Hybrid";

    /// <summary>
    /// L1 条目上限
    /// </summary>
    /// <remarks>
    /// 满时淘汰最久未访问的条目
    /// </remarks>
    public int LocalCapacity
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The local capacity must be positive.");
    } = 10_000;

    /// <summary>
    /// L1 存活时间上限：条目在 L1 里最多留这么久，别的实例的写入最长在这个窗口后可见
    /// </summary>
    public TimeSpan LocalTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The local time to live must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 从配置读取
    /// </summary>
    public static HybridCachingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new HybridCachingOptions();
        if (ConfigurationValues.ReadInt32(section, "LocalCapacity", 1) is { } capacity)
        {
            options = options with { LocalCapacity = capacity };
        }

        if (ConfigurationValues.ReadSeconds(section, "LocalTimeToLiveSeconds") is { } timeToLive)
        {
            options = options with { LocalTimeToLive = timeToLive };
        }

        return options;
    }
}
