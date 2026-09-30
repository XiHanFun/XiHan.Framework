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
    /// 校验历史保留天数与历史清理数值
    /// </summary>
    public ValidateOptionsResult Validate(string? name, XiHanJobOptions options)
    {
        List<string> failures = [];

        if (options.HistoryRetentionDays < 0)
        {
            failures.Add($"{nameof(XiHanJobOptions.HistoryRetentionDays)} 不能小于 0。");
        }

        if (options.HistoryCleanupIntervalMinutes <= 0)
        {
            failures.Add($"{nameof(XiHanJobOptions.HistoryCleanupIntervalMinutes)} 必须大于 0。");
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
