// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 发件箱选项。从配置节 <c>XiHan:Messaging:Outbox</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record OutboxOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:Outbox";

    /// <summary>
    /// 轮询间隔；默认 1 秒。
    /// </summary>
    public TimeSpan PollInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The poll interval must be positive.");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 每轮最多投递条数；默认 100。
    /// </summary>
    public int BatchSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The batch size must be positive.");
    } = 100;

    /// <summary>
    /// 从配置读取：<c>PollIntervalSeconds</c>、<c>BatchSize</c>。
    /// </summary>
    public static OutboxOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new OutboxOptions();
        var interval = configuration[$"{ConfigurationSection}:PollIntervalSeconds"];
        if (interval is not null)
        {
            options = double.TryParse(interval, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { PollInterval = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:PollIntervalSeconds must be a positive number, but was '{interval}'.");
        }

        var batch = configuration[$"{ConfigurationSection}:BatchSize"];
        if (batch is not null)
        {
            options = int.TryParse(batch, NumberStyles.None, CultureInfo.InvariantCulture, out var size) && size >= 1
                ? options with { BatchSize = size }
                : throw new ArgumentException($"{ConfigurationSection}:BatchSize must be a positive integer, but was '{batch}'.");
        }

        return options;
    }
}
