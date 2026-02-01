#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TransientEventHandlerFactory
// Guid:04e1c589-d890-449d-a983-e4eccd0696c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 08:12:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.EventBus.Abstractions;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 泛型瞬时事件处理器工厂
/// 此 <see cref="IEventHandlerFactory"/> 实现用于通过瞬时实例对象处理事件
/// 每次请求都会创建新的处理器实例
/// </summary>
/// <typeparam name="THandler">事件处理器类型，必须实现 IEventHandler 接口并具有无参构造函数</typeparam>
/// <remarks>
/// 此类总是创建处理器类型的新瞬时实例，适用于无状态或需要独立实例的事件处理器
/// </remarks>
public class TransientEventHandlerFactory<THandler> : TransientEventHandlerFactory
    where THandler : IEventHandler, new()
{
    /// <summary>
    /// 初始化 TransientEventHandlerFactory&lt;THandler&gt; 类的新实例
    /// </summary>
    public TransientEventHandlerFactory()
        : base(typeof(THandler))
    {
    }

    /// <summary>
    /// 创建事件处理器实例
    /// 使用泛型约束确保类型安全的实例创建
    /// </summary>
    /// <returns>新创建的事件处理器实例</returns>
    protected override IEventHandler CreateHandler()
    {
        return new THandler();
    }
}

/// <summary>
/// 瞬时事件处理器工厂
/// 此 <see cref="IEventHandlerFactory"/> 实现用于通过瞬时实例对象处理事件
/// 每次请求都会创建新的处理器实例
/// </summary>
/// <remarks>
/// 此类总是创建处理器类型的新瞬时实例，适用于无状态或需要独立实例的事件处理器
/// </remarks>
public class TransientEventHandlerFactory : IEventHandlerFactory
{
    /// <summary>
    /// 初始化 TransientEventHandlerFactory 类的新实例
    /// </summary>
    /// <param name="handlerType">事件处理器类型</param>
    /// <exception cref="ArgumentNullException">当 handlerType 为 null 时</exception>
    public TransientEventHandlerFactory(Type handlerType)
    {
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
    }

    /// <summary>
    /// 获取事件处理器类型
    /// </summary>
    /// <value>处理器的类型信息</value>
    public Type HandlerType { get; }

    /// <summary>
    /// 创建事件处理器的新实例
    /// 每次调用都会创建一个新的处理器实例，并包装在释放包装器中
    /// </summary>
    /// <returns>包装了事件处理器实例的释放包装器</returns>
    /// <exception cref="InvalidOperationException">当无法创建处理器实例时</exception>
    public virtual IEventHandlerDisposeWrapper GetHandler()
    {
        var handler = CreateHandler();
        return new EventHandlerDisposeWrapper(
            handler,
            () => (handler as IDisposable)?.Dispose()
        );
    }

    /// <summary>
    /// 检查当前工厂是否已存在于工厂列表中
    /// 通过比较处理器类型来确定是否为同一个工厂
    /// </summary>
    /// <param name="handlerFactories">事件处理器工厂列表</param>
    /// <returns>如果工厂已存在则返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当 handlerFactories 为 null 时</exception>
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        ArgumentNullException.ThrowIfNull(handlerFactories);

        return handlerFactories
            .OfType<TransientEventHandlerFactory>()
            .Any(f => f.HandlerType == HandlerType);
    }

    /// <summary>
    /// 创建事件处理器实例
    /// 使用 Activator.CreateInstance 方法创建处理器类型的实例
    /// </summary>
    /// <returns>新创建的事件处理器实例</returns>
    /// <exception cref="InvalidOperationException">当无法创建处理器实例时</exception>
    protected virtual IEventHandler CreateHandler()
    {
        try
        {
            return (IEventHandler)Activator.CreateInstance(HandlerType)!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"无法创建事件处理器实例，类型：{HandlerType.FullName}。请确保该类型具有无参构造函数。",
                ex);
        }
    }
}
