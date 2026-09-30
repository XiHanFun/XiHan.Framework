// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundJobs.Models;

/// <summary>
/// 后台作业管理操作
/// </summary>
public enum BackgroundJobManagementOperation
{
    /// <summary>
    /// 重试已放弃的作业
    /// </summary>
    Retry = 0,

    /// <summary>
    /// 取消作业
    /// </summary>
    Cancel = 1
}
