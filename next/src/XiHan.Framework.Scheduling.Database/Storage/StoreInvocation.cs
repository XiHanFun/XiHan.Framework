// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;

namespace XiHan.Framework.Scheduling.Database.Storage;

/// <summary>
/// 存储与租约共用的执行方式：每个操作在根提供者的新作用域里经 <see cref="App.InvokeAsync"/> 走完整管道，仓储只能在 Transaction 阶段内使用，动作返回时工作单元提交。
/// 动作返回失败时把失败写成调用结果让工作单元回滚；管道自身的失败原样返回；应用不在服务状态时返回 <see cref="SchedulingErrors.StoreUnavailable"/>。
/// </summary>
internal sealed class StoreInvocation
{
    /// <summary>
    /// 调度子系统错误码的前缀；带此前缀的失败是业务语义（作业不存在等），不归为存储不可达。
    /// </summary>
    private const string SchedulingPrefix = "SCHEDULING_";

    /// <summary>
    /// 根服务提供者；每次操作开新作用域。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 应用；操作经它的管道执行。
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 创建。
    /// </summary>
    public StoreInvocation(IServiceProvider services, App app)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _app = app ?? throw new ArgumentNullException(nameof(app));
    }

    /// <summary>
    /// 在新作用域的管道里执行并取回动作的值。
    /// </summary>
    public async ValueTask<Result<T>> RunAsync<T>(Func<InvocationContext, ValueTask<Result<T>>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        Result<T> inner = default;
        Result outcome;
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var context = App.CreateContext(scope.ServiceProvider, cancellationToken);
            outcome = await _app.InvokeAsync(context, async ctx =>
            {
                inner = await action(ctx).ConfigureAwait(false);
                // 动作失败时把失败写成调用结果：Transaction 阶段据此回滚，本次写入一条也不落库
                if (inner.IsFailure)
                {
                    ctx.SetOutcome(Result.Failure(inner.Error!));
                }
            }).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 应用不在服务状态时 InvokeAsync 直接抛出；其余异常在管道内已被转成 Unexpected 结果，这里只剩调用管道前后的意外
            return Unavailable(exception.Message);
        }

        // 取消不以失败结果掩盖：调用方按异步约定收到 OperationCanceledException
        cancellationToken.ThrowIfCancellationRequested();
        return outcome.IsFailure ? Result.Failure<T>(outcome.Error!) : inner;
    }

    /// <summary>
    /// 在新作用域的管道里执行没有返回值的动作。
    /// </summary>
    public async ValueTask<Result> RunAsync(Func<InvocationContext, ValueTask<Result>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var result = await RunAsync(async ctx =>
        {
            var outcome = await action(ctx).ConfigureAwait(false);
            return outcome.IsSuccess ? Result.Success(true) : Result.Failure<bool>(outcome.Error!);
        }, cancellationToken).ConfigureAwait(false);
        return result.ToResult();
    }

    /// <summary>
    /// 取当前调用作用域里的仓储。
    /// </summary>
    public static IRepository<TEntity, string> Repository<TEntity>(InvocationContext context)
        where TEntity : class, IEntity<string>
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Services.GetRequiredService<IRepository<TEntity, string>>();
    }

    /// <summary>
    /// 把失败归为存储不可达：调度子系统自己的错误（作业不存在、运行不存在、已经是存储不可达）原样返回，其余以原错误码为原因、原错误为内部错误包一层。
    /// </summary>
    public static Error Unavailable(Error cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        return cause.Code.StartsWith(SchedulingPrefix, StringComparison.Ordinal)
            ? cause
            : new Error(SchedulingErrors.StoreUnavailable, SchedulingMessages.StoreUnavailable, MessageArguments.Of(("reason", cause.Code)), cause);
    }

    /// <summary>
    /// 以文本原因构造存储不可达。
    /// </summary>
    public static Error Unavailable(string reason)
        => new(SchedulingErrors.StoreUnavailable, SchedulingMessages.StoreUnavailable, MessageArguments.Of(("reason", reason)));

    /// <summary>
    /// 失败归为存储不可达，成功原样返回。
    /// </summary>
    public static Result<T> OrUnavailable<T>(Result<T> result) => result.IsSuccess ? result : Unavailable(result.Error!);

    /// <summary>
    /// 失败归为存储不可达，成功原样返回。
    /// </summary>
    public static Result OrUnavailable(Result result) => result.IsSuccess ? result : Unavailable(result.Error!);
}
