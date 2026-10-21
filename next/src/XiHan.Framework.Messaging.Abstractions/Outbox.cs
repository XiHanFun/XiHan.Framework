// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Domain;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 发件箱选项。从配置节 <c>XiHan:Messaging:Outbox</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record OutboxOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:Outbox";

    /// <summary>
    /// 轮询间隔；默认 1 秒。
    /// </summary>
    public TimeSpan PollInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "轮询间隔必须为正。");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 每轮最多投递条数；默认 100。
    /// </summary>
    public int BatchSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "批大小必须为正。");
    } = 100;

    /// <summary>
    /// 从配置读取：<c>PollIntervalSeconds</c>、<c>BatchSize</c>。
    /// </summary>
    public static OutboxOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new OutboxOptions();
        var interval = configuration[$"{ConfigurationSection}:PollIntervalSeconds"];
        if (interval is not null)
        {
            options = double.TryParse(interval, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { PollInterval = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:PollIntervalSeconds 必须是正数，实际 '{interval}'。");
        }

        var batch = configuration[$"{ConfigurationSection}:BatchSize"];
        if (batch is not null)
        {
            options = int.TryParse(batch, NumberStyles.None, CultureInfo.InvariantCulture, out var size) && size >= 1
                ? options with { BatchSize = size }
                : throw new ArgumentException($"{ConfigurationSection}:BatchSize 必须是正整数，实际 '{batch}'。");
        }

        return options;
    }
}

/// <summary>
/// 发件箱里的一条消息：与业务数据同事务写入，提交后投递。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class OutboxMessage : IEntity<Guid>, IHasCreationTime
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>
    /// 主题。
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// CloudEvents 结构化 JSON。
    /// </summary>
    [SuppressMessage("Performance", "CA1819:属性不应返回数组", Justification = "持久化字段，Provider 直接映射为二进制列。")]
    public byte[] Payload { get; set; } = [];

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 投递成功时间；未投递为 <see langword="null"/>。
    /// </summary>
    public DateTimeOffset? DispatchedAt { get; set; }

    /// <summary>
    /// 已尝试投递次数。
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// 最近一次失败原因。
    /// </summary>
    public string? LastError { get; set; }
}

/// <summary>
/// 事务性发布者：有活动工作单元时写入发件箱表（随业务事务提交或回滚），否则直接发布到代理。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class OutboxPublisher(IUnitOfWork unitOfWork, IRepository<OutboxMessage, Guid> outbox, IIdGenerator<Guid> ids, IMessageBroker broker, TimeProvider timeProvider) : IMessagePublisher
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IRepository<OutboxMessage, Guid> _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    private readonly IIdGenerator<Guid> _ids = ids ?? throw new ArgumentNullException(nameof(ids));
    private readonly IMessageBroker _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (!TopicName.IsValid(topic))
        {
            return Error.Validation(MessagingErrors.InvalidTopic, $"主题 '{topic}' 不合法。");
        }

        if (_unitOfWork.State != UnitOfWorkState.Active)
        {
            return await _broker.PublishAsync(topic, envelope, cancellationToken).ConfigureAwait(false);
        }

        var message = new OutboxMessage
        {
            Id = _ids.NewId(),
            Topic = topic,
            Payload = CloudEventsFormat.Encode(envelope),
            CreatedAt = _timeProvider.GetUtcNow(),
        };
        return await _outbox.AddAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// 发件箱投递循环：按创建时间取未投递的消息，发布到代理，成功记投递时间，失败记次数与原因；每轮一个工作单元。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class OutboxDispatcher(IServiceProvider services, IMessageBroker broker, OutboxOptions options, TimeProvider timeProvider, ILoggerFactory loggerFactory)
{
    private static readonly Specification<OutboxMessage> Pending = new(static m => m.DispatchedAt == null);

    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly IMessageBroker _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    private readonly OutboxOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ILogger _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<OutboxDispatcher>();

    /// <summary>
    /// 投递一轮；返回成功投递的条数。
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var outbox = scope.ServiceProvider.GetRequiredService<IRepository<OutboxMessage, Guid>>();
        await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, cancellationToken).ConfigureAwait(false);

        var dispatched = 0;
        try
        {
            var pending = await outbox.ListAsync(EntityQuery.All<OutboxMessage>().Where(Pending).OrderBy(static m => m.CreatedAt).Page(0, _options.BatchSize), cancellationToken).ConfigureAwait(false);
            foreach (var message in pending)
            {
                var decoded = CloudEventsFormat.Decode(message.Payload);
                var published = decoded.TryGetValue(out var envelope)
                    ? await _broker.PublishAsync(message.Topic, envelope, cancellationToken).ConfigureAwait(false)
                    : decoded.ToResult();

                message.Attempts++;
                if (published.IsSuccess)
                {
                    message.DispatchedAt = _timeProvider.GetUtcNow();
                    message.LastError = null;
                    dispatched++;
                }
                else
                {
                    message.LastError = published.Error?.ToString();
                    LogDispatchFailed(_logger, message.Id, message.Topic, message.Attempts, published.Error?.Code ?? "?");
                }

                (await outbox.UpdateAsync(message, cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            }

            (await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            return dispatched;
        }
        catch
        {
            if (unitOfWork.State == UnitOfWorkState.Active)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>
    /// 按轮询间隔持续投递，直到取消。单轮异常记录后继续。
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogRoundFailed(_logger, exception);
            }

            try
            {
                await Task.Delay(_options.PollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "发件箱消息 {MessageId}（主题 {Topic}）第 {Attempts} 次投递失败：{ErrorCode}")]
    private static partial void LogDispatchFailed(ILogger logger, Guid messageId, string topic, int attempts, string errorCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "发件箱投递一轮失败，下一轮继续。")]
    private static partial void LogRoundFailed(ILogger logger, Exception exception);
}

/// <summary>
/// 事务性发件箱特性：需要 <c>messaging.queue@1</c> 与 <c>persistence.relational@1</c>；把 <see cref="IMessagePublisher"/> 换成 <see cref="OutboxPublisher"/>，
/// 登记 <see cref="OutboxMessage"/> 实体，启动后运行投递循环。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class OutboxFeature : IHostedFeature, IDisposable
{
    private CancellationTokenSource? _stopping;
    private Task? _loop;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.messaging.outbox",
        Package = "XiHan.Framework.Messaging.Abstractions",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
        ],
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Messaging") },
        Entry = typeof(OutboxFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddSingleton(OutboxOptions.FromConfiguration(context.Configuration));
        context.Services.AddRepository<OutboxMessage, Guid>();
        context.Services.Replace(ServiceDescriptor.Scoped<IMessagePublisher, OutboxPublisher>());
        context.Services.TryAddSingleton<OutboxDispatcher>();
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _stopping = new CancellationTokenSource();
        _loop = services.GetRequiredService<OutboxDispatcher>().RunAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stopping is null || _loop is null)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        await _loop.ConfigureAwait(false);
        _stopping.Dispose();
        _stopping = null;
        _loop = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _stopping = null;
    }
}
