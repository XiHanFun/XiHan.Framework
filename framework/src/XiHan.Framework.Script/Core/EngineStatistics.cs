#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EngineStatistics
// Guid:c3bf7b6b-6e94-4fc4-a61d-29aa7c2ff931
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 06:12:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Core;

/// <summary>
/// 引擎统计信息
/// </summary>
public class EngineStatistics
{
    /// <summary>
    /// 总执行次数
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// 成功执行次数
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// 失败执行次数
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// 缓存命中次数
    /// </summary>
    public long CacheHits { get; set; }

    /// <summary>
    /// 缓存未命中次数
    /// </summary>
    public long CacheMisses { get; set; }

    /// <summary>
    /// 平均执行时间(毫秒)
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// 平均编译时间(毫秒)
    /// </summary>
    public double AverageCompilationTimeMs { get; set; }

    /// <summary>
    /// 缓存大小
    /// </summary>
    public int CacheSize { get; set; }

    /// <summary>
    /// 总内存使用量(字节)
    /// </summary>
    public long TotalMemoryUsage { get; set; }

    /// <summary>
    /// 引擎启动时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate => TotalExecutions == 0 ? 0 : (double)SuccessfulExecutions / TotalExecutions * 100;

    /// <summary>
    /// 缓存命中率
    /// </summary>
    public double CacheHitRate => CacheHits + CacheMisses == 0 ? 0 : (double)CacheHits / (CacheHits + CacheMisses) * 100;

    /// <summary>
    /// 运行时间
    /// </summary>
    public TimeSpan Uptime => DateTime.Now - StartTime;
}
