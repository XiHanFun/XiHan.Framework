#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IPerformanceLogger
// Guid:9071461d-825d-4634-a01f-c8b3ad89d6f1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:35:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// 性能日志器接口
/// </summary>
public interface IPerformanceLogger
{
    /// <summary>
    /// 记录操作性能
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="duration">持续时间</param>
    /// <param name="additionalData">附加数据</param>
    void LogOperation(string operationName, TimeSpan duration, object? additionalData = null);

    /// <summary>
    /// 记录 API 调用性能
    /// </summary>
    /// <param name="apiName">API 名称</param>
    /// <param name="duration">持续时间</param>
    /// <param name="statusCode">状态码</param>
    /// <param name="additionalData">附加数据</param>
    void LogApiCall(string apiName, TimeSpan duration, int statusCode, object? additionalData = null);

    /// <summary>
    /// 记录数据库查询性能
    /// </summary>
    /// <param name="queryName">查询名称</param>
    /// <param name="duration">持续时间</param>
    /// <param name="recordCount">记录数量</param>
    /// <param name="additionalData">附加数据</param>
    void LogDatabaseQuery(string queryName, TimeSpan duration, int recordCount, object? additionalData = null);

    /// <summary>
    /// 开始性能计时
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <returns>性能计时器</returns>
    IPerformanceTimer StartTimer(string operationName);

    /// <summary>
    /// 记录内存使用情况
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="memoryBefore">操作前内存</param>
    /// <param name="memoryAfter">操作后内存</param>
    void LogMemoryUsage(string operationName, long memoryBefore, long memoryAfter);

    /// <summary>
    /// 记录 CPU 使用情况
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="cpuUsage">CPU 使用率</param>
    /// <param name="duration">持续时间</param>
    void LogCpuUsage(string operationName, double cpuUsage, TimeSpan duration);
}

/// <summary>
/// 性能计时器接口
/// </summary>
public interface IPerformanceTimer : IDisposable
{
    /// <summary>
    /// 操作名称
    /// </summary>
    string OperationName { get; }

    /// <summary>
    /// 秒表
    /// </summary>
    Stopwatch Stopwatch { get; }

    /// <summary>
    /// 附加数据
    /// </summary>
    object? AdditionalData { get; set; }

    /// <summary>
    /// 停止计时并记录
    /// </summary>
    void Stop();
}
