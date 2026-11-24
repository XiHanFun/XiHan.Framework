// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 发件箱的死信：把指定的死信行重新置为待投递
/// </summary>
/// <remarks>
/// 死信行（尝试次数用尽或载荷无法解码）留在发件箱表里不再投递。排除了原因（代理恢复、主题建好）之后用 <see cref="RedriveManyAsync"/> 重投：行回到待投递，由投递循环在下一轮照常发出（ADR-0272）。不在任何调用里，自己在每个库上开工作单元，覆盖的库与投递循环相同：默认库在前，装配了租户分片时再逐个找分片库
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class OutboxDeadLetters
{
    /// <summary>
    /// 发件箱行所在的库
    /// </summary>
    private readonly OutboxDatabases _databases;

    /// <summary>
    /// 时间源
    /// </summary>
    /// <remarks>
    /// 重投的行的下一次投递时间取它的现在
    /// </remarks>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="services">根服务提供者，每个库的工作单元在它上面开新作用域</param>
    /// <param name="options">发件箱选项：是否纳入分片库与分片清单的重读间隔</param>
    /// <param name="timeProvider">时间源</param>
    /// <param name="loggerFactory">日志工厂</param>
    public OutboxDeadLetters(IServiceProvider services, OutboxOptions options, TimeProvider timeProvider, ILoggerFactory loggerFactory)
        : this(new OutboxDatabases(services, options, timeProvider), timeProvider, loggerFactory)
    {
    }

    /// <summary>
    /// 创建，库的清单由给定的对象提供
    /// </summary>
    /// <remarks>
    /// 发件箱特性把同一个 <see cref="OutboxDatabases"/> 交给投递循环、健康探针与死信重投，三处共用一份分片清单
    /// </remarks>
    internal OutboxDeadLetters(OutboxDatabases databases, TimeProvider timeProvider, ILoggerFactory loggerFactory)
    {
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<OutboxDeadLetters>();
    }

    /// <summary>
    /// 把这些死信行重新置为待投递
    /// </summary>
    /// <remarks>
    /// 逐项结果与入参同序、等长（<c>spec/conventions/batch-first.md</c>），单条是它的特例（传一个元素的列表）：
    /// <list type="bullet">
    /// <item><description>行在死信里：<see cref="OutboxMessage.DeadLetteredAt"/> 置空、<see cref="OutboxMessage.NextAttemptAt"/> 设为现在、<see cref="OutboxMessage.Attempts"/> 归零，该项成功。行重新获得完整的尝试次数，下一轮与首次投递的消息一样按主题成批发出；<see cref="OutboxMessage.LastError"/> 留着，直到下一次投递的结果覆盖它</description></item>
    /// <item><description>行存在但不在死信里（待投递、退避中或已经投递）：不改动，该项是 <see cref="MessagingErrors.OutboxNotDeadLettered"/></description></item>
    /// <item><description>哪个库里都没有这一行：该项是 <see cref="MessagingErrors.OutboxMessageNotFound"/></description></item>
    /// <item><description>写回时仓储逐项返回的失败原样作为该项的结果</description></item>
    /// <item><description>重复的标识：第一次按上面的规则处理，之后的每一次看见第一次的效果——第一次重投成功的，后面是 <see cref="MessagingErrors.OutboxNotDeadLettered"/>；第一次没有改动行的，后面与第一次相同</description></item>
    /// </list>
    /// 空集合返回空列表，不触存储。每个库各自一个工作单元，找到的行在所在的库里一次写回、随那个库的工作单元提交；全部标识都有了着落就不再看后面的库。一个库的存储失败（连不上、分片库没有发件箱表、提交失败）不挡其他库：这时还有标识没有着落就抛出（一个库失败抛出它的异常，多个库失败抛出 <see cref="AggregateException"/>），已经在别的库里重投的行保持已重投，重试时它们是 <see cref="MessagingErrors.OutboxNotDeadLettered"/>；全部标识都已经有着落则照常返回，失败的库只记一条警告。取消在进入每个库之前与库内的读写上生效，已经提交的库不回退
    /// </remarks>
    /// <param name="ids">要重投的发件箱行的标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>与 <paramref name="ids"/> 同序、等长的逐项结果</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ids"/> 为 <see langword="null"/></exception>
    public async ValueTask<IReadOnlyList<Result>> RedriveManyAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return [];
        }

        // 每个标识在入参里出现的位置，按首次出现的顺序；重复的标识只读写一次
        var positions = new Dictionary<Guid, List<int>>();
        var remaining = new List<Guid>();
        for (var index = 0; index < ids.Count; index++)
        {
            if (!positions.TryGetValue(ids[index], out var at))
            {
                positions[ids[index]] = at = [];
                remaining.Add(ids[index]);
            }

            at.Add(index);
        }

        var results = new Result[ids.Count];
        var redriven = 0;
        List<Exception>? failures = null;
        foreach (var shard in await _databases.ListAsync(_logger, cancellationToken).ConfigureAwait(false))
        {
            // 全部标识都有了着落：后面的库不用再看
            if (remaining.Count == 0)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<(Guid Id, Result Outcome)> located;
            try
            {
                IReadOnlyList<Guid> wanted = [.. remaining];
                var redrove = await _databases.RunAsync(shard, readOnly: false, (outbox, ct) => RedriveAsync(outbox, wanted, ct), _logger, cancellationToken).ConfigureAwait(false);
                // 提交返回的失败按存储失败处理：这个库里的改动已经回滚
                located = redrove.ValueOrThrow();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // 一个库的存储失败不挡其他库：要找的行可能在别的库里
                (failures ??= []).Add(exception);
                continue;
            }

            foreach (var (id, outcome) in located)
            {
                var at = positions[id];
                results[at[0]] = outcome;
                // 重复项看见第一次的效果：重投成功之后行已经不在死信里；第一次没有改动行的，再来一次结论相同
                var repeated = outcome.IsSuccess ? NotDeadLettered(id) : outcome;
                for (var occurrence = 1; occurrence < at.Count; occurrence++)
                {
                    results[at[occurrence]] = repeated;
                }

                if (outcome.IsSuccess)
                {
                    redriven++;
                }

                remaining.Remove(id);
            }
        }

        if (failures is not null)
        {
            if (remaining.Count > 0)
            {
                // 还有标识没有着落，而有的库没能看：说不清它们是不存在还是在那个库里，不伪装成「不存在」
                if (failures.Count == 1)
                {
                    ExceptionDispatchInfo.Throw(failures[0]);
                }

                throw new AggregateException("Redriving outbox dead letters failed on more than one database.", failures);
            }

            foreach (var failure in failures)
            {
                LogDatabaseSkipped(_logger, failure);
            }
        }

        foreach (var id in remaining)
        {
            var missing = Error.NotFound(MessagingErrors.OutboxMessageNotFound, MessagingMessages.OutboxMessageNotFound, MessageArguments.Of(("id", id.ToString())));
            foreach (var index in positions[id])
            {
                results[index] = missing;
            }
        }

        LogRedriven(_logger, redriven, ids.Count);
        return results;
    }

    /// <summary>
    /// 在一个库里找出这些行，把其中的死信行置为待投递并一次写回
    /// </summary>
    /// <remarks>
    /// 只返回在这个库里找到的行各自的结果；没找到的留给后面的库
    /// </remarks>
    private async ValueTask<Result<IReadOnlyList<(Guid Id, Result Outcome)>>> RedriveAsync(IRepository<OutboxMessage, Guid> outbox, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var rows = await outbox.FindManyAsync(ids, cancellationToken).ConfigureAwait(false);
        var located = new List<(Guid Id, Result Outcome)>(rows.Count);
        var updates = new List<OutboxMessage>(rows.Count);
        var now = _timeProvider.GetUtcNow();
        foreach (var row in rows)
        {
            if (row.DeadLetteredAt is null)
            {
                // 待投递、退避中或已经投递：不是重投的对象，不改动
                located.Add((row.Id, NotDeadLettered(row.Id)));
                continue;
            }

            // 回到刚写入时的状态：马上到期、尝试次数从头算。失败原因留着供排查
            row.DeadLetteredAt = null;
            row.NextAttemptAt = now;
            row.Attempts = 0;
            updates.Add(row);
        }

        if (updates.Count > 0)
        {
            var written = await outbox.UpdateRangeAsync(updates, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < updates.Count; index++)
            {
                located.Add((updates[index].Id, written[index]));
            }
        }

        return Result.Success<IReadOnlyList<(Guid Id, Result Outcome)>>(located);
    }

    /// <summary>
    /// 行不在死信里的错误
    /// </summary>
    private static Error NotDeadLettered(Guid id)
        => Error.Conflict(MessagingErrors.OutboxNotDeadLettered, MessagingMessages.OutboxNotDeadLettered, MessageArguments.Of(("id", id.ToString())));

    /// <summary>
    /// 记录一次重投的结果
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Put {Count} dead-lettered outbox message(s) back for dispatch out of {Requested} requested.")]
    private static partial void LogRedriven(ILogger logger, int count, int requested);

    /// <summary>
    /// 记录重投时有一个库没能读写
    /// </summary>
    /// <remarks>
    /// 要重投的行都在别的库里找到了，这个库的失败不影响结果
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Redriving outbox dead letters could not use one database; every requested message was found in another one.")]
    private static partial void LogDatabaseSkipped(ILogger logger, Exception exception);
}
