// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Web.Core.Tests.Infrastructure;

/// <summary>
/// 手写的宿主生命周期替身，用于在用例中主动触发停止事件
/// </summary>
public sealed class FakeHostApplicationLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource _startedSource = new();
    private readonly CancellationTokenSource _stoppingSource = new();
    private readonly CancellationTokenSource _stoppedSource = new();

    /// <summary>
    /// 应用已启动
    /// </summary>
    public CancellationToken ApplicationStarted => _startedSource.Token;

    /// <summary>
    /// 应用停止中
    /// </summary>
    public CancellationToken ApplicationStopping => _stoppingSource.Token;

    /// <summary>
    /// 应用已停止
    /// </summary>
    public CancellationToken ApplicationStopped => _stoppedSource.Token;

    /// <summary>
    /// StopApplication 被调用的次数
    /// </summary>
    public int StopApplicationCallCount { get; private set; }

    /// <summary>
    /// 请求停止应用
    /// </summary>
    public void StopApplication()
    {
        StopApplicationCallCount++;
    }

    /// <summary>
    /// 触发"停止中"事件，同步执行已登记的回调
    /// </summary>
    public void RaiseStopping()
    {
        _stoppingSource.Cancel();
    }

    /// <summary>
    /// 触发"已停止"事件，同步执行已登记的回调
    /// </summary>
    public void RaiseStopped()
    {
        _stoppedSource.Cancel();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _startedSource.Dispose();
        _stoppingSource.Dispose();
        _stoppedSource.Dispose();
    }
}
