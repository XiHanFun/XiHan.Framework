#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicServiceConfig
// Guid:${guid}
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/17 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Tasks.BackgroundServices;

/// <summary>
/// 动态服务配置实现
/// 提供线程安全的运行时配置调整功能，支持最大并发数、空闲延迟时间和任务处理开关的动态修改
/// </summary>
public class DynamicServiceConfig : IDynamicServiceConfig
{
    /// <summary>
    /// 线程同步锁，确保配置更新的原子性
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 最大并发任务数（volatile 确保可见性）
    /// </summary>
    private volatile int _maxConcurrentTasks;

    /// <summary>
    /// 空闲延迟时间（毫秒）（volatile 确保可见性）
    /// </summary>
    private volatile int _idleDelayMilliseconds;

    /// <summary>
    /// 任务处理是否启用（volatile 确保可见性）
    /// </summary>
    private volatile bool _isTaskProcessingEnabled = true;

    /// <summary>
    /// 初始化动态服务配置实例
    /// 从静态配置选项中读取初始值，后续可通过方法动态调整
    /// </summary>
    /// <param name="options">后台服务初始配置选项</param>
    /// <exception cref="ArgumentNullException">当 options 为 null 时抛出</exception>
    public DynamicServiceConfig(IOptions<XiHanBackgroundServiceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var opts = options.Value;
        _maxConcurrentTasks = opts.MaxConcurrentTasks;
        _idleDelayMilliseconds = opts.IdleDelayMilliseconds;
    }

    /// <summary>
    /// 配置变更时触发的事件
    /// 当任何配置项（最大并发数、空闲延迟时间、任务处理开关）发生变更时触发
    /// </summary>
    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    /// <summary>
    /// 获取当前最大并发任务数
    /// 该值可通过 <see cref="UpdateMaxConcurrentTasks"/> 方法动态调整
    /// </summary>
    /// <value>当前允许的最大并发任务数，必须大于 0</value>
    public int MaxConcurrentTasks => _maxConcurrentTasks;

    /// <summary>
    /// 获取当前空闲延迟时间（毫秒）
    /// 当队列为空或达到最大并发数时，服务等待的时间间隔
    /// </summary>
    /// <value>空闲延迟时间（毫秒），不能为负数</value>
    public int IdleDelayMilliseconds => _idleDelayMilliseconds;

    /// <summary>
    /// 获取任务处理是否启用
    /// 当设置为 false 时，服务将暂停处理新任务，但不会停止已运行的任务
    /// </summary>
    /// <value>true 表示启用任务处理，false 表示暂停任务处理</value>
    public bool IsTaskProcessingEnabled => _isTaskProcessingEnabled;

    /// <summary>
    /// 动态调整最大并发任务数
    /// 此方法是线程安全的，调整后会立即生效并触发 <see cref="ConfigChanged"/> 事件
    /// </summary>
    /// <param name="maxConcurrentTasks">新的最大并发任务数，必须大于 0</param>
    /// <exception cref="ArgumentException">当 maxConcurrentTasks 小于等于 0 时抛出</exception>
    /// <remarks>
    /// 调整后的并发数会在下一轮任务获取时生效，正在运行的任务不会受到影响
    /// </remarks>
    public void UpdateMaxConcurrentTasks(int maxConcurrentTasks)
    {
        if (maxConcurrentTasks <= 0)
        {
            throw new ArgumentException("最大并发任务数必须大于0", nameof(maxConcurrentTasks));
        }

        lock (_lock)
        {
            var oldValue = _maxConcurrentTasks;
            if (oldValue != maxConcurrentTasks)
            {
                _maxConcurrentTasks = maxConcurrentTasks;
                OnConfigChanged(nameof(MaxConcurrentTasks), oldValue, maxConcurrentTasks);
            }
        }
    }

    /// <summary>
    /// 动态调整空闲延迟时间
    /// 此方法是线程安全的，调整后会立即生效并触发 <see cref="ConfigChanged"/> 事件
    /// </summary>
    /// <param name="idleDelayMilliseconds">新的空闲延迟时间（毫秒），不能为负数</param>
    /// <exception cref="ArgumentException">当 idleDelayMilliseconds 小于 0 时抛出</exception>
    /// <remarks>
    /// 空闲延迟时间影响服务在无任务或达到最大并发时的等待间隔，
    /// 较小的值会提高响应速度但增加 CPU 消耗，较大的值则相反
    /// </remarks>
    public void UpdateIdleDelay(int idleDelayMilliseconds)
    {
        if (idleDelayMilliseconds < 0)
        {
            throw new ArgumentException("空闲延迟时间不能为负数", nameof(idleDelayMilliseconds));
        }

        lock (_lock)
        {
            var oldValue = _idleDelayMilliseconds;
            if (oldValue != idleDelayMilliseconds)
            {
                _idleDelayMilliseconds = idleDelayMilliseconds;
                OnConfigChanged(nameof(IdleDelayMilliseconds), oldValue, idleDelayMilliseconds);
            }
        }
    }

    /// <summary>
    /// 动态启用或禁用任务处理
    /// 此方法是线程安全的，调整后会立即生效并触发 <see cref="ConfigChanged"/> 事件
    /// </summary>
    /// <param name="enabled">true 表示启用任务处理，false 表示暂停任务处理</param>
    /// <remarks>
    /// 当设置为 false 时，服务将停止获取新任务，但已经在运行的任务会继续执行到完成。
    /// 这可用于优雅地暂停服务或进行维护操作。
    /// </remarks>
    public void SetTaskProcessingEnabled(bool enabled)
    {
        lock (_lock)
        {
            var oldValue = _isTaskProcessingEnabled;
            if (oldValue != enabled)
            {
                _isTaskProcessingEnabled = enabled;
                OnConfigChanged(nameof(IsTaskProcessingEnabled), oldValue, enabled);
            }
        }
    }

    /// <summary>
    /// 触发配置变更事件
    /// </summary>
    /// <param name="propertyName">配置项名称</param>
    /// <param name="oldValue">旧值</param>
    /// <param name="newValue">新值</param>
    protected virtual void OnConfigChanged(string propertyName, object? oldValue, object? newValue)
    {
        ConfigChanged?.Invoke(this, new ConfigChangedEventArgs(propertyName, oldValue, newValue));
    }
}
