// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 两级缓存的选项。
/// </summary>
/// <remarks>
/// 配置节 <c>XiHan:Caching:Hybrid</c>：<c>LocalCapacity</c>（L1 条目上限，默认 10000）、<c>LocalTimeToLiveSeconds</c>（L1 存活时间上限，默认 30 秒）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record HybridCachingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:Hybrid";

    /// <summary>
    /// L1 条目上限。
    /// </summary>
    /// <remarks>
    /// 满时淘汰最久未访问的条目。
    /// </remarks>
    public int LocalCapacity
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The local capacity must be positive.");
    } = 10_000;

    /// <summary>
    /// L1 存活时间上限：条目在 L1 里最多留这么久，别的实例的写入最长在这个窗口后可见。
    /// </summary>
    public TimeSpan LocalTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The local time to live must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static HybridCachingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new HybridCachingOptions();
        var capacity = configuration[$"{ConfigurationSection}:LocalCapacity"];
        if (capacity is not null)
        {
            options = int.TryParse(capacity, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
                ? options with { LocalCapacity = value }
                : throw new ArgumentException($"{ConfigurationSection}:LocalCapacity must be a positive integer, but was '{capacity}'.");
        }

        var seconds = configuration[$"{ConfigurationSection}:LocalTimeToLiveSeconds"];
        if (seconds is not null)
        {
            options = double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0
                ? options with { LocalTimeToLive = TimeSpan.FromSeconds(value) }
                : throw new ArgumentException($"{ConfigurationSection}:LocalTimeToLiveSeconds must be a positive number, but was '{seconds}'.");
        }

        return options;
    }
}
