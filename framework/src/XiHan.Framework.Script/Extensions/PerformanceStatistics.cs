// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Extensions;

/// <summary>
/// 性能统计信息
/// </summary>
public class PerformanceStatistics
{
    private TimeSpan _totalElapsedTime;

    /// <summary>
    /// 总执行次数
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// 总耗时
    /// </summary>
    /// <remarks>
    /// 只覆盖正式执行，不含预热及预热后的强制回收。保留单调时钟的完整精度，
    /// <see cref="TotalTimeMs"/> 与 <see cref="ExecutionsPerSecond"/> 都由它派生。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">赋值为负时长时抛出</exception>
    public TimeSpan TotalElapsedTime
    {
        get => _totalElapsedTime;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            _totalElapsedTime = value;
        }
    }

    /// <summary>
    /// 总耗时(毫秒)
    /// </summary>
    /// <remarks>
    /// 由 <see cref="TotalElapsedTime"/> 截断到整毫秒，亚毫秒级的统计读数为 0；需要完整精度时读取 <see cref="TotalElapsedTime"/>。
    /// </remarks>
    public long TotalTimeMs => (long)TotalElapsedTime.TotalMilliseconds;

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
    /// <remarks>
    /// 按 <see cref="TotalElapsedTime"/> 的完整精度计算，亚毫秒级的统计也能得到有限值。
    /// 统计耗时为零时速率无从计算，返回 <see langword="null"/>，不以 0 或无穷大代替。
    /// </remarks>
    public double? ExecutionsPerSecond => TotalElapsedTime > TimeSpan.Zero ? TotalIterations / TotalElapsedTime.TotalSeconds : null;

    /// <summary>
    /// 成功率(百分比)
    /// </summary>
    public double SuccessRate => TotalIterations == 0 ? 0 : (double)SuccessCount / TotalIterations * 100;

    /// <summary>
    /// 缓存命中率(百分比)
    /// </summary>
    public double CacheHitRate => TotalIterations == 0 ? 0 : (double)CacheHitCount / TotalIterations * 100;
}
