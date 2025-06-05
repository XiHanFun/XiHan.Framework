#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InboxConfigDictionary
// Guid:a71a316d-51c2-4d8b-a9a0-419fa1680560
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 4:49:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 收件箱配置字典类
/// 管理多个收件箱配置的集合，提供配置收件箱的便捷方法
/// 继承自 Dictionary&lt;string, InboxConfig&gt;，以收件箱名称作为键
/// </summary>
public class InboxConfigDictionary : Dictionary<string, InboxConfig>
{
    /// <summary>
    /// 配置默认收件箱
    /// 使用 "Default" 作为收件箱名称进行配置
    /// </summary>
    /// <param name="configAction">收件箱配置委托</param>
    /// <exception cref="ArgumentNullException">当 configAction 为 null 时</exception>
    public void Configure(Action<InboxConfig> configAction)
    {
        Configure("Default", configAction);
    }

    /// <summary>
    /// 配置指定名称的收件箱
    /// 如果收件箱不存在则创建新的，如果已存在则更新配置
    /// </summary>
    /// <param name="outboxName">收件箱名称</param>
    /// <param name="configAction">收件箱配置委托</param>
    /// <exception cref="ArgumentNullException">当 outboxName 或 configAction 为 null 时</exception>
    /// <exception cref="ArgumentException">当 outboxName 为空字符串时</exception>
    public void Configure(string outboxName, Action<InboxConfig> configAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxName);
        ArgumentNullException.ThrowIfNull(configAction);

        var outboxConfig = this.GetOrAdd(outboxName, () => new InboxConfig(outboxName));
        configAction(outboxConfig);
    }
}
