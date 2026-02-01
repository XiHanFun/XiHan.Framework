#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptPerformanceInfo
// Guid:0b9c7d59-c532-4007-a400-373d1ba1195b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/01 11:06:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
