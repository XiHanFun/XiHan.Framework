// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Monitoring;

/// <summary>
/// 脚本性能信息
/// </summary>
public class ScriptPerformanceInfo
{
    /// <summary>
    /// 缓存键
    /// </summary>
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>
    /// 执行次数
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// 总执行时间(毫秒)
    /// </summary>
    public long TotalExecutionTimeMs { get; set; }

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
    /// 最后执行时间
    /// </summary>
    public DateTime LastExecutionTime { get; set; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate => ExecutionCount > 0 ? (double)SuccessCount / ExecutionCount * 100 : 0;
}
