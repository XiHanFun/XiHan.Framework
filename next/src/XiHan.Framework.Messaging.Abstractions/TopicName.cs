// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 主题文法：小写字母或数字开头，之后是小写字母、数字、<c>.</c>、<c>_</c>、<c>-</c>，不超过 128 个字符。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class TopicName
{
    /// <summary>
    /// 最大长度。
    /// </summary>
    public const int MaxLength = 128;

    /// <summary>
    /// 是否合法。
    /// </summary>
    public static bool IsValid(string? topic) => topic is not null && Pattern().IsMatch(topic);

    /// <summary>
    /// 校验并返回；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string Validate(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        return IsValid(topic)
            ? topic
            : throw new ArgumentException($"主题 '{topic}' 不合法：小写字母或数字开头，只含小写字母、数字、'.'、'_'、'-'，不超过 {MaxLength} 个字符。", nameof(topic));
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,127}$")]
    private static partial Regex Pattern();
}
