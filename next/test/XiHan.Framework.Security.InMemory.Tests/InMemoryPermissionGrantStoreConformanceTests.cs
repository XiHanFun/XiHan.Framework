// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Conformance;

namespace XiHan.Framework.Security.InMemory.Tests;

public sealed class InMemoryPermissionGrantStoreConformanceTests : PermissionGrantStoreConformance
{
    protected override ValueTask<IPermissionGrantStore> CreateStoreAsync(IReadOnlyList<PermissionGrant> grants)
        => ValueTask.FromResult<IPermissionGrantStore>(new InMemoryPermissionGrantStore(new InMemorySecurityOptions { Grants = grants }));
}
