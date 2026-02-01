#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxyStatistics
// Guid:6b14a727-644d-4dc8-9733-8cd7cbeb8df0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Http.Models;

/// <summary>
/// 代理统计信息
/// </summary>
public class ProxyStatistics
{
    /// <summary>
    /// 代理地址
    /// </summary>
    public string ProxyAddress { get; set; } = string.Empty;

    /// <summary>
    /// 总请求次数
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// 当前并发连接数
    /// </summary>
    public int CurrentConnections { get; set; }

    /// <summary>
    /// 平均响应时间(毫秒)
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// 最后成功时间
    /// </summary>
    public DateTime? LastSuccessAt { get; set; }

    /// <summary>
    /// 最后失败时间
    /// </summary>
    public DateTime? LastFailureAt { get; set; }

    /// <summary>
    /// 最后验证时间
    /// </summary>
    public DateTime? LastValidatedAt { get; set; }

    /// <summary>
    /// 是否可用
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// 连续失败次数
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate => TotalRequests > 0 ? (double)SuccessCount / TotalRequests : 0;

    /// <summary>
    /// 记录请求
    /// </summary>
    /// <param name="success">是否成功</param>
    /// <param name="responseTime">响应时间</param>
    public void RecordRequest(bool success, long responseTime)
    {
        TotalRequests++;
        LastUsedAt = DateTime.UtcNow;

        if (success)
        {
            SuccessCount++;
            LastSuccessAt = DateTime.UtcNow;
            ConsecutiveFailures = 0;

            // 更新平均响应时间
            AverageResponseTime = ((AverageResponseTime * (SuccessCount - 1)) + responseTime) / SuccessCount;
        }
        else
        {
            FailureCount++;
            LastFailureAt = DateTime.UtcNow;
            ConsecutiveFailures++;
        }
    }

    /// <summary>
    /// 记录验证结果
    /// </summary>
    /// <param name="isAvailable">是否可用</param>
    public void RecordValidation(bool isAvailable)
    {
        IsAvailable = isAvailable;
        LastValidatedAt = DateTime.UtcNow;

        if (!isAvailable)
        {
            ConsecutiveFailures++;
        }
        else
        {
            ConsecutiveFailures = 0;
        }
    }

    /// <summary>
    /// 重置统计
    /// </summary>
    public void Reset()
    {
        TotalRequests = 0;
        SuccessCount = 0;
        FailureCount = 0;
        AverageResponseTime = 0;
        ConsecutiveFailures = 0;
    }
}
