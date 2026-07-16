#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UserTaskCompletionPolicy
// Guid:8a17d0e4-6b92-4c35-af81-d5240c96e7b3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:36:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.UserTasks;

/// <summary>
/// 人工任务完成策略
/// </summary>
public enum UserTaskCompletionPolicy
{
    /// <summary>
    /// 或签（任一受理人同意即通过，任一拒绝即拒绝）
    /// </summary>
    Any = 1,

    /// <summary>
    /// 会签（全部受理人同意才通过，任一拒绝即一票否决）
    /// </summary>
    All = 2,

    /// <summary>
    /// 依次审批（按受理人顺序逐一审批，全部同意才通过，任一拒绝即拒绝）
    /// </summary>
    Sequential = 3
}
