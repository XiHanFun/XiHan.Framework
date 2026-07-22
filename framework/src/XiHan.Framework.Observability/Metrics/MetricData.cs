// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Observability.Metrics;

/// <summary>
/// 指标数据
/// </summary>
public class MetricData
{
    /// <summary>
    /// 指标名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 指标类型
    /// </summary>
    public MetricType Type { get; set; }

    /// <summary>
    /// 指标值
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 单位
    /// </summary>
    public string? Unit { get; set; }
}

/// <summary>
/// 指标类型
/// </summary>
public enum MetricType
{
    /// <summary>
    /// 计数器
    /// </summary>
    Counter,

    /// <summary>
    /// 测量值
    /// </summary>
    Gauge,

    /// <summary>
    /// 直方图
    /// </summary>
    Histogram,

    /// <summary>
    /// 摘要
    /// </summary>
    Summary
}
