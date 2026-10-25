// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.Abstractions.Tests;

/// <summary>
/// 作业名、定义与登记表。
/// </summary>
public sealed class JobRegistryTests
{
    /// <summary>
    /// 什么都不做的作业。
    /// </summary>
    private sealed class NoopJob : IJob
    {
        /// <inheritdoc />
        public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Success());
    }

    /// <summary>
    /// 另一个什么都不做的作业。
    /// </summary>
    private sealed class OtherJob : IJob
    {
        /// <inheritdoc />
        public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Success());
    }

    [Fact]
    public void Names_AndDefinitions_Validate()
    {
        Assert.True(JobName.IsValid("cleanup.nightly-1_a"));
        Assert.Throws<ArgumentException>(static () => new JobName("Upper"));
        Assert.Throws<ArgumentException>(static () => new JobName(".dot"));
        Assert.Throws<ArgumentException>(static () => new JobName(string.Empty));
        Assert.Throws<ArgumentException>(static () => new JobDefinition("x", JobSchedule.Manual, typeof(string)));

        var definition = new JobDefinition("x", JobSchedule.Manual, typeof(NoopJob));
        Assert.True(definition.LeaderOnly);
        Assert.False(definition.AllowConcurrent);
        Assert.Equal(JobTrigger.Manual, JobRun.Create("x", DateTimeOffset.UnixEpoch, JobTrigger.Manual).Trigger);
    }

    [Fact]
    public void AddJob_RegistersScopedJobsInASharedRegistry_AndRejectsDuplicates()
    {
        var services = new ServiceCollection();
        services.AddJob<NoopJob>("a", JobSchedule.Manual);
        services.AddJob<OtherJob>(new JobDefinition("b", JobSchedule.Every(TimeSpan.FromSeconds(1)), typeof(OtherJob)) { AllowConcurrent = true });

        var registry = services.GetJobRegistry();
        Assert.Same(registry, services.GetJobRegistry());
        Assert.Equal(["a", "b"], registry.Jobs.Select(static j => j.Name.Value));
        Assert.True(registry.Find("b").ValueOrThrow().AllowConcurrent);
        Assert.False(registry.Find("c").HasValue);
        Assert.Contains(services, static d => d.ServiceType == typeof(NoopJob) && d.Lifetime == ServiceLifetime.Scoped);

        Assert.Throws<ArgumentException>(() => services.AddJob<NoopJob>("a", JobSchedule.Manual));
        Assert.Throws<ArgumentException>(() => services.AddJob<NoopJob>(new JobDefinition("d", JobSchedule.Manual, typeof(OtherJob))));
    }
}
