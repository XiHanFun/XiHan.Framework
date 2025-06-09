#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileLogger
// Guid:24e38f75-3d64-4a8c-b21e-cdf5b0ba1ed3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-05-30 上午 12:12:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 简单的文件日志输出类
/// </summary>
public static class FileLogger
{
    private static readonly Lock ObjLock = new();
    private static readonly Dictionary<string, int> LogFileCounter = [];
    private static string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

    /// <summary>
    /// 设置日志文件目录
    /// </summary>
    /// <param name="directoryPath">日志文件目录路径</param>
    public static void SetLogDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        _logDirectory = directoryPath;
    }

    /// <summary>
    /// 正常信息
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    /// <param name="fileName">日志文件名(不含扩展名)</param>
    public static void Info(string? inputStr, string? fileName = "info")
    {
        WriteToFile(inputStr, "INFO", fileName);
    }

    /// <summary>
    /// 成功信息
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    /// <param name="fileName">日志文件名(不含扩展名)</param>
    public static void Success(string? inputStr, string? fileName = "success")
    {
        WriteToFile(inputStr, "SUCCESS", fileName);
    }

    /// <summary>
    /// 处理、查询信息
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    /// <param name="fileName">日志文件名(不含扩展名)</param>
    public static void Handle(string? inputStr, string? fileName = "handle")
    {
        WriteToFile(inputStr, "HANDLE", fileName);
    }

    /// <summary>
    /// 警告、新增、更新信息
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    /// <param name="fileName">日志文件名(不含扩展名)</param>
    public static void Warn(string? inputStr, string? fileName = "warn")
    {
        WriteToFile(inputStr, "WARN", fileName);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    /// <param name="fileName">日志文件名(不含扩展名)</param>
    public static void Error(string? inputStr, string? fileName = "error")
    {
        WriteToFile(inputStr, "ERROR", fileName);
    }

    /// <summary>
    /// 写入文件
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    /// <param name="logType">日志类型</param>
    /// <param name="fileName">日志文件名(不含扩展名)</param>
    private static void WriteToFile(string? inputStr, string logType, string? fileName = null)
    {
        if (inputStr == null)
        {
            return;
        }

        lock (ObjLock)
        {
            try
            {
                // 确保日志目录存在
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                // 确定日志文件名
                var logDate = DateTime.Now.ToString("yyyy-MM-dd");
                var logFileName = string.IsNullOrWhiteSpace(fileName)
                    ? $"{logDate}.log"
                    : $"{logDate}_{fileName}.log";

                var logFilePath = Path.Combine(_logDirectory, logFileName);

                // 检查文件大小，如果超过10MB，则创建新文件
                if (File.Exists(logFilePath))
                {
                    var fileInfo = new FileInfo(logFilePath);
                    if (fileInfo.Length > 10 * 1024 * 1024) // 10MB
                    {
                        // 获取或初始化计数器
                        if (!LogFileCounter.TryGetValue(logFileName, out var counter))
                        {
                            counter = 0;
                        }

                        counter++;
                        LogFileCounter[logFileName] = counter;

                        // 创建新的日志文件名，加上序号
                        logFileName = string.IsNullOrWhiteSpace(fileName)
                            ? $"{logDate}_{counter}.log"
                            : $"{logDate}_{fileName}_{counter}.log";

                        logFilePath = Path.Combine(_logDirectory, logFileName);
                    }
                }

                // 格式化日志内容
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logLine = $"[{timestamp}] [{logType}] {inputStr}{Environment.NewLine}";

                // 写入日志文件
                File.AppendAllText(logFilePath, logLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 如果写日志出错，尝试写入备用错误日志
                try
                {
                    var errorLogPath = Path.Combine(_logDirectory, "error_logger.log");
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var errorMessage = $"[{timestamp}] [LOGGER_ERROR] 日志写入失败: {ex.Message}{Environment.NewLine}";
                    File.AppendAllText(errorLogPath, errorMessage, Encoding.UTF8);
                }
                catch
                {
                    // 忽略备用日志记录失败
                }
            }
        }
    }
}
