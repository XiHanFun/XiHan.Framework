// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessagingCapabilities
{
    /// <summary>工作队列：至少一次投递、重试与死信。</summary>
    public const string Queue = "messaging.queue";

    /// <summary>流（预留）。</summary>
    public const string Stream = "messaging.stream";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}

/// <summary>
/// 消息错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessagingErrors
{
    /// <summary>主题不合法。</summary>
    public const string InvalidTopic = "MESSAGING_VALIDATION_001";

    /// <summary>信封不合法或无法解码。</summary>
    public const string InvalidEnvelope = "MESSAGING_VALIDATION_002";

    /// <summary>代理不可达或操作失败。</summary>
    public const string BrokerUnavailable = "MESSAGING_UNAVAILABLE_001";

    /// <summary>队列已满。</summary>
    public const string QueueFull = "MESSAGING_UNAVAILABLE_002";
}

/// <summary>
/// 消息选项。从配置节 <c>XiHan:Messaging</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MessagingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging";

    /// <summary>
    /// 一条消息最多投递次数（含首次）；超过进入死信。默认 3。
    /// </summary>
    public int MaxAttempts
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "最多投递次数必须为正。");
    } = 3;

    /// <summary>
    /// 死信主题后缀；默认 <c>.dlq</c>。
    /// </summary>
    public string DeadLetterSuffix
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("死信后缀不能为空。", nameof(value));
    } = ".dlq";

    /// <summary>
    /// 从配置读取：<c>MaxAttempts</c>、<c>DeadLetterSuffix</c>。
    /// </summary>
    public static MessagingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new MessagingOptions();
        var attempts = configuration[$"{ConfigurationSection}:MaxAttempts"];
        if (attempts is not null)
        {
            options = int.TryParse(attempts, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
                ? options with { MaxAttempts = value }
                : throw new ArgumentException($"{ConfigurationSection}:MaxAttempts 必须是正整数，实际 '{attempts}'。");
        }

        if (configuration[$"{ConfigurationSection}:DeadLetterSuffix"] is { } suffix)
        {
            options = options with { DeadLetterSuffix = suffix };
        }

        return options;
    }
}
