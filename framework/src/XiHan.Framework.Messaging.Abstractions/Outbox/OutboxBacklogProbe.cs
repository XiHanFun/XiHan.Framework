// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 健康项 <see cref="OutboxFeature.HealthName"/> 的探针
/// </summary>
/// <remarks>
/// 在默认库与每个分片库上各开一个只读工作单元（与投递循环覆盖同一批库，<see cref="OutboxDatabases"/>），数出未投递的行数、最老一条未投递的行的创建时间与死信行数，各库相加后与 <see cref="OutboxOptions"/> 的三个阈值比较（ADR-0272）。不经管道：启动期应用还不在服务状态时也要能探。只读工作单元配了从库时读从库，数字落后一个复制延迟
/// <para>
/// 任何一个库读不出（连不上、没有发件箱表、探测超时）即整次探测失败：异常原样抛出，由探针宿主记成 Degraded（<c>KERNEL_UNAVAILABLE_005</c>），上一次数出的数字随之作废，不再作为健康数据展示
/// </para>
/// </remarks>
internal sealed class OutboxBacklogProbe
{
    /// <summary>
    /// 发件箱行所在的库
    /// </summary>
    private readonly OutboxDatabases _databases;

    /// <summary>
    /// 发件箱选项：三个健康阈值
    /// </summary>
    private readonly OutboxOptions _options;

    /// <summary>
    /// 时间源
    /// </summary>
    /// <remarks>
    /// 算最老一条等了多久
    /// </remarks>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 日志
    /// </summary>
    /// <remarks>
    /// 只记分片清单读取失败与收尾（回滚、释放作用域）的失败；探到的异常由探针宿主记
    /// </remarks>
    private readonly ILogger _logger;

    /// <summary>
    /// 最近一次成功数出的状态
    /// </summary>
    /// <remarks>
    /// 还没探过、或最近一次探测失败时为空。整个替换，读它不加锁
    /// </remarks>
    private volatile OutboxBacklog? _backlog;

    /// <summary>
    /// 创建探针
    /// </summary>
    public OutboxBacklogProbe(OutboxDatabases databases, OutboxOptions options, TimeProvider timeProvider, ILogger logger)
    {
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 最近一次成功数出的状态
    /// </summary>
    /// <remarks>
    /// 还没探过、或最近一次探测失败时为 <see langword="null"/>
    /// </remarks>
    public OutboxBacklog? Backlog => _backlog;

    /// <summary>
    /// 数一遍各库的发件箱并与阈值比较
    /// </summary>
    /// <remarks>
    /// 积压超过阈值返回 <see cref="MessagingErrors.OutboxBacklog"/>，否则死信超过阈值返回 <see cref="MessagingErrors.OutboxDeadLettered"/>，都没超过返回成功；两样都超过时报告积压，死信的数字仍在健康数据里。仓储与工作单元抛出的异常原样传播
    /// </remarks>
    public async ValueTask<Result> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var backlog = await CountAsync(cancellationToken).ConfigureAwait(false);
            _backlog = backlog;
            return Evaluate(backlog);
        }
        catch
        {
            // 读不出时上一次的数字已经不能代表现在：清掉，健康数据里不留过期的数
            _backlog = null;
            throw;
        }
    }

    /// <summary>
    /// 健康数据：最近一次成功数出的数字
    /// </summary>
    /// <remarks>
    /// <c>pending</c>、<c>oldestPendingSeconds</c>（没有未投递的行为 <see langword="null"/>）、<c>deadLettered</c> 与 <c>databases</c>；没有可用的数字时为空表
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Data()
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_backlog is { } backlog)
        {
            data["pending"] = backlog.Pending;
            data["oldestPendingSeconds"] = backlog.OldestPendingAge is { } age ? Seconds(age) : null;
            data["deadLettered"] = backlog.DeadLettered;
            data["databases"] = backlog.Databases;
        }

        return data;
    }

    /// <summary>
    /// 在每个库上数一遍，各库相加
    /// </summary>
    private async ValueTask<OutboxBacklog> CountAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        long pending = 0;
        long deadLettered = 0;
        DateTimeOffset? oldest = null;
        var databases = await _databases.ListAsync(_logger, cancellationToken).ConfigureAwait(false);
        foreach (var shard in databases)
        {
            var counted = await _databases.RunAsync(
                shard,
                readOnly: true,
                static async (outbox, ct) =>
                {
                    var undelivered = EntityQuery.All<OutboxMessage>().Where(new Specification<OutboxMessage>(static m => m.DispatchedAt == null && m.DeadLetteredAt == null));
                    var pendingRows = await outbox.CountAsync(undelivered, ct).ConfigureAwait(false);
                    // 最老的一条走与投递循环相同的索引顺序，只取一行；没有未投递的行就不查
                    var first = pendingRows == 0 ? [] : await outbox.ListAsync(undelivered.OrderBy(static m => m.CreatedAt).Page(0, 1), ct).ConfigureAwait(false);
                    // 死信行从没有投递成功过：带上「未投递」这个条件，计数与上面两次查询一样落在 (DispatchedAt, DeadLetteredAt, CreatedAt) 索引的前缀上
                    var deadRows = await outbox.CountAsync(EntityQuery.All<OutboxMessage>().Where(new Specification<OutboxMessage>(static m => m.DispatchedAt == null && m.DeadLetteredAt != null)), ct).ConfigureAwait(false);
                    return Result.Success((Pending: pendingRows, Oldest: first.Count == 0 ? (DateTimeOffset?)null : first[0].CreatedAt, DeadLettered: deadRows));
                },
                _logger,
                cancellationToken).ConfigureAwait(false);

            // 只读工作单元的提交返回失败同样是读不出：按存储失败抛出
            var (pendingRows, oldestRow, deadRows) = counted.ValueOrThrow();
            pending += pendingRows;
            deadLettered += deadRows;
            if (oldestRow is { } created && (oldest is not { } known || created < known))
            {
                oldest = created;
            }
        }

        // 时钟回拨或行的创建时间在未来时等待时间不取负数
        var age = oldest is { } since ? (now > since ? now - since : TimeSpan.Zero) : (TimeSpan?)null;
        return new OutboxBacklog(pending, age, deadLettered, databases.Count);
    }

    /// <summary>
    /// 把数出的状态与阈值比较
    /// </summary>
    private Result Evaluate(OutboxBacklog backlog)
    {
        // 积压在前：消息正在发不出去比已经进了死信更急；两样都超过时死信的数字仍在健康数据里
        if (backlog.Pending > _options.HealthMaxPending || backlog.OldestPendingAge > _options.HealthMaxPendingAge)
        {
            return Error.Unavailable(
                MessagingErrors.OutboxBacklog,
                MessagingMessages.OutboxBacklog,
                MessageArguments.Of(
                    ("pending", backlog.Pending),
                    ("maxPending", _options.HealthMaxPending),
                    ("oldestSeconds", backlog.OldestPendingAge is { } age ? Seconds(age) : 0L),
                    ("maxOldestSeconds", Seconds(_options.HealthMaxPendingAge))));
        }

        if (backlog.DeadLettered > _options.HealthMaxDeadLettered)
        {
            return Error.Unavailable(
                MessagingErrors.OutboxDeadLettered,
                MessagingMessages.OutboxDeadLettered,
                MessageArguments.Of(("deadLettered", backlog.DeadLettered), ("maxDeadLettered", _options.HealthMaxDeadLettered)));
        }

        return Result.Success();
    }

    /// <summary>
    /// 时长的整秒数
    /// </summary>
    /// <remarks>
    /// 向下取整：健康数据与错误参数里都用整数秒，不带小数
    /// </remarks>
    private static long Seconds(TimeSpan duration) => (long)duration.TotalSeconds;
}
