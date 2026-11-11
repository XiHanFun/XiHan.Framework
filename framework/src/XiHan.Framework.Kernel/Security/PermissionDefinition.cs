// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 权限定义
/// </summary>
/// <remarks>
/// 权限码与说明。权限码小写、点分、至少两段，每段以小写字母开头，可含数字与连字符，如 <c>orders.read</c>；要带领域前缀时写成 <c>sales.orders.read</c>（RFC-0008「授权」第 2 条）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record PermissionDefinition
{
    /// <summary>
    /// 创建权限定义
    /// </summary>
    /// <remarks>
    /// <c>description</c> 取 <c>null</c>
    /// </remarks>
    public PermissionDefinition(string code, string displayName)
        : this(code, displayName, null)
    {
    }

    /// <summary>
    /// 创建权限定义
    /// </summary>
    public PermissionDefinition(string code, string displayName, string? description)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (!IsValidCode(code))
        {
            throw new ArgumentException($"Permission code '{code}' must be a lowercase dot-separated identifier with at least two segments.", nameof(code));
        }

        Code = code;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>
    /// 权限码
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 显示名
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 说明
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// 权限码是否合文法
    /// </summary>
    /// <remarks>
    /// 插件清单 <c>contributes.permissions</c> 的每一项由清单读取器按它核对（宿主要替插件登记这些码，ADR-0080）
    /// </remarks>
    internal static bool IsValidCode(string? code) => code is not null && CodePattern().IsMatch(code);

    /// <summary>
    /// 权限码格式的正则：至少两段、点分隔、每段小写字母开头并允许数字与连字符
    /// </summary>
    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$")]
    private static partial Regex CodePattern();
}
