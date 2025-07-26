#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TaskPriority
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5e0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 任务优先级
/// </summary>
public enum TaskPriority
{
    /// <summary>
    /// 低
    /// </summary>
    Low = 0,

    /// <summary>
    /// 普通
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 高
    /// </summary>
    High = 2,

    /// <summary>
    /// 紧急
    /// </summary>
    Urgent = 3,

    /// <summary>
    /// 关键
    /// </summary>
    Critical = 4
}
