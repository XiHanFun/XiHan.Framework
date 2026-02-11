#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PerformanceStatistics
// Guid:a3158a28-d343-4784-8300-09c9fd3cb253
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 06:20:22
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Extensions;

/// <summary>
/// 性能统计信息
/// </summary>
public class PerformanceStatistics
{
    /// <summary>
    /// 总执行次数
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// 总耗时(毫秒)
    /// </summary>
    public long TotalTimeMs { get; set; }

    /// <summary>
    /// 内存使用量(字节)
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// 平均执行时间(毫秒)
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// 最小执行时间(毫秒)
    /// </summary>
    public long MinExecutionTimeMs { get; set; }

    /// <summary>
    /// 最大执行时间(毫秒)
    /// </summary>
    public long MaxExecutionTimeMs { get; set; }

    /// <summary>
    /// 缓存命中次数
    /// </summary>
    public int CacheHitCount { get; set; }

    /// <summary>
    /// 平均编译时间(毫秒)
    /// </summary>
    public double AverageCompilationTimeMs { get; set; }

    /// <summary>
    /// 每秒执行次数
    /// </summary>
    public double ExecutionsPerSecond => TotalIterations / (TotalTimeMs / 1000.0);

    /// <summary>
    /// 成功率(百分比)
    /// </summary>
    public double SuccessRate => TotalIterations == 0 ? 0 : (double)SuccessCount / TotalIterations * 100;

    /// <summary>
    /// 缓存命中率(百分比)
    /// </summary>
    public double CacheHitRate => TotalIterations == 0 ? 0 : (double)CacheHitCount / TotalIterations * 100;
}
