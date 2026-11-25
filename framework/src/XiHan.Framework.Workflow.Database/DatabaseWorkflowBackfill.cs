// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Database.Entities;
using XiHan.Framework.Workflow.Database.Locking;
using XiHan.Framework.Workflow.Database.Storage;

namespace XiHan.Framework.Workflow.Database;

/// <summary>
/// 已有行的查询列回填（单例）
/// </summary>
/// <remarks>
/// 查询列（定义与实例的关键字、标题、状态序号与分类，书签的节点、所属实例的定义名与状态、任务标题与实例一侧的关键字）是写入行时由文档算出、为把过滤与排序下推到 SQL 而存的列。规则变了或列是后来加的，已有的行里就是旧值或空值：关键字的小写改成简单映射之后（ADR-0296），含 <c>İ</c> 或平面以外大写字母的行匹配不到，升级前写入、列还为空的行不被关键字、分类匹配，按标题与状态排序时落在空值的位置。回填按存储写入时同一份算法从文档重新算出这些列，与行里的值不同才改写（RFC-0021「默认实现与 Provider」，ADR-0304）
/// <para>
/// 逐个分区、按批进行：定义与实例各按创建时间与 ID 的次序每批读至多 <c>batchSize</c> 行（连同它们的书签），每批在自己的只读事务里扫描，要改写的再开可写事务；实例行与它的书签行在实例锁内重读再改写，与引擎的写入互斥，锁在等待上限内拿不到的这次跳过（<see cref="DatabaseWorkflowBackfillReport.Busy"/>）。定义的写入本来不加锁：要改写的定义在一个短事务里重读再写，建议在没有人编辑定义的时候运行。可以重复运行，已是新值的行不再改写；取消在每批之前检查，已经提交的批留着，进行中的那一批回滚。不经管道：租户分区在新作用域里绑定租户的分片（<c>TenantShardScope</c>），不看租户是否启用
/// </para>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DatabaseWorkflowBackfill
{
    /// <summary>
    /// 批大小的上限
    /// </summary>
    /// <remarks>
    /// 与一次查询最多取出的行数相同（<see cref="EntityQuery.DefaultMaxTake"/>）：实例行带着整份文档，一批的内存与一次事务的时长都随它变
    /// </remarks>
    public const int MaxBatchSize = EntityQuery.DefaultMaxTake;

    /// <summary>
    /// 根服务提供者：每个事务开新作用域
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 实例锁：改写实例与书签的行时与引擎互斥
    /// </summary>
    private readonly DatabaseWorkflowInstanceLock _locks;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建（由特性登记成单例）
    /// </summary>
    internal DatabaseWorkflowBackfill(IServiceProvider services, DatabaseWorkflowInstanceLock locks, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _locks = locks ?? throw new ArgumentNullException(nameof(locks));
        _logger = loggerFactory.CreateLogger<DatabaseWorkflowBackfill>();
    }

    /// <summary>
    /// 逐个回填这些分区的查询列
    /// </summary>
    /// <remarks>
    /// 结果与 <paramref name="partitions"/> 等长同序（批量优先的逐项结果）：一个分区失败只让它那一项失败（存储不可达是 <c>WORKFLOW_UNAVAILABLE_001</c>，此外是分片解析、提交或读文档的错误），别的分区照常；失败的分区里已经提交的批留着。空集合不触存储；同一分区出现两次就回填两次，第二次看见第一次的结果，改写的行数是零。取消时抛出 <see cref="OperationCanceledException"/>
    /// </remarks>
    /// <param name="partitions">分区：<see langword="null"/> 为平台，其余是租户 ID</param>
    /// <param name="batchSize">每批读的行数，1 到 <see cref="MaxBatchSize"/></param>
    /// <param name="cancellationToken">取消令牌</param>
    public async ValueTask<IReadOnlyList<Result<DatabaseWorkflowBackfillReport>>> RunAsync(IReadOnlyList<string?> partitions, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, MaxBatchSize);
        foreach (var tenantId in partitions)
        {
            // 先核对全部入参再动存储：空白的租户 ID 是编程错误，不回填半截之后才抛出
            if (tenantId is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(tenantId, nameof(partitions));
            }
        }

        var results = new List<Result<DatabaseWorkflowBackfillReport>>(partitions.Count);
        foreach (var tenantId in partitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(tenantId is null
                ? await new PartitionBackfill<PlatformWorkflowDefinitionRecord, PlatformWorkflowInstanceRecord, PlatformWorkflowNodeInstanceRecord, PlatformWorkflowBookmarkRecord>(_services, _locks, _logger, null, batchSize).RunAsync(cancellationToken).ConfigureAwait(false)
                : await new PartitionBackfill<WorkflowDefinitionRecord, WorkflowInstanceRecord, WorkflowNodeInstanceRecord, WorkflowBookmarkRecord>(_services, _locks, _logger, tenantId, batchSize).RunAsync(cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <summary>
    /// 回填的一个事务抛出异常
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Backfilling the workflow query columns of partition '{TenantId}' threw.")]
    internal static partial void LogBackfillThrew(ILogger logger, string tenantId, Exception exception);
}
