// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 按租户解析数据分片（<c>multitenancy.sharding@1</c> 的 Provider 契约）。
/// </summary>
/// <remarks>
/// Tenant 阶段在租户解析后调用一次，结果写进调用上下文，持久化 Provider 据此选连接。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface ITenantShardResolver
{
    /// <summary>
    /// 解析分片。
    /// </summary>
    /// <remarks>
    /// 没有分片（共库租户）返回 <see cref="Maybe.None{T}"/>；存储不可达返回失败。
    /// </remarks>
    ValueTask<Result<Maybe<TenantShard>>> ResolveAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 丢弃缓存的分片（租户连接变更后调用）。
    /// </summary>
    /// <remarks>
    /// <paramref name="tenantId"/> 为空清全部。
    /// </remarks>
    void Invalidate(string? tenantId = null);
}
