// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单 ID 格式：<c>xihan.&lt;领域&gt;.&lt;名称&gt;</c>，例如 <c>xihan.persistence.efcore</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class ManifestId
{
    /// <summary>
    /// 是否符合格式。
    /// </summary>
    public static bool IsValid(string? id) => id is not null && Pattern().IsMatch(id);

    /// <summary>
    /// 校验格式，不合法时抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string Validate(string id, [CallerArgumentExpression(nameof(id))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(id, paramName);
        if (!Pattern().IsMatch(id))
        {
            throw new ArgumentException($"Manifest ID '{id}' does not match the 'xihan.<domain>.<name>' format.", paramName);
        }

        return id;
    }

    /// <summary>
    /// 清单 ID 格式的正则。
    /// </summary>
    /// <remarks>
    /// <c>xihan.</c> 前缀，小写领域段，小写字母开头的名称段（允许数字与连字符）。
    /// </remarks>
    [GeneratedRegex("^xihan\\.[a-z][a-z0-9]*\\.[a-z][a-z0-9-]*$")]
    private static partial Regex Pattern();
}
