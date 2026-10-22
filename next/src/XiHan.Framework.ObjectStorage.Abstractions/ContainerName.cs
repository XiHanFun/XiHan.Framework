// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions;

/// <summary>
/// 容器名：小写字母、数字与连字符，3 到 63 个字符，首尾是字母或数字（与 S3 桶名兼容）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public readonly partial record struct ContainerName
{
    /// <summary>
    /// 创建；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public ContainerName(string value)
    {
        Value = Validate(value);
    }

    /// <summary>
    /// 容器名文本。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 从字符串隐式创建。
    /// </summary>
    public static implicit operator ContainerName(string value) => new(value);

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
            : throw new ArgumentException($"Container name '{value}' is invalid: it must be 3 to 63 lowercase letters, digits or hyphens, starting and ending with a letter or digit.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// 容器名的匹配。
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$")]
    private static partial Regex Pattern();
}
