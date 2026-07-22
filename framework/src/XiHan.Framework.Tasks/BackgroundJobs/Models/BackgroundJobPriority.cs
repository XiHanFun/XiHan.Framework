// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
