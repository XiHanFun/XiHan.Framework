// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Messaging</c> 显式读取。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MessagingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging";

    /// <summary>
    /// 一条消息最多投递次数（含首次）。
    /// </summary>
    /// <remarks>
    /// 超过进入死信。默认 3。
    /// </remarks>
    public int MaxAttempts
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of attempts must be positive.");
    } = 3;

    /// <summary>
    /// 死信主题后缀。
    /// </summary>
    /// <remarks>
    /// 默认 <c>.dlq</c>。
    /// </remarks>
    public string DeadLetterSuffix
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The dead-letter suffix must not be empty.", nameof(value));
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
                : throw new ArgumentException($"{ConfigurationSection}:MaxAttempts must be a positive integer, but was '{attempts}'.");
        }

        if (configuration[$"{ConfigurationSection}:DeadLetterSuffix"] is { } suffix)
        {
            options = options with { DeadLetterSuffix = suffix };
        }

        return options;
    }
}
