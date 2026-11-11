// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

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
    public Result Validate()
        => Skip >= 0 && Take is >= 1 and <= MaxTake
            ? Result.Success()
            : Error.Validation(SecurityErrors.GrantQueryInvalid, SecurityMessages.GrantQueryPageInvalid, MessageArguments.Of(("skip", Skip), ("take", Take), ("max", MaxTake)));
}
