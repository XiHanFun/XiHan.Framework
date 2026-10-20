// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 能力 ID 格式：<c>&lt;领域小写&gt;.&lt;能力&gt;</c>，例如 <c>persistence.relational</c>。见 <c>spec/conventions/capability-ids.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class CapabilityId
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
            throw new ArgumentException($"能力 ID '{id}' 不符合 <领域>.<能力> 格式。", paramName);
        }

        return id;
    }

    /// <summary>
    /// 取领域段，例如 <c>persistence.relational</c> 的 <c>persistence</c>。
    /// </summary>
    public static string DomainOf(string id)
    {
        Validate(id);
        return id[..id.IndexOf('.', StringComparison.Ordinal)];
    }

    [GeneratedRegex("^[a-z][a-z0-9]*\\.[a-z][a-z0-9-]*$")]
    private static partial Regex Pattern();
}

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
            throw new ArgumentException($"清单 ID '{id}' 不符合 xihan.<领域>.<名称> 格式。", paramName);
        }

        return id;
    }

    [GeneratedRegex("^xihan\\.[a-z][a-z0-9]*\\.[a-z][a-z0-9-]*$")]
    private static partial Regex Pattern();
}
