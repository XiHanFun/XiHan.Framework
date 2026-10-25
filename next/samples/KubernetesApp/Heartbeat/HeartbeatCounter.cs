// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace KubernetesApp.Heartbeat;

/// <summary>
/// 心跳作业的运行计数。
/// </summary>
public sealed class HeartbeatCounter
{
    /// <summary>
    /// 计数。
    /// </summary>
    private long _ticks;

    /// <summary>
    /// 已运行次数。
    /// </summary>
    public long Ticks => Interlocked.Read(ref _ticks);

    /// <summary>
    /// 加一。
    /// </summary>
    public long Increment() => Interlocked.Increment(ref _ticks);
}
