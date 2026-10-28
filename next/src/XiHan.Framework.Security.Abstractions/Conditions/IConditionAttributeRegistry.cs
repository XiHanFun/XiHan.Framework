// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件属性登记表。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IConditionAttributeRegistry
{
    /// <summary>
    /// 全部登记项（含族）。
    /// </summary>
    IReadOnlyList<ConditionAttributeDescriptor> Attributes { get; }

    /// <summary>
    /// 按属性名（不区分大小写）查登记项：先精确名，再别名，再最长匹配的族。
    /// </summary>
    bool TryResolve(string attributeName, [NotNullWhen(true)] out ConditionAttributeResolution? resolution);
}
