#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IocEventHandlerFactory
// Guid:a7637604-20c6-4209-998a-bf4927c54ed7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 08:14:48
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.Abstractions;

namespace XiHan.Framework.EventBus;

/// <summary>
/// IoC 容器事件处理器工厂
/// 此 <see cref="IEventHandlerFactory"/> 实现使用 IoC 容器来获取和释放事件处理器
/// 支持依赖注入和生命周期管理，适用于需要依赖注入的复杂事件处理器
/// </summary>
/// <remarks>
/// 此工厂通过 IoC 容器解析处理器实例，支持所有 DI 容器注册的生命周期管理
/// </remarks>
public class IocEventHandlerFactory : IEventHandlerFactory, IDisposable
{
    /// <summary>
    /// 初始化 IocEventHandlerFactory 类的新实例
    /// </summary>
    /// <param name="scopeFactory">服务作用域工厂，用于创建独立的服务作用域</param>
    /// <param name="handlerType">事件处理器类型</param>
    /// <exception cref="ArgumentNullException">当 scopeFactory 或 handlerType 为 null 时</exception>
    public IocEventHandlerFactory(IServiceScopeFactory scopeFactory, Type handlerType)
    {
        ScopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
    }

    /// <summary>
    /// 获取事件处理器类型
    /// </summary>
    /// <value>处理器的类型信息</value>
    public Type HandlerType { get; }

    /// <summary>
    /// 获取服务作用域工厂
    /// 用于创建独立的依赖注入作用域
    /// </summary>
    /// <value>服务作用域工厂实例</value>
    protected IServiceScopeFactory ScopeFactory { get; }

    /// <summary>
    /// 从 IoC 容器解析事件处理器对象
    /// 创建新的服务作用域并从中解析处理器实例
    /// </summary>
    /// <returns>包装了事件处理器实例的释放包装器，包含作用域释放逻辑</returns>
    /// <exception cref="InvalidOperationException">当无法从容器解析处理器时</exception>
    public IEventHandlerDisposeWrapper GetHandler()
    {
        var scope = ScopeFactory.CreateScope();
        try
        {
            var handler = (IEventHandler)scope.ServiceProvider.GetRequiredService(HandlerType);
            return new EventHandlerDisposeWrapper(
                handler,
                scope.Dispose
            );
        }
        catch (Exception ex)
        {
            scope.Dispose();
            throw new InvalidOperationException(
                $"无法从 IoC 容器解析事件处理器，类型：{HandlerType.FullName}。请确保该类型已正确注册到依赖注入容器中。",
                ex);
        }
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
            .OfType<IocEventHandlerFactory>()
            .Any(f => f.HandlerType == HandlerType);
    }

    /// <summary>
    /// 释放资源
    /// 当前实现为空，因为实际的资源释放由各个作用域负责
    /// </summary>
    public void Dispose()
    {
        // 当前实现为空，因为实际的资源释放由各个服务作用域负责
        // 每个 GetHandler() 调用创建的作用域会在包装器释放时自动释放
    }
}
