// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.InMemory;

/// <summary>
/// 内存代理选项。从配置节 <c>XiHan:Messaging:InMemory</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record InMemoryMessagingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:InMemory";

    /// <summary>
    /// 每个主题（含其死信列表）的条目上限；默认 10000。
    /// </summary>
    public int MaxQueuedMessages
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "条目上限必须为正。");
    } = 10_000;

    /// <summary>
    /// 从配置读取：<c>MaxQueuedMessages</c>。
    /// </summary>
    public static InMemoryMessagingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration[$"{ConfigurationSection}:MaxQueuedMessages"];
        if (raw is null)
        {
            return new InMemoryMessagingOptions();
        }

        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
            ? new InMemoryMessagingOptions { MaxQueuedMessages = value }
            : throw new ArgumentException($"{ConfigurationSection}:MaxQueuedMessages 必须是正整数，实际 '{raw}'。");
    }
}
