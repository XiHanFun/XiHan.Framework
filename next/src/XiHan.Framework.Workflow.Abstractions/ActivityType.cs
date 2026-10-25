// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 活动类型名：<c>^[A-Za-z][A-Za-z0-9._-]{0,63}$</c>；内置活动用 PascalCase 名（<c>Start</c>、<c>UserTask</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
[JsonConverter(typeof(ActivityTypeJsonConverter))]
public readonly partial record struct ActivityType
{
    /// <summary>
    /// 最大长度。
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// 创建；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public ActivityType(string value)
    {
        Value = Validate(value);
    }

    /// <summary>
    /// 类型名文本。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 从字符串隐式创建。
    /// </summary>
    public static implicit operator ActivityType(string value) => new(value);

    /// <summary>
    /// 是否合法。
    /// </summary>
    public static bool IsValid([NotNullWhen(true)] string? value) => value is not null && Pattern().IsMatch(value);

    /// <summary>
    /// 校验并返回；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string Validate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsValid(value)
            ? value
            : throw new ArgumentException($"Activity type '{value}' is invalid: it must match ^[A-Za-z][A-Za-z0-9._-]{{0,63}}$.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// 类型名的匹配。
    /// </summary>
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex Pattern();
}
