// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
