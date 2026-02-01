#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogHelper
// Guid:824ca05d-f5be-49a9-96f9-8a6502e5b064
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/08 12:40:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.ConsoleTools;
using XiHan.Framework.Utils.Logging.Formatters;

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 主要日志输出类（同时支持控制台和文件输出）
/// 推荐作为首选日志接口使用
/// </summary>
public static class LogHelper
{
    private static readonly Lock LogLock = new();
    private static readonly Lock ConfigLock = new();
    private static readonly LogOptions Options = new();
    private static readonly LogStatistics Statistics = new();
    private static ILogFormatter _formatter = new TextLogFormatter();

    /// <summary>
    /// 配置日志选项
    /// </summary>
    /// <param name="configure">配置操作</param>
    public static void Configure(Action<LogOptions> configure)
    {
        lock (ConfigLock)
        {
            configure?.Invoke(Options);
            ApplyConfiguration();
        }
    }

    /// <summary>
    /// 获取当前配置信息
    /// </summary>
    /// <returns>配置选项对象</returns>
    public static LogOptions GetConfiguration()
    {
        return Options;
    }

    /// <summary>
    /// 获取日志统计信息
    /// </summary>
    /// <returns>日志统计信息</returns>
    public static LogStatistics GetStatistics()
    {
        return Statistics;
    }

    /// <summary>
    /// 重置统计信息
    /// </summary>
    public static void ResetStatistics()
    {
        Statistics.Reset();
    }

    /// <summary>
    /// 获取当前最小日志等级
    /// </summary>
    /// <returns>当前最小日志等级</returns>
    public static LogLevel GetMinimumLevel()
    {
        return Options.MinimumLevel;
    }

    /// <summary>
    /// 获取是否显示日志头的设置
    /// </summary>
    /// <returns>是否显示日志头</returns>
    public static bool GetIsDisplayHeader()
    {
        return Options.DisplayHeader;
    }

    /// <summary>
    /// 设置最小日志等级（小于该等级的日志将被忽略）
    /// </summary>
    /// <param name="level">日志等级，默认 Info</param>
    public static void SetMinimumLevel(LogLevel level)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentException($"无效的日志等级: {level}", nameof(level));
        }

        lock (ConfigLock)
        {
            Options.MinimumLevel = level;

            // 同步设置到文件输出
            if (Options.EnableFileOutput)
            {
                LogFileHelper.SetMinimumLevel(level);
            }
        }
    }

    /// <summary>
    /// 设置是否启用文件输出
    /// </summary>
    /// <param name="enable">是否启用文件输出，默认不启用</param>
    public static void SetFileOutputEnabled(bool enable)
    {
        lock (ConfigLock)
        {
            Options.EnableFileOutput = enable;
            if (enable)
            {
                ApplyConfiguration();
            }
        }
    }

    /// <summary>
    /// 设置是否启用控制台输出
    /// </summary>
    /// <param name="enable">是否启用控制台输出，默认启用</param>
    public static void SetConsoleOutputEnabled(bool enable)
    {
        lock (ConfigLock)
        {
            Options.EnableConsoleOutput = enable;
        }
    }

    /// <summary>
    /// 设置文件输出目录
    /// </summary>
    /// <param name="directoryPath">日志文件目录</param>
    public static void SetLogDirectory(string directoryPath)
    {
        lock (ConfigLock)
        {
            Options.LogDirectory = directoryPath;
            LogFileHelper.SetLogDirectory(directoryPath);
        }
    }

    /// <summary>
    /// 设置文件最大大小
    /// </summary>
    /// <param name="maxSizeBytes">最大文件大小（字节）</param>
    public static void SetMaxFileSize(long maxSizeBytes)
    {
        lock (ConfigLock)
        {
            Options.MaxFileSize = maxSizeBytes;
            LogFileHelper.SetMaxFileSize(maxSizeBytes);
        }
    }

    /// <summary>
    /// 设置是否显示日志头
    /// </summary>
    /// <param name="isDisplayHeader">是否显示头部信息，默认显示</param>
    public static void SetIsDisplayHeader(bool isDisplayHeader)
    {
        lock (ConfigLock)
        {
            Options.DisplayHeader = isDisplayHeader;
        }
    }

    /// <summary>
    /// 正常信息
    /// </summary>
    /// <param name="message">消息内容</param>
    public static void Info(string? message)
    {
        WriteColorLine(message, LogLevel.Info);
    }

    /// <summary>
    /// 正常信息
    /// </summary>
    /// <param name="formattedMessage">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Info(string? formattedMessage, params object[] args)
    {
        var message = FormatMessage(formattedMessage, args);
        WriteColorLine(message, LogLevel.Info);
    }

    /// <summary>
    /// 输出信息级别的表格
    /// </summary>
    /// <param name="table">控制台表格</param>
    public static void InfoTable(ConsoleTable table)
    {
        var message = Environment.NewLine + table.ToString();
        WriteColorLine(message, LogLevel.Info);
    }

    /// <summary>
    /// 成功信息
    /// </summary>
    /// <param name="message">消息内容</param>
    public static void Success(string? message)
    {
        WriteColorLine(message, LogLevel.Success);
    }

    /// <summary>
    /// 成功信息
    /// </summary>
    /// <param name="formattedMessage">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Success(string? formattedMessage, params object[] args)
    {
        var message = FormatMessage(formattedMessage, args);
        WriteColorLine(message, LogLevel.Success);
    }

    /// <summary>
    /// 输出成功级别的表格
    /// </summary>
    /// <param name="table">控制台表格</param>
    public static void SuccessTable(ConsoleTable table)
    {
        var message = Environment.NewLine + table.ToString();
        WriteColorLine(message, LogLevel.Success);
    }

    /// <summary>
    /// 处理、查询信息
    /// </summary>
    /// <param name="message">消息内容</param>
    public static void Handle(string? message)
    {
        WriteColorLine(message, LogLevel.Handle);
    }

    /// <summary>
    /// 处理、查询信息
    /// </summary>
    /// <param name="formattedMessage">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Handle(string? formattedMessage, params object[] args)
    {
        var message = FormatMessage(formattedMessage, args);
        WriteColorLine(message, LogLevel.Handle);
    }

    /// <summary>
    /// 输出处理级别的表格
    /// </summary>
    /// <param name="table">控制台表格</param>
    public static void HandleTable(ConsoleTable table)
    {
        var message = Environment.NewLine + table.ToString();
        WriteColorLine(message, LogLevel.Handle);
    }

    /// <summary>
    /// 警告、新增、更新信息
    /// </summary>
    /// <param name="message">消息内容</param>
    public static void Warn(string? message)
    {
        WriteColorLine(message, LogLevel.Warn);
    }

    /// <summary>
    /// 警告、新增、更新信息
    /// </summary>
    /// <param name="formattedMessage">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Warn(string? formattedMessage, params object[] args)
    {
        var message = FormatMessage(formattedMessage, args);
        WriteColorLine(message, LogLevel.Warn);
    }

    /// <summary>
    /// 输出警告级别的表格
    /// </summary>
    /// <param name="table">控制台表格</param>
    public static void WarnTable(ConsoleTable table)
    {
        var message = Environment.NewLine + table.ToString();
        WriteColorLine(message, LogLevel.Warn);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="message">消息内容</param>
    public static void Error(string? message)
    {
        WriteColorLine(message, LogLevel.Error);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="ex">异常</param>
    public static void Error(Exception ex)
    {
        var message = $"{ex}";
        WriteColorLine(message, LogLevel.Error);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="ex">异常</param>
    /// <param name="errorMessage">消息模板</param>
    public static void Error(Exception ex, string? errorMessage)
    {
        var message = $"{errorMessage}{Environment.NewLine}{ex}";
        WriteColorLine(message, LogLevel.Error);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="formattedMessage">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Error(string? formattedMessage, params object[] args)
    {
        var message = FormatMessage(formattedMessage, args);
        WriteColorLine(message, LogLevel.Error);
    }

    /// <summary>
    /// 输出错误级别的表格
    /// </summary>
    /// <param name="table">控制台表格</param>
    public static void ErrorTable(ConsoleTable table)
    {
        var message = Environment.NewLine + table.ToString();
        WriteColorLine(message, LogLevel.Error);
    }

    /// <summary>
    /// 渐变信息
    /// </summary>
    /// <remarks>
    /// 一般为展示项目信息(如LOGO)使用，不显示日志头
    /// </remarks>
    /// <param name="message">消息内容</param>
    public static void Rainbow(string? message)
    {
        WriteColorLineRainbow(message);
    }

    /// <summary>
    /// 渐变信息
    /// </summary>
    /// <remarks>
    /// 一般为展示项目信息(如LOGO)使用，不显示日志头
    /// </remarks>
    /// <param name="formattedMessage">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Rainbow(string? formattedMessage, params object[] args)
    {
        var message = FormatMessage(formattedMessage, args);
        WriteColorLineRainbow(message);
    }

    /// <summary>
    /// 清除控制台内容
    /// </summary>
    public static void Clear()
    {
        lock (LogLock)
        {
            try
            {
                Console.Clear();
                // 重置颜色缓存状态
                ConsoleColorWriter.ResetColorCache();
            }
            catch
            {
                // 忽略清除失败的异常，某些环境下可能不支持清除
            }
        }
    }

    /// <summary>
    /// 清除所有日志文件
    /// </summary>
    public static void ClearLogFiles()
    {
        if (Options.EnableFileOutput)
        {
            LogFileHelper.Clear();
        }
    }

    /// <summary>
    /// 清除指定日期的日志文件
    /// </summary>
    /// <param name="date">指定日期</param>
    public static void ClearLogFiles(DateTime date)
    {
        if (Options.EnableFileOutput)
        {
            LogFileHelper.Clear(date);
        }
    }

    /// <summary>
    /// 立即刷新所有待处理日志到文件
    /// </summary>
    public static void FlushToFile()
    {
        if (Options.EnableFileOutput)
        {
            LogFileHelper.Flush();
        }
    }

    /// <summary>
    /// 优雅关闭日志系统
    /// </summary>
    public static void Shutdown()
    {
        if (Options.EnableFileOutput)
        {
            LogFileHelper.Shutdown();
        }
    }

    #region 内部方法

    /// <summary>
    /// 是否启用日志,大于等于最小级别才输出
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <returns>是否启用</returns>
    private static bool IsEnabled(LogLevel level)
    {
        return Options.MinimumLevel != LogLevel.None && level >= Options.MinimumLevel;
    }

    /// <summary>
    /// 格式化消息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    /// <returns>格式化后的消息</returns>
    private static string? FormatMessage(string? message, params object[] args)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        try
        {
            // 如果没有参数，直接返回原消息
            if (args == null || args.Length == 0)
            {
                return message;
            }

            // 使用 string.Format 进行格式化
            return string.Format(message, args);
        }
        catch (FormatException)
        {
            // 如果格式化失败，返回原消息并附加参数信息
            var argStr = string.Join(", ", args?.Select(arg => arg?.ToString() ?? "null") ?? []);
            return $"{message} [Args: {argStr}]";
        }
        catch
        {
            // 其他异常情况，返回原消息
            return message;
        }
    }

    /// <summary>
    /// 获取日志类型对应的颜色
    /// </summary>
    /// <param name="logLevel">日志等级</param>
    /// <returns>日志类型</returns>
    private static ConsoleColor GetLogLevelColor(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Warn => ConsoleColor.Yellow,
            LogLevel.Handle => ConsoleColor.Cyan,
            LogLevel.Success => ConsoleColor.Green,
            LogLevel.Info => ConsoleColor.White,
            _ => ConsoleColor.White,
        };
    }

    /// <summary>
    /// 应用配置
    /// </summary>
    private static void ApplyConfiguration()
    {
        // 应用格式化器
        _formatter = Options.LogFormat switch
        {
            LogFormat.Json => new JsonLogFormatter(),
            LogFormat.Structured => new StructuredLogFormatter(),
            _ => new TextLogFormatter()
        };

        // 同步到文件助手
        if (Options.EnableFileOutput)
        {
            LogFileHelper.SetMinimumLevel(Options.MinimumLevel);
            LogFileHelper.SetLogDirectory(Options.LogDirectory);
            LogFileHelper.SetMaxFileSize(Options.MaxFileSize);
            LogFileHelper.SetQueueCapacity(Options.QueueCapacity);
            LogFileHelper.SetBatchSize(Options.BatchSize);
            LogFileHelper.SetAsyncWriteEnabled(Options.EnableAsyncWrite);
            LogFileHelper.SetStatisticsEnabled(Options.EnableStatistics);
        }
    }

    /// <summary>
    /// 输出日志（同时输出到控制台和文件）
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="logLevel">日志等级</param>
    private static void WriteColorLine(string? message, LogLevel logLevel)
    {
        if (!IsEnabled(logLevel) || string.IsNullOrEmpty(message))
        {
            return;
        }

        // 记录统计
        if (Options.EnableStatistics)
        {
            Statistics.RecordLogWritten(logLevel);
        }

        // 输出到控制台
        if (Options.EnableConsoleOutput)
        {
            WriteToConsole(message, logLevel);
            if (Options.EnableStatistics)
            {
                Statistics.RecordConsoleWrite();
            }
        }

        // 输出到文件
        if (Options.EnableFileOutput)
        {
            WriteToFile(message, logLevel);
            if (Options.EnableStatistics)
            {
                Statistics.RecordFileWrite();
            }
        }
    }

    /// <summary>
    /// 输出到控制台
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="logLevel">日志等级</param>
    private static void WriteToConsole(string? message, LogLevel logLevel)
    {
        lock (LogLock)
        {
            try
            {
                // 根据格式化器类型决定输出方式
                if (_formatter is TextLogFormatter)
                {
                    // 文本格式使用彩色控制台输出
                    var frontColor = GetLogLevelColor(logLevel);
                    var logType = logLevel.ToString();
                    ConsoleColorWriter.WriteLog(message ?? "", logType, frontColor, Options.DisplayHeader);
                }
                else
                {
                    // JSON 或结构化格式直接输出格式化后的结果
                    var formattedMessage = _formatter.Format(DateTimeOffset.UtcNow, logLevel, message ?? string.Empty, null);
                    Console.WriteLine(formattedMessage);
                }
            }
            catch (Exception ex)
            {
                // 出现异常时使用备用输出方法
                Console.WriteLine($"日志输出异常: {ex.Message}");
                Console.WriteLine($"原始消息: {message}");
            }
        }
    }

    /// <summary>
    /// 输出到文件
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="logLevel">日志等级</param>
    private static void WriteToFile(string? message, LogLevel logLevel)
    {
        try
        {
            switch (logLevel)
            {
                case LogLevel.Info:
                    LogFileHelper.Info(message);
                    break;

                case LogLevel.Success:
                    LogFileHelper.Success(message);
                    break;

                case LogLevel.Handle:
                    LogFileHelper.Handle(message);
                    break;

                case LogLevel.Warn:
                    LogFileHelper.Warn(message);
                    break;

                case LogLevel.Error:
                    LogFileHelper.Error(message);
                    break;
            }
        }
        catch (Exception ex)
        {
            // 记录写入错误
            if (Options.EnableStatistics)
            {
                Statistics.RecordWriteError();
            }

            // 文件输出失败时，在控制台显示警告
            if (Options.EnableConsoleOutput)
            {
                Console.WriteLine($"[文件输出失败] {ex.Message}: {message}");
            }
        }
    }

    /// <summary>
    /// 在控制台输出彩虹渐变文本
    /// </summary>
    /// <param name="message">消息内容</param>
    private static void WriteColorLineRainbow(string? message)
    {
        if (string.IsNullOrEmpty(message) || !Options.EnableConsoleOutput)
        {
            return;
        }

        lock (LogLock)
        {
            try
            {
                ConsoleColorWriter.WriteColoredRainbowMessage(message);
            }
            catch (Exception ex)
            {
                // 出现异常时使用普通输出
                Console.WriteLine($"彩虹输出异常: {ex.Message}");
                Console.WriteLine($"原始消息: {message}");
            }
        }
    }

    #endregion 内部方法
}
