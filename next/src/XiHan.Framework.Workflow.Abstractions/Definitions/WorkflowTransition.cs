// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 两个节点之间的转移：条件为空即无条件，默认出边只在没有其他出边匹配时走。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowTransition
{
    /// <summary>
    /// 转移 ID，定义内唯一。
    /// </summary>
    public required string Id
    {
        get => field;
        init => field = WorkflowIdentifiers.IsValidElementId(value) ? value : throw new ArgumentException($"Transition ID '{value}' is invalid.", nameof(value));
    }

    /// <summary>
    /// 源节点 ID。
    /// </summary>
    public required string SourceNodeId { get; init; }

    /// <summary>
    /// 目标节点 ID。
    /// </summary>
    public required string TargetNodeId { get; init; }

    /// <summary>
    /// 条件表达式；空为无条件。作用域是实例变量加 <c>outcome</c>。
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    /// 优先级，小者先评估；默认 0。
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// 是否默认出边：没有其他出边匹配时走它。
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// 显示名。
    /// </summary>
    public string? DisplayName { get; init; }
}
