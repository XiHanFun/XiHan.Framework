// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Keys;

/// <summary>
/// 对象键
/// </summary>
/// <remarks>
/// <c>/</c> 分段的路径，1 到 <see cref="MaxLength"/> 个字符；每段非空、不是 <c>.</c> 或 <c>..</c>；不含空白、控制字符与 <c>\</c>；首段不是保留段 <see cref="ReservedSegment"/>（框架自己的对象，ADR-0090）。区分大小写
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct BlobKey
{
    /// <summary>
    /// 最大长度
    /// </summary>
    public const int MaxLength = 1024;

    /// <summary>
    /// 段分隔符
    /// </summary>
    public const char Separator = '/';

    /// <summary>
    /// 保留段：首段是它的键留给框架自己的对象
    /// </summary>
    /// <remarks>
    /// 如 S3 的条件写入探测键 <c>.xihan/probe/conditional-write</c>。这样的键（<c>.xihan</c> 本身与 <c>.xihan/…</c>，区分大小写）不在键文法之内：构造时抛出，业务写不进、读不到、删不掉；各 Provider 列举时跳过存储里已有的这类对象（RFC-0012「标识」，ADR-0090）
    /// </remarks>
    public const string ReservedSegment = ".xihan";

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
    public BlobKey(string value)
    {
        _value = Validate(value);
    }

    /// <summary>
    /// 键文本
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> 值没有键文本，读取抛出 <see cref="InvalidOperationException"/>（RFC-0001「标识值」）
    /// </remarks>
    public string Value => _value ?? throw new InvalidOperationException("The 'BlobKey' is the default value and has no text; create it with the constructor.");

    /// <summary>
    /// 从字符串显式创建
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>；会抛出的转换不做成隐式（RFC-0001「标识值」）
    /// </remarks>
    public static explicit operator BlobKey(string value) => new(value);

    /// <summary>
    /// 是否合法
    /// </summary>
    public static bool IsValid([NotNullWhen(true)] string? value)
    {
        // 保留前缀下的是框架自己的对象：不让业务写入，列举时以同一判定跳过
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength || !IsValidPrefix(value) || IsReserved(value))
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
    /// 是否在保留前缀下：首段是 <see cref="ReservedSegment"/>
    /// </summary>
    /// <remarks>
    /// 只看首段且区分大小写：<c>.xihanx/a</c>、<c>.XiHan/a</c>、<c>a/.xihan/b</c> 都不是
    /// </remarks>
    public static bool IsReserved([NotNullWhen(true)] string? value)
        => value is not null
            && value.StartsWith(ReservedSegment, StringComparison.Ordinal)
            && (value.Length == ReservedSegment.Length || value[ReservedSegment.Length] == Separator);

    /// <summary>
    /// 前缀是否合法：与键同样的字符约束，但允许空串与以 <c>/</c> 结尾
    /// </summary>
    /// <remarks>
    /// 前缀只是筛选，不受保留前缀限制；以它为前缀的列举总是空的
    /// </remarks>
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
    /// 校验并返回
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static string Validate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (IsReserved(value))
        {
            throw new ArgumentException($"Blob key '{value}' is reserved: keys whose first segment is '{ReservedSegment}' belong to the framework and are skipped when listing.", nameof(value));
        }

        return IsValid(value)
            ? value
            : throw new ArgumentException($"Blob key '{value}' is invalid: it must be 1 to {MaxLength} characters of non-empty '/'-separated segments other than '.' and '..', without whitespace, control characters or backslashes.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;
}
