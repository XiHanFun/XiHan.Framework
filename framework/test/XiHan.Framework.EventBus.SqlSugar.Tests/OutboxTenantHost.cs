// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Options;
using XiHan.Framework.EventBus.SqlSugar.Outbox;
using XiHan.Framework.MultiTenancy.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 以真实 SqlSugar 发件箱、投递目标扫描器与待送数计数器组装的测试宿主，代表一个应用实例
/// </summary>
internal sealed class OutboxTenantHost : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="resolver">客户端解析器</param>
    /// <param name="connectionScope">入箱连接范围</param>
    /// <param name="targetProvider">投递目标目录，为 null 表示未注册</param>
    /// <param name="batchSize">每轮预算</param>
    public OutboxTenantHost(
        ICurrentTenant currentTenant,
        ISqlSugarClientResolver resolver,
        ISqlSugarOutboxConnectionScope connectionScope,
        IOutboxDeliveryTargetProvider? targetProvider,
        int batchSize = 10)
    {
        DistributedOptions = new XiHanDistributedEventBusOptions();
        DistributedOptions.Outboxes.Configure(config => config.ImplementationType = typeof(SqlSugarEventOutbox));
        OutboxConfig = DistributedOptions.Outboxes.Values.Single();
        BatchSize = batchSize;

        var services = new ServiceCollection();
        services.AddSingleton(currentTenant);
        services.AddSingleton(resolver);
        services.AddSingleton(connectionScope);
        if (targetProvider is not null)
        {
            services.AddSingleton(targetProvider);
        }

        services.AddSingleton(MsOptions.Create(new XiHanSqlSugarEventBoxOptions { ClaimTimeout = TimeSpan.FromMinutes(5) }));
        services.AddSingleton(MsOptions.Create(DistributedOptions));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped<SqlSugarEventOutbox>();
        services.AddSingleton<IDistributedEventBus>(Bus);

        Provider = services.BuildServiceProvider();

        var scopeFactory = Provider.GetRequiredService<IServiceScopeFactory>();
        Scanner = new OutboxDeliveryTargetScanner(
            scopeFactory,
            MsOptions.Create(new EventBoxProcessingOptions { PollingIntervalMilliseconds = 1, OutboxBatchSize = batchSize }),
            NullLogger<OutboxDeliveryTargetScanner>.Instance);
        Counter = new OutboxPendingEventCounter(scopeFactory, MsOptions.Create(DistributedOptions));
    }

    /// <summary>
    /// 分布式事件总线选项
    /// </summary>
    public XiHanDistributedEventBusOptions DistributedOptions { get; }

    /// <summary>
    /// 发件箱配置
    /// </summary>
    public OutboxConfig OutboxConfig { get; }

    /// <summary>
    /// 每轮预算
    /// </summary>
    public int BatchSize { get; }

    /// <summary>
    /// 记录投递的事件总线
    /// </summary>
    public RecordingEventBoxBus Bus { get; } = new();

    /// <summary>
    /// 服务提供器
    /// </summary>
    public ServiceProvider Provider { get; }

    /// <summary>
    /// 投递目标扫描器
    /// </summary>
    public OutboxDeliveryTargetScanner Scanner { get; }

    /// <summary>
    /// 待送数计数器
    /// </summary>
    public OutboxPendingEventCounter Counter { get; }

    /// <summary>
    /// 以测试夹具的依赖组装宿主
    /// </summary>
    /// <param name="context">发件箱测试夹具</param>
    /// <param name="batchSize">每轮预算</param>
    /// <returns>测试宿主</returns>
    public static OutboxTenantHost For(OutboxTestContext context, int batchSize = 10)
    {
        return new OutboxTenantHost(context.CurrentTenant, context.Resolver, context.ConnectionScope, context.TargetProvider, batchSize);
    }

    /// <summary>
    /// 执行一轮扫描
    /// </summary>
    /// <returns>本轮领取的事件总数</returns>
    public Task<int> RoundAsync()
    {
        return Scanner.SendRoundAsync(OutboxConfig, BatchSize, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 在指定租户上下文、独立作用域内入箱一条事件
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <returns>入箱的事件</returns>
    public async Task<OutgoingEventInfo> EnqueueAsync(long tenantId)
    {
        var info = new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1, 2, 3], DateTime.UtcNow);

        using var scope = Provider.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        using (currentTenant.Change(tenantId))
        {
            await scope.ServiceProvider.GetRequiredService<SqlSugarEventOutbox>().EnqueueAsync(info);
        }

        return info;
    }

    /// <summary>
    /// 释放服务提供器
    /// </summary>
    public void Dispose()
    {
        Provider.Dispose();
    }
}

/// <summary>
/// 只记录发件箱投递的事件总线替身
/// </summary>
internal sealed class RecordingEventBoxBus : IDistributedEventBus, ISupportsEventBoxes
{
    /// <summary>
    /// 已投递的事件
    /// </summary>
    public ConcurrentQueue<OutgoingEventInfo> Published { get; } = new();

    /// <summary>
    /// 读取租户上下文的探针，在投递时调用
    /// </summary>
    public Func<long?>? TenantProbe { get; set; }

    /// <summary>
    /// 设置探针后按投递顺序记录的事件标识与投递时的租户上下文
    /// </summary>
    public ConcurrentQueue<(Guid EventId, long? TenantId)> PublishedTenants { get; } = new();

    /// <summary>
    /// 记录单个投递
    /// </summary>
    /// <param name="outgoingEvent">出站事件</param>
    /// <param name="outboxConfig">发件箱配置</param>
    /// <returns>任务</returns>
    public Task PublishFromOutboxAsync(OutgoingEventInfo outgoingEvent, OutboxConfig outboxConfig)
    {
        Record(outgoingEvent);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 记录批量投递
    /// </summary>
    /// <param name="outgoingEvents">出站事件</param>
    /// <param name="outboxConfig">发件箱配置</param>
    /// <returns>任务</returns>
    public Task PublishManyFromOutboxAsync(IEnumerable<OutgoingEventInfo> outgoingEvents, OutboxConfig outboxConfig)
    {
        foreach (var outgoingEvent in outgoingEvents)
        {
            Record(outgoingEvent);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 记录一条投递及投递时的租户上下文
    /// </summary>
    /// <param name="outgoingEvent">出站事件</param>
    private void Record(OutgoingEventInfo outgoingEvent)
    {
        Published.Enqueue(outgoingEvent);

        if (TenantProbe is not null)
        {
            PublishedTenants.Enqueue((outgoingEvent.Id, TenantProbe()));
        }
    }

    public Task ProcessFromInboxAsync(IncomingEventInfo incomingEvent, InboxConfig inboxConfig)
    {
        throw new NotSupportedException();
    }

    public IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
    {
        throw new NotSupportedException();
    }

    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
    {
        throw new NotSupportedException();
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    public IDisposable Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IEventHandler, new()
    {
        throw new NotSupportedException();
    }

    public IDisposable Subscribe(Type eventType, IEventHandler handler)
    {
        throw new NotSupportedException();
    }

    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        throw new NotSupportedException();
    }

    public void Unsubscribe<TEvent>(Func<TEvent, Task> action)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    public void Unsubscribe(Type eventType, IEventHandler handler)
    {
        throw new NotSupportedException();
    }

    public void Unsubscribe<TEvent>(IEventHandlerFactory factory)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    public void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        throw new NotSupportedException();
    }

    public void UnsubscribeAll<TEvent>()
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    public void UnsubscribeAll(Type eventType)
    {
        throw new NotSupportedException();
    }
}
