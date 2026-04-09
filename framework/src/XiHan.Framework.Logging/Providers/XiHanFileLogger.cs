#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFileLogger
// Guid:7424c43d-e80d-4c6d-a4bb-9fc52ee02108
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 12:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Text;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Providers;

/// <summary>
/// XiHan 文件日志器
/// </summary>
internal class XiHanFileLogger : ILogger
{
    private static readonly Lock FileWriteLock = new();
    private readonly string _categoryName;
    private readonly XiHanFileLoggerOptions _options;
    private readonly IExternalScopeProvider _scopeProvider;
    private readonly Encoding _encoding;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="categoryName">分类名称</param>
    /// <param name="options">文件日志选项</param>
    /// <param name="scopeProvider">作用域提供器</param>
    public XiHanFileLogger(string categoryName, XiHanFileLoggerOptions options, IExternalScopeProvider scopeProvider)
    {
        _categoryName = categoryName;
        _options = options;
        _scopeProvider = scopeProvider;
        _encoding = ResolveEncoding(options.Encoding);
    }

    /// <summary>
    /// 开始日志作用域
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns></returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _scopeProvider.Push(state);
    }

    /// <summary>
    /// 检查是否启用指定日志级别
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns></returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _options.MinLevel;
    }

    /// <summary>
    /// 记录日志
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="logLevel">日志级别</param>
    /// <param name="eventId">事件唯一标识</param>
    /// <param name="state">状态</param>
    /// <param name="exception">异常</param>
    /// <param name="formatter">格式化器</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var scopeText = XiHanLogEntryFormatter.BuildScopeText(_scopeProvider, _options.IncludeScopes);
        var logEntry = XiHanLogEntryFormatter.Format(
            _options.LogFormat,
            DateTimeOffset.Now,
            logLevel,
            _categoryName,
            message,
            exception,
            scopeText);

        try
        {
            var filePath = ResolveFilePath(DateTimeOffset.Now);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (FileWriteLock)
            {
                EnsureFileSizeLimit(filePath);
                File.AppendAllText(filePath, logEntry + Environment.NewLine, _encoding);
            }
        }
        catch
        {
            // 忽略文件写入错误
        }
    }

    private string ResolveFilePath(DateTimeOffset now)
    {
        var filePath = string.IsNullOrWhiteSpace(_options.FilePath)
            ? "logs/xihan.log"
            : _options.FilePath.Trim();

        if (filePath.Contains("{Date}", StringComparison.OrdinalIgnoreCase))
        {
            return filePath.Replace("{Date}", now.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase);
        }

        // 兼容 logs/xihan-.log 这种按天切分命名约定
        var fileName = Path.GetFileName(filePath);
        if (fileName.Contains("-.", StringComparison.Ordinal))
        {
            var datedFileName = fileName.Replace("-.", $"-{now:yyyyMMdd}.", StringComparison.Ordinal);
            var directory = Path.GetDirectoryName(filePath);
            return string.IsNullOrEmpty(directory)
                ? datedFileName
                : Path.Combine(directory, datedFileName);
        }

        return filePath;
    }

    private void EnsureFileSizeLimit(string filePath)
    {
        if (_options.FileSizeLimit <= 0 || !File.Exists(filePath))
        {
            return;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < _options.FileSizeLimit)
        {
            return;
        }

        var archivePath = BuildArchivePath(filePath);
        File.Move(filePath, archivePath, overwrite: true);
        CleanupArchivedFiles(filePath);
    }

    private string BuildArchivePath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var suffix = DateTimeOffset.Now.ToString("yyyyMMddHHmmssfff");
        var archiveFileName = $"{fileNameWithoutExtension}.{suffix}{extension}";
        return Path.Combine(directory, archiveFileName);
    }

    private void CleanupArchivedFiles(string filePath)
    {
        if (_options.RetainedFileCountLimit <= 0)
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var searchPattern = $"{fileNameWithoutExtension}.*{extension}";

        var archives = Directory
            .EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        foreach (var archive in archives.Skip(_options.RetainedFileCountLimit))
        {
            try
            {
                archive.Delete();
            }
            catch
            {
                // 忽略归档清理错误
            }
        }
    }

    private static Encoding ResolveEncoding(string encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        try
        {
            return Encoding.GetEncoding(encodingName);
        }
        catch (ArgumentException)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
    }
}
