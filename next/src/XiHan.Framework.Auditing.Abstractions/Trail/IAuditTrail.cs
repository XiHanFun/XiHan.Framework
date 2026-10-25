// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Trail;

/// <summary>
/// 审计查询（RFC-0015）。Provider 同时实现 <c>IAuditSink</c>（写入）与本接口（读取）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IAuditTrail
{
    /// <summary>
    /// 按条件分页查询，按写入顺序升序。不合法的查询 → <see cref="AuditingErrors.QueryInvalid"/>；存储不可达 → <see cref="AuditingErrors.TrailUnavailable"/>。
    /// </summary>
    ValueTask<Result<AuditPage>> QueryAsync(AuditQuery query, CancellationToken cancellationToken);
}
