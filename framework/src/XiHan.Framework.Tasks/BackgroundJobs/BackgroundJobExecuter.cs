// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.BackgroundJobs;

/// <summary>
/// 默认后台作业执行器：解析处理器并经 IAsyncBackgroundJob&lt;TArgs&gt; 接口调用
/// </summary>
public class BackgroundJobExecuter : IBackgroundJobExecuter
{
    private readonly ILogger<BackgroundJobExecuter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public BackgroundJobExecuter(ILogger<BackgroundJobExecuter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <exception cref="BackgroundJobExecutionException">作业执行失败（可退避重试信号）</exception>
    public async Task ExecuteAsync(BackgroundJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var job = context.ServiceProvider.GetService(context.JobType)
            ?? throw new BackgroundJobExecutionException(
                $"无法解析后台作业处理器：{context.JobType.AssemblyQualifiedName}（是否未注册或缺少生命周期标记？）");

        var jobInterface = Array.Find(
            context.JobType.GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncBackgroundJob<>))
            ?? throw new BackgroundJobExecutionException(
                $"作业处理器 {context.JobType.Name} 未实现 IAsyncBackgroundJob<TArgs>");

        var method = jobInterface.GetMethod(nameof(IAsyncBackgroundJob<object>.ExecuteAsync))
            ?? throw new BackgroundJobExecutionException(
                $"作业处理器 {context.JobType.Name} 缺少 ExecuteAsync 方法");

        try
        {
            var result = method.Invoke(job, [context.JobArgs]);
            if (result is Task task)
            {
                await task;
            }
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            _logger.LogError(ex.InnerException, "后台作业执行失败：{JobType}", context.JobType.Name);
            throw new BackgroundJobExecutionException($"后台作业执行失败：{context.JobType.Name}", ex.InnerException);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "后台作业执行失败：{JobType}", context.JobType.Name);
            throw new BackgroundJobExecutionException($"后台作业执行失败：{context.JobType.Name}", ex);
        }
    }
}
