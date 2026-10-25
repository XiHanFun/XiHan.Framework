// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 按租户解析数据分片（<c>multitenancy.sharding@1</c> 的 Provider 契约）。Tenant 阶段在租户解析后调用一次，结果写进调用上下文，持久化 Provider 据此选连接。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface ITenantShardResolver
{
    /// <summary>
    /// 解析分片；没有分片（共库租户）返回 <see cref="Maybe.None{T}"/>；存储不可达返回失败。
    /// </summary>
    ValueTask<Result<Maybe<TenantShard>>> ResolveAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 丢弃缓存的分片（租户连接变更后调用）；<paramref name="tenantId"/> 为空清全部。
    /// </summary>
    void Invalidate(string? tenantId = null);
}
