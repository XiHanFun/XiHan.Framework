#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogHelper
// Guid:824ca05d-f5be-49a9-96f9-8a6502e5b064
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-05-30 上午 12:12:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.ConsoleTools;

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 简单日志输出类
/// </summary>
public static class LogHelper
{
    private static readonly Lock ObjLock = new();
    private static bool _isDisplayHeader = true;
    private static volatile LogLevel _minimumLevel = LogLevel.Info;

    /// <summary>
    /// 设置最小日志等级（小于该等级的日志将被忽略）
    /// </summary>
    /// <param name="level">日志等级</param>
    public static void SetMinimumLevel(LogLevel level)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentException($"无效的日志等级: {level}", nameof(level));
        }

        _minimumLevel = level;
    }

    /// <summary>
    /// 设置是否显示日志头，默认显示
    /// </summary>
    /// <param name="isDisplayHeader">是否显示头部信息</param>
    public static void SetIsDisplayHeader(bool isDisplayHeader)
    {
        _isDisplayHeader = isDisplayHeader;
    }

    /// <summary>
    /// 获取当前最小日志等级
    /// </summary>
    /// <returns>当前最小日志等级</returns>
    public static LogLevel GetMinimumLevel()
    {
        return _minimumLevel;
    }

    /// <summary>
    /// 获取是否显示日志头的设置
    /// </summary>
    /// <returns>是否显示日志头</returns>
    public static bool GetIsDisplayHeader()
    {
        return _isDisplayHeader;
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
        var message = table.ToString();
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
        var message = table.ToString();
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
        var message = table.ToString();
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
        var message = table.ToString();
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
    /// <param name="errorMessage">消息模板</param>
    /// <param name="ex">异常</param>
    public static void Error(string? errorMessage, Exception ex)
    {
        var message = $"{errorMessage} {ex}";
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
        var message = table.ToString();
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
        lock (ObjLock)
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

    #region 内部方法

    /// <summary>
    /// 是否启用日志
    /// </summary>
    /// <param name="level"></param>
    /// <returns></returns>
    private static bool IsEnabled(LogLevel level)
    {
        return _minimumLevel != LogLevel.None && level <= _minimumLevel;
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
    /// 在控制台输出
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="logLevel">日志等级</param>
    private static void WriteColorLine(string? message, LogLevel logLevel)
    {
        if (!IsEnabled(logLevel) || string.IsNullOrEmpty(message))
        {
            return;
        }

        lock (ObjLock)
        {
            try
            {
                var frontColor = GetLogLevelColor(logLevel);
                var logType = logLevel.ToString();
                ConsoleColorWriter.WriteLog(message, logType, frontColor, _isDisplayHeader);
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
    /// 在控制台输出彩虹渐变文本
    /// </summary>
    /// <param name="message">消息内容</param>
    private static void WriteColorLineRainbow(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        lock (ObjLock)
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
