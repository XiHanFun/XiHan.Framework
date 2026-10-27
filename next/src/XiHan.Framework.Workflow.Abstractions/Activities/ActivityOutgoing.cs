// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 节点完成后如何选出边。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum ActivityOutgoing
{
    /// <summary>
    /// 取全部无条件或条件为真的出边；一条都没有时取默认出边；仍没有则实例故障。
    /// </summary>
    AllMatched = 0,

    /// <summary>
    /// 按优先级取第一条无条件或条件为真的出边；没有则默认出边；仍没有则实例故障。
    /// </summary>
    Exclusive = 1,

    /// <summary>
    /// 取全部出边（并行网关）。
    /// </summary>
    All = 2,

    /// <summary>
    /// 不流出（结束、终止、故障）。
    /// </summary>
    None = 3,
}
