// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tenants;

/// <summary>
/// 租户存储，Provider 实现它
/// </summary>
/// <remarks>
/// ID 区分大小写。存储不可达（连接、超时、事务失败）以 <see cref="MultiTenancyErrors.StoreUnavailable"/> 返回，不伪装成不存在：
/// 不存在会让调用以 404 结束，故障必须以 503 结束（ADR-0140）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface ITenantStore
{
    /// <summary>
    /// 按 ID 查找
    /// </summary>
    /// <remarks>
    /// 没有这个租户是成功的无值；ID 不合乎文法同样是无值
    /// </remarks>
    ValueTask<Result<Maybe<TenantInfo>>> FindAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 升序列出一页租户
    /// </summary>
    /// <remarks>
    /// 比较规则由存储决定（内存实现按序数，关系库按库的排序规则），同一存储里稳定。每页条数越界或续页令牌不是本存储给出的，返回 <see cref="MultiTenancyErrors.QueryInvalid"/>。列出全部租户要逐页读到续页令牌为 <see langword="null"/>，存储不在一次调用里返回无界的结果
    /// </remarks>
    ValueTask<Result<TenantPage>> ListAsync(TenantQuery query, CancellationToken cancellationToken);
}
