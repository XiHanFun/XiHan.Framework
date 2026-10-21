// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 生命周期状态的合法迁移表。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class LifecycleStateExtensions
{
    /// <summary>
    /// 判断从 <paramref name="from"/> 迁移到 <paramref name="to"/> 是否合法。
    /// 任何非终态都可以迁移到 <see cref="LifecycleState.Failed"/>；<see cref="LifecycleState.Failed"/> 只能回到
    /// <see cref="LifecycleState.Resolved"/>（重启）；<see cref="LifecycleState.Stopped"/> 是终态。
    /// </summary>
    public static bool CanTransitionTo(this LifecycleState from, LifecycleState to)
    {
        if (from == to)
        {
            return false;
        }

        if (to == LifecycleState.Failed)
        {
            return from is not LifecycleState.Stopped and not LifecycleState.Failed;
        }

        return (from, to) switch
        {
            (LifecycleState.Declared, LifecycleState.Resolved) => true,
            (LifecycleState.Resolved, LifecycleState.Configured) => true,
            (LifecycleState.Configured, LifecycleState.Started) => true,
            (LifecycleState.Started, LifecycleState.Ready) => true,
            (LifecycleState.Started, LifecycleState.Degraded) => true,
            (LifecycleState.Started, LifecycleState.Draining) => true,
            (LifecycleState.Ready, LifecycleState.Degraded) => true,
            (LifecycleState.Ready, LifecycleState.Draining) => true,
            (LifecycleState.Degraded, LifecycleState.Ready) => true,
            (LifecycleState.Degraded, LifecycleState.Draining) => true,
            (LifecycleState.Draining, LifecycleState.Stopped) => true,
            (LifecycleState.Failed, LifecycleState.Resolved) => true,
            _ => false,
        };
    }

    /// <summary>
    /// 状态是否表示可以接收调用（<see cref="LifecycleState.Ready"/> 或 <see cref="LifecycleState.Degraded"/>）。
    /// </summary>
    public static bool IsServing(this LifecycleState state) => state is LifecycleState.Ready or LifecycleState.Degraded;

    /// <summary>
    /// 状态是否为终态（<see cref="LifecycleState.Stopped"/>）。
    /// </summary>
    public static bool IsTerminal(this LifecycleState state) => state == LifecycleState.Stopped;
}
