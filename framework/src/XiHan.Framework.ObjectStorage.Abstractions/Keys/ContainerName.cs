// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Keys;

/// <summary>
/// 容器名
/// </summary>
/// <remarks>
/// 小写字母、数字与连字符，3 到 63 个字符，首尾是字母或数字（与 S3 桶名兼容）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public readonly partial record struct ContainerName
{
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
    public ContainerName(string value)
    {
        _value = Validate(value);
    }

    /// <summary>
    /// 容器名文本
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> 值没有容器名文本，读取抛出 <see cref="InvalidOperationException"/>（RFC-0001「标识值」）
    /// </remarks>
    public string Value => _value ?? throw new InvalidOperationException("The 'ContainerName' is the default value and has no text; create it with the constructor.");

    /// <summary>
    /// 从字符串显式创建
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>；会抛出的转换不做成隐式（RFC-0001「标识值」）
    /// </remarks>
    public static explicit operator ContainerName(string value) => new(value);

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
            : throw new ArgumentException($"Container name '{value}' is invalid: it must be 3 to 63 lowercase letters, digits or hyphens, starting and ending with a letter or digit.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;

    /// <summary>
    /// 容器名的匹配
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$")]
    private static partial Regex Pattern();
}
