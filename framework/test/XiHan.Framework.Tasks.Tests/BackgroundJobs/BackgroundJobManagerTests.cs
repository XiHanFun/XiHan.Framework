// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.BackgroundJobs.Options;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs;

/// <summary>
/// 默认后台作业管理器测试
/// </summary>
/// <remarks>
/// 入队是"写一条记录就返回"的纯编排：不触发执行、不等待结果。
/// 因此这里断言的是落库记录的每一个字段是否按契约填好——
/// 作业名决定将来能否找回处理器，NextTryTime 决定延迟语义，TenantId 决定执行时的租户上下文。
/// </remarks>
public class BackgroundJobManagerTests
{
    private static readonly DateTime Now = new(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc);

    /// <summary>
    /// 入队写入一条记录并返回其标识
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_InsertsSingleJobAndReturnsItsId()
    {
        var store = new RecordingBackgroundJobStore();
        var manager = CreateManager(store, out _);

        var jobId = await manager.EnqueueAsync(new NamedJobArgs { Value = "订单", Count = 1 });

        var inserted = Assert.Single(store.Inserted);
        Assert.Equal(inserted.Id.ToString(), jobId);
        Assert.NotEqual(Guid.Empty, inserted.Id);
    }

    /// <summary>
    /// 参数为 null 时抛出空引用参数异常，不落库
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenArgsNull_ThrowsAndDoesNotInsert()
    {
        var store = new RecordingBackgroundJobStore();
        var manager = CreateManager(store, out _);

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.EnqueueAsync<NamedJobArgs>(null!));

        Assert.Empty(store.Inserted);
    }

    /// <summary>
    /// 参数类型已注册时使用注册表里的作业名
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenArgsTypeRegistered_UsesRegisteredJobName()
    {
        var store = new RecordingBackgroundJobStore();
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<NamedArgsJob>();
        var manager = CreateManager(store, out _, jobOptions: jobOptions);

        await manager.EnqueueAsync(new NamedJobArgs());

        Assert.Equal("xihan-tests-named-args", store.Inserted[0].JobName);
    }

    /// <summary>
    /// 参数类型未注册时回退按参数类型解析作业名
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenArgsTypeNotRegistered_FallsBackToArgsTypeName()
    {
        var store = new RecordingBackgroundJobStore();
        var manager = CreateManager(store, out _);

        await manager.EnqueueAsync(new UnnamedJobArgs { Value = "x" });

        Assert.Equal(typeof(UnnamedJobArgs).FullName, store.Inserted[0].JobName);
    }

    /// <summary>
    /// 作业参数被序列化后落库
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_SerializesArgs()
    {
        var store = new RecordingBackgroundJobStore();
        var manager = CreateManager(store, out _);

        await manager.EnqueueAsync(new NamedJobArgs { Value = "订单", Count = 3 });

        Assert.Equal("""{"Value":"订单","Count":3}""", store.Inserted[0].JobArgs);
    }

    /// <summary>
    /// 不传延迟时创建时间与下次执行时间都取当前时钟，表示尽快执行
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenNoDelay_SchedulesImmediately()
    {
        var store = new RecordingBackgroundJobStore();
        var manager = CreateManager(store, out _);

        await manager.EnqueueAsync(new UnnamedJobArgs());

        var inserted = store.Inserted[0];
        Assert.Equal(Now, inserted.CreationTime);
        Assert.Equal(Now, inserted.NextTryTime);
    }

    /// <summary>
    /// 传延迟时下次执行时间为当前时钟加延迟，创建时间不受影响
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenDelayed_PushesNextTryTimeOnly()
    {
        var store = new RecordingBackgroundJobStore();
        var manager = CreateManager(store, out _);

        await manager.EnqueueAsync(new UnnamedJobArgs(), delay: TimeSpan.FromMinutes(90));

        var inserted = store.Inserted[0];
        Assert.Equal(Now, inserted.CreationTime);
        Assert.Equal(Now.AddMinutes(90), inserted.NextTryTime);
    }

    /// <summary>
    /// 优先级默认为普通，显式指定时按指定值落库
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WritesPriority()
    {
        var store = new RecordingBackgroundJobStore();
        var manager = CreateManager(store, out _);

        await manager.EnqueueAsync(new UnnamedJobArgs());
        await manager.EnqueueAsync(new UnnamedJobArgs(), BackgroundJobPriority.High);

        Assert.Equal(BackgroundJobPriority.Normal, store.Inserted[0].Priority);
        Assert.Equal(BackgroundJobPriority.High, store.Inserted[1].Priority);
    }

    /// <summary>
    /// 入队时携带当前租户标识，执行时据此还原租户上下文
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_CapturesCurrentTenant()
    {
        var store = new RecordingBackgroundJobStore();
        var manager = CreateManager(store, out var currentTenant);

        using (currentTenant.Change(1024))
        {
            await manager.EnqueueAsync(new UnnamedJobArgs());
        }

        await manager.EnqueueAsync(new UnnamedJobArgs());

        Assert.Equal(1024L, store.Inserted[0].TenantId);
        Assert.Null(store.Inserted[1].TenantId);
    }

    /// <summary>
    /// 入队时写入应用名，供多实例隔离领取
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WritesApplicationNameFromWorkerOptions()
    {
        var store = new RecordingBackgroundJobStore();
        var workerOptions = new BackgroundJobWorkerOptions { ApplicationName = "order-service" };
        var manager = CreateManager(store, out _, workerOptions: workerOptions);

        await manager.EnqueueAsync(new UnnamedJobArgs());

        Assert.Equal("order-service", store.Inserted[0].ApplicationName);
    }

    /// <summary>
    /// 新入队作业未尝试过、未放弃、无上次尝试时间
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_NewJobIsFresh()
    {
        var store = new RecordingBackgroundJobStore();
        var manager = CreateManager(store, out _);

        await manager.EnqueueAsync(new UnnamedJobArgs());

        var inserted = store.Inserted[0];
        Assert.Equal((short)0, inserted.TryCount);
        Assert.False(inserted.IsAbandoned);
        Assert.Null(inserted.LastTryTime);
    }

    /// <summary>
    /// 多次入队产生互不相同的作业标识
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_GeneratesUniqueIds()
    {
        var store = new RecordingBackgroundJobStore();
        var manager = CreateManager(store, out _);

        for (var i = 0; i < 20; i++)
        {
            await manager.EnqueueAsync(new UnnamedJobArgs());
        }

        Assert.Equal(20, store.Inserted.Select(x => x.Id).Distinct().Count());
    }

    /// <summary>
    /// 创建管理器
    /// </summary>
    /// <param name="store">存储替身</param>
    /// <param name="currentTenant">当前租户替身</param>
    /// <param name="jobOptions">注册表选项</param>
    /// <param name="workerOptions">Worker 选项</param>
    /// <returns>管理器</returns>
    private static BackgroundJobManager CreateManager(
        RecordingBackgroundJobStore store,
        out FakeCurrentTenant currentTenant,
        BackgroundJobOptions? jobOptions = null,
        BackgroundJobWorkerOptions? workerOptions = null)
    {
        currentTenant = new FakeCurrentTenant();

        return new BackgroundJobManager(
            store,
            new BackgroundJobSerializer(),
            new FakeClock(Now),
            currentTenant,
            Microsoft.Extensions.Options.Options.Create(jobOptions ?? new BackgroundJobOptions()),
            Microsoft.Extensions.Options.Options.Create(workerOptions ?? new BackgroundJobWorkerOptions()));
    }
}
