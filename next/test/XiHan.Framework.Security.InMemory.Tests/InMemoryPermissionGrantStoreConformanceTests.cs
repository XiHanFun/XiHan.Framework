// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Conformance;

namespace XiHan.Framework.Security.InMemory.Tests;

/// <summary>
/// 内存授予存储跑一致性套件。
/// </summary>
public sealed class InMemoryPermissionGrantStoreConformanceTests : PermissionGrantStoreConformance
{
    /// <inheritdoc />
    protected override ValueTask<IPermissionGrantStore> CreateStoreAsync(IReadOnlyList<PermissionGrant> grants)
        => ValueTask.FromResult<IPermissionGrantStore>(new InMemoryPermissionGrantStore(new InMemorySecurityOptions { Grants = grants }));
}
