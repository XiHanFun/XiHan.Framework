// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 一个服务作用域里的工作流执行范围（作用域服务）
/// </summary>
/// <remarks>
/// 引擎、定义目录、任务服务与外部任务服务共用：决定每个操作在调用方的事务里执行还是自己开一次调用（<c>RunAsync</c>），给出调用方的租户（即操作的分区），并持有本作用域取得的实例锁——锁在作用域结束时才释放，也就是调用方的事务提交或回滚之后，别的执行者不会在提交之前读到旧状态再改写它
/// </remarks>
internal sealed class WorkflowScope : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 与调用方事务的关系
    /// </summary>
    private readonly IWorkflowTransaction _transaction;

    /// <summary>
    /// 实例锁（Provider）
    /// </summary>
    private readonly IWorkflowInstanceLock _lock;

    /// <summary>
    /// 本作用域的调用上下文访问器
    /// </summary>
    private readonly IInvocationAccessor? _invocation;

    /// <summary>
    /// 根作用域工厂：自己开调用时建新作用域
    /// </summary>
    private readonly IServiceScopeFactory _scopes;

    /// <summary>
    /// 应用
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 本作用域持有的锁：键 → 句柄与是否正在使用
    /// </summary>
    private readonly Dictionary<string, HeldLock> _held = new(StringComparer.Ordinal);

    /// <summary>
    /// 保护 <see cref="_held"/>
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 是否已释放
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 创建
    /// </summary>
    public WorkflowScope(IWorkflowTransaction transaction, IWorkflowInstanceLock instanceLock, IServiceScopeFactory scopes, App app, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _lock = instanceLock ?? throw new ArgumentNullException(nameof(instanceLock));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _invocation = services.GetService<IInvocationAccessor>();
    }

    /// <summary>
    /// 当前调用的上下文
    /// </summary>
    /// <remarks>
    /// 加入调用方事务时一定有值：活动在它里面执行
    /// </remarks>
    public Maybe<InvocationContext> Invocation => _invocation?.Current ?? default;

    /// <summary>
    /// 调用方的租户，即这次操作的分区，空为平台
    /// </summary>
    public string? TenantId => Invocation.TryGetValue(out var context) ? context.GetSecurity().Tenant?.Id : null;

    /// <summary>
    /// 在调用方的事务里执行 <paramref name="body"/>，或自己开一次调用在里面执行 <paramref name="nested"/>
    /// </summary>
    /// <typeparam name="TService">操作所在的服务契约：自己开调用时从新作用域解析它，调用同一个操作</typeparam>
    /// <typeparam name="TResult">操作的返回值</typeparam>
    /// <param name="write">是否写入：只读事务里写入在取锁、执行活动之前即以拒绝返回</param>
    /// <param name="target">记进调用来源的操作名</param>
    /// <param name="body">在调用方事务里执行的操作</param>
    /// <param name="nested">在自己开的调用里对新作用域的服务执行同一个操作</param>
    /// <param name="outcome">操作结果对事务的意义：失败即回滚</param>
    /// <param name="fail">把范围级的失败（拒绝写入、不可达、提交失败）表示成操作的返回值</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async ValueTask<TResult> RunAsync<TService, TResult>(bool write, string target, Func<ValueTask<TResult>> body, Func<TService, ValueTask<TResult>> nested, Func<TResult, Result> outcome, Func<Error, TResult> fail, CancellationToken cancellationToken)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(nested);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(fail);
        if (_transaction.IsActive)
        {
            // 加入调用方的事务：写入先确认事务可写，只读的调用不会先执行活动的副作用再在写回时失败
            if (write && _transaction.EnsureWritable() is { IsFailure: true } denied)
            {
                return fail(denied.Error!);
            }

            return await body().ConfigureAwait(false);
        }

        // 本作用域就是工作流自己开的调用：事务必须可以加入，仍不能加入是装配错误，返回不可达而不是再开一层调用
        if (Invocation.TryGetValue(out var current) && current.Get(WorkflowInvocations.OwnedKey).HasValue)
        {
            // 自己开的调用里事务仍不能加入：持久化没有在 Transaction 阶段开启工作单元，是装配错误，不再开一层调用（否则无限递归）
            return fail(Error.Unavailable(WorkflowErrors.StoreUnavailable, WorkflowMessages.StoreUnitOfWorkMissing));
        }

        TResult result = default!;
        var ran = false;
        var invoked = await WorkflowInvocations.InvokeAsync(_scopes, _app, TenantId, target, async services =>
        {
            result = await nested(services.GetRequiredService<TService>()).ConfigureAwait(false);
            ran = true;
            return outcome(result);
        }, cancellationToken).ConfigureAwait(false);
        if (invoked.IsSuccess)
        {
            return result;
        }

        // 操作自己的失败（已回滚）原样返回；操作成功而提交失败、或管道在操作之前失败，以那个错误表示整个操作的结果
        return ran && outcome(result).IsFailure ? result : fail(invoked.Error!);
    }

    /// <summary>
    /// 单个结果的操作：失败即回滚
    /// </summary>
    public ValueTask<Result<T>> RunAsync<TService, T>(bool write, string target, Func<ValueTask<Result<T>>> body, Func<TService, ValueTask<Result<T>>> nested, CancellationToken cancellationToken)
        where TService : notnull
        => RunAsync(write, target, body, nested, static result => result.IsSuccess ? Result.Success() : Result.Failure(result.Error!), static error => Result.Failure<T>(error), cancellationToken);

    /// <summary>
    /// 没有返回值的操作：失败即回滚
    /// </summary>
    public ValueTask<Result> RunAsync<TService>(bool write, string target, Func<ValueTask<Result>> body, Func<TService, ValueTask<Result>> nested, CancellationToken cancellationToken)
        where TService : notnull
        => RunAsync(write, target, body, nested, static result => result, static error => Result.Failure(error), cancellationToken);

    /// <summary>
    /// 逐项结果的批量操作
    /// </summary>
    /// <remarks>
    /// 自己开调用时整批一个事务：逐项失败不回滚其余项（失败的项没有写入），提交失败时每一项都报告提交的错误
    /// </remarks>
    public ValueTask<IReadOnlyList<Result<T>>> RunBatchAsync<TService, T>(int count, string target, Func<ValueTask<IReadOnlyList<Result<T>>>> body, Func<TService, ValueTask<IReadOnlyList<Result<T>>>> nested, CancellationToken cancellationToken)
        where TService : notnull
        => RunAsync(true, target, body, nested, static _ => Result.Success(), error => (IReadOnlyList<Result<T>>)Enumerable.Repeat(Result.Failure<T>(error), count).ToList(), cancellationToken);

    /// <summary>
    /// 逐项结果、没有返回值的批量操作
    /// </summary>
    /// <remarks>
    /// 语义同 <see cref="RunBatchAsync{TService, T}"/>
    /// </remarks>
    public ValueTask<IReadOnlyList<Result>> RunBatchAsync<TService>(int count, string target, Func<ValueTask<IReadOnlyList<Result>>> body, Func<TService, ValueTask<IReadOnlyList<Result>>> nested, CancellationToken cancellationToken)
        where TService : notnull
        => RunAsync(true, target, body, nested, static _ => Result.Success(), error => (IReadOnlyList<Result>)Enumerable.Repeat(Result.Failure(error), count).ToList(), cancellationToken);

    /// <summary>
    /// 取实例锁
    /// </summary>
    /// <remarks>
    /// 本作用域已持有且空闲的锁直接复用（同一事务里先后几次操作同一实例：子流程回调父实例、同一请求里办理两个任务）；正在使用中再取是同一执行流里的重入，等也等不到，直接按等锁超时返回。新取得的锁持有到作用域结束：释放返回的句柄只把锁标为空闲
    /// </remarks>
    public async ValueTask<Result<IAsyncDisposable>> LockAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_held.TryGetValue(key, out var held))
            {
                if (held.InUse)
                {
                    return new Error(WorkflowErrors.LockTimeout, WorkflowMessages.LockTimeout, MessageArguments.Of(("id", key)));
                }

                held.InUse = true;
                return new Lease(this, held);
            }
        }

        var acquired = await _lock.AcquireAsync(key, cancellationToken).ConfigureAwait(false);
        if (!acquired.TryGetValue(out var handle))
        {
            return acquired.Error!;
        }

        var entry = new HeldLock(handle) { InUse = true };
        lock (_gate)
        {
            if (!_disposed)
            {
                _held[key] = entry;
                return new Lease(this, entry);
            }
        }

        // 作用域在等锁期间已结束：刚拿到的锁立即释放，不让它悬挂
        await handle.DisposeAsync().ConfigureAwait(false);
        throw new ObjectDisposedException(nameof(WorkflowScope));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var handle in TakeAll())
        {
            await handle.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // 同步释放只在容器以同步方式结束作用域时发生：租约句柄只有异步释放，在这里等它完成
        foreach (var handle in TakeAll())
        {
            handle.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// 标记已释放并取走全部持有的锁
    /// </summary>
    private List<IAsyncDisposable> TakeAll()
    {
        lock (_gate)
        {
            _disposed = true;
            var handles = _held.Values.Select(static held => held.Handle).ToList();
            _held.Clear();
            return handles;
        }
    }

    /// <summary>
    /// 一把持有中的锁
    /// </summary>
    private sealed class HeldLock(IAsyncDisposable handle)
    {
        /// <summary>
        /// Provider 的锁句柄：作用域结束时释放
        /// </summary>
        public IAsyncDisposable Handle { get; } = handle;

        /// <summary>
        /// 是否正在被某个操作使用
        /// </summary>
        public bool InUse { get; set; }
    }

    /// <summary>
    /// 交给操作的锁句柄：释放只把锁标为空闲
    /// </summary>
    private sealed class Lease(WorkflowScope owner, HeldLock held) : IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            lock (owner._gate)
            {
                held.InUse = false;
            }

            return ValueTask.CompletedTask;
        }
    }
}
