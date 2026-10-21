// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Domain;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 发件箱投递循环：按创建时间取未投递的消息，发布到代理，成功记投递时间，失败记次数与原因；每轮一个工作单元。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class OutboxDispatcher(IServiceProvider services, IMessageBroker broker, OutboxOptions options, TimeProvider timeProvider, ILoggerFactory loggerFactory)
{
    private static readonly Specification<OutboxMessage> _pending = new(static m => m.DispatchedAt == null);

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
            var pending = await outbox.ListAsync(EntityQuery.All<OutboxMessage>().Where(_pending).OrderBy(static m => m.CreatedAt).Page(0, _options.BatchSize), cancellationToken).ConfigureAwait(false);
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
