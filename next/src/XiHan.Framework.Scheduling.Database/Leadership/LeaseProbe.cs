// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Database.Entities;

namespace XiHan.Framework.Scheduling.Database.Leadership;

/// <summary>
/// 健康项 <c>scheduling.database.leader</c> 的探针。
/// </summary>
/// <remarks>
/// 租约表可达——读一次本节点竞争的租约行。不经 <c>App.InvokeAsync</c>（启动期应用还不在服务状态会被挡住，探针也不需要 Tenant / Authorize 阶段），而是在根提供者的新作用域里开一个只读工作单元读行（仓储只能在活动的工作单元内使用），读完回滚结束。
/// </remarks>
internal sealed class LeaseProbe
{
    /// <summary>
    /// 根服务提供者。
    /// </summary>
    /// <remarks>
    /// 每次探测开新作用域。
    /// </remarks>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 租约资源名，即租约行的主键。
    /// </summary>
    private readonly string _resource;

    /// <summary>
    /// 创建。
    /// </summary>
    public LeaseProbe(IServiceProvider services, string resource)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));
    }

    /// <summary>
    /// 读一次租约行。
    /// </summary>
    /// <remarks>
    /// 读到或没读到都算可达，仓储与工作单元抛出的异常原样传播，由探针宿主记成 Degraded。
    /// </remarks>
    public async ValueTask<Result> ProbeAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: true), cancellationToken).ConfigureAwait(false);
        try
        {
            _ = await scope.ServiceProvider.GetRequiredService<IRepository<SchedulerLeaseEntity, string>>().FindAsync(_resource, cancellationToken).ConfigureAwait(false);
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
