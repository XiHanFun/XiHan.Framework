// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Secrets;

/// <summary>
/// 密钥名
/// </summary>
/// <remarks>
/// <c>^[A-Za-z0-9][A-Za-z0-9._/-]{0,255}$</c>，不含 <c>..</c> 与 <c>//</c>，不以 <c>/</c> 结尾；大小写敏感
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public readonly partial record struct SecretName
{
    /// <summary>
    /// 最大长度
    /// </summary>
    public const int MaxLength = 256;

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
    public SecretName(string value)
    {
        _value = Validate(value);
    }

    /// <summary>
    /// 名字文本
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> 值没有名字文本，读取抛出 <see cref="InvalidOperationException"/>（RFC-0001「标识值」）
    /// </remarks>
    public string Value => _value ?? throw new InvalidOperationException("The 'SecretName' is the default value and has no text; create it with the constructor.");

    /// <summary>
    /// 从字符串显式创建
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>；会抛出的转换不做成隐式（RFC-0001「标识值」）
    /// </remarks>
    public static explicit operator SecretName(string value) => new(value);

    /// <summary>
    /// 是否合法
    /// </summary>
    public static bool IsValid([NotNullWhen(true)] string? value)
        => value is not null && Pattern().IsMatch(value) && !value.Contains("..", StringComparison.Ordinal) && !value.Contains("//", StringComparison.Ordinal) && !value.EndsWith('/');

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
            : throw new ArgumentException($"Secret name '{value}' is invalid: it must match ^[A-Za-z0-9][A-Za-z0-9._/-]{{0,255}}$ without '..', '//' or a trailing '/'.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;

    /// <summary>
    /// 名字的匹配
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._/-]{0,255}$")]
    private static partial Regex Pattern();
}
