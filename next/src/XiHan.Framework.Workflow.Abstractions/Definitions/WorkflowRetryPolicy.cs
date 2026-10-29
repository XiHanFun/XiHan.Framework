// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 节点的重试策略：失败后按指数退避再试，总尝试次数含首次。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowRetryPolicy
{
    /// <summary>
    /// 总尝试次数（含首次），至少 1。
    /// </summary>
    /// <remarks>
    /// 1 表示不重试。
    /// </remarks>
    public int MaxAttempts
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum attempts must be at least 1.");
    } = 1;

    /// <summary>
    /// 首次重试前的等待。
    /// </summary>
    /// <remarks>
    /// 默认 10 秒。
    /// </remarks>
    public TimeSpan FirstDelay
    {
        get => field;
        init => field = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The first delay must not be negative.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 退避因子。
    /// </summary>
    /// <remarks>
    /// 第 n 次重试前等待 <c>FirstDelay × BackoffFactor^(n−1)</c>；至少 1，默认 2。
    /// </remarks>
    public double BackoffFactor
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The backoff factor must be at least 1.");
    } = 2.0;

    /// <summary>
    /// 第 <paramref name="attempt"/> 次（从 1 起）尝试失败后是否还能重试。
    /// </summary>
    public bool CanRetry(int attempt) => attempt < MaxAttempts;

    /// <summary>
    /// 第 <paramref name="attempt"/> 次（从 1 起）尝试失败后到下一次尝试的等待。
    /// </summary>
    public TimeSpan DelayAfter(int attempt)
    {
        var factor = Math.Pow(BackoffFactor, Math.Max(0, attempt - 1));
        var ticks = FirstDelay.Ticks * factor;
        return ticks >= TimeSpan.MaxValue.Ticks ? TimeSpan.MaxValue : TimeSpan.FromTicks((long)ticks);
    }
}
