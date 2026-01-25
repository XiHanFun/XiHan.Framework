#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PerformanceModels
// Guid:c8d9e0f1-a2b3-44c5-d6e7-f8a9b0c1d2e3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/26 4:03:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Observability.Performance;

/// <summary>
/// 性能记录
/// </summary>
public class PerformanceRecord
{
    /// <summary>
    /// 操作名称
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// 持续时间（毫秒）
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// 检查点
    /// </summary>
    public List<Checkpoint> Checkpoints { get; set; } = new();

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// 异常信息
    /// </summary>
    public string? Exception { get; set; }
}

/// <summary>
/// 检查点
/// </summary>
public class Checkpoint
{
    /// <summary>
    /// 检查点名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// 相对时间（毫秒）
    /// </summary>
    public double ElapsedMs { get; set; }
}

/// <summary>
/// 性能统计
/// </summary>
public class PerformanceStatistics
{
    /// <summary>
    /// 总操作数
    /// </summary>
    public int TotalOperations { get; set; }

    /// <summary>
    /// 成功操作数
    /// </summary>
    public int SuccessfulOperations { get; set; }

    /// <summary>
    /// 失败操作数
    /// </summary>
    public int FailedOperations { get; set; }

    /// <summary>
    /// 平均持续时间（毫秒）
    /// </summary>
    public double AverageDurationMs { get; set; }

    /// <summary>
    /// 最小持续时间（毫秒）
    /// </summary>
    public double MinDurationMs { get; set; }

    /// <summary>
    /// 最大持续时间（毫秒）
    /// </summary>
    public double MaxDurationMs { get; set; }

    /// <summary>
    /// P50 持续时间（毫秒）
    /// </summary>
    public double P50DurationMs { get; set; }

    /// <summary>
    /// P95 持续时间（毫秒）
    /// </summary>
    public double P95DurationMs { get; set; }

    /// <summary>
    /// P99 持续时间（毫秒）
    /// </summary>
    public double P99DurationMs { get; set; }

    /// <summary>
    /// 操作统计
    /// </summary>
    public Dictionary<string, OperationStatistics> OperationStats { get; set; } = new();
}

/// <summary>
/// 操作统计
/// </summary>
public class OperationStatistics
{
    /// <summary>
    /// 操作名称
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// 调用次数
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// 平均持续时间（毫秒）
    /// </summary>
    public double AverageDurationMs { get; set; }

    /// <summary>
    /// 最小持续时间（毫秒）
    /// </summary>
    public double MinDurationMs { get; set; }

    /// <summary>
    /// 最大持续时间（毫秒）
    /// </summary>
    public double MaxDurationMs { get; set; }
}
