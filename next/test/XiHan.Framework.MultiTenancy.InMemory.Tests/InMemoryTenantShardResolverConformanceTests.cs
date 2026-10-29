// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Conformance;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

/// <summary>
/// 内存租户存储上分片解析器的一致性用例。
/// </summary>
/// <remarks>
/// 让基于 <see cref="InMemoryTenantStore"/> 的 <see cref="TenantStoreShardResolver"/> 跑完整套 <see cref="TenantShardResolverConformance"/> 用例。
/// </remarks>
public sealed class InMemoryTenantShardResolverConformanceTests : TenantShardResolverConformance
{
    /// <summary>
    /// 用给定租户建内存存储，再在其上建分片解析器。
    /// </summary>
    protected override ValueTask<ITenantShardResolver> CreateResolverAsync(IReadOnlyList<TenantInfo> tenants)
        => ValueTask.FromResult<ITenantShardResolver>(new TenantStoreShardResolver(new InMemoryTenantStore(new InMemoryMultiTenancyOptions { Tenants = tenants }), new MultiTenancyOptions()));
}
