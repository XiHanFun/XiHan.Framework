// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 没有解析出指派人时怎么办。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum EmptyAssigneeStrategy
{
    /// <summary>
    /// 节点故障（默认）。
    /// </summary>
    Fault = 1,

    /// <summary>
    /// 自动通过。
    /// </summary>
    AutoApprove = 2,

    /// <summary>
    /// 自动拒绝。
    /// </summary>
    AutoReject = 3,

    /// <summary>
    /// 用 <c>FallbackAssigneeRules</c> 再解析一次。
    /// </summary>
    Fallback = 4,
}
