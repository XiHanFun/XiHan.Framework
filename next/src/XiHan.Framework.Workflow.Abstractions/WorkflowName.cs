// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 工作流定义名：<c>^[a-z0-9][a-z0-9._-]{0,127}$</c>（与主题文法相同）；一个名字下有多个版本。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
[JsonConverter(typeof(WorkflowNameJsonConverter))]
public readonly partial record struct WorkflowName
{
    /// <summary>
    /// 最大长度。
    /// </summary>
    public const int MaxLength = 128;

    /// <summary>
    /// 创建；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public WorkflowName(string value)
    {
        Value = Validate(value);
    }

    /// <summary>
    /// 名字文本。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 从字符串隐式创建。
    /// </summary>
    public static implicit operator WorkflowName(string value) => new(value);

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
            : throw new ArgumentException($"Workflow name '{value}' is invalid: it must match ^[a-z0-9][a-z0-9._-]{{0,127}}$.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// 名字的匹配。
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,127}$")]
    private static partial Regex Pattern();
}
