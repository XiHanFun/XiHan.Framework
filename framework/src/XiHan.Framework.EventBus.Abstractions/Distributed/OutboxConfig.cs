#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OutboxConfig
// Guid:34567acc-2de5-4215-985b-8ff800de77c0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 04:48:27
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 发件箱配置类
/// 用于配置分布式事件总线的发件箱（Outbox）模式，管理传出事件的发送策略和数据库配置
/// </summary>
public class OutboxConfig
{
    private string _databaseName = default!;

    /// <summary>
    /// 初始化 OutboxConfig 类的新实例
    /// </summary>
    /// <param name="name">发件箱名称</param>
    /// <exception cref="ArgumentException">当 name 为 null 或空白字符串时</exception>
    public OutboxConfig(string name)
    {
        Name = Guard.NotNullOrWhiteSpace(name, nameof(name));
    }

    /// <summary>
    /// 获取发件箱名称
    /// </summary>
    /// <value>发件箱的唯一标识名称</value>
    public string Name { get; }

    /// <summary>
    /// 获取或设置数据库名称
    /// 指定发件箱数据存储的目标数据库
    /// </summary>
    /// <value>数据库名称</value>
    /// <exception cref="ArgumentException">当设置值为 null 或空白字符串时</exception>
    public string DatabaseName
    {
        get => _databaseName;
        set => _databaseName = Guard.NotNullOrWhiteSpace(value, nameof(DatabaseName));
    }

    /// <summary>
    /// 获取或设置实现类型
    /// 指定发件箱的具体实现类型，用于依赖注入
    /// </summary>
    /// <value>发件箱实现的类型信息</value>
    public Type ImplementationType { get; set; } = default!;

    /// <summary>
    /// 获取或设置事件选择器
    /// 用于确定哪些事件类型应该被此发件箱处理
    /// </summary>
    /// <value>事件类型筛选委托，如果为 null 则处理所有事件</value>
    public Func<Type, bool>? Selector { get; set; }

    /// <summary>
    /// 获取或设置是否启用发件箱发送功能
    /// 用于控制发件箱是否向消息中间件发送事件
    /// </summary>
    /// <value>如果启用事件发送则为 true，否则为 false。默认值为 true</value>
    public bool IsSendingEnabled { get; set; } = true;
}
