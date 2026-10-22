// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// 套件用的宿主句柄：被测调度器与释放宿主的方法。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobSchedulerHost : IAsyncDisposable
{
    /// <summary>
    /// 释放宿主。
    /// </summary>
    private readonly IAsyncDisposable _host;

    /// <summary>
    /// 创建。
    /// </summary>
    public JobSchedulerHost(IJobScheduler scheduler, IAsyncDisposable host)
    {
        Scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// 被测调度器。
    /// </summary>
    public IJobScheduler Scheduler { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
