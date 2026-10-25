// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 结果处理器：带 <c>Outcome</c> 的结束节点执行时在实例锁内依次调用（登记为可枚举的作用域服务）；必须幂等，失败即节点故障。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowOutcomeHandler
{
    /// <summary>
    /// 处理结果。
    /// </summary>
    ValueTask<Result> HandleAsync(WorkflowOutcomeContext context, CancellationToken cancellationToken);
}
