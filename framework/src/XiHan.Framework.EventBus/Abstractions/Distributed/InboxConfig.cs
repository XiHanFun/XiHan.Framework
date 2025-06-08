#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InboxConfig
// Guid:57ddd7f0-134b-4f0e-a912-5bcf30186f29
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 4:50:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.System;

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 收件箱配置类
/// 用于配置分布式事件总线的收件箱（Inbox）模式，管理传入事件的处理策略和数据库配置
/// </summary>
public class InboxConfig
{
    private string _databaseName = default!;

    /// <summary>
    /// 初始化 InboxConfig 类的新实例
    /// </summary>
    /// <param name="name">收件箱名称</param>
    /// <exception cref="ArgumentException">当 name 为 null 或空白字符串时</exception>
    public InboxConfig(string name)
    {
        Name = Guard.NotNullOrWhiteSpace(name, nameof(name));
    }

    /// <summary>
    /// 获取收件箱名称
    /// </summary>
    /// <value>收件箱的唯一标识名称</value>
    public string Name { get; }

    /// <summary>
    /// 获取或设置数据库名称
    /// 指定收件箱数据存储的目标数据库
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
    /// 指定收件箱的具体实现类型，用于依赖注入
    /// </summary>
    /// <value>收件箱实现的类型信息</value>
    public Type ImplementationType { get; set; } = default!;

    /// <summary>
    /// 获取或设置事件选择器
    /// 用于确定哪些事件类型应该被此收件箱处理
    /// </summary>
    /// <value>事件类型筛选委托，如果为 null 则处理所有事件</value>
    public Func<Type, bool>? EventSelector { get; set; }

    /// <summary>
    /// 获取或设置处理器选择器
    /// 用于确定哪些事件处理器应该被此收件箱使用
    /// </summary>
    /// <value>处理器类型筛选委托，如果为 null 则使用所有处理器</value>
    public Func<Type, bool>? HandlerSelector { get; set; }

    /// <summary>
    /// 获取或设置是否启用传入事件处理
    /// 用于控制收件箱是否处理接收到的事件
    /// </summary>
    /// <value>如果启用事件处理则为 true，否则为 false。默认值为 true</value>
    public bool IsProcessingEnabled { get; set; } = true;
}
