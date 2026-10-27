// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 同一定义名与关联 ID 下已有未结束实例时的启动策略；没有关联 ID 时不生效。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum WorkflowReusePolicy
{
    /// <summary>
    /// 照常再启动一个（默认）。
    /// </summary>
    AllowDuplicate = 0,

    /// <summary>
    /// 拒绝：<see cref="WorkflowErrors.ActiveInstanceExists"/>。
    /// </summary>
    RejectActive = 1,

    /// <summary>
    /// 返回已有的实例，不启动新的。
    /// </summary>
    ReturnActive = 2,
}
