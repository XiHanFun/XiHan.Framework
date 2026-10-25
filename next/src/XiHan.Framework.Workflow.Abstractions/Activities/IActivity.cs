// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动：节点的执行逻辑。作用域服务，每次节点执行在新作用域、新上下文里经完整内核管道调用；返回失败的结果即节点故障。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IActivity
{
    /// <summary>
    /// 执行。
    /// </summary>
    ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken);
}
