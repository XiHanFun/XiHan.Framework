// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 错误码格式：<c>&lt;领域大写&gt;_&lt;KIND&gt;_&lt;NNN&gt;</c>，例如 <c>PERSISTENCE_CONFLICT_001</c>。
/// 格式与登记表在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class ErrorCode
{
    /// <summary>
    /// 原语参数非法。
    /// </summary>
    public const string InvalidArgument = "PRIMITIVES_VALIDATION_001";

    /// <summary>
    /// 使用了未初始化的 <see cref="Result"/> 或 <see cref="Result{T}"/>（default 值）。
    /// </summary>
    public const string UninitializedResult = "PRIMITIVES_VALIDATION_002";

    /// <summary>
    /// 判断错误码是否符合格式。
    /// </summary>
    public static bool IsValid(string? code) => code is not null && Pattern().IsMatch(code);

    /// <summary>
    /// 校验错误码格式，不合法时抛出 <see cref="ArgumentException"/>。
    /// </summary>
    /// <returns>原样返回 <paramref name="code"/>，便于在初始化表达式中使用。</returns>
    public static string Validate(string code, [CallerArgumentExpression(nameof(code))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(code, paramName);
        if (!Pattern().IsMatch(code))
        {
            throw new ArgumentException($"Error code '{code}' does not match the '<DOMAIN>_<KIND>_<NNN>' format.", paramName);
        }

        return code;
    }

    /// <summary>
    /// 取错误码的 KIND 段，例如 <c>PERSISTENCE_CONFLICT_001</c> 的 <c>CONFLICT</c>。
    /// </summary>
    public static string KindOf(string code)
    {
        Validate(code);
        // 格式已保证恰好三段，KIND 位于首个与末个下划线之间。
        var first = code.IndexOf('_', StringComparison.Ordinal);
        var last = code.LastIndexOf('_');
        return code[(first + 1)..last];
    }

    /// <summary>
    /// 取错误码的领域段，例如 <c>PERSISTENCE_CONFLICT_001</c> 的 <c>PERSISTENCE</c>。
    /// </summary>
    public static string DomainOf(string code)
    {
        Validate(code);
        return code[..code.IndexOf('_', StringComparison.Ordinal)];
    }

    /// <summary>
    /// 错误码格式的正则：三段大写标识，末段为三位数字。
    /// </summary>
    [GeneratedRegex("^[A-Z][A-Z0-9]*_[A-Z][A-Z0-9]*_[0-9]{3}$")]
    private static partial Regex Pattern();
}
