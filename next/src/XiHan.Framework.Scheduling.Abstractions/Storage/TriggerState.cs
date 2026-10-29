// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Storage;

/// <summary>
/// 触发器的状态。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum TriggerState
{
    /// <summary>
    /// 等待到期。
    /// </summary>
    Waiting = 0,

    /// <summary>
    /// 已暂停。
    /// </summary>
    Paused = 1,

    /// <summary>
    /// 已被某个节点抢占（间隔计划在运行结束前保持此状态）。
    /// </summary>
    Acquired = 2,

    /// <summary>
    /// 到期但作业不允许并发且上一次未结束。
    /// </summary>
    /// <remarks>
    /// 上一次结束时回到 <see cref="Waiting"/>。
    /// </remarks>
    Blocked = 3,

    /// <summary>
    /// 没有下一次（一次性已运行、次数用尽、超过结束时刻）。
    /// </summary>
    Complete = 4,

    /// <summary>
    /// 无法运行（作业类型未登记等），见触发器记录的 <c>Error</c>。
    /// </summary>
    Error = 5,
}
