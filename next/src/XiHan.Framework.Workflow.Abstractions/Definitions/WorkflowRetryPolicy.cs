// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
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
    /// 1 表示不重试。JSON 里没写时取初值 1（ADR-0107）。
    /// </remarks>
    [JsonIgnore]
    public int MaxAttempts
    {
        get => field;
        init => field = WorkflowDefinitionRules.IsValidMaxAttempts(value) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum attempts must be at least 1.");
    } = 1;

    /// <summary>
    /// 序列化用的 <see cref="MaxAttempts"/>。
    /// </summary>
    /// <remarks>
    /// 源生成的序列化器把不在场的 init 成员设成类型的默认值、盖掉初值（ADR-0107）：经这一层可空的成员，不在场或给 <see langword="null"/> 时什么也不赋，策略保留 <see cref="MaxAttempts"/> 的初值；给了值照常经 <see cref="MaxAttempts"/> 的校验。写出时总是写出当前值。
    /// </remarks>
    [JsonInclude]
    [JsonPropertyName("maxAttempts")]
    internal int? MaxAttemptsJson
    {
        get => MaxAttempts;
        init
        {
            if (value is { } given)
            {
                MaxAttempts = given;
            }
        }
    }

    /// <summary>
    /// 首次重试前的等待。
    /// </summary>
    /// <remarks>
    /// 默认 10 秒；JSON 里没写时同样是 10 秒（ADR-0107）。
    /// </remarks>
    [JsonIgnore]
    public TimeSpan FirstDelay
    {
        get => field;
        init => field = WorkflowDefinitionRules.IsValidFirstDelay(value) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The first delay must not be negative.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 序列化用的 <see cref="FirstDelay"/>。
    /// </summary>
    /// <remarks>
    /// 源生成的序列化器把不在场的 init 成员设成类型的默认值、盖掉初值（ADR-0107）：经这一层可空的成员，不在场或给 <see langword="null"/> 时什么也不赋，策略保留 <see cref="FirstDelay"/> 的初值；给了值照常经 <see cref="FirstDelay"/> 的校验。写出时总是写出当前值。
    /// </remarks>
    [JsonInclude]
    [JsonPropertyName("firstDelay")]
    internal TimeSpan? FirstDelayJson
    {
        get => FirstDelay;
        init
        {
            if (value is { } given)
            {
                FirstDelay = given;
            }
        }
    }

    /// <summary>
    /// 退避因子。
    /// </summary>
    /// <remarks>
    /// 第 n 次重试前等待 <c>FirstDelay × BackoffFactor^(n−1)</c>；至少 1，默认 2；JSON 里没写时同样是 2（ADR-0107）。
    /// </remarks>
    [JsonIgnore]
    public double BackoffFactor
    {
        get => field;
        init => field = WorkflowDefinitionRules.IsValidBackoffFactor(value) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The backoff factor must be at least 1.");
    } = 2.0;

    /// <summary>
    /// 序列化用的 <see cref="BackoffFactor"/>。
    /// </summary>
    /// <remarks>
    /// 源生成的序列化器把不在场的 init 成员设成类型的默认值、盖掉初值（ADR-0107）：经这一层可空的成员，不在场或给 <see langword="null"/> 时什么也不赋，策略保留 <see cref="BackoffFactor"/> 的初值；给了值照常经 <see cref="BackoffFactor"/> 的校验。写出时总是写出当前值。
    /// </remarks>
    [JsonInclude]
    [JsonPropertyName("backoffFactor")]
    internal double? BackoffFactorJson
    {
        get => BackoffFactor;
        init
        {
            if (value is { } given)
            {
                BackoffFactor = given;
            }
        }
    }

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
