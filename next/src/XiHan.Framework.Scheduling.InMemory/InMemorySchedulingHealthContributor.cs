// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.InMemory;

/// <summary>
/// 健康项 <see cref="InMemorySchedulingFeature.HealthName"/>。
/// </summary>
/// <remarks>
/// 报告领导权状态。进程内的租约没有别的竞争者，持有与未持有都是 Ready；引擎停止（或已停止）为 Stopped；本包的作业能力被别的 Provider 顶替、没有引擎时也是 Ready。同步计算，不起后台循环。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemorySchedulingHealthContributor : IHealthContributor
{
    /// <summary>
    /// 引擎。
    /// </summary>
    /// <remarks>
    /// 本包没被选中提供作业能力时为 <see langword="null"/>。
    /// </remarks>
    private readonly JobSchedulerEngine? _engine;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemorySchedulingHealthContributor(JobSchedulerEngine? engine)
    {
        _engine = engine;
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
    {
        // 没有引擎就没有领导权可报；引擎在跑时持有与否都不影响服务（单进程）；停了才是 Stopped
        var state = _engine is null || _engine.IsRunning ? LifecycleState.Ready : LifecycleState.Stopped;
        return [new ContributorStatus(InMemorySchedulingFeature.HealthName, state)];
    }
}
