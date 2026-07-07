#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBackgroundJobExecuter
// Guid:2badb381-e7da-45e4-b772-d99ab3ea7aac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

/// <summary>
/// 后台作业执行器（解析处理器并反射调用 ExecuteAsync）
/// </summary>
public interface IBackgroundJobExecuter
{
    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>任务</returns>
    Task ExecuteAsync(BackgroundJobExecutionContext context);
}
