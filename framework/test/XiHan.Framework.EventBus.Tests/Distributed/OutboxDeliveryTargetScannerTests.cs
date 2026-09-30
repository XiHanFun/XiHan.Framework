// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Local;
using XiHan.Framework.EventBus.Tests.Fakes;
using XiHan.Framework.MultiTenancy.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace XiHan.Framework.EventBus.Tests.Distributed;

/// <summary>
/// 发件箱投递目标轮转扫描器测试
/// </summary>
public class OutboxDeliveryTargetScannerTests
{
    /// <summary>
    /// 单轮领取总数不超过预算
    /// </summary>
    [Fact]
    public async Task 单轮领取总数不超过预算()
    {
        using var harness = new ScannerHarness();
        harness.Directory.Targets.AddRange([new OutboxDeliveryTarget(1001), new OutboxDeliveryTarget(1002)]);
        harness.Outbox.Seed(0, 5);
        harness.Outbox.Seed(1001, 5);
        harness.Outbox.Seed(1002, 5);

        var claimed = await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 7, TestContext.Current.CancellationToken);

        Assert.Equal(7, claimed);
        Assert.Equal(7, harness.Bus.OutboxPublished.Count);
        Assert.Equal(8, harness.Outbox.TotalRemaining);
    }

    /// <summary>
    /// 热点目标不会让其他目标等待超过一轮
    /// </summary>
    [Fact]
    public async Task 热点目标不会让其他目标等待超过一轮()
    {
        using var harness = new ScannerHarness();
        harness.Directory.Targets.AddRange([new OutboxDeliveryTarget(1001), new OutboxDeliveryTarget(1002)]);
        harness.Outbox.Seed(1001, 100);
        harness.Outbox.Seed(1002, 1);

        await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);
        Assert.Equal(1, harness.Outbox.RemainingOf(1002));

        await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);
        Assert.Equal(0, harness.Outbox.RemainingOf(1002));
    }

    /// <summary>
    /// 故障目标不阻塞健康目标
    /// </summary>
    [Fact]
    public async Task 故障目标不阻塞健康目标()
    {
        using var harness = new ScannerHarness();
        harness.Directory.Targets.AddRange([new OutboxDeliveryTarget(1001), new OutboxDeliveryTarget(1002)]);
        harness.Outbox.Seed(1001, 3);
        harness.Outbox.Seed(1002, 3);
        harness.Outbox.FaultyTenants.Add(1001);

        var claimed = await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);

        Assert.Equal(3, claimed);
        Assert.Equal(0, harness.Outbox.RemainingOf(1002));
        Assert.Equal(3, harness.Outbox.RemainingOf(1001));
    }

    /// <summary>
    /// 目录读取失败时保留游标，退避期间只扫描宿主布局
    /// </summary>
    [Fact]
    public async Task 目录读取失败时保留游标且退避期间只扫描宿主()
    {
        using var harness = new ScannerHarness(pageSize: 1);
        harness.Directory.Targets.AddRange([new OutboxDeliveryTarget(1001), new OutboxDeliveryTarget(1002)]);
        harness.Outbox.Seed(1002, 1);
        harness.Directory.FailingCursors.Add("1");

        await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);
        Assert.Equal(new string?[] { null, "1" }, harness.Directory.RequestedCursors);

        harness.Outbox.Seed(0, 1);
        await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);
        Assert.Equal(0, harness.Outbox.RemainingOf(0));
        Assert.Equal(2, harness.Directory.RequestedCursors.Count);

        harness.Directory.FailingCursors.Clear();
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);

        Assert.Equal("1", harness.Directory.RequestedCursors[2]);
        Assert.Equal(0, harness.Outbox.RemainingOf(1002));
    }

    /// <summary>
    /// 宿主在无租户上下文扫描，目标在各自租户上下文扫描，结束后还原
    /// </summary>
    [Fact]
    public async Task 按目标切换租户上下文且结束后还原()
    {
        using var harness = new ScannerHarness();
        harness.Directory.Targets.Add(new OutboxDeliveryTarget(1001, "acme"));

        await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);

        Assert.Null(harness.CurrentTenant.Id);
        Assert.Equal(new long?[] { null, 1001 }, harness.CurrentTenant.ChangedIds);
        Assert.Equal(new long[] { 0, 1001 }, harness.Outbox.ClaimRequests.Select(request => request.TenantId));
    }

    /// <summary>
    /// 停用目标的既有事件仍被送完
    /// </summary>
    [Fact]
    public async Task 停用目标的既有事件仍被送完()
    {
        using var harness = new ScannerHarness();
        harness.Directory.Targets.Add(new OutboxDeliveryTarget(1001, isEnabled: false));
        harness.Outbox.Seed(1001, 2);

        await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.Outbox.RemainingOf(1001));
        Assert.Equal(2, harness.Bus.OutboxPublished.Count);
    }

    /// <summary>
    /// 单轮耗时达到上限后不再访问新目标
    /// </summary>
    [Fact]
    public async Task 单轮耗时达到上限后不再访问新目标()
    {
        using var harness = new ScannerHarness(roundTimeLimitMilliseconds: 100);
        harness.Directory.Targets.AddRange(
            [new OutboxDeliveryTarget(1001), new OutboxDeliveryTarget(1002), new OutboxDeliveryTarget(1003)]);
        harness.Outbox.OnClaim = () => harness.Time.Advance(TimeSpan.FromMilliseconds(60));

        await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);

        Assert.Equal(2, harness.Outbox.ClaimRequests.Count);
    }

    /// <summary>
    /// 目录故障期间每轮仍扫描宿主布局
    /// </summary>
    [Fact]
    public async Task 目录故障期间每轮仍扫描宿主()
    {
        using var harness = new ScannerHarness();
        harness.Directory.Targets.Add(new OutboxDeliveryTarget(1001));
        harness.Directory.FailingCursors.Add(string.Empty);
        harness.Outbox.Seed(0, 25);

        for (var round = 0; round < 3; round++)
        {
            await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, harness.Outbox.RemainingOf(0));
    }

    /// <summary>
    /// 读到空页但仍有下一页时同一轮继续读取
    /// </summary>
    [Fact]
    public async Task 空页有下一页游标时同一轮继续读取()
    {
        using var harness = new ScannerHarness(pageSize: 1);
        harness.Directory.Targets.AddRange([new OutboxDeliveryTarget(1001), new OutboxDeliveryTarget(1002)]);
        harness.Directory.EmptyCursors.Add(string.Empty);
        harness.Outbox.Seed(1002, 2);

        await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 10, TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.Outbox.RemainingOf(1002));
    }

    /// <summary>
    /// 已注册目录但发件箱不按租户定位时只在无租户上下文领取一次
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task 发件箱不按租户定位时发送后台服务不走目标扫描()
    {
        var currentTenant = new FakeCurrentTenant();
        var directory = new FakeTargetProvider();
        directory.Targets.Add(new OutboxDeliveryTarget(1001));
        var distributedOptions = new XiHanDistributedEventBusOptions();
        distributedOptions.Outboxes.Configure(config => config.ImplementationType = typeof(DefaultEventOutbox));
        var processingOptions = new EventBoxProcessingOptions { PollingIntervalMilliseconds = 1, OutboxBatchSize = 10 };

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentTenant>(currentTenant);
        services.AddSingleton<DefaultEventOutbox>();
        services.AddSingleton<IOutboxDeliveryTargetProvider>(directory);
        services.AddSingleton<IDistributedEventBus>(serviceProvider => new RecordingDistributedEventBus(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            currentTenant,
            new FakeUnitOfWorkManager(),
            MsOptions.Create(distributedOptions),
            new StubGuidGenerator(),
            new StubClock(),
            new EventHandlerInvoker(),
            NullLocalEventBus.Instance,
            new FakeCorrelationIdProvider()));
        services.AddSingleton(serviceProvider => new OutboxDeliveryTargetScanner(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            MsOptions.Create(processingOptions),
            NullLogger<OutboxDeliveryTargetScanner>.Instance));
        using var provider = services.BuildServiceProvider();
        var bus = (RecordingDistributedEventBus)provider.GetRequiredService<IDistributedEventBus>();
        var outbox = provider.GetRequiredService<DefaultEventOutbox>();
        await outbox.EnqueueAsync(new OutgoingEventInfo(Guid.NewGuid(), "Test.Event", [1], DateTime.UtcNow));

        using var hostedService = new EventBoxOutboxSenderHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            MsOptions.Create(distributedOptions),
            MsOptions.Create(processingOptions),
            NullLogger<EventBoxOutboxSenderHostedService>.Instance);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (bus.OutboxPublished.IsEmpty && DateTime.UtcNow < deadline)
            {
                await Task.Delay(25, TestContext.Current.CancellationToken);
            }

            await Task.Delay(500, TestContext.Current.CancellationToken);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        Assert.Single(bus.OutboxPublished);
        Assert.Empty(currentTenant.ChangedIds);
        Assert.Empty(directory.RequestedCursors);
    }

    /// <summary>
    /// 预算不为正时不访问任何目标
    /// </summary>
    [Fact]
    public async Task 预算不为正时不访问任何目标()
    {
        using var harness = new ScannerHarness();

        Assert.Equal(0, await harness.Scanner.SendRoundAsync(harness.OutboxConfig, 0, TestContext.Current.CancellationToken));
        Assert.Empty(harness.Outbox.ClaimRequests);
    }

    /// <summary>
    /// 注册目录后发送后台服务按目标投递租户事件
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task 注册目录后发送后台服务按目标投递租户事件()
    {
        using var harness = new ScannerHarness();
        harness.Directory.Targets.Add(new OutboxDeliveryTarget(1001));
        harness.Outbox.Seed(1001, 1);

        using var hostedService = new EventBoxOutboxSenderHostedService(
            harness.Provider.GetRequiredService<IServiceScopeFactory>(),
            MsOptions.Create(harness.DistributedOptions),
            MsOptions.Create(harness.ProcessingOptions),
            NullLogger<EventBoxOutboxSenderHostedService>.Instance);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (harness.Outbox.RemainingOf(1001) != 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(25, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        Assert.Equal(0, harness.Outbox.RemainingOf(1001));
    }

    /// <summary>
    /// 待送数按目标租户统计且结束后还原租户上下文
    /// </summary>
    [Fact]
    public async Task 待送数按目标租户统计()
    {
        using var harness = new ScannerHarness();
        harness.Outbox.Seed(1001, 2);
        harness.Outbox.Seed(1002, 5);
        var counter = new OutboxPendingEventCounter(
            harness.Provider.GetRequiredService<IServiceScopeFactory>(),
            MsOptions.Create(harness.DistributedOptions));

        var count = await counter.GetPendingCountAsync(new OutboxDeliveryTarget(1001), TestContext.Current.CancellationToken);

        Assert.Equal(2, count);
        Assert.Null(harness.CurrentTenant.Id);
    }

    /// <summary>
    /// 发件箱不按租户定位时待送数统计抛出不支持
    /// </summary>
    [Fact]
    public async Task 发件箱不按租户定位时待送数统计抛出不支持()
    {
        var distributedOptions = new XiHanDistributedEventBusOptions();
        distributedOptions.Outboxes.Configure(config => config.ImplementationType = typeof(DefaultEventOutbox));
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant());
        services.AddSingleton<DefaultEventOutbox>();
        using var provider = services.BuildServiceProvider();
        var counter = new OutboxPendingEventCounter(
            provider.GetRequiredService<IServiceScopeFactory>(),
            MsOptions.Create(distributedOptions));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => counter.GetPendingCountAsync(new OutboxDeliveryTarget(1001), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 扫描器测试装配
    /// </summary>
    private sealed class ScannerHarness : IDisposable
    {
        public ScannerHarness(int pageSize = 100, int roundTimeLimitMilliseconds = 30000)
        {
            CurrentTenant = new FakeCurrentTenant();
            Outbox = new TenantScopedFakeOutbox(CurrentTenant);
            DistributedOptions = new XiHanDistributedEventBusOptions();
            DistributedOptions.Outboxes.Configure(config => config.ImplementationType = typeof(TenantScopedFakeOutbox));
            OutboxConfig = DistributedOptions.Outboxes.Values.Single();
            ProcessingOptions = new EventBoxProcessingOptions
            {
                PollingIntervalMilliseconds = 1,
                OutboxBatchSize = 10,
                OutboxTargetPageSize = pageSize,
                OutboxRoundTimeLimitMilliseconds = roundTimeLimitMilliseconds
            };

            var services = new ServiceCollection();
            services.AddSingleton<ICurrentTenant>(CurrentTenant);
            services.AddSingleton(Outbox);
            services.AddSingleton<IOutboxDeliveryTargetProvider>(Directory);
            services.AddSingleton<IDistributedEventBus>(serviceProvider => new RecordingDistributedEventBus(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                CurrentTenant,
                new FakeUnitOfWorkManager(),
                MsOptions.Create(DistributedOptions),
                new StubGuidGenerator(),
                new StubClock(),
                new EventHandlerInvoker(),
                NullLocalEventBus.Instance,
                new FakeCorrelationIdProvider()));
            services.AddSingleton(serviceProvider => new OutboxDeliveryTargetScanner(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                MsOptions.Create(ProcessingOptions),
                NullLogger<OutboxDeliveryTargetScanner>.Instance,
                Time));

            Provider = services.BuildServiceProvider();
            Bus = (RecordingDistributedEventBus)Provider.GetRequiredService<IDistributedEventBus>();
            Scanner = Provider.GetRequiredService<OutboxDeliveryTargetScanner>();
        }

        public FakeCurrentTenant CurrentTenant { get; }

        public TenantScopedFakeOutbox Outbox { get; }

        public FakeTargetProvider Directory { get; } = new();

        public ManualTimeProvider Time { get; } = new();

        public XiHanDistributedEventBusOptions DistributedOptions { get; }

        public OutboxConfig OutboxConfig { get; }

        public EventBoxProcessingOptions ProcessingOptions { get; }

        public ServiceProvider Provider { get; }

        public RecordingDistributedEventBus Bus { get; }

        public OutboxDeliveryTargetScanner Scanner { get; }

        public void Dispose()
        {
            Provider.Dispose();
        }
    }
}
