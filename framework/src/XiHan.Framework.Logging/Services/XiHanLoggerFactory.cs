// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// XiHan 日志工厂实现
/// </summary>
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
    /// <param name="categoryName">分类名称</param>
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
    /// <param name="categoryName">分类名称</param>
    /// <returns></returns>
    public IStructuredLogger CreateStructuredLogger(string categoryName)
    {
        return _serviceProvider.GetRequiredService<IStructuredLogger>();
    }

    /// <summary>
    /// 创建性能日志器
    /// </summary>
    /// <param name="categoryName">分类名称</param>
    /// <returns></returns>
    public IPerformanceLogger CreatePerformanceLogger(string categoryName)
    {
        return _serviceProvider.GetRequiredService<IPerformanceLogger>();
    }
}
