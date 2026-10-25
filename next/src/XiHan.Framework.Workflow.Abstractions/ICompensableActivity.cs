// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 可补偿的活动：实例被取消且定义启用补偿时，按完成顺序逆序调用。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface ICompensableActivity : IActivity
{
    /// <summary>
    /// 补偿已完成的执行。
    /// </summary>
    ValueTask<Result> CompensateAsync(ActivityContext context, CancellationToken cancellationToken);
}
