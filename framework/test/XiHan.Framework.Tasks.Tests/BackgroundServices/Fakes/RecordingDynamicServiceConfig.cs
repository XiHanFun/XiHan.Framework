// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.BackgroundServices;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices.Fakes;

/// <summary>
/// 会记录读取次数的动态配置装饰器
/// </summary>
/// <remarks>
/// 后台服务基类"暂停处理"这条分支唯一的可观察副作用就是反复读取空闲延迟时间。
/// 把这个读取计数暴露出来，用例就能用条件轮询确认"确实空转了若干轮"，
/// 而不必写死一个 Task.Delay 去赌时间够不够。
/// </remarks>
public sealed class RecordingDynamicServiceConfig : IDynamicServiceConfig
{
    private readonly DynamicServiceConfig _inner;
    private int _idleDelayReadCount;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">后台服务配置选项</param>
    public RecordingDynamicServiceConfig(IOptions<XiHanBackgroundServiceOptions> options)
    {
        _inner = new DynamicServiceConfig(options);
        _inner.ConfigChanged += OnInnerConfigChanged;
    }

    /// <summary>
    /// 配置变更事件
    /// </summary>
    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    /// <summary>
    /// 最大并发任务数
    /// </summary>
    public int MaxConcurrentTasks => _inner.MaxConcurrentTasks;

    /// <summary>
    /// 空闲延迟时间（毫秒），每次读取都会计数
    /// </summary>
    public int IdleDelayMilliseconds
    {
        get
        {
            Interlocked.Increment(ref _idleDelayReadCount);
            return _inner.IdleDelayMilliseconds;
        }
    }

    /// <summary>
    /// 是否启用任务处理
    /// </summary>
    public bool IsTaskProcessingEnabled => _inner.IsTaskProcessingEnabled;

    /// <summary>
    /// 空闲延迟时间被读取的次数
    /// </summary>
    public int IdleDelayReadCount => Volatile.Read(ref _idleDelayReadCount);

    /// <summary>
    /// 动态调整最大并发任务数
    /// </summary>
    /// <param name="maxConcurrentTasks">新的最大并发数</param>
    public void UpdateMaxConcurrentTasks(int maxConcurrentTasks)
    {
        _inner.UpdateMaxConcurrentTasks(maxConcurrentTasks);
    }

    /// <summary>
    /// 动态调整空闲延迟时间
    /// </summary>
    /// <param name="idleDelayMilliseconds">新的延迟时间</param>
    public void UpdateIdleDelay(int idleDelayMilliseconds)
    {
        _inner.UpdateIdleDelay(idleDelayMilliseconds);
    }

    /// <summary>
    /// 启用或禁用任务处理
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public void SetTaskProcessingEnabled(bool enabled)
    {
        _inner.SetTaskProcessingEnabled(enabled);
    }

    private void OnInnerConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        ConfigChanged?.Invoke(this, e);
    }
}
