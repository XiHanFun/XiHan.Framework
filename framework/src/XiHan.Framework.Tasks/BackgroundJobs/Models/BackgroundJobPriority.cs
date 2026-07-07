#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BackgroundJobPriority
// Guid:60de3c3b-0ddc-4857-b88b-adf2b78a4e54
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.BackgroundJobs.Models;

/// <summary>
/// 后台作业优先级（取待执行作业时按此降序，值越大越优先）
/// </summary>
public enum BackgroundJobPriority : byte
{
    /// <summary>
    /// 低
    /// </summary>
    Low = 5,

    /// <summary>
    /// 偏低
    /// </summary>
    BelowNormal = 10,

    /// <summary>
    /// 普通（默认）
    /// </summary>
    Normal = 15,

    /// <summary>
    /// 偏高
    /// </summary>
    AboveNormal = 20,

    /// <summary>
    /// 高
    /// </summary>
    High = 25
}
