// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Conformance;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

/// <summary>
/// 让 <see cref="InMemoryTenantStore"/> 跑完整套 <see cref="TenantStoreConformance"/> 用例。
/// </summary>
public sealed class InMemoryTenantStoreConformanceTests : TenantStoreConformance
{
    /// <summary>
    /// 用给定租户与默认容量创建内存存储。
    /// </summary>
    protected override ValueTask<ITenantStore> CreateStoreAsync(IReadOnlyList<TenantInfo> tenants)
        => ValueTask.FromResult<ITenantStore>(new InMemoryTenantStore(new InMemoryMultiTenancyOptions { Tenants = tenants }));
}
