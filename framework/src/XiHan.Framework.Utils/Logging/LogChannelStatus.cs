#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogChannelStatus
// Guid:1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 日志通道状态信息
/// </summary>
public class LogChannelStatus
{
    /// <summary>
    /// 通道中待处理的日志数量
    /// </summary>
    public int ChannelCount { get; init; }

    /// <summary>
    /// 工作任务是否活跃
    /// </summary>
    public bool IsWorkerActive { get; init; }

    /// <summary>
    /// 是否已关闭
    /// </summary>
    public bool IsShutdown { get; init; }

    /// <summary>
    /// 队列容量
    /// </summary>
    public int QueueCapacity { get; init; }

    /// <summary>
    /// 批处理大小
    /// </summary>
    public int BatchSize { get; init; }

    /// <summary>
    /// 通道使用率 (0-1)
    /// </summary>
    public double UsageRate => QueueCapacity > 0 ? (double)ChannelCount / QueueCapacity : 0;

    /// <summary>
    /// 是否接近满载 (>= 80%)
    /// </summary>
    public bool IsNearFull => UsageRate >= 0.8;

    /// <summary>
    /// 上次清理时间
    /// </summary>
    public DateTimeOffset LastCleanupTime { get; init; }

    /// <summary>
    /// 日志保留天数（0表示永久保留）
    /// </summary>
    public int RetentionDays { get; init; }

    /// <summary>
    /// 是否启用自动清理
    /// </summary>
    public bool IsAutoCleanupEnabled => RetentionDays > 0;

    /// <summary>
    /// 获取状态摘要
    /// </summary>
    /// <returns>状态摘要字符串</returns>
    public string GetSummary()
    {
        var status = IsShutdown ? "已关闭" : IsNearFull ? "接近满载" : "正常";
        var cleanup = IsAutoCleanupEnabled
            ? $"自动清理: 启用({RetentionDays}天), 上次: {LastCleanupTime:yyyy-MM-dd HH:mm}"
            : "自动清理: 禁用";

        return $"通道状态 - 待处理: {ChannelCount}/{QueueCapacity} ({UsageRate:P0}), " +
               $"工作任务: {(IsWorkerActive ? "活跃" : "停止")}, " +
               $"批次大小: {BatchSize}, " +
               $"状态: {status}, {cleanup}";
    }
}
