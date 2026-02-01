#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanLoggerFactory
// Guid:bee3da65-c382-440f-a05b-7c56e412ded3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
