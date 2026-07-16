#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowJoinState
// Guid:2a6b91d5-c7f0-4e38-a2b4-58d1f9e60c37
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:13:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 汇聚网关波次状态（按汇聚节点记录当前波次的分支到达情况）
/// </summary>
public class WorkflowJoinState
{
    /// <summary>
    /// 当前波次已到达的入边连线标识
    /// </summary>
    public HashSet<string> ArrivedTransitionIds { get; set; } = [];

    /// <summary>
    /// 当前波次是否已触发（WaitAny 模式触发后吞掉同波次后续到达）
    /// </summary>
    public bool Fired { get; set; }
}
