// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// 作业状态列表的一页查询
/// </summary>
/// <remarks>
/// 按作业名的序数升序分页；续页令牌对调用方不透明，传上一页 <see cref="JobStatusPage.ContinuationToken"/> 的原值（ADR-0148）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobStatusQuery
{
    /// <summary>
    /// 每页条数的缺省值
    /// </summary>
    public const int DefaultPageSize = 100;

    /// <summary>
    /// 每页条数的上限
    /// </summary>
    public const int MaxPageSize = 1000;

    /// <summary>
    /// 每页条数
    /// </summary>
    /// <remarks>
    /// 1 到 <see cref="MaxPageSize"/>，缺省 <see cref="DefaultPageSize"/>
    /// </remarks>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// 续页令牌
    /// </summary>
    /// <remarks>
    /// 第一页为 <see langword="null"/>
    /// </remarks>
    public string? ContinuationToken { get; init; }

    /// <summary>
    /// 校验每页条数与续页令牌的格式
    /// </summary>
    /// <remarks>
    /// 越界或令牌不是调度器给出的返回 <see cref="SchedulingErrors.StatusQueryInvalid"/>
    /// </remarks>
    public Result Validate()
    {
        if (PageSize is < 1 or > MaxPageSize)
        {
            return Error.Validation(SchedulingErrors.StatusQueryInvalid, SchedulingMessages.StatusQueryPageInvalid, MessageArguments.Of(("pageSize", PageSize), ("max", MaxPageSize)));
        }

        // 令牌是上一页最后一个作业名：不是合法的作业名就不是调度器给出的
        if (ContinuationToken is not null && !JobName.IsValid(ContinuationToken))
        {
            return Error.Validation(SchedulingErrors.StatusQueryInvalid, SchedulingMessages.StatusQueryTokenInvalid);
        }

        return Result.Success();
    }
}
