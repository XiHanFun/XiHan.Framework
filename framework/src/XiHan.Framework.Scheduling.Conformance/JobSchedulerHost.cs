// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// 套件用的宿主句柄：被测调度器、轮询一次的入口与释放宿主的方法
/// </summary>
/// <remarks>
/// 套件不靠后台轮询循环推进：用例推进注入的时钟，再经 <see cref="PollAsync"/> 主动轮询一次
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobSchedulerHost : IAsyncDisposable
{
    /// <summary>
    /// 释放宿主
    /// </summary>
    private readonly IAsyncDisposable _host;

    /// <summary>
    /// 轮询一次
    /// </summary>
    private readonly Func<CancellationToken, ValueTask> _poll;

    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// <paramref name="poll"/> 执行一个轮询周期，并在本周期起的运行连同收尾结束后才完成（<see cref="JobSchedulerEngine.PollOnceAsync"/> 即是）
    /// </remarks>
    public JobSchedulerHost(IJobScheduler scheduler, Func<CancellationToken, ValueTask> poll, IAsyncDisposable host)
    {
        Scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _poll = poll ?? throw new ArgumentNullException(nameof(poll));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// 被测调度器
    /// </summary>
    public IJobScheduler Scheduler { get; }

    /// <summary>
    /// 轮询一次，等本周期起的运行连同收尾结束
    /// </summary>
    public ValueTask PollAsync(CancellationToken cancellationToken = default) => _poll(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
