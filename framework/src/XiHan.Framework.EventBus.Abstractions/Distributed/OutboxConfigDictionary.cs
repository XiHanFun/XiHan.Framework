#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OutboxConfigDictionary
// Guid:95df0a0c-c59e-4101-bed8-633c15ec4a04
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 04:48:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 发件箱配置字典类
/// 管理多个发件箱配置的集合，提供配置发件箱的便捷方法
/// 继承自 Dictionary&lt;string, OutboxConfig&gt;，以发件箱名称作为键
/// </summary>
public class OutboxConfigDictionary : Dictionary<string, OutboxConfig>
{
    /// <summary>
    /// 配置默认发件箱
    /// 使用 "Default" 作为发件箱名称进行配置
    /// </summary>
    /// <param name="configAction">发件箱配置委托</param>
    /// <exception cref="ArgumentNullException">当 configAction 为 null 时</exception>
    public void Configure(Action<OutboxConfig> configAction)
    {
        Configure("Default", configAction);
    }

    /// <summary>
    /// 配置指定名称的发件箱
    /// 如果发件箱不存在则创建新的，如果已存在则更新配置
    /// </summary>
    /// <param name="outboxName">发件箱名称</param>
    /// <param name="configAction">发件箱配置委托</param>
    /// <exception cref="ArgumentNullException">当 outboxName 或 configAction 为 null 时</exception>
    /// <exception cref="ArgumentException">当 outboxName 为空字符串时</exception>
    public void Configure(string outboxName, Action<OutboxConfig> configAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxName);
        ArgumentNullException.ThrowIfNull(configAction);

        var outboxConfig = this.GetOrAdd(outboxName, () => new OutboxConfig(outboxName));
        configAction(outboxConfig);
    }
}
