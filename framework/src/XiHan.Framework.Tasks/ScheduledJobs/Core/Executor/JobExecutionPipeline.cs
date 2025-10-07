#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobExecutionPipeline.cs
// Guid:0c763054-a534-4f93-9869-537c3e461eb4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:32:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Core.Executor;

/// <summary>
/// 任务执行管道
/// </summary>
public class JobExecutionPipeline
{
    private readonly List<IJobMiddleware> _middlewares = [];
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    public JobExecutionPipeline(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// 添加中间件
    /// </summary>
    public JobExecutionPipeline Use(IJobMiddleware middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>
    /// 构建执行委托
    /// </summary>
    public JobExecutionDelegate Build(IJob job)
    {
        JobExecutionDelegate pipeline = async context =>
        {
            try
            {
                return await job.ExecuteAsync(context, context.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                return JobResult.Canceled();
            }
            catch (Exception ex)
            {
                return JobResult.Failure($"任务执行异常: {ex.Message}", ex);
            }
        };

        // 反向构建中间件链
        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = pipeline;
            pipeline = context => middleware.InvokeAsync(context, next);
        }

        return pipeline;
    }

    /// <summary>
    /// 执行任务
    /// </summary>
    public async Task<JobResult> ExecuteAsync(IJobContext context, IJob job)
    {
        var pipeline = Build(job);
        return await pipeline(context);
    }
}
