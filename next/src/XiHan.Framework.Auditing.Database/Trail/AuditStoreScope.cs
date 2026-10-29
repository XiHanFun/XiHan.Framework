// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;

namespace XiHan.Framework.Auditing.Database.Trail;

/// <summary>
/// 审计记录的读写方式。
/// </summary>
/// <remarks>
/// 每个操作在根提供者的新作用域里直接开一个工作单元，动作返回时提交，异常时回滚。不经 <c>App.InvokeAsync</c> 再走一遍管道：写入由 Observe 阶段的中间件或后台线程发起、查询可能来自 Authorize 阶段之前，再过 Tenant / Authorize 阶段会在要求租户时失败，而且启动与停止期间应用不在服务状态也要能落库。审计记录是平台级数据：实体不实现 <c>IMultiTenant</c>，工作单元不带租户。
/// </remarks>
internal sealed class AuditStoreScope
{
    /// <summary>
    /// 根服务提供者。
    /// </summary>
    /// <remarks>
    /// 每次操作开新作用域。
    /// </remarks>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 创建。
    /// </summary>
    public AuditStoreScope(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// 在新作用域的工作单元里执行 <paramref name="action"/> 并返回它的值。
    /// </summary>
    /// <remarks>
    /// 提交失败抛出携带持久化错误码的 <c>FrameworkException</c>。
    /// </remarks>
    public async Task<T> RunAsync<T>(bool readOnly, Func<IRepository<AuditRecord, Guid>, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: readOnly), cancellationToken).ConfigureAwait(false);
        try
        {
            var value = await action(scope.ServiceProvider.GetRequiredService<IRepository<AuditRecord, Guid>>(), cancellationToken).ConfigureAwait(false);
            (await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            return value;
        }
        catch
        {
            // 只在工作单元仍活动时回滚；用 None 令牌保证取消引发的异常也能完成回滚
            if (unitOfWork.State == UnitOfWorkState.Active)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }
}
