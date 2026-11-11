// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tenants;

/// <summary>
/// 租户列表的一页查询
/// </summary>
/// <remarks>
/// 按租户 ID 升序（比较规则由存储决定），以续页令牌逐页读；越界不在构造时抛出，由存储以 <see cref="MultiTenancyErrors.QueryInvalid"/> 返回（查询常由请求绑定，越界是调用方可以改正的输入）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record TenantQuery
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
    /// 上一页返回的续页令牌
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 从头开始；令牌对调用方不透明，只能原样传回
    /// </remarks>
    public string? ContinuationToken { get; init; }

    /// <summary>
    /// 是否只列启用的租户
    /// </summary>
    public bool EnabledOnly { get; init; }

    /// <summary>
    /// 校验每页条数
    /// </summary>
    /// <remarks>
    /// 存储在读之前调用；续页令牌是否有效只有给出它的存储知道，由存储自己校验
    /// </remarks>
    public Result Validate()
        => PageSize is >= 1 and <= MaxPageSize
            ? Result.Success()
            : Error.Validation(MultiTenancyErrors.QueryInvalid, MultiTenancyMessages.QueryPageInvalid, MessageArguments.Of(("pageSize", PageSize), ("max", MaxPageSize)));
}
