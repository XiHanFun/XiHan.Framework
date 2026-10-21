// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 权限定义：权限码与说明。权限码格式 <c>&lt;领域&gt;.&lt;资源&gt;.&lt;动作&gt;</c>，小写点分。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record PermissionDefinition
{
    /// <summary>
    /// 创建权限定义。
    /// </summary>
    public PermissionDefinition(string code, string displayName, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (!CodePattern().IsMatch(code))
        {
            throw new ArgumentException($"权限码 '{code}' 必须是至少两段的小写点分标识。", nameof(code));
        }

        Code = code;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>
    /// 权限码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 显示名。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; }

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$")]
    private static partial Regex CodePattern();
}
