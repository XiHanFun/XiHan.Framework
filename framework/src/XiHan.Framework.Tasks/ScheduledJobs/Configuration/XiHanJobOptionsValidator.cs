// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Tasks.ScheduledJobs.Configuration;

/// <summary>
/// 曦寒任务调度配置选项校验器
/// </summary>
internal sealed class XiHanJobOptionsValidator : IValidateOptions<XiHanJobOptions>
{
    /// <summary>
    /// 清理间隔允许的最大分钟数（周期定时器的最大周期）
    /// </summary>
    internal const int MaxHistoryCleanupIntervalMinutes = 71582;

    /// <summary>
    /// 启用历史清理时校验历史保留天数与历史清理数值
    /// </summary>
    public ValidateOptionsResult Validate(string? name, XiHanJobOptions options)
    {
        if (!options.HistoryCleanupEnabled)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];

        if (options.HistoryRetentionDays < 0)
        {
            failures.Add($"{nameof(XiHanJobOptions.HistoryRetentionDays)} 不能小于 0。");
        }

        if (options.HistoryCleanupIntervalMinutes is <= 0 or > MaxHistoryCleanupIntervalMinutes)
        {
            failures.Add($"{nameof(XiHanJobOptions.HistoryCleanupIntervalMinutes)} 必须在 1 到 {MaxHistoryCleanupIntervalMinutes} 之间。");
        }

        if (options.HistoryCleanupBatchSize <= 0)
        {
            failures.Add($"{nameof(XiHanJobOptions.HistoryCleanupBatchSize)} 必须大于 0。");
        }

        if (options.HistoryCleanupMaxBatchesPerRun <= 0)
        {
            failures.Add($"{nameof(XiHanJobOptions.HistoryCleanupMaxBatchesPerRun)} 必须大于 0。");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
