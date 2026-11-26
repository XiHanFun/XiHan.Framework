// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database;

/// <summary>
/// 一个分区回填查询列的结果
/// </summary>
/// <remarks>
/// 见 <see cref="DatabaseWorkflowBackfill"/>（ADR-0304）。<see cref="Busy"/> 不为零时这次没有处理完：那些实例或定义的锁在等待上限内没有拿到，它们的行原样留着，再跑一次即可
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DatabaseWorkflowBackfillReport
{
    /// <summary>
    /// 读过的行数
    /// </summary>
    /// <remarks>
    /// 定义、实例与书签合计，只算按批扫描时读到的；改写之前在锁内重读的不再计
    /// </remarks>
    public long Scanned { get; init; }

    /// <summary>
    /// 改写的行数
    /// </summary>
    /// <remarks>
    /// 只有算出的查询列与行里现有的值不同的行才改写；已是新值的行不计，所以同一个分区回填第二次是零
    /// </remarks>
    public long Rewritten { get; init; }

    /// <summary>
    /// 因为锁被占用而跳过的实例与定义数
    /// </summary>
    /// <remarks>
    /// 实例行与它的书签行在实例锁内改写，与引擎的写入互斥；定义行与它的定义级定时书签在定义 ID 的锁内改写，与目录改写定义互斥（ADR-0326）。等待超过 <c>WorkflowOptions.LockTimeout</c> 的这次跳过，留给下一次回填
    /// </remarks>
    public long Busy { get; init; }

    /// <summary>
    /// 扫描的批数
    /// </summary>
    /// <remarks>
    /// 定义与实例各自按批读，每批最多一次 <c>batchSize</c> 行、在自己的只读事务里；只算读到了行的批
    /// </remarks>
    public int Batches { get; init; }
}
