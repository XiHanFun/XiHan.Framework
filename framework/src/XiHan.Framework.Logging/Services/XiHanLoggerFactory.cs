// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// XiHan 日志工厂实现
/// </summary>
/// <remarks>
/// 本工厂是容器解析的直通层，不按分类构造日志器。三个日志器实现注入的都是固定的
/// ILogger&lt;XiHanLogger&gt; / ILogger&lt;StructuredLogger&gt; / ILogger&lt;PerformanceLogger&gt;，
/// 分类（SourceContext）由这些闭合泛型决定；且 IStructuredLogger 与 IPerformanceLogger 按单例登记，
/// 本就不可能做到「一个分类一个实例」。因此各方法的 categoryName 只是保留的接口形参，不参与解析，
/// 需要按分类分流请直接注入 ILogger&lt;T&gt;。这一点由既有回归用例锁定，改动会破坏公共 API 与既有行为。
/// </remarks>
public class XiHanLoggerFactory : IXiHanLoggerFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">服务提供器</param>
    public XiHanLoggerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 创建日志器
    /// </summary>
    /// <param name="categoryName">分类名称，当前实现不使用，见类型说明</param>
    /// <returns></returns>
    public IXiHanLogger CreateLogger(string categoryName)
    {
        return _serviceProvider.GetRequiredService<IXiHanLogger>();
    }

    /// <summary>
    /// 创建泛型日志器
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <returns></returns>
    public IXiHanLogger<T> CreateLogger<T>()
    {
        return _serviceProvider.GetRequiredService<IXiHanLogger<T>>();
    }

    /// <summary>
    /// 创建结构化日志器
    /// </summary>
    /// <param name="categoryName">分类名称，当前实现不使用，见类型说明</param>
    /// <returns></returns>
    public IStructuredLogger CreateStructuredLogger(string categoryName)
    {
        return _serviceProvider.GetRequiredService<IStructuredLogger>();
    }

    /// <summary>
    /// 创建性能日志器
    /// </summary>
    /// <param name="categoryName">分类名称，当前实现不使用，见类型说明</param>
    /// <returns></returns>
    public IPerformanceLogger CreatePerformanceLogger(string categoryName)
    {
        return _serviceProvider.GetRequiredService<IPerformanceLogger>();
    }
}
