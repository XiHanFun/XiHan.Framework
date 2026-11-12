// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.OutOfProcess.Runtime;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 在途调用的租户登记（ADR-0195）
/// </summary>
/// <remarks>
/// 回调能带的租户是此刻在途的某个调用的租户：同一租户的调用都结束才撤销，调用结束后不留条目
/// </remarks>
public sealed class PluginTenantCallsTests
{
    [Fact]
    public void Tenant_IsHandledUntilItsLastCallEnds()
    {
        var calls = new PluginTenantCalls();

        var first = calls.Enter("acme");
        var second = calls.Enter("acme");
        Assert.True(calls.Contains("acme"));
        Assert.False(calls.Contains("globex"));

        first.Dispose();
        Assert.True(calls.Contains("acme"));

        second.Dispose();
        Assert.False(calls.Contains("acme"));
    }

    [Fact]
    public void CallWithoutATenant_RegistersNothing()
    {
        var calls = new PluginTenantCalls();

        using (calls.Enter(null))
        {
            Assert.False(calls.Contains(string.Empty));
        }
    }

    [Fact]
    public async Task ConcurrentCalls_LeaveNoEntryBehind()
    {
        var calls = new PluginTenantCalls();
        var tenants = Enumerable.Range(0, 8).Select(static i => $"t{i}").ToArray();

        await Parallel.ForEachAsync(Enumerable.Range(0, 2000), async (i, ct) =>
        {
            using var entry = calls.Enter(tenants[i % tenants.Length]);
            await Task.Yield();
            Assert.True(calls.Contains(tenants[i % tenants.Length]));
        });

        Assert.All(tenants, tenant => Assert.False(calls.Contains(tenant)));
    }
}
