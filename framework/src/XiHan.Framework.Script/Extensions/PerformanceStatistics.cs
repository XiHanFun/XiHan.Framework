// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// <remarks>只覆盖正式执行，不含预热及预热后的强制回收。</remarks>
    public long TotalTimeMs { get; set; }

    /// <summary>
    /// 正式执行期间分配的字节数
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Core.MemoryUsage.AllocatedBytes"/> 同一口径：取 <see cref="GC.GetTotalAllocatedBytes(bool)"/>
    /// 在正式执行前后的差值，不含预热。执行期间发生回收不影响读数，恒为非负；
    /// 它反映的是分配了多少，而不是执行结束后还占用多少。
    /// 该值为进程级计数，统计期间其他线程的分配也会计入。
    /// </remarks>
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
