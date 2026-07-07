#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RedisDistributedEventBus
// Guid:4e2a14b0-df4f-485e-8d6c-806ea9b1885f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Uow;

namespace XiHan.Framework.EventBus.Redis;

/// <summary>
/// 基于 Redis Streams 的分布式事件总线
/// </summary>
/// <remarks>
/// 发布：以 <c>XADD</c> 把事件写入同一 Stream，字段含事件名 / messageId / correlationId / 数据。
/// 消费：通过消费者组 <c>XREADGROUP</c> 竞争消费，处理成功后 <c>XACK</c>，保证分布式事件在集群中只被处理一次。
/// 独立连接（与缓存各连各的），带近似长度裁剪防止 Stream 无限增长。
/// </remarks>
[ExposeServices(typeof(IDistributedEventBus), typeof(RedisDistributedEventBus))]
public class RedisDistributedEventBus : BrokerDistributedEventBusBase, ISingletonDependency, IAsyncDisposable
{
    private const string FieldEvent = "event";
    private const string FieldMessageId = "mid";
    private const string FieldCorrelationId = "cid";
    private const string FieldData = "data";

    private readonly XiHanRedisEventBusOptions _options;
    private readonly ILogger<RedisDistributedEventBus> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly string _consumerName = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    private IConnectionMultiplexer? _connection;
    private CancellationTokenSource? _consumeCts;
    private Task? _consumeTask;
    private volatile bool _initialized;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RedisDistributedEventBus(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanDistributedEventBusOptions> distributedEventBusOptions,
        IOptions<XiHanRedisEventBusOptions> redisOptions,
        IDistributedIdGenerator<Guid> guidGenerator,
        IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<RedisDistributedEventBus> logger)
        : base(serviceScopeFactory,
            currentTenant,
            unitOfWorkManager,
            distributedEventBusOptions,
            guidGenerator,
            clock,
            eventHandlerInvoker,
            localEventBus,
            correlationIdProvider)
    {
        _options = redisOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// 初始化：建立连接、创建消费者组并启动消费循环
    /// </summary>
    public override async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            _connection = await ConnectionMultiplexer.ConnectAsync(_options.Configuration);
            await EnsureConsumerGroupAsync();

            _consumeCts = new CancellationTokenSource();
            _consumeTask = Task.Factory.StartNew(
                () => ConsumeLoopAsync(_consumeCts.Token),
                _consumeCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();

            _initialized = true;
            _logger.LogInformation("Redis 分布式事件总线已初始化：stream={Stream}, group={Group}, consumer={Consumer}",
                _options.StreamKey, _options.ConsumerGroup, _consumerName);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 把事件写入 Redis Stream
    /// </summary>
    protected override async Task PublishToBrokerAsync(string eventName, byte[] body, string? messageId, string? correlationId)
    {
        if (!_initialized || _connection is null)
        {
            await InitializeAsync();
        }

        var db = _connection!.GetDatabase();
        var fields = new NameValueEntry[]
        {
            new(FieldEvent, eventName),
            new(FieldMessageId, messageId ?? string.Empty),
            new(FieldCorrelationId, correlationId ?? string.Empty),
            new(FieldData, body)
        };

        if (_options.MaxStreamLength > 0)
        {
            await db.StreamAddAsync(
                _options.StreamKey,
                fields,
                messageId: null,
                maxLength: _options.MaxStreamLength,
                useApproximateMaxLength: true);
        }
        else
        {
            await db.StreamAddAsync(_options.StreamKey, fields);
        }
    }

    /// <summary>
    /// 释放连接与消费循环
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        try
        {
            if (_consumeCts is not null)
            {
                await _consumeCts.CancelAsync();
            }

            if (_consumeTask is not null)
            {
                try
                {
                    await _consumeTask;
                }
                catch (OperationCanceledException)
                {
                    // 正常退出
                }
            }

            if (_connection is not null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放 Redis 连接时发生异常");
        }

        _consumeCts?.Dispose();
        _initLock.Dispose();
    }

    /// <summary>
    /// 确保消费者组存在（幂等）
    /// </summary>
    private async Task EnsureConsumerGroupAsync()
    {
        var db = _connection!.GetDatabase();
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                _options.StreamKey,
                _options.ConsumerGroup,
                StreamPosition.NewMessages,
                createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            // 消费者组已存在，忽略
        }
    }

    /// <summary>
    /// 消费循环：XREADGROUP → 处理 → XACK
    /// </summary>
    private async Task ConsumeLoopAsync(CancellationToken cancellationToken)
    {
        var db = _connection!.GetDatabase();

        while (!cancellationToken.IsCancellationRequested)
        {
            StreamEntry[] entries;
            try
            {
                entries = await db.StreamReadGroupAsync(
                    _options.StreamKey,
                    _options.ConsumerGroup,
                    _consumerName,
                    StreamPosition.NewMessages,
                    _options.ReadBatchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取 Redis Stream 失败，stream={Stream}", _options.StreamKey);
                await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
                continue;
            }

            if (entries is null || entries.Length == 0)
            {
                await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
                continue;
            }

            foreach (var entry in entries)
            {
                try
                {
                    var eventName = GetField(entry, FieldEvent).ToString();
                    var messageId = GetField(entry, FieldMessageId);
                    var correlationId = GetField(entry, FieldCorrelationId);
                    var body = (byte[]?)GetField(entry, FieldData) ?? [];

                    await ProcessIncomingMessageAsync(
                        messageId.IsNullOrEmpty ? null : messageId.ToString(),
                        eventName,
                        correlationId.IsNullOrEmpty ? null : correlationId.ToString(),
                        body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理 Redis 事件失败，stream={Stream}", _options.StreamKey);
                    // 仍 XACK：避免毒消息滞留 PEL；可靠重试由收件箱负责（若已配置）
                }
                finally
                {
                    await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id);
                }
            }
        }
    }

    /// <summary>
    /// 读取 Stream 条目中的指定字段
    /// </summary>
    private static RedisValue GetField(StreamEntry entry, string name)
    {
        foreach (var pair in entry.Values)
        {
            if (pair.Name == name)
            {
                return pair.Value;
            }
        }

        return RedisValue.Null;
    }
}
