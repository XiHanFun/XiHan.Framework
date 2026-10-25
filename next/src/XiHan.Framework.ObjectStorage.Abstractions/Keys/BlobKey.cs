// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Keys;

/// <summary>
/// 对象键：<c>/</c> 分段的路径，1 到 <see cref="MaxLength"/> 个字符；每段非空、不是 <c>.</c> 或 <c>..</c>；不含空白、控制字符与 <c>\</c>。区分大小写。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public readonly record struct BlobKey
{
    /// <summary>
    /// 最大长度。
    /// </summary>
    public const int MaxLength = 1024;

    /// <summary>
    /// 段分隔符。
    /// </summary>
    public const char Separator = '/';

    /// <summary>
    /// 创建；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public BlobKey(string value)
    {
        Value = Validate(value);
    }

    /// <summary>
    /// 键文本。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 从字符串隐式创建。
    /// </summary>
    public static implicit operator BlobKey(string value) => new(value);

    /// <summary>
    /// 是否合法。
    /// </summary>
    public static bool IsValid([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength || !IsValidPrefix(value))
        {
            return false;
        }

        // 每段都要非空且不是 . 或 ..：键会映射到路径与 URL，这些段会逃逸或产生歧义
        foreach (var segment in value.Split(Separator))
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 前缀是否合法：与键同样的字符约束，但允许空串与以 <c>/</c> 结尾。
    /// </summary>
    public static bool IsValidPrefix([NotNullWhen(true)] string? value)
    {
        if (value is null || value.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c) || c == '\\')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 校验并返回；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string Validate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsValid(value)
            ? value
            : throw new ArgumentException($"Blob key '{value}' is invalid: it must be 1 to {MaxLength} characters of non-empty '/'-separated segments other than '.' and '..', without whitespace, control characters or backslashes.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
