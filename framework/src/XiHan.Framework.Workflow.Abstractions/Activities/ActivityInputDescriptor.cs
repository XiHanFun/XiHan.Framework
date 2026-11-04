// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动的一个输入属性（节点 <c>Properties</c> 里的键）
/// </summary>
/// <param name="Name">属性名</param>
/// <param name="Kind">值的种类：<c>string</c>、<c>number</c>、<c>boolean</c>、<c>object</c>、<c>array</c>、<c>expression</c>、<c>template</c></param>
/// <param name="Required">是否必填</param>
/// <param name="Description">说明（没有键或目录里找不到时的回退）</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ActivityInputDescriptor(string Name, string Kind, bool Required = false, string? Description = null)
{
    /// <summary>
    /// 说明的消息键
    /// </summary>
    /// <remarks>
    /// 有键时 <see cref="Localize"/> 按当前区域的文案替换 <see cref="Description"/>（RFC-0010，ADR-0106）
    /// </remarks>
    public string? DescriptionKey { get; init; }

    /// <summary>
    /// 按当前消息作用域渲染说明
    /// </summary>
    public ActivityInputDescriptor Localize() => this with { Description = ActivityDescriptor.Resolve(DescriptionKey) ?? Description };
}
