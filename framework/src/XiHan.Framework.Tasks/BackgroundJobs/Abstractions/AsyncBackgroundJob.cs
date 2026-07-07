#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AsyncBackgroundJob
// Guid:fb61a546-8e9d-4ecf-b063-c9efe8e66c69
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

/// <summary>
/// 异步后台作业处理器基类
/// </summary>
/// <typeparam name="TArgs">作业参数类型</typeparam>
/// <remarks>
/// 派生本基类的处理器会被约定注册为瞬时服务并自动纳入后台作业注册表；
/// 若直接实现 <see cref="IAsyncBackgroundJob{TArgs}"/> 而不继承本基类，需自行带生命周期标记或手动注册。
/// </remarks>
public abstract class AsyncBackgroundJob<TArgs> : IAsyncBackgroundJob<TArgs>, ITransientDependency
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <returns>任务</returns>
    public abstract Task ExecuteAsync(TArgs args);
}
