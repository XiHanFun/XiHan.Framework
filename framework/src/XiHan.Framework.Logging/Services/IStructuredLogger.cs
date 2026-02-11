#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IStructuredLogger
// Guid:bf43b8ad-b60a-4e71-b42d-9d262847fd06
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// 结构化日志器接口
/// </summary>
public interface IStructuredLogger
{
    /// <summary>
    /// 记录结构化信息日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="data">结构化数据</param>
    void LogInformation(string message, object data);

    /// <summary>
    /// 记录结构化警告日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="data">结构化数据</param>
    void LogWarning(string message, object data);

    /// <summary>
    /// 记录结构化错误日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="data">结构化数据</param>
    void LogError(string message, object data);

    /// <summary>
    /// 记录结构化错误日志
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="message">消息</param>
    /// <param name="data">结构化数据</param>
    void LogError(Exception exception, string message, object data);

    /// <summary>
    /// 记录自定义级别的结构化日志
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">消息</param>
    /// <param name="data">结构化数据</param>
    void Log(LogLevel level, string message, object data);

    /// <summary>
    /// 记录事件日志
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventData">事件数据</param>
    void LogEvent(string eventName, object eventData);

    /// <summary>
    /// 记录业务日志
    /// </summary>
    /// <param name="businessAction">业务动作</param>
    /// <param name="businessData">业务数据</param>
    void LogBusiness(string businessAction, object businessData);
}
