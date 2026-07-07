#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:KafkaDistributedEventBus
// Guid:5a775554-d210-4806-abbb-1250843f1b88
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
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

namespace XiHan.Framework.EventBus.Kafka;

/// <summary>
/// 基于 Kafka 的分布式事件总线
/// </summary>
/// <remarks>
/// 发布：所有事件写入同一主题，以事件名作为消息 Key、事件数据作为 Value、messageId/correlationId 放入 Header。
/// 消费：同一消费者组竞争消费，保证分布式事件在集群中只被处理一次；手动提交偏移。
/// </remarks>
[ExposeServices(typeof(IDistributedEventBus), typeof(KafkaDistributedEventBus))]
public class KafkaDistributedEventBus : BrokerDistributedEventBusBase, ISingletonDependency, IAsyncDisposable
{
    private const string MessageIdHeaderName = "messageId";

    private readonly XiHanKafkaEventBusOptions _options;
    private readonly ILogger<KafkaDistributedEventBus> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IProducer<string, byte[]>? _producer;
    private IConsumer<string, byte[]>? _consumer;
    private CancellationTokenSource? _consumeCts;
    private Task? _consumeTask;
    private volatile bool _initialized;

    /// <summary>
    /// 构造函数
    /// </summary>
    public KafkaDistributedEventBus(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanDistributedEventBusOptions> distributedEventBusOptions,
        IOptions<XiHanKafkaEventBusOptions> kafkaOptions,
        IDistributedIdGenerator<Guid> guidGenerator,
        IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<KafkaDistributedEventBus> logger)
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
        _options = kafkaOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// 初始化：确保主题、建立生产者与消费者并启动消费循环
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

            if (_options.EnsureTopicExists)
            {
                await EnsureTopicExistsAsync();
            }

            _producer = new ProducerBuilder<string, byte[]>(new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true
            }).Build();

            _consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                GroupId = _options.GroupId,
                EnableAutoCommit = false,
                AutoOffsetReset = ParseOffsetReset(_options.AutoOffsetReset)
            }).Build();

            _consumeCts = new CancellationTokenSource();
            _consumeTask = Task.Factory.StartNew(
                () => ConsumeLoopAsync(_consumeCts.Token),
                _consumeCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();

            _initialized = true;
            _logger.LogInformation("Kafka 分布式事件总线已初始化：servers={Servers}, topic={Topic}, group={Group}",
                _options.BootstrapServers, _options.TopicName, _options.GroupId);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 把事件推入 Kafka 主题
    /// </summary>
    protected override async Task PublishToBrokerAsync(string eventName, byte[] body, string? messageId, string? correlationId)
    {
        if (!_initialized || _producer is null)
        {
            await InitializeAsync();
        }

        var headers = new Headers();
        if (!string.IsNullOrEmpty(messageId))
        {
            headers.Add(MessageIdHeaderName, Encoding.UTF8.GetBytes(messageId));
        }

        if (!string.IsNullOrEmpty(correlationId))
        {
            headers.Add(EventBusConsts.CorrelationIdHeaderName, Encoding.UTF8.GetBytes(correlationId));
        }

        var result = await _producer!.ProduceAsync(_options.TopicName, new Message<string, byte[]>
        {
            Key = eventName,
            Value = body,
            Headers = headers
        });

        if (result.Status != PersistenceStatus.Persisted)
        {
            _logger.LogWarning("Kafka 消息未确认持久化，事件名={EventName}, 状态={Status}", eventName, result.Status);
        }
    }

    /// <summary>
    /// 释放生产者与消费者
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

            _consumer?.Close();
            _consumer?.Dispose();

            _producer?.Flush(TimeSpan.FromSeconds(5));
            _producer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放 Kafka 资源时发生异常");
        }

        _consumeCts?.Dispose();
        _initLock.Dispose();
    }

    /// <summary>
    /// 解析首次消费偏移策略
    /// </summary>
    private static AutoOffsetReset ParseOffsetReset(string value)
    {
        return value?.ToLowerInvariant() switch
        {
            "latest" => AutoOffsetReset.Latest,
            "error" => AutoOffsetReset.Error,
            _ => AutoOffsetReset.Earliest
        };
    }

    /// <summary>
    /// 从消息头读取字符串值
    /// </summary>
    private static string? GetHeader(Message<string, byte[]> message, string key)
    {
        return message.Headers.TryGetLastBytes(key, out var bytes)
            ? Encoding.UTF8.GetString(bytes)
            : null;
    }

    /// <summary>
    /// 确保主题存在（不存在则创建，已存在则忽略）
    /// </summary>
    private async Task EnsureTopicExistsAsync()
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = _options.BootstrapServers
        }).Build();

        try
        {
            await admin.CreateTopicsAsync(
            [
                new TopicSpecification
                {
                    Name = _options.TopicName,
                    NumPartitions = _options.TopicPartitionCount,
                    ReplicationFactor = _options.TopicReplicationFactor
                }
            ]);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // 主题已存在，忽略
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "确保 Kafka 主题存在时发生异常，topic={Topic}", _options.TopicName);
        }
    }

    /// <summary>
    /// 消费循环
    /// </summary>
    private async Task ConsumeLoopAsync(CancellationToken cancellationToken)
    {
        _consumer!.Subscribe(_options.TopicName);

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, byte[]>? consumeResult = null;
            try
            {
                consumeResult = _consumer.Consume(cancellationToken);
                if (consumeResult?.Message is null)
                {
                    continue;
                }

                var message = consumeResult.Message;
                await ProcessIncomingMessageAsync(
                    GetHeader(message, MessageIdHeaderName),
                    message.Key,
                    GetHeader(message, EventBusConsts.CorrelationIdHeaderName),
                    message.Value);

                _consumer.Commit(consumeResult);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 Kafka 事件失败，topic={Topic}", _options.TopicName);

                // 提交偏移，避免毒消息阻塞分区；可靠重试由收件箱负责（若已配置）
                if (consumeResult is not null)
                {
                    try
                    {
                        _consumer.Commit(consumeResult);
                    }
                    catch (Exception commitEx)
                    {
                        _logger.LogWarning(commitEx, "提交 Kafka 偏移失败");
                    }
                }
            }
        }
    }
}
