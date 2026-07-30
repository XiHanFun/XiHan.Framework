// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Attributes;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.BackgroundJobs.Options;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.BackgroundJobs;

/// <summary>
/// 默认后台作业管理器：入队仅写存储并返回标识，执行交由 Worker 轮询驱动
/// </summary>
public class BackgroundJobManager : IBackgroundJobManager
{
    private readonly IBackgroundJobStore _store;
    private readonly IBackgroundJobSerializer _serializer;
    private readonly IClock _clock;
    private readonly ICurrentTenant _currentTenant;
    private readonly BackgroundJobOptions _jobOptions;
    private readonly BackgroundJobWorkerOptions _workerOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    public BackgroundJobManager(
        IBackgroundJobStore store,
        IBackgroundJobSerializer serializer,
        IClock clock,
        ICurrentTenant currentTenant,
        IOptions<BackgroundJobOptions> jobOptions,
        IOptions<BackgroundJobWorkerOptions> workerOptions)
    {
        _store = store;
        _serializer = serializer;
        _clock = clock;
        _currentTenant = currentTenant;
        _jobOptions = jobOptions.Value;
        _workerOptions = workerOptions.Value;
    }

    /// <summary>
    /// 入队一个后台作业
    /// </summary>
    public async Task<string> EnqueueAsync<TArgs>(
        TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null)
    {
        ArgumentNullException.ThrowIfNull(args);

        // 优先使用已注册配置的作业名，确保入队名与执行查找名一致；未注册则回退按参数类型解析
        var jobName = _jobOptions.GetJobByArgsOrNull(typeof(TArgs))?.JobName
            ?? BackgroundJobNameAttribute.GetName(typeof(TArgs));

        var now = _clock.Now;
        var jobInfo = new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            ApplicationName = _workerOptions.ApplicationName,
            TenantId = _currentTenant.Id,
            JobName = jobName,
            JobArgs = _serializer.Serialize(args),
            Priority = priority,
            CreationTime = now,
            NextTryTime = delay.HasValue ? now.Add(delay.Value) : now
        };

        await _store.InsertAsync(jobInfo);
        return jobInfo.Id.ToString();
    }
}
