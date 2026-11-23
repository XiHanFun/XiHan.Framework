// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.InMemory;

/// <summary>
/// 内存代理选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Messaging:InMemory</c> 显式读取
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record InMemoryMessagingOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:InMemory";

    /// <summary>
    /// 每个主题（含其死信列表）的条目上限
    /// </summary>
    /// <remarks>
    /// 默认 10000
    /// </remarks>
    public int MaxQueuedMessages
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of queued messages must be positive.");
    } = 10_000;

    /// <summary>
    /// 从配置读取：<c>MaxQueuedMessages</c>
    /// </summary>
    /// <remarks>
    /// 主题数不另设上限（ADR-0082）：通道只为订阅表里的主题创建，订阅表在装配时定好、之后只读，发布到没人订阅的主题直接丢弃
    /// </remarks>
    public static InMemoryMessagingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new InMemoryMessagingOptions();
        if (ConfigurationValues.ReadInt32(configuration.GetSection(ConfigurationSection), "MaxQueuedMessages", 1) is { } queued)
        {
            options = options with { MaxQueuedMessages = queued };
        }

        return options;
    }
}
