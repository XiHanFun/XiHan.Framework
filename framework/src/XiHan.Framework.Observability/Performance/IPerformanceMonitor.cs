// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Observability.Performance;

/// <summary>
/// 性能监控接口
/// </summary>
public interface IPerformanceMonitor
{
    /// <summary>
    /// 开始监控操作
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <returns>性能追踪器</returns>
    IPerformanceTracker BeginOperation(string operationName);

    /// <summary>
    /// 获取性能统计
    /// </summary>
    /// <returns>性能统计信息</returns>
    PerformanceStatistics GetStatistics();

    /// <summary>
    /// 获取慢操作列表
    /// </summary>
    /// <param name="thresholdMs">阈值（毫秒）</param>
    /// <returns>慢操作列表</returns>
    IReadOnlyList<PerformanceRecord> GetSlowOperations(double thresholdMs = 1000);

    /// <summary>
    /// 清空性能记录
    /// </summary>
    void Clear();
}

/// <summary>
/// 性能追踪器接口
/// </summary>
public interface IPerformanceTracker : IDisposable
{
    /// <summary>
    /// 操作名称
    /// </summary>
    string OperationName { get; }

    /// <summary>
    /// 添加标签
    /// </summary>
    /// <param name="key">标签键</param>
    /// <param name="value">标签值</param>
    void AddTag(string key, string value);

    /// <summary>
    /// 记录检查点
    /// </summary>
    /// <param name="checkpointName">检查点名称</param>
    void Checkpoint(string checkpointName);
}
