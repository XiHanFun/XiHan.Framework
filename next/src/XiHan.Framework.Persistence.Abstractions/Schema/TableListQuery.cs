// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 列表的过滤与分页。
/// </summary>
/// <remarks>
/// 取出条数与仓储查询同一上限：<see cref="Take"/> 取 1 到 <see cref="EntityQuery.DefaultMaxTake"/>，缺省取上限。分页越界不在构造时抛出：查询常由请求绑定而来，越界是调用方可以改正的输入，由读结构的 Provider 经 <see cref="Validate"/> 以校验结果返回（HTTP 400）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record TableListQuery
{
    /// <summary>
    /// 关键字：在表名与注释里不区分大小写地包含匹配。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 或空白不过滤。
    /// </remarks>
    public string? Keyword { get; init; }

    /// <summary>
    /// 跳过的条数（不小于 0）。
    /// </summary>
    /// <remarks>
    /// 为负由 <see cref="Validate"/> 报告。
    /// </remarks>
    public int Skip { get; init; }

    /// <summary>
    /// 取出的条数（1 到 <see cref="EntityQuery.DefaultMaxTake"/>，缺省取上限）。
    /// </summary>
    /// <remarks>
    /// 越界由 <see cref="Validate"/> 报告；不静默截断，要更多就分页。
    /// </remarks>
    public int Take { get; init; } = EntityQuery.DefaultMaxTake;

    /// <summary>
    /// 核对分页参数。
    /// </summary>
    /// <remarks>
    /// 读结构的 Provider 在 <see cref="ISchemaReader.ListTablesAsync"/> 开头调用，失败时原样返回、不连库、不登记数据源。先核对下界再核对上限。
    /// </remarks>
    /// <returns>成功；<see cref="Skip"/> 为负或 <see cref="Take"/> 小于 1 时为 <see cref="PersistenceErrors.ListPageInvalid"/> 校验错误，<see cref="Take"/> 超过 <see cref="EntityQuery.DefaultMaxTake"/> 时为 <see cref="PersistenceErrors.TakeExceeded"/> 校验错误。</returns>
    public Result Validate()
    {
        if (Skip < 0 || Take < 1)
        {
            return Error.Validation(PersistenceErrors.ListPageInvalid, PersistenceMessages.ListPageInvalid, MessageArguments.Of(("skip", Skip), ("take", Take)));
        }

        return Take <= EntityQuery.DefaultMaxTake
            ? Result.Success()
            : Error.Validation(PersistenceErrors.TakeExceeded, PersistenceMessages.TakeExceeded, MessageArguments.Of(("take", Take), ("max", EntityQuery.DefaultMaxTake)));
    }
}
