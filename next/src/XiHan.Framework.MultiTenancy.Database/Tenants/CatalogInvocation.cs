// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 单例目录读数据库的方式。
/// </summary>
/// <remarks>
/// 在根提供者的新作用域里直接开只读工作单元取仓储，读完提交，异常回滚后原样抛出。不经 <c>App.InvokeAsync</c> 再走一遍管道：目录是 Tenant 阶段自己要查的东西，嵌套调用会再过一遍 Tenant / Authorize 阶段（<c>RequireTenant</c> 为真时嵌套调用没有租户就失败，目录读自己成了自指），而且要求应用处于服务状态，启动期读不了。租户是平台级数据，<see cref="TenantRecord"/> 不实现 <c>IMultiTenant</c>，工作单元不带租户。
/// </remarks>
internal sealed class CatalogInvocation
{
    /// <summary>
    /// 只读、无租户的工作单元选项：目录只读不写，Provider 不必开写事务。
    /// </summary>
    private static readonly UnitOfWorkOptions _readOnly = new(IsReadOnly: true);

    /// <summary>
    /// 根服务提供者。
    /// </summary>
    /// <remarks>
    /// 每次读取开新作用域。
    /// </remarks>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 创建。
    /// </summary>
    public CatalogInvocation(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// 在新作用域的只读工作单元里用仓储读取并返回结果。
    /// </summary>
    /// <remarks>
    /// 提交失败抛出 <see cref="FrameworkException"/>，基础设施异常原样抛出。
    /// </remarks>
    public async ValueTask<T> ReadAsync<T>(Func<IRepository<TenantRecord, string>, CancellationToken, ValueTask<T>> read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        // 仓储只在活动的工作单元内工作，读也要先开：这里不在任何调用里，没有 Transaction 阶段替我们开
        await unitOfWork.BeginAsync(_readOnly, cancellationToken).ConfigureAwait(false);
        try
        {
            var value = await read(scope.ServiceProvider.GetRequiredService<IRepository<TenantRecord, string>>(), cancellationToken).ConfigureAwait(false);
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
