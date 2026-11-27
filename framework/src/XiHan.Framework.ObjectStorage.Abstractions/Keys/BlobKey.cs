// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Keys;

/// <summary>
/// 对象键
/// </summary>
/// <remarks>
/// <c>/</c> 分段的路径，UTF-8 编码后 1 到 <see cref="MaxBytes"/> 字节；每段非空、不是 <c>.</c> 或 <c>..</c>；不含空白、控制字符、<c>\</c> 与孤立的代理项（成对的代理项即平面以外的字符照常合法，ADR-0316）；首段不是保留段 <see cref="ReservedSegment"/>（框架自己的对象，ADR-0090）。区分大小写
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct BlobKey
{
    /// <summary>
    /// 键文本 UTF-8 编码后的最大字节数
    /// </summary>
    /// <remarks>
    /// 不是 <see cref="string.Length"/>：一个非 ASCII 字符占 2 到 4 个字节，含这类字符的键能写的字符数少于这个数。与 S3 对象键的上限一致，查询前缀同此上限（RFC-0012，ADR-0282）
    /// </remarks>
    public const int MaxBytes = 1024;

    /// <summary>
    /// 一个 UTF-16 码元在 UTF-8 里最多占的字节数
    /// </summary>
    /// <remarks>
    /// 基本平面的字符最多 3 个字节；代理对是两个码元共 4 个字节，平均每个码元 2 个
    /// </remarks>
    private const int MaxBytesPerCodeUnit = 3;

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
    /// <remarks>
    /// 长度按 UTF-8 字节计，不按 <see cref="string.Length"/>
    /// </remarks>
    public static bool IsValid([NotNullWhen(true)] string? value)
    {
        // 字符约束与字节上限同前缀；保留前缀下的是框架自己的对象：不让业务写入，列举时以同一判定跳过
        if (string.IsNullOrEmpty(value) || !IsValidPrefix(value) || IsReserved(value))
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
    /// 前缀是否合法：与键同样的字符约束与字节上限，但允许空串与以 <c>/</c> 结尾
    /// </summary>
    /// <remarks>
    /// 前缀只是筛选：对各段不作要求，也不受保留前缀限制（以保留段开头的前缀列举总是空的）。上限同样按 UTF-8 字节计（ADR-0282），同样不能含孤立的代理项（ADR-0316）
    /// </remarks>
    public static bool IsValidPrefix([NotNullWhen(true)] string? value)
    {
        // 每个码元至少编成一个字节：码元数已经超过上限的不必再数字节。
        // 孤立的代理项编不成 UTF-8：宽松编码把它换成 U+FFFD，不同的键撞成同一个对象，签出的地址也对上另一个对象（ADR-0316）。
        // 判定用原语的那一份（RFC-0001「孤立的代理项」，ADR-0334），对象键、查询前缀、保存的文件名、上传标识与元数据的值都用它
        if (value is null || value.Length > MaxBytes || UnicodeText.HasLoneSurrogate(value))
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

        // 短到每个码元都按最多字节算也不超的不用数，其余（长键里可能有非 ASCII 字符）才数字节
        return value.Length * MaxBytesPerCodeUnit <= MaxBytes || Encoding.UTF8.GetByteCount(value) <= MaxBytes;
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
            : throw new ArgumentException($"Blob key '{value}' is invalid: it must be 1 to {MaxBytes} bytes long in UTF-8, made of non-empty '/'-separated segments other than '.' and '..', without whitespace, control characters, backslashes or lone surrogates.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;
}
