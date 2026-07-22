// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// XiHan 日志工厂接口
/// </summary>
public interface IXiHanLoggerFactory
{
    /// <summary>
    /// 创建日志器
    /// </summary>
    /// <param name="categoryName">分类名称</param>
    /// <returns></returns>
    IXiHanLogger CreateLogger(string categoryName);

    /// <summary>
    /// 创建泛型日志器
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <returns></returns>
    IXiHanLogger<T> CreateLogger<T>();

    /// <summary>
    /// 创建结构化日志器
    /// </summary>
    /// <param name="categoryName">分类名称</param>
    /// <returns></returns>
    IStructuredLogger CreateStructuredLogger(string categoryName);

    /// <summary>
    /// 创建性能日志器
    /// </summary>
    /// <param name="categoryName">分类名称</param>
    /// <returns></returns>
    IPerformanceLogger CreatePerformanceLogger(string categoryName);
}
