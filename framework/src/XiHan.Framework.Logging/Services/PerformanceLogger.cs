#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PerformanceLogger
// Guid:fa9e2dc6-041b-47c5-b28b-a738b6ce3191
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 12:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// 性能日志器实现
/// </summary>
public class PerformanceLogger : IPerformanceLogger
{
    private readonly ILogger<PerformanceLogger> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志器</param>
    public PerformanceLogger(ILogger<PerformanceLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 记录操作性能
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="duration">持续时间</param>
    /// <param name="additionalData">附加数据</param>
    public void LogOperation(string operationName, TimeSpan duration, object? additionalData = null)
    {
        _logger.LogInformation("Operation {OperationName} completed in {Duration}ms. Data: {@AdditionalData}",
            operationName, duration.TotalMilliseconds, additionalData);
    }

    /// <summary>
    /// 记录 API 调用性能
    /// </summary>
    /// <param name="apiName">API 名称</param>
    /// <param name="duration">持续时间</param>
    /// <param name="statusCode">状态码</param>
    /// <param name="additionalData">附加数据</param>
    public void LogApiCall(string apiName, TimeSpan duration, int statusCode, object? additionalData = null)
    {
        _logger.LogInformation("API {ApiName} responded with {StatusCode} in {Duration}ms. Data: {@AdditionalData}",
            apiName, statusCode, duration.TotalMilliseconds, additionalData);
    }

    /// <summary>
    /// 记录数据库查询性能
    /// </summary>
    /// <param name="queryName">查询名称</param>
    /// <param name="duration">持续时间</param>
    /// <param name="recordCount">记录数量</param>
    /// <param name="additionalData">附加数据</param>
    public void LogDatabaseQuery(string queryName, TimeSpan duration, int recordCount, object? additionalData = null)
    {
        _logger.LogInformation("Database query {QueryName} returned {RecordCount} records in {Duration}ms. Data: {@AdditionalData}",
            queryName, recordCount, duration.TotalMilliseconds, additionalData);
    }

    /// <summary>
    /// 开始性能计时
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <returns>性能计时器</returns>
    public IPerformanceTimer StartTimer(string operationName)
    {
        return new PerformanceTimer(operationName, this);
    }

    /// <summary>
    /// 记录内存使用情况
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="memoryBefore">操作前内存</param>
    /// <param name="memoryAfter">操作后内存</param>
    public void LogMemoryUsage(string operationName, long memoryBefore, long memoryAfter)
    {
        var memoryDiff = memoryAfter - memoryBefore;
        _logger.LogInformation("Operation {OperationName} memory usage: Before={MemoryBefore}KB, After={MemoryAfter}KB, Diff={MemoryDiff}KB",
            operationName, memoryBefore / 1024, memoryAfter / 1024, memoryDiff / 1024);
    }

    /// <summary>
    /// 记录 CPU 使用情况
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="cpuUsage">CPU 使用率</param>
    /// <param name="duration">持续时间</param>
    public void LogCpuUsage(string operationName, double cpuUsage, TimeSpan duration)
    {
        _logger.LogInformation("Operation {OperationName} CPU usage: {CpuUsage}% over {Duration}ms",
            operationName, cpuUsage, duration.TotalMilliseconds);
    }
}

/// <summary>
/// 性能计时器实现
/// </summary>
internal class PerformanceTimer : IPerformanceTimer
{
    private readonly string _operationName;
    private readonly PerformanceLogger _performanceLogger;
    private bool _disposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="performanceLogger">性能日志器</param>
    public PerformanceTimer(string operationName, PerformanceLogger performanceLogger)
    {
        _operationName = operationName;
        _performanceLogger = performanceLogger;
        Stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// 操作名称
    /// </summary>
    public string OperationName => _operationName;

    /// <summary>
    /// 秒表
    /// </summary>
    public Stopwatch Stopwatch { get; }

    /// <summary>
    /// 附加数据
    /// </summary>
    public object? AdditionalData { get; set; }

    /// <summary>
    /// 停止计时并记录
    /// </summary>
    public void Stop()
    {
        if (!_disposed)
        {
            Stopwatch.Stop();
            _performanceLogger.LogOperation(_operationName, Stopwatch.Elapsed, AdditionalData);
            _disposed = true;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Stop();
    }
}
