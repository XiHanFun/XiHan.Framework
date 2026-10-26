// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Schedules;

/// <summary>
/// 触发器的到期时刻落后当前时刻超过 misfire 阈值（停机、暂停、节点失联造成）时的处理策略。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum MisfirePolicy
{
    /// <summary>立即运行一次，再从现在推算下一次（默认）。</summary>
    RunOnce = 0,

    /// <summary>不补运行，从现在推算下一次。</summary>
    Skip = 1,

    /// <summary>按原计划逐次补齐：每个轮询周期补一次，直至追平。</summary>
    RunAll = 2,
}
