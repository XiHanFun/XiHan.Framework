// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Polling;

/// <summary>
/// 健康项 <c>workflow.database.poller</c> 的探针：轮询领导权的租约表可达——读一次 <see cref="PollerLeadership.Resource"/> 的租约行。
/// 不经 <c>App.InvokeAsync</c>（启动期应用还不在服务状态会被挡住，探针也不需要 Tenant / Authorize 阶段），而是在根提供者的新作用域里开一个只读工作单元读行（仓储只能在活动的工作单元内使用），读完回滚结束。
/// </summary>
internal sealed class PollerLeaseProbe
{
    /// <summary>
    /// 根服务提供者；每次探测开新作用域。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 创建。
    /// </summary>
    public PollerLeaseProbe(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// 读一次租约行；读到或没读到都算可达，仓储与工作单元抛出的异常原样传播，由探针宿主记成 Degraded。
    /// </summary>
    public async ValueTask<Result> ProbeAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: true), cancellationToken).ConfigureAwait(false);
        try
        {
            _ = await scope.ServiceProvider.GetRequiredService<IRepository<WorkflowLockRecord, string>>().FindAsync(PollerLeadership.Resource, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        finally
        {
            // 只读探针不提交：工作单元仍活动就回滚结束，用 None 令牌保证取消时也能收尾
            if (unitOfWork.State == UnitOfWorkState.Active)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
