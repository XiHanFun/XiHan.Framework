// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// 管道之外的工作单元外壳
/// </summary>
/// <remarks>
/// 单例存储、后台循环与健康探针不在任何调用里，没有 Transaction 阶段替它们开工作单元。这里把那一段收成一处：在 <c>root</c> 的新服务作用域里解析 <see cref="IUnitOfWork"/>、按给定选项开始、执行 <c>body</c>，再按结果收尾（ADR-0259）
/// <para>
/// 收尾规则：<c>body</c> 返回成功即提交，提交返回的失败原样返回；<c>body</c> 返回失败即回滚并原样返回；开始、<c>body</c> 或提交抛出即回滚。回滚只在工作单元仍活动时进行，一律用 <see cref="CancellationToken.None"/>。回滚与释放作用域自身的失败只经 <c>logger</c> 记警告，不改变已经确定的结果或异常
/// </para>
/// <para>
/// 抛出的异常有两种交还方式。不带 <c>fault</c> 的重载回滚后原样抛出。带 <c>fault</c> 的重载回滚后把异常交给 <c>fault</c> 映射成调用方自己的错误并作为失败结果返回：外壳不认识各子系统的错误码，也不替调用方记这条日志——异常消息是驱动写的，可能带连接串与主机名：错误里只该写异常类型的全名（<see cref="FrameworkException"/> 可以写它的错误码），完整异常由调用方记进日志
/// </para>
/// <para>
/// 取消只有一种：抛出的是 <see cref="OperationCanceledException"/> 且调用方的令牌已请求取消。这时回滚后原样抛出，不交给 <c>fault</c>。令牌没有取消时的 <see cref="OperationCanceledException"/>（驱动把命令超时报告成取消）与其他异常一样是故障
/// </para>
/// <para>
/// 容器里没有 <see cref="IUnitOfWork"/> 是装配错误，原样抛出，不交给 <c>fault</c>。<c>body</c> 收到的是新作用域的服务提供者，仓储从它取；不经管道，所以没有 Tenant / Authorize 阶段，租户、只读、命名连接与读主库都由选项决定，领域事件也不在这里派发
/// </para>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static partial class UnitOfWorkScope
{
    /// <summary>
    /// 在新作用域的工作单元里执行并取回值，异常回滚后原样抛出
    /// </summary>
    /// <param name="root">服务提供者，每次调用在它上面开新作用域</param>
    /// <param name="options">开始工作单元的选项</param>
    /// <param name="body">在活动的工作单元里执行的动作，参数是新作用域的服务提供者</param>
    /// <param name="logger">记录回滚与释放作用域失败的日志</param>
    /// <param name="cancellationToken">取消令牌，传给开始、动作与提交</param>
    /// <returns>动作的结果，或提交返回的失败</returns>
    public static ValueTask<Result<T>> RunAsync<T>(IServiceProvider root, UnitOfWorkOptions options, Func<IServiceProvider, CancellationToken, ValueTask<Result<T>>> body, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(logger);
        return RunCoreAsync(root, options, body, null, logger, cancellationToken);
    }

    /// <summary>
    /// 在新作用域的工作单元里执行没有返回值的动作，异常回滚后原样抛出
    /// </summary>
    /// <param name="root">服务提供者，每次调用在它上面开新作用域</param>
    /// <param name="options">开始工作单元的选项</param>
    /// <param name="body">在活动的工作单元里执行的动作，参数是新作用域的服务提供者</param>
    /// <param name="logger">记录回滚与释放作用域失败的日志</param>
    /// <param name="cancellationToken">取消令牌，传给开始、动作与提交</param>
    /// <returns>动作的结果，或提交返回的失败</returns>
    public static ValueTask<Result> RunAsync(IServiceProvider root, UnitOfWorkOptions options, Func<IServiceProvider, CancellationToken, ValueTask<Result>> body, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(logger);
        return RunCoreAsync(root, options, body, null, logger, cancellationToken);
    }

    /// <summary>
    /// 在新作用域的工作单元里执行并取回值，异常回滚后经映射成为失败结果
    /// </summary>
    /// <param name="root">服务提供者，每次调用在它上面开新作用域</param>
    /// <param name="options">开始工作单元的选项</param>
    /// <param name="body">在活动的工作单元里执行的动作，参数是新作用域的服务提供者</param>
    /// <param name="fault">把开始、动作或提交抛出的异常映射成调用方的错误；调用方取消不经过它</param>
    /// <param name="logger">记录回滚与释放作用域失败的日志</param>
    /// <param name="cancellationToken">取消令牌，传给开始、动作与提交</param>
    /// <returns>动作的结果、提交返回的失败，或 <paramref name="fault"/> 给出的错误</returns>
    public static ValueTask<Result<T>> RunAsync<T>(IServiceProvider root, UnitOfWorkOptions options, Func<IServiceProvider, CancellationToken, ValueTask<Result<T>>> body, Func<Exception, Error> fault, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(fault);
        ArgumentNullException.ThrowIfNull(logger);
        return RunCoreAsync(root, options, body, fault, logger, cancellationToken);
    }

    /// <summary>
    /// 在新作用域的工作单元里执行没有返回值的动作，异常回滚后经映射成为失败结果
    /// </summary>
    /// <param name="root">服务提供者，每次调用在它上面开新作用域</param>
    /// <param name="options">开始工作单元的选项</param>
    /// <param name="body">在活动的工作单元里执行的动作，参数是新作用域的服务提供者</param>
    /// <param name="fault">把开始、动作或提交抛出的异常映射成调用方的错误；调用方取消不经过它</param>
    /// <param name="logger">记录回滚与释放作用域失败的日志</param>
    /// <param name="cancellationToken">取消令牌，传给开始、动作与提交</param>
    /// <returns>动作的结果、提交返回的失败，或 <paramref name="fault"/> 给出的错误</returns>
    public static ValueTask<Result> RunAsync(IServiceProvider root, UnitOfWorkOptions options, Func<IServiceProvider, CancellationToken, ValueTask<Result>> body, Func<Exception, Error> fault, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(fault);
        ArgumentNullException.ThrowIfNull(logger);
        return RunCoreAsync(root, options, body, fault, logger, cancellationToken);
    }

    /// <summary>
    /// 没有返回值的动作借带值的那一条路径执行
    /// </summary>
    /// <remarks>
    /// 成功换成一个占位的真值，失败原样带出，收尾规则只写一份
    /// </remarks>
    private static async ValueTask<Result> RunCoreAsync(IServiceProvider root, UnitOfWorkOptions options, Func<IServiceProvider, CancellationToken, ValueTask<Result>> body, Func<Exception, Error>? fault, ILogger logger, CancellationToken cancellationToken)
    {
        var result = await RunCoreAsync(
            root,
            options,
            async (services, ct) =>
            {
                var outcome = await body(services, ct).ConfigureAwait(false);
                return outcome.IsSuccess ? Result.Success(true) : Result.Failure<bool>(outcome.Error!);
            },
            fault,
            logger,
            cancellationToken).ConfigureAwait(false);
        return result.ToResult();
    }

    /// <summary>
    /// 开作用域、执行、释放作用域
    /// </summary>
    /// <remarks>
    /// <paramref name="fault"/> 为 <see langword="null"/> 时异常原样抛出
    /// </remarks>
    private static async ValueTask<Result<T>> RunCoreAsync<T>(IServiceProvider root, UnitOfWorkOptions options, Func<IServiceProvider, CancellationToken, ValueTask<Result<T>>> body, Func<Exception, Error>? fault, ILogger logger, CancellationToken cancellationToken)
    {
        var scope = root.CreateAsyncScope();
        Result<T> result;
        try
        {
            // 解析放在守卫之外：容器里没有工作单元是装配错误，不归为存储故障，不交给 fault
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            result = await RunInScopeAsync(scope.ServiceProvider, unitOfWork, options, body, fault, logger, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 不用 await using：数据库断开时回滚失败，Provider 在释放作用域时还会再回滚一次、再抛一次，
            // 那个异常不能盖掉这里正要抛出的原异常
            await DisposeAsync(scope, logger).ConfigureAwait(false);
            throw;
        }

        // 结果已经确定（已提交、已回滚或已映射）：释放失败同样只记日志
        await DisposeAsync(scope, logger).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 开始、执行、按结果提交或回滚
    /// </summary>
    private static async ValueTask<Result<T>> RunInScopeAsync<T>(IServiceProvider services, IUnitOfWork unitOfWork, UnitOfWorkOptions options, Func<IServiceProvider, CancellationToken, ValueTask<Result<T>>> body, Func<Exception, Error>? fault, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            // 仓储只在活动的工作单元内工作，读也要先开：这里不在任何调用里，没有 Transaction 阶段替我们开
            await unitOfWork.BeginAsync(options, cancellationToken).ConfigureAwait(false);
            var result = await body(services, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                // 动作失败：本次写入一条也不落库，失败原样交还调用方
                await RollbackAsync(unitOfWork, logger).ConfigureAwait(false);
                return result;
            }

            var committed = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            if (committed.IsFailure)
            {
                // 提交返回失败时 Provider 已经自己回滚；工作单元仍活动才补一次
                await RollbackAsync(unitOfWork, logger).ConfigureAwait(false);
                return Result.Failure<T>(committed.Error!);
            }

            return result;
        }
        catch (Exception exception)
        {
            // 先回滚再决定去向：不论是取消、故障还是要原样抛出，已做的写入都不能留在事务里
            await RollbackAsync(unitOfWork, logger).ConfigureAwait(false);

            // 调用方取消不是故障：按异步约定抛出，不交给 fault。令牌没取消时的取消异常是驱动报告的超时，按故障走
            if (fault is null || (exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                throw;
            }

            return fault(exception);
        }
    }

    /// <summary>
    /// 工作单元仍活动时回滚
    /// </summary>
    /// <remarks>
    /// 用 <see cref="CancellationToken.None"/> 保证取消引发的异常也能完成回滚；回滚本身失败（数据库已断开时常见）只记日志，不覆盖已经确定的结果或异常
    /// </remarks>
    private static async ValueTask RollbackAsync(IUnitOfWork unitOfWork, ILogger logger)
    {
        // 没开起来、提交已经收尾的都不用回滚
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
            LogRollbackFailed(logger, exception);
        }
    }

    /// <summary>
    /// 释放作用域
    /// </summary>
    /// <remarks>
    /// 释放失败只记日志
    /// </remarks>
    private static async ValueTask DisposeAsync(AsyncServiceScope scope, ILogger logger)
    {
        try
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogDisposeFailed(logger, exception);
        }
    }

    /// <summary>
    /// 回滚失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Rolling back a unit of work outside the pipeline failed; the outcome that was already decided is kept.")]
    private static partial void LogRollbackFailed(ILogger logger, Exception exception);

    /// <summary>
    /// 释放作用域失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Disposing the service scope of a unit of work outside the pipeline failed; the outcome that was already decided is kept.")]
    private static partial void LogDisposeFailed(ILogger logger, Exception exception);
}
