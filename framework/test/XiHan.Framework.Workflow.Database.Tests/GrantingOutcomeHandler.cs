// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Tasks;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 审批通过即写一行 <see cref="ApprovalGrant"/> 的结果处理器
/// </summary>
/// <remarks>
/// 经结束节点所在的作用域取仓储：与引擎的写入同一个工作单元
/// </remarks>
public sealed class GrantingOutcomeHandler : IWorkflowOutcomeHandler
{
    /// <inheritdoc />
    public async ValueTask<Result> HandleAsync(WorkflowOutcomeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(context.Outcome, UserTaskOutcomes.Approved, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        var repository = context.Services.GetRequiredService<IRepository<ApprovalGrant, string>>();
        return await repository.AddAsync(new ApprovalGrant { Id = Guid.CreateVersion7().ToString("N"), InstanceId = context.Instance.Id }, cancellationToken).ConfigureAwait(false);
    }
}
