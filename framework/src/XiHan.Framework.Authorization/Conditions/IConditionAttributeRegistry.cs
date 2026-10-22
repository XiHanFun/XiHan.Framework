// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件属性登记表
/// </summary>
public interface IConditionAttributeRegistry
{
    /// <summary>
    /// 全部登记项（含族）
    /// </summary>
    IReadOnlyList<ConditionAttributeDescriptor> Attributes { get; }

    /// <summary>
    /// 按属性名查登记项：先精确名，再别名，再最长匹配的族
    /// </summary>
    /// <param name="attributeName">属性名（不区分大小写）</param>
    /// <param name="resolution">查到时的解析结果</param>
    /// <returns>是否查到</returns>
    bool TryResolve(string attributeName, out ConditionAttributeResolution resolution);
}
