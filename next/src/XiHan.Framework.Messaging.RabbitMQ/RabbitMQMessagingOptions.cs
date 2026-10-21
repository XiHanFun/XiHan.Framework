// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ Provider 选项。从配置节 <c>XiHan:Messaging:RabbitMQ</c> 与绑定 <c>queue</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record RabbitMQMessagingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:RabbitMQ";

    /// <summary>
    /// AMQP URI（如 <c>amqp://guest:guest@localhost:5672</c>）。
    /// </summary>
    public required string Configuration
    {
        get => field;
        init => field = Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "amqp" or "amqps"
            ? value
            : throw new ArgumentException("RabbitMQ 连接必须是 amqp:// 或 amqps:// URI。", nameof(value));
    }

    /// <summary>
    /// 队列名前缀；默认 <c>xihan.</c>。
    /// </summary>
    public string QueuePrefix { get; init; } = "xihan.";

    /// <summary>
    /// 每个消费者的预取数；默认 16。
    /// </summary>
    public ushort PrefetchCount
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "预取数必须为正。");
    } = 16;

    /// <summary>
    /// 从配置与绑定读取：<c>Configuration</c>（配置优先于绑定值）、<c>QueuePrefix</c>、<c>PrefetchCount</c>。
    /// </summary>
    public static RabbitMQMessagingOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = configuration[$"{ConfigurationSection}:Configuration"]
            ?? (binding.TryGetValue(out var bound) ? bound.Value : null)
            ?? throw new ArgumentException($"没有 RabbitMQ 连接：设置 {ConfigurationSection}:Configuration 或提供绑定 queue。");

        var options = new RabbitMQMessagingOptions { Configuration = connection };
        if (configuration[$"{ConfigurationSection}:QueuePrefix"] is { } prefix)
        {
            options = options with { QueuePrefix = prefix };
        }

        var prefetch = configuration[$"{ConfigurationSection}:PrefetchCount"];
        if (prefetch is not null)
        {
            options = ushort.TryParse(prefetch, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= 1
                ? options with { PrefetchCount = count }
                : throw new ArgumentException($"{ConfigurationSection}:PrefetchCount 必须是 1–65535 的整数，实际 '{prefetch}'。");
        }

        return options;
    }
}
