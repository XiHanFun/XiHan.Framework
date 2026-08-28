// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Text;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Extensions.Exceptions;
using XiHan.Framework.Core.Logging;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Core.Extensions.Logging;

/// <summary>
/// 日志扩展方法
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="logLevel"></param>
    /// <param name="message"></param>
    public static void LogWithLevel(this ILogger logger, LogLevel logLevel, string message)
    {
        switch (logLevel)
        {
            case LogLevel.Critical:
                logger.LogCritical("{message}", message);
                break;

            case LogLevel.Error:
                logger.LogError("{message}", message);
                break;

            case LogLevel.Warning:
                logger.LogWarning("{message}", message);
                break;

            case LogLevel.Information:
                logger.LogInformation("{message}", message);
                break;

            case LogLevel.Trace:
                logger.LogTrace("{message}", message);
                break;

            // LogLevel.Debug || LogLevel.None
            case LogLevel.Debug:
            case LogLevel.None:
            default:
                logger.LogDebug("{message}", message);
                break;
        }
    }

    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="logLevel"></param>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    public static void LogWithLevel(this ILogger logger, LogLevel logLevel, string message, Exception exception)
    {
        // 原先七个分支都写成 logger.LogXxx("{exception}{message}", exception, message)：异常被当成模板的
        // 第一个格式化参数，而不是 ILogger 的 exception 形参。结果 ILogger.Log 收到的 exception 恒为 null，
        // 异常只以 ToString() 文本混进消息里，Serilog / OTel / AppInsights 这类结构化接收端拿不到异常对象，
        // 堆栈与异常类型无法被索引。改为走带异常形参的重载，消息模板只留 {message}。
        switch (logLevel)
        {
            case LogLevel.Critical:
                logger.LogCritical(exception, "{message}", message);
                break;

            case LogLevel.Error:
                logger.LogError(exception, "{message}", message);
                break;

            case LogLevel.Warning:
                logger.LogWarning(exception, "{message}", message);
                break;

            case LogLevel.Information:
                logger.LogInformation(exception, "{message}", message);
                break;

            case LogLevel.Trace:
                logger.LogTrace(exception, "{message}", message);
                break;

            // LogLevel.Debug || LogLevel.None
            case LogLevel.Debug:
            case LogLevel.None:
            default:
                logger.LogDebug(exception, "{message}", message);
                break;
        }
    }

    /// <summary>
    /// 记录异常
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="ex"></param>
    /// <param name="level"></param>
    public static void LogException(this ILogger logger, Exception ex, LogLevel? level = null)
    {
        var selectedLevel = level ?? ex.GetLogLevel();

        logger.LogWithLevel(selectedLevel, ex.Message, ex);
        LogKnownProperties(logger, ex, selectedLevel);
        LogSelfLogging(logger, ex);
        LogData(logger, ex, selectedLevel);
    }

    /// <summary>
    /// 记录已知异常属性
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="exception"></param>
    /// <param name="logLevel"></param>
    private static void LogKnownProperties(ILogger logger, Exception exception, LogLevel logLevel)
    {
        if (exception is IHasErrorCode exceptionWithErrorCode)
        {
            logger.LogWithLevel(logLevel, "异常代码:" + exceptionWithErrorCode.Code);
        }

        if (exception is IHasErrorDetails exceptionWithErrorDetails)
        {
            logger.LogWithLevel(logLevel, "异常详情:" + exceptionWithErrorDetails.Details);
        }
    }

    /// <summary>
    /// 记录异常数据
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="exception"></param>
    /// <param name="logLevel"></param>
    private static void LogData(ILogger logger, Exception exception, LogLevel logLevel)
    {
        if (exception.Data.Count <= 0)
        {
            return;
        }

        StringBuilder exceptionData = new();
        exceptionData.AppendLine("---------- 异常数据 ----------");
        foreach (var key in exception.Data.Keys)
        {
            exceptionData.AppendLine($"{key} = {exception.Data[key]}");
        }

        logger.LogWithLevel(logLevel, exceptionData.ToString());
    }

    /// <summary>
    /// 记录自身日志
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="exception"></param>
    private static void LogSelfLogging(ILogger logger, Exception exception)
    {
        List<IExceptionWithSelfLogging> loggingExceptions = [];

        switch (exception)
        {
            case IExceptionWithSelfLogging logging:
                loggingExceptions.Add(logging);
                break;

            case AggregateException { InnerException: not null } aggException:
                {
                    if (aggException.InnerException is IExceptionWithSelfLogging selfLogging)
                    {
                        loggingExceptions.Add(selfLogging);
                    }

                    foreach (var innerException in aggException.InnerExceptions)
                    {
                        if (innerException is IExceptionWithSelfLogging withSelfLogging)
                        {
                            loggingExceptions.AddIfNotContains(withSelfLogging);
                        }
                    }

                    break;
                }
        }

        foreach (var ex in loggingExceptions)
        {
            ex.Log(logger);
        }
    }
}
