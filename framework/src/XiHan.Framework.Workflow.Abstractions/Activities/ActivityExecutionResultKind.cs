#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ActivityExecutionResultKind
// Guid:d1a83f60-9c25-4b7e-80f4-52e6b09d3c18
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:24:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动执行结果种类
/// </summary>
public enum ActivityExecutionResultKind
{
    /// <summary>
    /// 已完成（继续流转）
    /// </summary>
    Completed = 1,

    /// <summary>
    /// 已挂起（等待书签恢复）
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// 已故障（按重试策略处理）
    /// </summary>
    Faulted = 3
}
