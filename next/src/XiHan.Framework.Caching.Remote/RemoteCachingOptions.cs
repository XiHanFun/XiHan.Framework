// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Remote;

/// <summary>
/// 远程缓存适配器的选项，配置节 <c>XiHan:Caching:Remote</c>。
/// </summary>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed class RemoteCachingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:Remote";

    /// <summary>
    /// 每次操作的截止时间。
    /// </summary>
    /// <remarks>
    /// 超时按存储不可用处理。默认 5 秒。
    /// </remarks>
    public TimeSpan Timeout
    {
        get;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive.");
    } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 从配置读取：<c>TimeoutMilliseconds</c>。
    /// </summary>
    /// <remarks>
    /// 未配置用默认值；配置了但不是正整数时抛出 <see cref="ArgumentException"/>，与其他选项一致，不静默退回默认值。
    /// </remarks>
    public static RemoteCachingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var timeout = configuration[$"{ConfigurationSection}:TimeoutMilliseconds"];
        if (timeout is null)
        {
            return new RemoteCachingOptions();
        }

        return int.TryParse(timeout, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds) && milliseconds > 0
            ? new RemoteCachingOptions { Timeout = TimeSpan.FromMilliseconds(milliseconds) }
            : throw new ArgumentException($"{ConfigurationSection}:TimeoutMilliseconds must be a positive integer, but was '{timeout}'.");
    }
}
