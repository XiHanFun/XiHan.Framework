// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动类型名
/// </summary>
/// <remarks>
/// <c>^[A-Za-z][A-Za-z0-9._-]{0,63}$</c>；内置活动用 PascalCase 名（<c>Start</c>、<c>UserTask</c>）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[JsonConverter(typeof(ActivityTypeJsonConverter))]
public readonly partial record struct ActivityType
{
    /// <summary>
    /// 最大长度
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// 校验过的文本
    /// </summary>
    /// <remarks>
    /// 只有 <see langword="default"/> 值为 <see langword="null"/>
    /// </remarks>
    private readonly string? _value;

    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public ActivityType(string value)
    {
        _value = Validate(value);
    }

    /// <summary>
    /// 类型名文本
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> 值没有类型名文本，读取抛出 <see cref="InvalidOperationException"/>（RFC-0001「标识值」）
    /// </remarks>
    public string Value => _value ?? throw new InvalidOperationException("The 'ActivityType' is the default value and has no text; create it with the constructor.");

    /// <summary>
    /// 从字符串显式创建
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>；会抛出的转换不做成隐式（RFC-0001「标识值」）
    /// </remarks>
    public static explicit operator ActivityType(string value) => new(value);

    /// <summary>
    /// 是否合法
    /// </summary>
    public static bool IsValid([NotNullWhen(true)] string? value) => value is not null && Pattern().IsMatch(value);

    /// <summary>
    /// 校验并返回
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static string Validate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsValid(value)
            ? value
            : throw new ArgumentException($"Activity type '{value}' is invalid: it must match ^[A-Za-z][A-Za-z0-9._-]{{0,63}}$.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;

    /// <summary>
    /// 类型名的匹配
    /// </summary>
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex Pattern();
}
