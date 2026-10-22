// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件属性登记选项
/// </summary>
/// <remarks>
/// 应用经 <c>AddXiHanConditionAttributes</c> 往这里追加登记项；登记表在首次解析时一次建好，重名、别名冲突即抛出。
/// </remarks>
public class XiHanConditionAttributeOptions
{
    /// <summary>
    /// 登记项
    /// </summary>
    public List<ConditionAttributeDescriptor> Attributes { get; } = [];
}
