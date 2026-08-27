// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 记录执行上下文的作业执行器替身
/// </summary>
/// <remarks>
/// Worker 用例只关心"上下文里带了什么、失败信号是哪一类"，
/// 真正的反射调用由 <c>BackgroundJobExecuter</c> 自己的用例覆盖，这里不重复。
/// </remarks>
public sealed class RecordingBackgroundJobExecuter : IBackgroundJobExecuter
{
    private readonly ICurrentTenant? _currentTenant;
    private readonly object _gate = new();
    private readonly List<BackgroundJobExecutionContext> _contexts = [];
    private readonly List<long?> _observedTenantIds = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentTenant">当前租户（用于观察执行期间的租户上下文）</param>
    public RecordingBackgroundJobExecuter(ICurrentTenant? currentTenant = null)
    {
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 执行时抛出的异常（为空表示执行成功）
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>
    /// 收到过的执行上下文
    /// </summary>
    public IReadOnlyList<BackgroundJobExecutionContext> Contexts
    {
        get
        {
            lock (_gate)
            {
                return [.. _contexts];
            }
        }
    }

    /// <summary>
    /// 每次执行时观察到的租户标识
    /// </summary>
    public IReadOnlyList<long?> ObservedTenantIds
    {
        get
        {
            lock (_gate)
            {
                return [.. _observedTenantIds];
            }
        }
    }

    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>任务</returns>
    public Task ExecuteAsync(BackgroundJobExecutionContext context)
    {
        lock (_gate)
        {
            _contexts.Add(context);
            _observedTenantIds.Add(_currentTenant?.Id);
        }

        var exception = ExceptionToThrow;
        return exception is null ? Task.CompletedTask : Task.FromException(exception);
    }
}
