// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 把两个并发启动卡在"查重之后、写入之前"的探针。
/// </summary>
/// <remarks>
/// 装上后，用 <see cref="Run"/> 标记的启动在查未结束实例时停在闸门前，直到 <see cref="Release"/>。第二个启动走到查重（查重与写入不原子）或等实例锁（查重与写入在同一把锁里）都算到达，两种实现都不用睡眠或计时赛跑就能把交错固定下来。被测 Provider 的存储与锁经装饰器接入，装饰器取 Provider 自己登记的具体类型。
/// </remarks>
internal sealed class StartRaceProbe
{
    /// <summary>
    /// 当前异步流属于哪个被标记的启动。
    /// </summary>
    private static readonly AsyncLocal<StartRaceProbe.Racer?> _current = new();

    /// <summary>
    /// 闸门。
    /// </summary>
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 第一个启动停在查重处。
    /// </summary>
    private readonly TaskCompletionSource _firstQueried = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 第二个启动到达查重处或实例锁处。
    /// </summary>
    private readonly TaskCompletionSource _secondArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 标记两个启动。
    /// </summary>
    public enum Racer
    {
        /// <summary>
        /// 先发起的启动。
        /// </summary>
        First,

        /// <summary>
        /// 后发起的启动。
        /// </summary>
        Second,
    }

    /// <summary>
    /// 第一个启动已停在查重处。
    /// </summary>
    public Task FirstQueried => _firstQueried.Task;

    /// <summary>
    /// 第二个启动已到达查重处或实例锁处。
    /// </summary>
    public Task SecondArrived => _secondArrived.Task;

    /// <summary>
    /// 以给定标记执行一次启动。
    /// </summary>
    public static async Task<T> Run<T>(Racer racer, Func<Task<T>> start)
    {
        ArgumentNullException.ThrowIfNull(start);
        _current.Value = racer;
        return await start().ConfigureAwait(false);
    }

    /// <summary>
    /// 把实例存储与实例锁换成探针的装饰器。
    /// </summary>
    public void Install(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 工厂在解析时才执行，那时 Provider 已登记完具体类型；先登记的接口让 Provider 的 TryAdd 让位
        services.AddSingleton<IWorkflowInstanceStore>(sp => new GatedInstanceStore((IWorkflowInstanceStore)sp.GetRequiredService(ProviderType<IWorkflowInstanceStore>(services)), this));
        services.AddSingleton<IWorkflowInstanceLock>(sp => new ObservedInstanceLock((IWorkflowInstanceLock)sp.GetRequiredService(ProviderType<IWorkflowInstanceLock>(services)), this));
    }

    /// <summary>
    /// 打开闸门。
    /// </summary>
    public void Release() => _gate.TrySetResult();

    /// <summary>
    /// 被标记的启动查未结束实例：记下到达并停在闸门前。
    /// </summary>
    public async Task OnActiveQueryAsync(WorkflowInstanceQuery query)
    {
        if (_current.Value is not { } racer || query.CorrelationId is null)
        {
            return;
        }

        (racer == Racer.First ? _firstQueried : _secondArrived).TrySetResult();
        await _gate.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// 第二个启动请求实例锁：记下到达。
    /// </summary>
    public void OnLockRequested()
    {
        if (_current.Value == Racer.Second)
        {
            _secondArrived.TrySetResult();
        }
    }

    /// <summary>
    /// Provider 登记的具体类型（实现了该接口、但不是接口本身的服务）。
    /// </summary>
    /// <remarks>
    /// Provider 要以具体类型登记存储与锁、再以 <c>TryAdd</c> 把接口转给它，套件才装得上装饰器。
    /// </remarks>
    private static Type ProviderType<TService>(IServiceCollection services)
        => services.LastOrDefault(descriptor => descriptor.ServiceType != typeof(TService) && typeof(TService).IsAssignableFrom(descriptor.ServiceType))?.ServiceType
           ?? throw new InvalidOperationException($"The provider must register its {typeof(TService).Name} as a concrete service and forward the interface with TryAdd, so the conformance suite can decorate it.");
}
