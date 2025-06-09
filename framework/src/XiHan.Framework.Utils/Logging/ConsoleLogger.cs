#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleLogger
// Guid:824ca05d-f5be-49a9-96f9-8a6502e5b064
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-05-30 上午 12:12:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 简单的控制台日志输出类
/// </summary>
public static class ConsoleLogger
{
    private static readonly Lock ObjLock = new();
    private static bool _isWriteToFile;

    /// <summary>
    /// 设置日志文件目录
    /// </summary>
    /// <param name="isWriteToFile">是否写入文件</param>
    public static void SetIsWriteToFile(bool isWriteToFile)
    {
        _isWriteToFile = isWriteToFile;
    }

    /// <summary>
    /// 正常信息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="frontColor">前景色</param>
    public static void Info(string? message, ConsoleColor frontColor = ConsoleColor.White)
    {
        WriteColorLine(message, frontColor);
        if (!_isWriteToFile)
        {
            return;
        }
        FileLogger.Info(message);
    }

    /// <summary>
    /// 正常信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Info(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Info(formattedMessage);
    }

    /// <summary>
    /// 成功信息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="frontColor">前景色</param>
    public static void Success(string? message, ConsoleColor frontColor = ConsoleColor.Green)
    {
        WriteColorLine(message, frontColor);
        if (!_isWriteToFile)
        {
            return;
        }
        FileLogger.Success(message);
    }

    /// <summary>
    /// 成功信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Success(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Success(formattedMessage);
    }

    /// <summary>
    /// 处理、查询信息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="frontColor">前景色</param>
    public static void Handle(string? message, ConsoleColor frontColor = ConsoleColor.Blue)
    {
        WriteColorLine(message, frontColor);
        if (!_isWriteToFile)
        {
            return;
        }
        FileLogger.Handle(message);
    }

    /// <summary>
    /// 处理、查询信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Handle(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Handle(formattedMessage);
    }

    /// <summary>
    /// 警告、新增、更新信息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="frontColor">前景色</param>
    public static void Warn(string? message, ConsoleColor frontColor = ConsoleColor.Yellow)
    {
        WriteColorLine(message, frontColor);
        if (!_isWriteToFile)
        {
            return;
        }
        FileLogger.Warn(message);
    }

    /// <summary>
    /// 警告、新增、更新信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Warn(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Warn(formattedMessage);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="frontColor">前景色</param>
    public static void Error(string? message, ConsoleColor frontColor = ConsoleColor.Red)
    {
        WriteColorLine(message, frontColor);
        if (!_isWriteToFile)
        {
            return;
        }
        FileLogger.Error(message);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Error(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Error(formattedMessage);
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
            return message;

        try
        {
            // 如果没有参数，直接返回原消息
            if (args == null || args.Length == 0)
                return message;

            // 使用 string.Format 进行格式化
            return string.Format(message, args);
        }
        catch (FormatException)
        {
            // 如果格式化失败，返回原消息并附加参数信息
            var argStr = string.Join(", ", args?.Select(arg => arg?.ToString() ?? "null") ?? Array.Empty<string>());
            return $"{message} [Args: {argStr}]";
        }
        catch
        {
            // 其他异常情况，返回原消息
            return message;
        }
    }

    /// <summary>
    /// 在控制台输出
    /// </summary>
    /// <param name="inputStr">打印文本</param>
    /// <param name="frontColor">前置颜色</param>
    private static void WriteColorLine(string? inputStr, ConsoleColor frontColor)
    {
        lock (ObjLock)
        {
            var currentForeColor = Console.ForegroundColor;
            Console.ForegroundColor = frontColor;
            Console.WriteLine(inputStr);
            Console.ForegroundColor = currentForeColor;
        }
    }
}
