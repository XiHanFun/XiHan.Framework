// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Trail;

/// <summary>
/// 审计查询：过滤项全部可选且取交集，按写入顺序升序分页
/// </summary>
/// <remarks>
/// 租户与主体用显式过滤，能区分「不过滤」与「只要全局 / 匿名」；其余可空的过滤项为 <see langword="null"/> 时不按它过滤（ADR-0169）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuditQuery
{
    /// <summary>
    /// 本版查询条件的修订号
    /// </summary>
    /// <remarks>
    /// Provider 在入口以 <c>Validate(AuditQuery.Revision)</c> 传入，常量在编译时写进 Provider，遇到更新的条件即失败；以后新增条件时加 1，新条件记在新修订号下（RFC-0001「Provider 契约的演进」，ADR-0228）。修订 1：5.0 的全部条件
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
    /// 页大小上限
    /// </summary>
    public const int MaxPageSize = 1000;

    /// <summary>
    /// 起始时间（含）
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// 结束时间（含）
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// 动作
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// 主体过滤
    /// </summary>
    /// <remarks>
    /// 缺省 <see cref="AuditPrincipalFilter.Any"/>；<see cref="AuditPrincipalFilter.Anonymous"/> 只要匿名调用的事件
    /// </remarks>
    public AuditPrincipalFilter Principal
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = AuditPrincipalFilter.Any;

    /// <summary>
    /// 租户过滤
    /// </summary>
    /// <remarks>
    /// 缺省 <see cref="AuditTenantFilter.Any"/>；<see cref="AuditTenantFilter.Global"/> 只要不属于任何租户的事件
    /// </remarks>
    public AuditTenantFilter Tenant
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = AuditTenantFilter.Any;

    /// <summary>
    /// trace-id
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 作用的资源
    /// </summary>
    /// <remarks>
    /// <c>xrn:</c> 文本，与 <see cref="AuditEvent.Resource"/> 按序数精确比较；为 <see langword="null"/> 不按资源过滤（ADR-0159）
    /// </remarks>
    public string? Resource { get; init; }

    /// <summary>
    /// 只取成功（<see langword="true"/>）或只取失败（<see langword="false"/>）
    /// </summary>
    public bool? Succeeded { get; init; }

    /// <summary>
    /// 页大小
    /// </summary>
    /// <remarks>
    /// 默认 100，1 到 <see cref="MaxPageSize"/>
    /// </remarks>
    public int PageSize { get; init; } = 100;

    /// <summary>
    /// 上一页返回的续页令牌
    /// </summary>
    public string? ContinuationToken { get; init; }

    /// <summary>
    /// 校验页大小与时间范围
    /// </summary>
    /// <remarks>
    /// 不合法返回 <see cref="AuditingErrors.QueryInvalid"/>。续页令牌由实现自己校验
    /// </remarks>
    /// <param name="supportedRevision">Provider 编译时取到的 <see cref="Revision"/>，先于其余校验核对</param>
    public Result Validate(int supportedRevision)
    {
        // 先核对修订号：用到了 Provider 不认识的条件时，其余校验无从谈起
        var revision = QueryRevision.Check(nameof(AuditQuery), RequiredRevision(), supportedRevision);
        if (revision.IsFailure)
        {
            return revision;
        }

        if (PageSize is < 1 or > MaxPageSize)
        {
            return Error.Validation(AuditingErrors.QueryInvalid, AuditingMessages.QueryInvalid, MessageArguments.Of(("reason", $"page size must be between 1 and {MaxPageSize}")));
        }

        if (From is { } from && To is { } to && from > to)
        {
            return Error.Validation(AuditingErrors.QueryInvalid, AuditingMessages.QueryInvalid, MessageArguments.Of(("reason", "from must not be later than to")));
        }

        return Result.Success();
    }

    /// <summary>
    /// 事件是否满足全部过滤项（不含分页）
    /// </summary>
    public bool Matches(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return (From is not { } from || auditEvent.Timestamp >= from)
            && (To is not { } to || auditEvent.Timestamp <= to)
            && (Action is null || string.Equals(Action, auditEvent.Action, StringComparison.Ordinal))
            && Principal.Matches(auditEvent.PrincipalId)
            && Tenant.Matches(auditEvent.TenantId)
            && (TraceId is null || string.Equals(TraceId, auditEvent.TraceId, StringComparison.Ordinal))
            && (Resource is null || string.Equals(Resource, auditEvent.Resource, StringComparison.Ordinal))
            && (Succeeded is not { } succeeded || auditEvent.Outcome.IsSuccess == succeeded);
    }
}
