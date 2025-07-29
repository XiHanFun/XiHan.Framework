#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StructuredLogger
// Guid:b0c5d6e7-9f8a-4b0c-a7d4-5e6f7a8b0c2p
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 12:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Serilog;

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// 结构化日志器实现
/// </summary>
public class StructuredLogger : IStructuredLogger
{
    private readonly ILogger<StructuredLogger> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志器</param>
    public StructuredLogger(ILogger<StructuredLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 记录结构化信息日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="data">结构化数据</param>
    public void LogInformation(string message, object data)
    {
        using (Serilog.Context.LogContext.PushProperty("StructuredData", data, true))
        {
            _logger.LogInformation(message);
        }
    }

    /// <summary>
    /// 记录结构化警告日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="data">结构化数据</param>
    public void LogWarning(string message, object data)
    {
        using (Serilog.Context.LogContext.PushProperty("StructuredData", data, true))
        {
            _logger.LogWarning(message);
        }
    }

    /// <summary>
    /// 记录结构化错误日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="data">结构化数据</param>
    public void LogError(string message, object data)
    {
        using (Serilog.Context.LogContext.PushProperty("StructuredData", data, true))
        {
            _logger.LogError(message);
        }
    }

    /// <summary>
    /// 记录结构化错误日志
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="message">消息</param>
    /// <param name="data">结构化数据</param>
    public void LogError(Exception exception, string message, object data)
    {
        using (Serilog.Context.LogContext.PushProperty("StructuredData", data, true))
        {
            _logger.LogError(exception, message);
        }
    }

    /// <summary>
    /// 记录自定义级别的结构化日志
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">消息</param>
    /// <param name="data">结构化数据</param>
    public void Log(LogLevel level, string message, object data)
    {
        using (Serilog.Context.LogContext.PushProperty("StructuredData", data, true))
        {
            _logger.Log(level, message);
        }
    }

    /// <summary>
    /// 记录事件日志
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventData">事件数据</param>
    public void LogEvent(string eventName, object eventData)
    {
        using (Serilog.Context.LogContext.PushProperty("EventName", eventName))
        using (Serilog.Context.LogContext.PushProperty("EventData", eventData, true))
        {
            _logger.LogInformation("Event: {EventName}", eventName);
        }
    }

    /// <summary>
    /// 记录业务日志
    /// </summary>
    /// <param name="businessAction">业务动作</param>
    /// <param name="businessData">业务数据</param>
    public void LogBusiness(string businessAction, object businessData)
    {
        using (Serilog.Context.LogContext.PushProperty("BusinessAction", businessAction))
        using (Serilog.Context.LogContext.PushProperty("BusinessData", businessData, true))
        {
            _logger.LogInformation("Business Action: {BusinessAction}", businessAction);
        }
    }
}
