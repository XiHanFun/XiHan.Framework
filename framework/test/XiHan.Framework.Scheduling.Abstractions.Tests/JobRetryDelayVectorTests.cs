// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Scheduling.Abstractions.Tests;

/// <summary>
/// 回放 <c>retry-delays.json</c>：作业重试的等待时间与重试策略的取值范围
/// </summary>
/// <remarks>
/// 规范 RFC-0013「作业与触发器」的重试：固定间隔每次都等 Delay，指数退避第 n 次重试等 Delay × 2 的 n − 1 次方，超过上限取上限、没设上限时超过 30 天按 30 天；重试次数 1 到 100，等待与上限大于零且不超过 30 天（ADR-0060、ADR-0277）
/// </remarks>
public sealed class JobRetryDelayVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly RetryDelayVectors _vectors = SpecVectors.Read<RetryDelayVectors>("scheduling", "retry-delays.json");

    [Fact]
    public void TheLimits_AreTheOnesInTheVectors()
    {
        Assert.Equal(JobRetryPolicy.MaxRetriesLimit, _vectors.MaxRetries);
        Assert.Equal(JobRetryPolicy.DelayLimit, TimeSpan.FromMilliseconds(_vectors.LimitMs));
    }

    [Fact]
    public void DelayFor_GivesTheWaitOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Waits);
        var failures = new List<string>();
        foreach (var vector in _vectors.Waits)
        {
            // 等待只看间隔、方式与上限，重试次数的上限取最大值，免得它先拦住第 100 次重试
            var policy = new JobRetryPolicy(_vectors.MaxRetries, TimeSpan.FromMilliseconds(vector.DelayMs))
            {
                Backoff = Enum.Parse<JobRetryBackoff>(vector.Backoff),
                MaxDelay = Milliseconds(vector.MaxDelayMs),
            };

            var wait = policy.DelayFor(vector.Retry);
            if (wait != TimeSpan.FromMilliseconds(vector.WaitMs))
            {
                failures.Add($"{vector.Name}: waits {wait.TotalMilliseconds} ms, the vector has {vector.WaitMs} ms");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ThePolicy_AcceptsOnlyTheValuesInRange()
    {
        Assert.NotEmpty(_vectors.Policies);
        var failures = new List<string>();
        foreach (var vector in _vectors.Policies)
        {
            var error = Record.Exception(() => JobRetryPolicy.Exponential(vector.MaxRetries, TimeSpan.FromMilliseconds(vector.DelayMs), Milliseconds(vector.MaxDelayMs)));
            if (error is not (null or ArgumentOutOfRangeException))
            {
                failures.Add($"{vector.Name}: threw '{error.GetType().Name}' instead of 'ArgumentOutOfRangeException'");
            }
            else if ((error is null) != vector.Valid)
            {
                failures.Add($"{vector.Name}: the values were {(error is null ? "accepted" : "rejected")}, the vector has valid = {vector.Valid}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverBothWaysTheCapsAndBothSidesOfTheRanges()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过
        // 两种等待方式；指数退避不设上限时按 30 天封顶，设了上限时没到与到了的各有
        Assert.Contains(_vectors.Waits, static vector => vector.Backoff == nameof(JobRetryBackoff.Fixed) && vector.Retry > 1);
        Assert.Contains(_vectors.Waits, vector => vector.Backoff == nameof(JobRetryBackoff.Exponential) && vector.MaxDelayMs is null && vector.WaitMs == _vectors.LimitMs && vector.DelayMs < _vectors.LimitMs);
        Assert.Contains(_vectors.Waits, static vector => vector.Backoff == nameof(JobRetryBackoff.Exponential) && vector.MaxDelayMs is { } cap && vector.WaitMs < cap && vector.Retry > 1);
        Assert.Contains(_vectors.Waits, static vector => vector.Backoff == nameof(JobRetryBackoff.Exponential) && vector.MaxDelayMs is { } cap && vector.WaitMs == cap);

        // 次数再多也不溢出：到重试次数的上限
        Assert.Contains(_vectors.Waits, vector => vector.Backoff == nameof(JobRetryBackoff.Exponential) && vector.Retry == _vectors.MaxRetries);

        // 取值范围：三个量各自正好到限的要收，各差一点的要拒
        Assert.Contains(_vectors.Policies, static vector => vector.Valid && vector.MaxRetries == 1);
        Assert.Contains(_vectors.Policies, vector => vector.Valid && vector.MaxRetries == _vectors.MaxRetries);
        Assert.Contains(_vectors.Policies, static vector => !vector.Valid && vector.MaxRetries == 0);
        Assert.Contains(_vectors.Policies, vector => !vector.Valid && vector.MaxRetries == _vectors.MaxRetries + 1);
        Assert.Contains(_vectors.Policies, vector => vector.Valid && vector.DelayMs == _vectors.LimitMs);
        Assert.Contains(_vectors.Policies, static vector => !vector.Valid && vector.DelayMs == 0);
        Assert.Contains(_vectors.Policies, vector => !vector.Valid && vector.DelayMs == _vectors.LimitMs + 1);
        Assert.Contains(_vectors.Policies, vector => vector.Valid && vector.MaxDelayMs == _vectors.LimitMs);
        Assert.Contains(_vectors.Policies, static vector => !vector.Valid && vector.MaxDelayMs == 0);
        Assert.Contains(_vectors.Policies, vector => !vector.Valid && vector.MaxDelayMs == _vectors.LimitMs + 1);
    }

    /// <summary>
    /// 向量里以毫秒计的可选时间
    /// </summary>
    private static TimeSpan? Milliseconds(long? milliseconds)
        => milliseconds is { } value ? TimeSpan.FromMilliseconds(value) : null;

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxRetries">重试次数的上限</param>
    /// <param name="LimitMs">等待与上限的最大值（毫秒）</param>
    /// <param name="Waits">等待时间的用例</param>
    /// <param name="Policies">取值范围的用例</param>
    private sealed record RetryDelayVectors(int MaxRetries, long LimitMs, RetryWaitCase[] Waits, RetryPolicyCase[] Policies);

    /// <summary>
    /// 一次重试前的等待
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Backoff">等待方式：<c>Fixed</c> 或 <c>Exponential</c></param>
    /// <param name="DelayMs">第一次重试前的等待（毫秒）</param>
    /// <param name="MaxDelayMs">指数退避的上限（毫秒）；<see langword="null"/> 表示不设</param>
    /// <param name="Retry">第几次重试，从 1 起</param>
    /// <param name="WaitMs">这次重试前的等待（毫秒）</param>
    private sealed record RetryWaitCase(string Name, string Backoff, long DelayMs, long? MaxDelayMs, int Retry, long WaitMs);

    /// <summary>
    /// 一组重试策略的取值
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="MaxRetries">重试次数</param>
    /// <param name="DelayMs">等待（毫秒）</param>
    /// <param name="MaxDelayMs">上限（毫秒）；<see langword="null"/> 表示不设</param>
    /// <param name="Valid">这组取值是否合法</param>
    private sealed record RetryPolicyCase(string Name, int MaxRetries, long DelayMs, long? MaxDelayMs, bool Valid);
}
