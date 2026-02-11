#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDynamicServiceConfig
// Guid:6ca5bdc6-0372-435a-9ba3-b82eab7dec63
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/17 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.BackgroundServices;

/// <summary>
/// 动态服务配置接口
/// 支持运行时动态调整服务配置
/// </summary>
public interface IDynamicServiceConfig
{
    /// <summary>
    /// 配置变更事件
    /// </summary>
    event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    /// <summary>
    /// 获取当前最大并发任务数
    /// </summary>
    int MaxConcurrentTasks { get; }

    /// <summary>
    /// 获取当前空闲延迟时间（毫秒）
    /// </summary>
    int IdleDelayMilliseconds { get; }

    /// <summary>
    /// 获取当前是否启用任务处理
    /// </summary>
    bool IsTaskProcessingEnabled { get; }

    /// <summary>
    /// 动态调整最大并发任务数
    /// </summary>
    /// <param name="maxConcurrentTasks">新的最大并发数</param>
    void UpdateMaxConcurrentTasks(int maxConcurrentTasks);

    /// <summary>
    /// 动态调整空闲延迟时间
    /// </summary>
    /// <param name="idleDelayMilliseconds">新的延迟时间</param>
    void UpdateIdleDelay(int idleDelayMilliseconds);

    /// <summary>
    /// 启用/禁用任务处理
    /// </summary>
    /// <param name="enabled">是否启用</param>
    void SetTaskProcessingEnabled(bool enabled);
}
