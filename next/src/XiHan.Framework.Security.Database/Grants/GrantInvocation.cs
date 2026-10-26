// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 单例授予存储读数据库的方式：在根提供者的新作用域里直接开一个只读工作单元，取仓储读完即提交。
/// 不经 <c>App.InvokeAsync</c> 再走一遍管道：授予是在 Authorize 阶段被查询的，嵌套调用会再次经过 Tenant / Authorize 阶段（要求租户时没有租户就失败），而且要求应用处于服务状态。
/// 授予是平台级数据，实体不实现 <c>IMultiTenant</c>，工作单元不带租户。基础设施异常原样抛出，由内核转成 Unexpected 结果。
/// </summary>
internal sealed class GrantInvocation
{
    /// <summary>
    /// 只读、无租户的工作单元选项。
    /// </summary>
    private static readonly UnitOfWorkOptions _readOnly = new(IsReadOnly: true);

    /// <summary>
    /// 根服务提供者；每次读取开新作用域。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 创建。
    /// </summary>
    public GrantInvocation(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// 在新作用域的只读工作单元里读仓储并返回结果。
    /// </summary>
    public async Task<T> ReadAsync<T>(Func<IRepository<PermissionGrantRecord, Guid>, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<PermissionGrantRecord, Guid>>();
        await unitOfWork.BeginAsync(_readOnly, cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await read(repository, cancellationToken).ConfigureAwait(false);
            (await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            return result;
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
