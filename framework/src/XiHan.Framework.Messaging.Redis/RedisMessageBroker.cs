// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Redis;

/// <summary>
/// 基于 Redis Streams 的消息代理
/// </summary>
/// <remarks>
/// 每个主题一条流 <c>&lt;KeyPrefix&gt;&lt;topic&gt;</c>，条目字段 <c>envelope</c> 是 CloudEvents 结构化 JSON（ADR-0130）。流只由订阅方建立：发布以 <c>NOMKSTREAM</c> 与 <c>MAXLEN ~</c> 写入，流不存在（没有订阅者）时丢弃并记 Debug 日志，发布照常成功；批量发布在一个 <c>MULTI</c> / <c>EXEC</c> 事务里，整批写入或整批不写入。每个订阅一个消费循环，竞争订阅在消费组 <see cref="RedisMessagingOptions.GroupName"/> 里，广播订阅在本实例独占的组 <c>&lt;GroupName&gt;@&lt;InstanceId&gt;</c> 里；失败原位重试，到上限竞争订阅进死信流，广播订阅丢弃；超时未确认的条目由 <c>XAUTOCLAIM</c> 回收
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class RedisMessageBroker : IMessageBroker, IAsyncDisposable
{
    /// <summary>
    /// 条目里信封的字段名
    /// </summary>
    internal const string EnvelopeField = "envelope";

    /// <summary>
    /// 死信条目里最后一次尝试号的字段名
    /// </summary>
    internal const string AttemptField = "attempt";

    /// <summary>
    /// 死信条目里最后一次失败的错误码的字段名
    /// </summary>
    internal const string ErrorField = "error";

    /// <summary>
    /// Provider 选项
    /// </summary>
    private readonly RedisMessagingOptions _options;

    /// <summary>
    /// 消息选项：最多投递次数与死信后缀
    /// </summary>
    private readonly MessagingOptions _messaging;

    /// <summary>
    /// 本实例的标识
    /// </summary>
    /// <remarks>
    /// 消费者名与广播组名里带它
    /// </remarks>
    private readonly MessagingInstance _instance;

    /// <summary>
    /// 应用：进入服务状态前不派发
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 时间源
    /// </summary>
    /// <remarks>
    /// 驱动消费循环的轮询与维护周期
    /// </remarks>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 日志
    /// </summary>
    /// <remarks>
    /// 消费循环共用这个类别
    /// </remarks>
    private readonly ILogger _logger;

    /// <summary>
    /// 建立连接
    /// </summary>
    /// <remarks>
    /// 缺省按选项连接；测试换成别的连接方式
    /// </remarks>
    private readonly Func<CancellationToken, Task<IConnectionMultiplexer>> _connect;

    /// <summary>
    /// 串行化建立连接的信号量
    /// </summary>
    /// <remarks>
    /// 发布与启动可能同时触发连接，只建一个
    /// </remarks>
    private readonly SemaphoreSlim _connectGate = new(1, 1);

    /// <summary>
    /// 每个订阅的消费与它的消费循环
    /// </summary>
    /// <remarks>
    /// 停止时等循环结束后逐个清理
    /// </remarks>
    private readonly List<(RedisStreamConsumer Consumer, Task Loop)> _consumers = [];

    /// <summary>
    /// 连接
    /// </summary>
    /// <remarks>
    /// <see cref="ConnectAsync"/> 建立，释放时关闭
    /// </remarks>
    private IConnectionMultiplexer? _connection;

    /// <summary>
    /// 停止信号
    /// </summary>
    /// <remarks>
    /// 取消后消费循环不再取新条目，进行中的投递不再确认
    /// </remarks>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 创建代理
    /// </summary>
    /// <remarks>
    /// 连接在 <see cref="ConnectAsync"/> 时建立
    /// </remarks>
    public RedisMessageBroker(RedisMessagingOptions options, MessagingOptions messaging, MessagingInstance instance, App app, TimeProvider timeProvider, ILoggerFactory loggerFactory)
        : this(options, messaging, instance, app, timeProvider, loggerFactory, null)
    {
    }

    /// <summary>
    /// 创建代理，连接由给定的委托建立
    /// </summary>
    /// <remarks>
    /// 委托为 <see langword="null"/> 时按选项连接
    /// </remarks>
    internal RedisMessageBroker(RedisMessagingOptions options, MessagingOptions messaging, MessagingInstance instance, App app, TimeProvider timeProvider, ILoggerFactory loggerFactory, Func<CancellationToken, Task<IConnectionMultiplexer>>? connect)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = loggerFactory.CreateLogger<RedisMessageBroker>();
        _connect = connect ?? (async _ => await ConnectionMultiplexer.ConnectAsync(ConnectionOptions(_options)).ConfigureAwait(false));
    }

    /// <summary>
    /// 是否已连接
    /// </summary>
    internal bool IsConnected => _connection?.IsConnected == true;

    /// <summary>
    /// 主题对应的流键
    /// </summary>
    internal static RedisKey KeyOf(RedisMessagingOptions options, string topic) => options.KeyPrefix + topic;

    /// <summary>
    /// 把选项换成客户端的连接选项
    /// </summary>
    /// <remarks>
    /// 连接字符串给出地址与缺省凭据；选项里的用户名与密码（来自绑定的属性，RFC-0016）以属性设置覆盖，不改写字符串
    /// </remarks>
    internal static ConfigurationOptions ConnectionOptions(RedisMessagingOptions options)
    {
        // 直接往字符串里拼 password= 遇到含逗号的密码会把字符串拆错，所以解析后按属性覆盖
        var parsed = ConfigurationOptions.Parse(options.Configuration);
        if (options.User is { } user)
        {
            parsed.User = user;
        }

        if (options.Password is { } password)
        {
            parsed.Password = password;
        }

        return parsed;
    }

    /// <summary>
    /// 连通性探针（健康项）
    /// </summary>
    /// <remarks>
    /// 对连接发一次 <c>PING</c>。还没建立连接时返回 <see cref="MessagingErrors.BrokerUnavailable"/>；连接断开时 <c>PING</c> 抛出连接或超时异常，由探针宿主记成 Degraded
    /// </remarks>
    internal async ValueTask<Result> ProbeAsync(CancellationToken cancellationToken)
    {
        if (_connection is not { } connection)
        {
            return Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", "The connection has not been established.")));
        }

        await connection.GetDatabase().PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// 建立连接
    /// </summary>
    /// <remarks>
    /// 连不上视为失败，不降级运行；连接字符串里写了 <c>abortConnect=false</c> 也一样，连接没有建立就抛出。可重复调用。连上后读一次 <c>INFO memory</c>，<c>maxmemory-policy</c> 会淘汰流的键（<c>allkeys-*</c>）时记 Warning
    /// </remarks>
    internal async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return;
        }

        await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is not null)
            {
                return;
            }

            IConnectionMultiplexer connection;
            try
            {
                connection = await _connect(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                throw Unavailable("Failed to connect to Redis", exception);
            }

            if (!connection.IsConnected)
            {
                await connection.CloseAsync().ConfigureAwait(false);
                connection.Dispose();
                throw new FrameworkException(MessagingErrors.BrokerUnavailable, "Failed to connect to Redis: no server is reachable.");
            }

            await WarnIfEvictingAsync(connection).ConfigureAwait(false);
            _connection = connection;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 等到 Redis 应答才返回成功；没有订阅者（流不存在）时丢弃并成功；连接或超时异常抛出携带 <see cref="MessagingErrors.BrokerUnavailable"/> 的 <see cref="FrameworkException"/>
    /// </remarks>
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        TopicName.Validate(topic);
        return (await PublishManyAsync(topic, [envelope], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <inheritdoc />
    /// <remarks>
    /// 多条时在一个 <c>MULTI</c> / <c>EXEC</c> 事务里连续 <c>XADD</c>：一次往返，主题内保持入参顺序，整批写入或整批不写入。逐项结果都是成功（写入，或没有订阅者而丢弃）；连接或超时异常让整批抛出
    /// </remarks>
    public async ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        TopicName.Validate(topic);
        if (envelopes.Count == 0)
        {
            return [];
        }

        // 先把全部信封编码好：任一信封为空在触及 Redis 前就失败
        var entries = new NameValueEntry[envelopes.Count][];
        for (var index = 0; index < envelopes.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(envelopes[index]);
            entries[index] = [new(EnvelopeField, CloudEventsFormat.Encode(envelopes[index]))];
        }

        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var key = KeyOf(_options, topic);
        // NOMKSTREAM：流只由订阅方建组时建出，键不存在就是没有订阅者，XADD 返回空、什么也不写
        var add = new StreamAddOptions { MaxLength = _options.MaxLength, Approximate = true, CreateStream = false };
        var database = _connection!.GetDatabase();
        RedisValue[] ids;
        try
        {
            if (entries.Length == 1)
            {
                ids = [await database.StreamAddAsync(key, entries[0], add).WaitAsync(cancellationToken).ConfigureAwait(false)];
            }
            else
            {
                // 事务里的命令一起执行、不与别的客户端交错：整批的条目在流里连续、按入参顺序，要么都写入要么都没写入
                var transaction = database.CreateTransaction();
                var pending = entries.Select(fields => transaction.StreamAddAsync(key, fields, add)).ToArray();
                await transaction.ExecuteAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                ids = await Task.WhenAll(pending).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable($"Failed to publish to stream '{key}'", exception);
        }

        var dropped = ids.Count(static id => id.IsNull);
        if (dropped > 0)
        {
            LogDropped(_logger, dropped, topic);
        }

        var results = new Result[ids.Length];
        Array.Fill(results, Result.Success());
        return results;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 先为每个订阅建好消费组与本实例的消费者（竞争订阅的组 <see cref="RedisMessagingOptions.GroupName"/>，广播订阅的组 <c>&lt;GroupName&gt;@&lt;InstanceId&gt;</c>，都从 <c>$</c> 开始），再起消费循环：返回之后发布的消息不会漏
    /// </remarks>
    public async Task StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, MessageDispatch dispatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(dispatch);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var database = _connection!.GetDatabase();
        var consumers = subscriptions.Select(subscription => new RedisStreamConsumer(database, _options, _messaging, _instance, _app, _timeProvider, _logger, subscription)).ToList();
        try
        {
            // 各订阅的建组相互独立，并发发出，一次往返
            await Task.WhenAll(consumers.Select(static consumer => consumer.EnsureGroupAsync())).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable("Failed to create the consumer groups for the subscriptions", exception);
        }

        _stopping = new CancellationTokenSource();
        var stopping = _stopping.Token;
        // 每个订阅一个循环，在线程池上运行；循环只有停止信号能让它退出
        _consumers.AddRange(consumers.Select(consumer => (consumer, Task.Run(() => consumer.RunAsync(dispatch, stopping), CancellationToken.None))));
    }

    /// <inheritdoc />
    /// <remarks>
    /// 发停止信号并等全部消费循环结束，再清理本实例在流上的痕迹：广播订阅删除自己的组，竞争订阅在没有待确认条目时删除自己的消费者。清理失败只记日志，遗留的组与消费者由别的实例维护时删除
    /// </remarks>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stopping is not null)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
        }

        if (_consumers.Count > 0)
        {
            await Task.WhenAll(_consumers.Select(static c => c.Loop)).WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.WhenAll(_consumers.Select(static c => c.Consumer.ReleaseAsync())).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                LogReleaseFailed(_logger, exception);
            }
        }

        _consumers.Clear();
        _stopping?.Dispose();
        _stopping = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        if (_connection is not null)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            _connection.Dispose();
            _connection = null;
        }

        _connectGate.Dispose();
    }

    /// <summary>
    /// 淘汰策略会删掉流的键时记 Warning
    /// </summary>
    /// <remarks>
    /// 读 <c>INFO memory</c> 的 <c>maxmemory_policy</c>。<c>allkeys-*</c> 在内存满时可以淘汰任何键，流连同未投递的消息一起消失；<c>noeviction</c> 与 <c>volatile-*</c> 不会（流的键没有过期时间）。StackExchange.Redis 只在管理模式下放行 <c>INFO</c> 命令，所以经脚本读；集群里只读到脚本落到的那个节点。读不到时只记 Debug
    /// </remarks>
    private async Task WarnIfEvictingAsync(IConnectionMultiplexer connection)
    {
        string? info;
        try
        {
            info = (string?)await connection.GetDatabase().ScriptEvaluateAsync(RedisStreamScripts.MemoryInfo).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            LogPolicyUnknown(_logger, exception);
            return;
        }

        const string Field = "maxmemory_policy:";
        var policy = info?.Split('\n').Select(static line => line.Trim()).FirstOrDefault(static line => line.StartsWith(Field, StringComparison.Ordinal))?[Field.Length..];
        if (policy is not null && policy.StartsWith("allkeys-", StringComparison.Ordinal))
        {
            LogEvictingPolicy(_logger, policy);
        }
    }

    /// <summary>
    /// 把客户端异常包装成携带 <see cref="MessagingErrors.BrokerUnavailable"/> 的框架异常
    /// </summary>
    /// <remarks>
    /// 框架异常的消息会进发件箱行与错误（ADR-0108、ADR-0115）：只写框架自己的文字，客户端的异常（可能带主机与端口）放在 <see cref="Exception.InnerException"/>
    /// </remarks>
    private static FrameworkException Unavailable(string what, Exception exception)
        => new(MessagingErrors.BrokerUnavailable, $"{what}.", exception);

    /// <summary>
    /// 记录发布到没有订阅者的主题的消息被丢弃
    /// </summary>
    /// <remarks>
    /// Debug 级别：调度、工作流等框架事件照常发布到应用不一定订阅的主题，这是常态；主题名写错或订阅方还没启动过时打开本类别的 Debug 日志排查
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped {Count} message(s) published to topic '{Topic}' because its stream does not exist (nothing subscribes to it).")]
    private static partial void LogDropped(ILogger logger, int count, string topic);

    /// <summary>
    /// 记录淘汰策略会删掉流的键
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis uses maxmemory-policy '{Policy}', which can evict message streams together with undelivered messages; use 'noeviction' or a 'volatile-*' policy for messaging.")]
    private static partial void LogEvictingPolicy(ILogger logger, string policy);

    /// <summary>
    /// 记录读不到淘汰策略
    /// </summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read the maxmemory-policy of Redis.")]
    private static partial void LogPolicyUnknown(ILogger logger, Exception exception);

    /// <summary>
    /// 记录停止时清理组或消费者失败
    /// </summary>
    /// <remarks>
    /// 停止流程继续，遗留的组与消费者由别的实例维护时删除
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Removing this instance's consumers or broadcast groups from Redis failed while stopping; other instances remove them during maintenance.")]
    private static partial void LogReleaseFailed(ILogger logger, Exception exception);
}
