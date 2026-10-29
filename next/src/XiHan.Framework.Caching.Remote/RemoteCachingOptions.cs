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
    public static RemoteCachingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var timeout = configuration[$"{ConfigurationSection}:TimeoutMilliseconds"];
        return int.TryParse(timeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds) && milliseconds > 0
            ? new RemoteCachingOptions { Timeout = TimeSpan.FromMilliseconds(milliseconds) }
            : new RemoteCachingOptions();
    }
}
