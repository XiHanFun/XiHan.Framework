#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobPriority.cs
// Guid:f26e9fbf-10e3-405c-9649-2219de460274
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 14:24:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Models;

/// <summary>
/// 任务优先级
/// </summary>
public enum JobPriority
{
    /// <summary>
    /// 低优先级
    /// </summary>
    Low = 0,

    /// <summary>
    /// 普通优先级
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 高优先级
    /// </summary>
    High = 2,

    /// <summary>
    /// 紧急优先级
    /// </summary>
    Critical = 3
}
