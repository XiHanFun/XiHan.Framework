// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动出边流转行为
/// </summary>
public enum ActivityOutgoingBehavior
{
    /// <summary>
    /// 沿所有条件满足（或无条件）的出边流转（默认）
    /// </summary>
    AllMatched = 0,

    /// <summary>
    /// 独占：按优先级取第一条条件满足的出边，均不满足时走默认边，仍无则故障
    /// </summary>
    Exclusive = 1,

    /// <summary>
    /// 全部：忽略条件沿所有出边流转（并行扇出）
    /// </summary>
    All = 2,

    /// <summary>
    /// 无：不再流转（结束类活动）
    /// </summary>
    None = 3
}
