#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ActivityOutgoingBehavior
// Guid:e60b3c17-52d8-4a94-bf06-19c7e2d85a43
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
