// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Observability.Metrics;

/// <summary>
/// 指标收集器接口
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    /// 记录计数器
    /// </summary>
    /// <param name="name">指标名称</param>
    /// <param name="value">计数值</param>
    /// <param name="tags">标签</param>
    void RecordCounter(string name, long value = 1, Dictionary<string, string>? tags = null);

    /// <summary>
    /// 记录测量值
    /// </summary>
    /// <param name="name">指标名称</param>
    /// <param name="value">测量值</param>
    /// <param name="tags">标签</param>
    void RecordMeasurement(string name, double value, Dictionary<string, string>? tags = null);

    /// <summary>
    /// 记录直方图
    /// </summary>
    /// <param name="name">指标名称</param>
    /// <param name="value">值</param>
    /// <param name="tags">标签</param>
    void RecordHistogram(string name, double value, Dictionary<string, string>? tags = null);

    /// <summary>
    /// 开始计时
    /// </summary>
    /// <param name="name">指标名称</param>
    /// <param name="tags">标签</param>
    /// <returns>计时器</returns>
    IDisposable BeginTimer(string name, Dictionary<string, string>? tags = null);

    /// <summary>
    /// 获取所有指标
    /// </summary>
    /// <returns>指标列表</returns>
    IReadOnlyList<MetricData> GetMetrics();

    /// <summary>
    /// 清空指标
    /// </summary>
    void Clear();
}
