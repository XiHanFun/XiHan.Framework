// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Monitoring;

/// <summary>
/// 脚本执行统计信息
/// </summary>
public class ScriptExecutionStatistics
{
    /// <summary>
    /// 总执行次数
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// 成功执行次数
    /// </summary>
    public int SuccessfulExecutions { get; set; }

    /// <summary>
    /// 失败执行次数
    /// </summary>
    public int FailedExecutions { get; set; }

    /// <summary>
    /// 平均执行时间(毫秒)
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// 平均编译时间(毫秒)
    /// </summary>
    public double AverageCompilationTimeMs { get; set; }

    /// <summary>
    /// 总内存使用量(字节)
    /// </summary>
    public long TotalMemoryUsageBytes { get; set; }

    /// <summary>
    /// 缓存命中率(百分比)
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// 最近一小时执行次数
    /// </summary>
    public int ExecutionsLastHour { get; set; }

    /// <summary>
    /// 平均每分钟执行次数
    /// </summary>
    public double AverageExecutionsPerMinute { get; set; }

    /// <summary>
    /// 最常见错误
    /// </summary>
    public Dictionary<string, int> TopErrors { get; set; } = [];

    /// <summary>
    /// 最慢脚本
    /// </summary>
    public IEnumerable<ScriptExecutionLog> SlowScripts { get; set; } = [];

    /// <summary>
    /// 成功率(百分比)
    /// </summary>
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;
}
