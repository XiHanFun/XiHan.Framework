// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 结束节点：没有 <c>Outcome</c> 时只完成本分支（没有剩余令牌与书签时实例完成）；有 <c>Outcome</c> 时先依次调用结果处理器，再以该结果结束实例。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EndActivity : IActivity
{
    /// <summary>
    /// 属性：结果。
    /// </summary>
    public const string OutcomeProperty = "Outcome";

    /// <inheritdoc />
    public async ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rendered = context.RenderProperty(OutcomeProperty);
        if (!rendered.TryGetValue(out var outcome))
        {
            return rendered.Error!;
        }

        if (string.IsNullOrWhiteSpace(outcome))
        {
            return ActivityOutcome.Complete();
        }

        // 结果处理器在实例锁内依次运行，任何一个失败即节点故障（实例不会以半处理的结果结束）
        var outcomeContext = new WorkflowOutcomeContext(context, outcome);
        foreach (var handler in context.Services.GetServices<IWorkflowOutcomeHandler>())
        {
            var handled = await handler.HandleAsync(outcomeContext, cancellationToken).ConfigureAwait(false);
            if (handled.IsFailure)
            {
                return handled.Error!;
            }
        }

        return ActivityOutcome.EndInstance(label: outcome);
    }
}
