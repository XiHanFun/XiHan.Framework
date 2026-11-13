// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 列出授予的查询
/// </summary>
/// <remarks>
/// 偏移分页（RFC-0001「分页」）。分页越界不在构造时抛出：查询常由管理界面的请求绑定而来，越界是调用方可以改正的输入，由 <see cref="IPermissionGrantAdministration.ListAsync"/> 经 <see cref="Validate"/> 返回 <see cref="SecurityErrors.GrantQueryInvalid"/>（HTTP 400）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record PermissionGrantQuery
{
    /// <summary>
    /// 本版查询条件的修订号
    /// </summary>
    /// <remarks>
    /// Provider 在入口以 <c>Validate(PermissionGrantQuery.Revision)</c> 传入，常量在编译时写进 Provider，遇到更新的条件即失败；以后新增条件时加 1，新条件记在新修订号下（RFC-0001「Provider 契约的演进」，ADR-0228）。修订 1：5.0 的全部条件
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
    /// 一页最多的条数
    /// </summary>
    public const int MaxTake = 1000;

    /// <summary>
    /// 主体
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 不按主体过滤
    /// </remarks>
    public GrantSubject? Subject { get; init; }

    /// <summary>
    /// 租户过滤
    /// </summary>
    /// <remarks>
    /// 缺省 <see cref="GrantTenantFilter.Any"/>
    /// </remarks>
    public GrantTenantFilter Tenant
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = GrantTenantFilter.Any;

    /// <summary>
    /// 跳过的条数
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// 最多返回的条数，1 到 <see cref="MaxTake"/>
    /// </summary>
    /// <remarks>
    /// 缺省 100
    /// </remarks>
    public int Take { get; init; } = 100;

    /// <summary>
    /// 核对分页
    /// </summary>
    /// <remarks>
    /// 实现在 <see cref="IPermissionGrantAdministration.ListAsync"/> 开头调用，失败时原样返回、不访问后端
    /// </remarks>
    /// <returns>成功；<see cref="Skip"/> 为负或 <see cref="Take"/> 不在 1 到 <see cref="MaxTake"/> 之间时为 <see cref="SecurityErrors.GrantQueryInvalid"/></returns>
    /// <param name="supportedRevision">Provider 编译时取到的 <see cref="Revision"/>，先于其余校验核对</param>
    public Result Validate(int supportedRevision)
    {
        // 先核对修订号：用到了 Provider 不认识的条件时，其余校验无从谈起
        var revision = QueryRevision.Check(nameof(PermissionGrantQuery), RequiredRevision(), supportedRevision);
        if (revision.IsFailure)
        {
            return revision;
        }

        return Skip >= 0 && Take is >= 1 and <= MaxTake
            ? Result.Success()
            : Error.Validation(SecurityErrors.GrantQueryInvalid, SecurityMessages.GrantQueryPageInvalid, MessageArguments.Of(("skip", Skip), ("take", Take), ("max", MaxTake)));
    }
}
