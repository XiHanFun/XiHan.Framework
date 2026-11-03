// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 单例授予存储读数据库的方式
/// </summary>
/// <remarks>
/// 在根提供者的新作用域里直接开一个只读工作单元，取仓储读完即提交。不经 <c>App.InvokeAsync</c> 再走一遍管道：授予是在 Authorize 阶段被查询的，嵌套调用会再次经过 Tenant / Authorize 阶段（要求租户时没有租户就失败），而且要求应用处于服务状态。授予是平台级数据，实体不实现 <c>IMultiTenant</c>，工作单元不带租户。数据库故障（开启、读取、提交失败，含连接与超时）返回 <see cref="SecurityErrors.StoreUnavailable"/> 并记警告日志；调用方取消照常抛出 <see cref="OperationCanceledException"/>；容器里缺工作单元或仓储是装配错误，照常抛出
/// </remarks>
internal sealed partial class GrantInvocation
{
    /// <summary>
    /// 只读、无租户的工作单元选项
    /// </summary>
    private static readonly UnitOfWorkOptions _readOnly = new(IsReadOnly: true);

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
    public GrantInvocation(IServiceProvider services, ILogger logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 在新作用域的只读工作单元里读仓储并返回结果
    /// </summary>
    /// <remarks>
    /// 数据库故障时返回 <see cref="SecurityErrors.StoreUnavailable"/>
    /// </remarks>
    public async Task<Result<T>> ReadAsync<T>(Func<IRepository<PermissionGrantRecord, Guid>, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        // 解析放在 try 之外：容器里没有工作单元或仓储是装配错误，不归为存储不可达
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<PermissionGrantRecord, Guid>>();
        try
        {
            await unitOfWork.BeginAsync(_readOnly, cancellationToken).ConfigureAwait(false);
            var result = await read(repository, cancellationToken).ConfigureAwait(false);
            var committed = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            if (committed.IsFailure)
            {
                LogCommitFailed(_logger, committed.Error!.Code);
                return Unavailable(committed.Error);
            }

            return Result.Success(result);
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
            // 授予读不到不能当成没有授予，Authorize 阶段据此以 503 结束调用而不是 403
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
        => new Error(SecurityErrors.StoreUnavailable, SecurityMessages.GrantStoreUnavailable, inner: cause);

    /// <summary>
    /// 工作单元仍活动时回滚
    /// </summary>
    /// <remarks>
    /// 用 None 令牌保证取消引发的异常也能完成回滚；回滚本身失败（数据库已断开时常见）只记日志，不覆盖已经确定的结果
    /// </remarks>
    private async Task RollbackAsync(IUnitOfWork unitOfWork)
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
    /// 读授予时数据库抛出异常
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading permission grants from the database failed; the authorization check ends as unavailable.")]
    private static partial void LogReadFailed(ILogger logger, Exception exception);

    /// <summary>
    /// 只读工作单元提交失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Committing the read-only unit of work for permission grants failed with '{Code}'; the authorization check ends as unavailable.")]
    private static partial void LogCommitFailed(ILogger logger, string code);

    /// <summary>
    /// 回滚失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Rolling back the read-only unit of work for permission grants failed.")]
    private static partial void LogRollbackFailed(ILogger logger, Exception exception);
}
