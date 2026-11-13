// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Validation;
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
    /// 本版查询条件的修订号
    /// </summary>
    /// <remarks>
    /// Provider 在入口以 <c>Validate(TenantQuery.Revision)</c> 传入，常量在编译时写进 Provider，遇到更新的条件即失败；以后新增条件时加 1，新条件记在新修订号下（RFC-0001「Provider 契约的演进」，ADR-0228）。修订 1：5.0 的全部条件
    /// </remarks>
    public const int Revision = 1;

    /// <summary>
    /// 这次查询用到的条件里最高的修订号
    /// </summary>
    /// <remarks>
    /// 没有用到新条件时不变，旧 Provider 照常服务。5.0 的条件都属于修订 1；以后新增条件时改为实例方法，按设置了的条件取其中最高的修订号，不要直接返回 <see cref="Revision"/>
    /// </remarks>
    private static int RequiredRevision() => 1;

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
    /// <param name="supportedRevision">Provider 编译时取到的 <see cref="Revision"/>，先于其余校验核对</param>
    public Result Validate(int supportedRevision)
    {
        // 先核对修订号：用到了 Provider 不认识的条件时，其余校验无从谈起
        var revision = QueryRevision.Check(nameof(TenantQuery), RequiredRevision(), supportedRevision);
        if (revision.IsFailure)
        {
            return revision;
        }

        return PageSize is >= 1 and <= MaxPageSize
            ? Result.Success()
            : Error.Validation(MultiTenancyErrors.QueryInvalid, MultiTenancyMessages.QueryPageInvalid, MessageArguments.Of(("pageSize", PageSize), ("max", MaxPageSize)));
    }
}
