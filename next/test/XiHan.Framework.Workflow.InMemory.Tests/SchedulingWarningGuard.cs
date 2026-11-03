// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 释放宿主后核对调度没有记告警。
/// </summary>
/// <remarks>
/// 调度的发布端发布失败或抛异常时记告警（如 <c>Publishing scheduling event '...' threw.</c>）。套件的每个用例都经它释放装了调度的宿主，释放后日志里有调度类别的告警或错误就让用例失败（ADR-0096 修订）。
/// </remarks>
internal sealed class SchedulingWarningGuard(IAsyncDisposable host, LogCapture logs) : IAsyncDisposable
{
    /// <summary>
    /// 调度各组件日志类别的共同前缀。
    /// </summary>
    private const string SchedulingCategory = "XiHan.Framework.Scheduling";

    /// <summary>
    /// 是不是调度类别的告警或错误。
    /// </summary>
    public static bool IsSchedulingWarning((LogLevel Level, string Category, string Message) entry)
        => entry.Level >= LogLevel.Warning && entry.Category.StartsWith(SchedulingCategory, StringComparison.Ordinal);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync().ConfigureAwait(false);
        Assert.DoesNotContain(logs.Entries, IsSchedulingWarning);
    }
}
