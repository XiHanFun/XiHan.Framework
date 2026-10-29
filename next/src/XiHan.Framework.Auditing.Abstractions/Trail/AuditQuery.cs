// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Trail;

/// <summary>
/// 审计查询：过滤项全部可选且取交集，按写入顺序升序分页。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AuditQuery
{
    /// <summary>
    /// 页大小上限。
    /// </summary>
    public const int MaxPageSize = 1000;

    /// <summary>
    /// 起始时间（含）。
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// 结束时间（含）。
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// 动作。
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// 主体 ID。
    /// </summary>
    public string? PrincipalId { get; init; }

    /// <summary>
    /// 租户 ID。
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// trace-id。
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 只取成功（<see langword="true"/>）或只取失败（<see langword="false"/>）。
    /// </summary>
    public bool? Succeeded { get; init; }

    /// <summary>
    /// 页大小。
    /// </summary>
    /// <remarks>
    /// 默认 100，1 到 <see cref="MaxPageSize"/>。
    /// </remarks>
    public int PageSize { get; init; } = 100;

    /// <summary>
    /// 上一页返回的续页令牌。
    /// </summary>
    public string? ContinuationToken { get; init; }

    /// <summary>
    /// 校验页大小与时间范围。
    /// </summary>
    /// <remarks>
    /// 不合法返回 <see cref="AuditingErrors.QueryInvalid"/>。续页令牌由实现自己校验。
    /// </remarks>
    public Result Validate()
    {
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
    /// 事件是否满足全部过滤项（不含分页）。
    /// </summary>
    public bool Matches(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return (From is not { } from || auditEvent.Timestamp >= from)
            && (To is not { } to || auditEvent.Timestamp <= to)
            && (Action is null || string.Equals(Action, auditEvent.Action, StringComparison.Ordinal))
            && (PrincipalId is null || string.Equals(PrincipalId, auditEvent.PrincipalId, StringComparison.Ordinal))
            && (TenantId is null || string.Equals(TenantId, auditEvent.TenantId, StringComparison.Ordinal))
            && (TraceId is null || string.Equals(TraceId, auditEvent.TraceId, StringComparison.Ordinal))
            && (Succeeded is not { } succeeded || auditEvent.Outcome.IsSuccess == succeeded);
    }
}
