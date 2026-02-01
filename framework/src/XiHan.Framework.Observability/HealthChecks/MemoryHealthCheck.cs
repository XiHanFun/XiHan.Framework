#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MemoryHealthCheck
// Guid:d3e4f5a6-b7c8-49d0-e1f2-a3b4c5d6e7f8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 04:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace XiHan.Framework.Observability.HealthChecks;

/// <summary>
/// 内存健康检查
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _thresholdBytes;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="thresholdMb">内存阈值（MB），默认1024MB</param>
    public MemoryHealthCheck(long thresholdMb = 1024)
    {
        _thresholdBytes = thresholdMb * 1024 * 1024;
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var allocated = GC.GetTotalMemory(false);
        var gcInfo = GC.GetGCMemoryInfo();

        var data = new Dictionary<string, object>
        {
            { "AllocatedBytes", allocated },
            { "AllocatedMB", allocated / 1024 / 1024 },
            { "Gen0Collections", GC.CollectionCount(0) },
            { "Gen1Collections", GC.CollectionCount(1) },
            { "Gen2Collections", GC.CollectionCount(2) },
            { "TotalAvailableMemoryBytes", gcInfo.TotalAvailableMemoryBytes },
            { "HighMemoryLoadThresholdBytes", gcInfo.HighMemoryLoadThresholdBytes }
        };

        if (allocated >= _thresholdBytes)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"内存使用过高: {allocated / 1024 / 1024} MB", null, data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("内存使用正常", data));
    }
}
