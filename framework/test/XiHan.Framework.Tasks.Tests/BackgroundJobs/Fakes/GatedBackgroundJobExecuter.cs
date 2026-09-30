// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 按用例给定的处理逻辑执行作业的执行器替身，记录每次执行的上下文
/// </summary>
public sealed class GatedBackgroundJobExecuter : IBackgroundJobExecuter
{
    private readonly object _gate = new();
    private readonly List<BackgroundJobExecutionContext> _started = [];
    private readonly Func<BackgroundJobExecutionContext, Task> _handler;
    private int _finishedCount;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="handler">处理逻辑</param>
    public GatedBackgroundJobExecuter(Func<BackgroundJobExecutionContext, Task> handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// 已开始执行的上下文
    /// </summary>
    public IReadOnlyList<BackgroundJobExecutionContext> Started
    {
        get
        {
            lock (_gate)
            {
                return [.. _started];
            }
        }
    }

    /// <summary>
    /// 已结束（含异常）的执行次数
    /// </summary>
    public int FinishedCount
    {
        get
        {
            lock (_gate)
            {
                return _finishedCount;
            }
        }
    }

    /// <summary>
    /// 执行作业
    /// </summary>
    public async Task ExecuteAsync(BackgroundJobExecutionContext context)
    {
        lock (_gate)
        {
            _started.Add(context);
        }

        try
        {
            await _handler(context);
        }
        finally
        {
            lock (_gate)
            {
                _finishedCount++;
            }
        }
    }
}
