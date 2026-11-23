// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Polling;

/// <summary>
/// 健康项 <c>workflow.database.poller</c> 的探针
/// </summary>
/// <remarks>
/// 轮询领导权的租约表可达——读一次 <see cref="PollerLeadership.Resource"/> 的租约行。不经 <c>App.InvokeAsync</c>（启动期应用还不在服务状态会被挡住，探针也不需要 Tenant / Authorize 阶段），而是在根提供者的新作用域里开一个只读工作单元读行（仓储只能在活动的工作单元内使用；<see cref="UnitOfWorkScope"/>，ADR-0259）；配置了 <see cref="DatabaseWorkflowOptions.LockConnection"/> 时工作单元绑定这个连接，探的就是租约库
/// </remarks>
internal sealed class PollerLeaseProbe
{
    /// <summary>
    /// 根服务提供者
    /// </summary>
    /// <remarks>
    /// 每次探测开新作用域
    /// </remarks>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 只读工作单元的选项，绑定租约表的命名连接（未配置为缺省落点）
    /// </summary>
    private readonly UnitOfWorkOptions _unitOfWork;

    /// <summary>
    /// 日志
    /// </summary>
    /// <remarks>
    /// 只记收尾（回滚、释放作用域）的失败；探到的异常由探针宿主记
    /// </remarks>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    public PollerLeaseProbe(IServiceProvider services, DatabaseWorkflowOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unitOfWork = new UnitOfWorkOptions { IsReadOnly = true, Connection = options.LockConnection };
    }

    /// <summary>
    /// 读一次租约行
    /// </summary>
    /// <remarks>
    /// 读到或没读到都算可达，仓储与工作单元抛出的异常原样传播，由探针宿主记成 Degraded
    /// </remarks>
    public ValueTask<Result> ProbeAsync(CancellationToken cancellationToken)
        => UnitOfWorkScope.RunAsync(
            _services,
            _unitOfWork,
            static async (services, ct) =>
            {
                _ = await services.GetRequiredService<IRepository<WorkflowLockRecord, string>>().FindAsync(PollerLeadership.Resource, ct).ConfigureAwait(false);
                return Result.Success();
            },
            _logger,
            cancellationToken);
}
