// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 结果类型为 <typeparamref name="T"/> 的在途回源：领头的调用写下结局，等待的调用读它
/// </summary>
/// <remarks>
/// 结局有三种：成功（带值）、失败（带异常）、交接（领头的调用取消了，等待的调用回去重新排）。结局只写一次，写完才发出完成信号，等待的调用在信号之后读，不需要加锁
/// </remarks>
internal sealed class InFlightLoad<T> : InFlightLoad
{
    /// <summary>
    /// 完成信号
    /// </summary>
    /// <remarks>
    /// 等待的调用的后续不在领头调用的栈上内联执行：领头的调用发出信号后立即返回自己的结果，不被别人的后续代码拖住
    /// </remarks>
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 成功时的值
    /// </summary>
    private T? _value;

    /// <summary>
    /// 失败时回源抛出的异常
    /// </summary>
    private ExceptionDispatchInfo? _failure;

    /// <summary>
    /// 是否以成功结束
    /// </summary>
    private bool _succeeded;

    /// <summary>
    /// 结局写下之后完成
    /// </summary>
    /// <remarks>
    /// 从不以失败或取消结束，结局经 <see cref="TryGetValue"/> 取
    /// </remarks>
    public Task Done => _done.Task;

    /// <summary>
    /// 以成功结束
    /// </summary>
    public void Succeed(T value)
    {
        _value = value;
        _succeeded = true;
        _done.TrySetResult();
    }

    /// <summary>
    /// 以失败结束：等待的调用都收到这个异常
    /// </summary>
    public void Fail(Exception exception)
    {
        _failure = ExceptionDispatchInfo.Capture(exception);
        _done.TrySetResult();
    }

    /// <summary>
    /// 领头的调用取消了：等待的调用回去重新排，先到的接手
    /// </summary>
    public void HandOver() => _done.TrySetResult();

    /// <summary>
    /// 读结局，<see cref="Done"/> 完成之后调用
    /// </summary>
    /// <remarks>
    /// 失败时抛出回源抛出的那个异常（同一个实例）
    /// </remarks>
    /// <returns>成功为 <see langword="true"/>，交接为 <see langword="false"/></returns>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        _failure?.Throw();
        value = _value;
        return _succeeded;
    }
}
