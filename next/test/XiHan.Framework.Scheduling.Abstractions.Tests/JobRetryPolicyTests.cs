// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Tests;

/// <summary>
/// 重试策略与执行时限的取值。
/// </summary>
/// <remarks>
/// 固定与指数退避的等待、上限截断，次数、等待与时限的范围（ADR-0060）。
/// </remarks>
public sealed class JobRetryPolicyTests
{
    [Fact]
    public void DelayFor_FixedAndExponential_WithCap()
    {
        var fixedPolicy = JobRetryPolicy.Fixed(3, TimeSpan.FromSeconds(5));
        Assert.Equal([5d, 5d, 5d], Enumerable.Range(1, 3).Select(n => fixedPolicy.DelayFor(n).TotalSeconds));

        var exponential = JobRetryPolicy.Exponential(10, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
        Assert.Equal([1d, 2d, 4d, 8d, 10d, 10d], Enumerable.Range(1, 6).Select(n => exponential.DelayFor(n).TotalSeconds));

        // 没设上限：受 30 天总上限约束，很大的次数也不溢出
        var unbounded = JobRetryPolicy.Exponential(100, TimeSpan.FromMinutes(1));
        Assert.Equal(JobRetryPolicy.DelayLimit, unbounded.DelayFor(100));
        Assert.Throws<ArgumentOutOfRangeException>(() => unbounded.DelayFor(0));
    }

    [Fact]
    public void Values_OutOfRange_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => JobRetryPolicy.Fixed(0, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(static () => JobRetryPolicy.Fixed(JobRetryPolicy.MaxRetriesLimit + 1, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(static () => JobRetryPolicy.Fixed(1, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(static () => JobRetryPolicy.Fixed(1, TimeSpan.FromDays(31)));
        Assert.Throws<ArgumentOutOfRangeException>(static () => JobRetryPolicy.Exponential(1, TimeSpan.FromSeconds(1), TimeSpan.Zero));

        var definition = new JobDefinition(new JobName("retry.limits"), typeof(NoopJob));
        Assert.Throws<ArgumentOutOfRangeException>(() => definition with { Timeout = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => definition with { Timeout = TimeSpan.FromDays(31) });
        Assert.Equal(TimeSpan.FromMinutes(5), (definition with { Timeout = TimeSpan.FromMinutes(5) }).Timeout);
        Assert.Throws<ArgumentOutOfRangeException>(() => JobRun.Create(definition.Name, DateTimeOffset.UtcNow, Schedulers.JobTrigger.Manual) with { Attempt = 0 });
    }

    /// <summary>
    /// 什么都不做的作业。
    /// </summary>
    private sealed class NoopJob : IJob
    {
        /// <inheritdoc />
        public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Success());
    }
}
