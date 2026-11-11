// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 单例目录读数据库的方式
/// </summary>
/// <remarks>
/// 在根提供者的新作用域里直接开只读工作单元取仓储，读完提交；数据库故障回滚后以 <see cref="MultiTenancyErrors.StoreUnavailable"/> 返回。不经 <c>App.InvokeAsync</c> 再走一遍管道：目录是 Tenant 阶段自己要查的东西，嵌套调用会再过一遍 Tenant / Authorize 阶段（<c>RequireTenant</c> 为真时嵌套调用没有租户就失败，目录读自己成了自指），而且要求应用处于服务状态，启动期读不了。租户是平台级数据，<see cref="TenantRecord"/> 不实现 <c>IMultiTenant</c>，工作单元不带租户
/// </remarks>
internal sealed partial class CatalogInvocation
{
    /// <summary>
    /// 只读、无租户、读主库的工作单元选项
    /// </summary>
    /// <remarks>
    /// 目录只读不写，Provider 不必开写事务。目录决定租户进不进得来：读到落后的从库，停用就要多等复制延迟，读回来的旧值还要再缓存一轮（ADR-0122）
    /// </remarks>
    private static readonly UnitOfWorkOptions _readOnly = new(IsReadOnly: true) { ReadsPrimary = true };

    /// <summary>
    /// 根服务提供者
    /// </summary>
    /// <remarks>
    /// 每次读取开新作用域
    /// </remarks>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 日志
    /// </summary>
    /// <remarks>
    /// 记录数据库故障的原因：错误本身不带底层异常的文本，不对调用方暴露连接串、主机名一类信息
    /// </remarks>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    public CatalogInvocation(IServiceProvider services, ILogger logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 在新作用域的只读工作单元里用仓储读取并返回结果
    /// </summary>
    /// <remarks>
    /// 数据库故障（连接、超时、提交失败）返回 <see cref="MultiTenancyErrors.StoreUnavailable"/>，调用方取消照常抛出
    /// </remarks>
    public async ValueTask<Result<T>> ReadAsync<T>(Func<IRepository<TenantRecord, string>, CancellationToken, ValueTask<T>> read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        await using var scope = _services.CreateAsyncScope();
        // 解析放在 try 之外：容器里没有工作单元或仓储是装配错误，不归为存储不可达
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<TenantRecord, string>>();
        try
        {
            // 仓储只在活动的工作单元内工作，读也要先开：这里不在任何调用里，没有 Transaction 阶段替我们开
            await unitOfWork.BeginAsync(_readOnly, cancellationToken).ConfigureAwait(false);
            var value = await read(repository, cancellationToken).ConfigureAwait(false);
            var committed = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            if (committed.IsFailure)
            {
                LogCommitFailed(_logger, committed.Error!.Code);
                return Unavailable(committed.Error);
            }

            return Result.Success(value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 调用方取消不是存储故障：回滚后照常抛出，按异步约定结束
            await RollbackAsync(unitOfWork).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            // 连接、超时（含驱动以取消异常报告的命令超时）、事务失败都归为存储不可达：
            // 租户读不到不能当成不存在，调用据此以 503 结束而不是 404
            LogReadFailed(_logger, exception);
            await RollbackAsync(unitOfWork).ConfigureAwait(false);
            return Unavailable(exception is FrameworkException framework ? framework.ToError() : null);
        }
    }

    /// <summary>
    /// 存储不可达的错误
    /// </summary>
    /// <remarks>
    /// <paramref name="cause"/> 作为内部错误保留，不进响应
    /// </remarks>
    private static Error Unavailable(Error? cause)
        => new(MultiTenancyErrors.StoreUnavailable, MultiTenancyMessages.StoreUnavailable, inner: cause);

    /// <summary>
    /// 工作单元仍活动时回滚
    /// </summary>
    /// <remarks>
    /// 用 None 令牌保证取消引发的异常也能完成回滚；回滚本身失败（数据库已断开时常见）只记日志，不覆盖已经确定的结果
    /// </remarks>
    private async ValueTask RollbackAsync(IUnitOfWork unitOfWork)
    {
        if (unitOfWork.State != UnitOfWorkState.Active)
        {
            return;
        }

        try
        {
            await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogRollbackFailed(_logger, exception);
        }
    }

    /// <summary>
    /// 读租户目录失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the tenant catalog failed; the tenant store is reported as unavailable.")]
    private static partial void LogReadFailed(ILogger logger, Exception exception);

    /// <summary>
    /// 提交只读工作单元失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Committing the read-only unit of work of the tenant catalog failed with '{ErrorCode}'.")]
    private static partial void LogCommitFailed(ILogger logger, string errorCode);

    /// <summary>
    /// 回滚失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Rolling back the unit of work of the tenant catalog failed.")]
    private static partial void LogRollbackFailed(ILogger logger, Exception exception);
}
