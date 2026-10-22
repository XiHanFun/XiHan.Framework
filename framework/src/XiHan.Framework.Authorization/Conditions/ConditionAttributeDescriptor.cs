// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件属性登记项
/// </summary>
/// <remarks>
/// 名称以 <c>.*</c> 结尾表示一族属性（如 <c>resource.query.*</c> 匹配 <c>resource.query.id</c>），族内同一值种类；
/// 精确登记优先于族。别名用于兼容旧写法，编译结果会标出用了别名的子句。
/// </remarks>
/// <param name="Name">登记名（不区分大小写）</param>
/// <param name="Kind">值种类</param>
/// <param name="IsMultiValued">是否多值（如角色编码列表）</param>
/// <param name="DisplayName">显示名</param>
/// <param name="Aliases">旧名别名</param>
public sealed record ConditionAttributeDescriptor(
    string Name,
    ConditionValueKind Kind,
    bool IsMultiValued = false,
    string? DisplayName = null,
    IReadOnlyList<string>? Aliases = null)
{
    /// <summary>
    /// 是否为一族属性（名称以 <c>.*</c> 结尾）
    /// </summary>
    public bool IsFamily => Name.EndsWith(".*", StringComparison.Ordinal);
}
