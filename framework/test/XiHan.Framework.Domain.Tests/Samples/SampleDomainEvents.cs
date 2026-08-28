// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Events;

namespace XiHan.Framework.Domain.Tests.Samples;

/// <summary>
/// 样例领域事件：实体已创建
/// </summary>
public sealed class SampleCreatedEvent : DomainEventBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">名称</param>
    public SampleCreatedEvent(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; }
}

/// <summary>
/// 样例领域事件：实体已更新
/// </summary>
public sealed class SampleUpdatedEvent : DomainEventBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">名称</param>
    public SampleUpdatedEvent(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; }
}
