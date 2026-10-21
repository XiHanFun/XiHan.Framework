// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Conformance;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

public sealed class InMemoryTenantStoreConformanceTests : TenantStoreConformance
{
    protected override ValueTask<ITenantStore> CreateStoreAsync(IReadOnlyList<TenantInfo> tenants)
        => ValueTask.FromResult<ITenantStore>(new InMemoryTenantStore(new InMemoryMultiTenancyOptions { Tenants = tenants }));
}
