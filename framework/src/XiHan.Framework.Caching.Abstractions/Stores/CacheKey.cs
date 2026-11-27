// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 缓存键
/// </summary>
/// <remarks>
/// 非空、不含空白、控制字符与孤立的代理项（成对的代理项即平面以外的字符照常合法，ADR-0316）、UTF-8 编码后不超过 <see cref="MaxBytes"/> 字节。区分大小写
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct CacheKey
{
    /// <summary>
    /// 键文本 UTF-8 编码后的最大字节数
    /// </summary>
    /// <remarks>
    /// 不是 <see cref="string.Length"/>：一个非 ASCII 字符占 2 到 4 个字节，含这类字符的键能写的字符数少于这个数（RFC-0006、ADR-0274）
    /// </remarks>
    public const int MaxBytes = 512;

    /// <summary>
    /// 一个 UTF-16 码元在 UTF-8 里最多占的字节数
    /// </summary>
    /// <remarks>
    /// 基本平面的字符最多 3 个字节；代理对是两个码元共 4 个字节，平均每个码元 2 个
    /// </remarks>
    private const int MaxBytesPerCodeUnit = 3;

    /// <summary>
    /// 校验过的文本
    /// </summary>
    /// <remarks>
    /// 只有 <see langword="default"/> 值为 <see langword="null"/>
    /// </remarks>
    private readonly string? _value;

    /// <summary>
    /// 创建键
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public CacheKey(string value)
    {
        _value = Validate(value);
    }

    /// <summary>
    /// 键文本
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> 值没有键文本，读取抛出 <see cref="InvalidOperationException"/>（RFC-0001「标识值」）
    /// </remarks>
    public string Value => _value ?? throw new InvalidOperationException("The 'CacheKey' is the default value and has no text; create it with the constructor.");

    /// <summary>
    /// 从字符串显式创建
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>；会抛出的转换不做成隐式（RFC-0001「标识值」）
    /// </remarks>
    public static explicit operator CacheKey(string value) => new(value);

    /// <summary>
    /// 是否合法
    /// </summary>
    /// <remarks>
    /// 长度按 UTF-8 字节计，不按 <see cref="string.Length"/>
    /// </remarks>
    public static bool IsValid(string? value)
    {
        // 每个码元至少编成一个字节：码元数已经超过上限的不必再数字节。
        // 孤立的代理项编不成 UTF-8：宽松编码（Redis 的键、System.Text.Json 写出的远程请求）把它换成 U+FFFD，两个不同的键撞成同一个条目（ADR-0316）。
        // 判定用原语的那一份（RFC-0001「孤立的代理项」，ADR-0334）
        if (string.IsNullOrEmpty(value) || value.Length > MaxBytes || UnicodeText.HasLoneSurrogate(value))
        {
            return false;
        }

        // 逐字符拒绝空白与控制字符：键会出现在日志、协议行与前缀拼接里，这些字符会造成截断或歧义
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return false;
            }
        }

        // 短到每个码元都按最多字节算也不超的不用数，其余（长键里可能有非 ASCII 字符）才数字节
        return value.Length * MaxBytesPerCodeUnit <= MaxBytes || Encoding.UTF8.GetByteCount(value) <= MaxBytes;
    }

    /// <summary>
    /// 是否是合法的键前缀
    /// </summary>
    /// <remarks>
    /// 与键同文法（上限同样按 UTF-8 字节计），但可以为空（空前缀表示全部，ADR-0055）
    /// </remarks>
    public static bool IsValidPrefix(string? value) => value is not null && (value.Length == 0 || IsValid(value));

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
            : throw new ArgumentException($"Cache key '{value}' is invalid: it must be non-empty, contain no whitespace, control characters or lone surrogates, and be at most {MaxBytes} bytes long in UTF-8.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;
}
