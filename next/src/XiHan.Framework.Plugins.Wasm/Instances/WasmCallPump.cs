// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace XiHan.Framework.Plugins.Wasm.Instances;

/// <summary>
/// 一个插件的调用泵：外层调用在执行线程上登记所有者；插件回调宿主期间，管道再次进入同一插件（它自己贡献的中间件、操作或订阅）时，
/// 调用锁由外层调用持有，再入必须回到执行线程上、在导入函数返回前同步完成（wasmtime 允许导入函数内再调导出），
/// 所以宿主导入函数等待管道时在这里泵出再入工作。
/// </summary>
internal sealed class WasmCallPump : IDisposable
{
    /// <summary>
    /// 当前异步流所属的泵：宿主导入函数启动管道前设置，随 ExecutionContext 流到管道里的每一步。
    /// </summary>
    private static readonly AsyncLocal<WasmCallPump?> _flow = new();

    /// <summary>
    /// 等待在执行线程上运行的再入工作。
    /// </summary>
    private readonly ConcurrentQueue<Action> _work = new();

    /// <summary>
    /// 有工作入队或等待的任务完成时释放一次。
    /// </summary>
    private readonly SemaphoreSlim _signal = new(0);

    /// <summary>
    /// 保护所有者注销与入队的原子性：注销后入队的工作没人泵。
    /// </summary>
    private readonly Lock _sync = new();

    /// <summary>
    /// 执行线程的托管线程 ID；0 表示没有外层调用。
    /// </summary>
    private int _owner;

    /// <summary>
    /// 当前异步流是否源自本泵所属插件的回调，且外层调用仍在进行。
    /// </summary>
    public bool IsReentrant => ReferenceEquals(_flow.Value, this) && Volatile.Read(ref _owner) != 0;

    /// <summary>
    /// 外层调用：在当前线程上登记所有者，结束时泵完剩余的再入工作再注销。
    /// </summary>
    public T Run<T>(Func<T> call)
    {
        Volatile.Write(ref _owner, Environment.CurrentManagedThreadId);
        try
        {
            return call();
        }
        finally
        {
            // 注销前泵到队列为空：注销后到达的再入会走普通路径；泵工作不能在锁内做，它可能再次回调宿主
            while (true)
            {
                Drain();
                lock (_sync)
                {
                    if (_work.IsEmpty)
                    {
                        _owner = 0;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 宿主导入函数等待管道：在执行线程上边等边泵再入工作；不在执行线程上时退化为普通等待。
    /// </summary>
    public T Wait<T>(Task<T> task)
    {
        if (Environment.CurrentManagedThreadId != Volatile.Read(ref _owner))
        {
            return task.GetAwaiter().GetResult();
        }

        task.ContinueWith(static (_, state) => ((SemaphoreSlim)state!).Release(), _signal, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        while (!task.IsCompleted)
        {
            _signal.Wait();
            Drain();
        }

        return task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// 再入：已在执行线程上就直接运行；否则交给执行线程并等待结果。外层调用已结束时返回假，调用方走普通路径。
    /// </summary>
    public bool TryRunNested<T>(Func<T> call, [MaybeNullWhen(false)] out T result)
    {
        if (Environment.CurrentManagedThreadId == Volatile.Read(ref _owner))
        {
            result = call();
            return true;
        }

        using var pending = new Pending<T>(call);
        lock (_sync)
        {
            if (_owner == 0)
            {
                result = default;
                return false;
            }

            _work.Enqueue(pending.Run);
        }

        _signal.Release();
        result = pending.Wait();
        return true;
    }

    /// <summary>
    /// 把当前异步流标记为本泵的回调流，释放时恢复原值。
    /// </summary>
    public IDisposable EnterCallback()
    {
        var scope = new FlowScope(_flow.Value);
        _flow.Value = this;
        return scope;
    }

    /// <inheritdoc />
    public void Dispose() => _signal.Dispose();

    /// <summary>
    /// 在执行线程上运行队列里的全部工作。
    /// </summary>
    private void Drain()
    {
        while (_work.TryDequeue(out var work))
        {
            work();
        }
    }

    /// <summary>
    /// 恢复回调流标记。
    /// </summary>
    private sealed class FlowScope(WasmCallPump? previous) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => _flow.Value = previous;
    }

    /// <summary>
    /// 一项等待执行线程运行的再入工作及其结果。
    /// </summary>
    private sealed class Pending<T>(Func<T> call) : IDisposable
    {
        /// <summary>
        /// 运行完成的信号。
        /// </summary>
        private readonly ManualResetEventSlim _done = new();

        /// <summary>
        /// 结果。
        /// </summary>
        private T? _result;

        /// <summary>
        /// 运行时抛出的异常。
        /// </summary>
        private ExceptionDispatchInfo? _error;

        /// <summary>
        /// 在执行线程上运行。
        /// </summary>
        public void Run()
        {
            try
            {
                _result = call();
            }
            catch (Exception exception)
            {
                _error = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                _done.Set();
            }
        }

        /// <summary>
        /// 等待运行结束并取结果；运行抛出的异常原样重抛。
        /// </summary>
        public T Wait()
        {
            _done.Wait();
            _error?.Throw();
            return _result!;
        }

        /// <inheritdoc />
        public void Dispose() => _done.Dispose();
    }
}
