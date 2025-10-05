#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanLogger
// Guid:e1f6a7b8-0c9d-4e1f-c8a5-6b7c9d8e1f3g
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// XiHan 日志器接口
/// </summary>
public interface IXiHanLogger
{
    /// <summary>
    /// 记录跟踪日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    void LogTrace(string message, params object[] args);

    /// <summary>
    /// 记录调试日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    void LogDebug(string message, params object[] args);

    /// <summary>
    /// 记录信息日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    void LogInfo(string message, params object[] args);

    /// <summary>
    /// 记录警告日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    void LogWarn(string message, params object[] args);

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    void LogError(string message, params object[] args);

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    void LogError(Exception exception, string message, params object[] args);

    /// <summary>
    /// 记录严重错误日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    void LogCritical(string message, params object[] args);

    /// <summary>
    /// 记录严重错误日志
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    void LogCritical(Exception exception, string message, params object[] args);

    /// <summary>
    /// 记录结构化日志
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">消息</param>
    /// <param name="properties">属性</param>
    void LogStructured(LogLevel level, string message, object properties);

    /// <summary>
    /// 记录性能日志
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="duration">持续时间</param>
    /// <param name="properties">附加属性</param>
    void LogPerformance(string operationName, TimeSpan duration, object? properties = null);

    /// <summary>
    /// 检查是否启用指定日志级别
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns></returns>
    bool IsEnabled(LogLevel logLevel);

    /// <summary>
    /// 开始日志作用域
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns></returns>
    IDisposable? BeginScope<TState>(TState state) where TState : notnull;
}

/// <summary>
/// 泛型 XiHan 日志器接口
/// </summary>
/// <typeparam name="T">类型</typeparam>
public interface IXiHanLogger<out T> : IXiHanLogger
{
}
