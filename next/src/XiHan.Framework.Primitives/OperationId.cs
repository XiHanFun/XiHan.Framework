// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 操作 ID 文法：<c>&lt;resource&gt;.&lt;verb&gt;</c>，资源是小写复数名词（连字符分隔），动词小写；见 <c>spec/conventions/operation-ids.md</c>。
/// 协议适配器按它推导路由（<c>orders.create</c> → <c>POST /orders</c>）、gRPC 方法与 MCP tool 名。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class OperationId
{
    /// <summary>
    /// 最大长度。
    /// </summary>
    public const int MaxLength = 128;

    /// <summary>
    /// 是否合法。
    /// </summary>
    public static bool IsValid([NotNullWhen(true)] string? id)
        => id is not null && id.Length <= MaxLength && Pattern().IsMatch(id);

    /// <summary>
    /// 校验；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string Validate(string id, string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(id, paramName ?? nameof(id));
        return IsValid(id)
            ? id
            : throw new ArgumentException($"Operation ID '{id}' is not valid; expected '<resource>.<verb>' such as 'orders.create'.", paramName ?? nameof(id));
    }

    /// <summary>
    /// 资源段（<c>orders.create</c> → <c>orders</c>）。
    /// </summary>
    public static string ResourceOf(string id)
    {
        Validate(id);
        return id[..id.IndexOf('.', StringComparison.Ordinal)];
    }

    /// <summary>
    /// 动词段（<c>orders.create</c> → <c>create</c>）。
    /// </summary>
    public static string VerbOf(string id)
    {
        Validate(id);
        return id[(id.IndexOf('.', StringComparison.Ordinal) + 1)..];
    }

    /// <summary>
    /// 操作 ID 文法：资源与动词都是小写字母开头、连字符分隔的小写字母数字段，中间一个点。
    /// </summary>
    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*\\.[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex Pattern();
}
