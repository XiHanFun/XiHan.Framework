// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 按租户解析数据分片（<c>multitenancy.sharding@1</c> 的 Provider 契约）
/// </summary>
/// <remarks>
/// Tenant 阶段在租户解析后调用一次，结果写进调用上下文，持久化 Provider 据此选连接
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface ITenantShardResolver
{
    /// <summary>
    /// 解析分片
    /// </summary>
    /// <remarks>
    /// 没有分片（共库租户）返回 <see cref="Maybe.None{T}"/>；存储不可达返回失败
    /// </remarks>
    ValueTask<Result<Maybe<TenantShard>>> ResolveAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 丢弃这些租户缓存的分片
    /// </summary>
    /// <remarks>
    /// 租户连接变更后调用；空集合什么也不做。要清全部用 <see cref="InvalidateAll"/>，不以 <see langword="null"/> 表示全部（ADR-0160）
    /// </remarks>
    void Invalidate(IReadOnlyList<string> tenantIds);

    /// <summary>
    /// 丢弃全部缓存的分片
    /// </summary>
    void InvalidateAll();
}
