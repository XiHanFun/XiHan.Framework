#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SingleInstanceHandlerFactory
// Guid:50815867-ce65-4bb5-aa7b-4957f17044ad
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 08:13:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.EventBus.Abstractions;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 单实例事件处理器工厂
/// 此 <see cref="IEventHandlerFactory"/> 实现用于通过单个实例对象处理事件
/// 始终返回同一个处理器实例，适用于有状态或需要保持单例的事件处理器
/// </summary>
/// <remarks>
/// 此类总是获取处理器的同一个单实例，所有事件都将由该实例处理
/// </remarks>
public class SingleInstanceHandlerFactory : IEventHandlerFactory
{
    /// <summary>
    /// 初始化 SingleInstanceHandlerFactory 类的新实例
    /// </summary>
    /// <param name="handler">事件处理器实例</param>
    /// <exception cref="ArgumentNullException">当 handler 为 null 时</exception>
    public SingleInstanceHandlerFactory(IEventHandler handler)
    {
        HandlerInstance = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// 获取事件处理器实例
    /// 存储的处理器单实例，将被重复使用处理所有事件
    /// </summary>
    /// <value>事件处理器的单实例对象</value>
    public IEventHandler HandlerInstance { get; }

    /// <summary>
    /// 获取事件处理器实例
    /// 始终返回同一个处理器实例，包装在释放包装器中
    /// </summary>
    /// <returns>包装了事件处理器实例的释放包装器</returns>
    /// <remarks>
    /// 由于是单实例模式，释放包装器不会实际释放处理器实例
    /// </remarks>
    public IEventHandlerDisposeWrapper GetHandler()
    {
        return new EventHandlerDisposeWrapper(HandlerInstance);
    }

    /// <summary>
    /// 检查当前工厂是否已存在于工厂列表中
    /// 通过比较处理器实例来确定是否为同一个工厂
    /// </summary>
    /// <param name="handlerFactories">事件处理器工厂列表</param>
    /// <returns>如果工厂已存在则返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当 handlerFactories 为 null 时</exception>
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        ArgumentNullException.ThrowIfNull(handlerFactories);

        return handlerFactories
            .OfType<SingleInstanceHandlerFactory>()
            .Any(f => f.HandlerInstance == HandlerInstance);
    }
}
