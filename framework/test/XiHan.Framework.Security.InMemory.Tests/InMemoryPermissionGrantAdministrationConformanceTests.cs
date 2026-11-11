// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Conformance;

namespace XiHan.Framework.Security.InMemory.Tests;

/// <summary>
/// 内存授予存储的授予管理跑一致性套件
/// </summary>
public sealed class InMemoryPermissionGrantAdministrationConformanceTests : PermissionGrantAdministrationConformance
{
    /// <summary>
    /// 本用例的存储
    /// </summary>
    private InMemoryPermissionGrantStore? _store;

    /// <inheritdoc />
    protected override ValueTask<IPermissionGrantStore> StartAsync()
    {
        _store = new InMemoryPermissionGrantStore(new InMemorySecurityOptions());
        return ValueTask.FromResult<IPermissionGrantStore>(_store);
    }

    /// <inheritdoc />
    protected override ValueTask<T> AdministerAsync<T>(Func<IPermissionGrantAdministration, CancellationToken, ValueTask<T>> action)
        => action(_store ?? throw new InvalidOperationException("StartAsync was not called."), CancellationToken.None);
}
