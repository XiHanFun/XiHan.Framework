// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 进程外插件的回调不能自己挑租户（ADR-0195）
/// </summary>
/// <remarks>
/// 安全审查 S-30：原先回调元数据里的 <c>xihan.tenant</c> 原样交给 Tenant 阶段，插件身份没有租户声明可比对，插件能在任何启用的租户里调用宿主操作。EchoPlugin 的 <c>echoes.relay</c> 原样转发收到的元数据，载荷给了 <c>tenant</c> 时改写其中的租户
/// </remarks>
public sealed class CallbackTenantTests
{
    [Fact]
    public async Task Relay_NamingAnotherTenant_IsDenied()
    {
        await using var host = await StartAsync();

        var relayed = await EchoPluginHost.InvokeAsync(host, "echoes.relay", """{"operation":"tenants.echo","payload":{"name":"x"},"tenant":"globex"}""", Tenant("acme"));

        // 修之前回调在 globex 里执行：外层调用属于 acme，插件自报的 globex 照样通过 Tenant 阶段
        Assert.Equal(PluginErrors.CallbackDenied, relayed.Outcome.Error?.Code);
    }

    [Fact]
    public async Task Relay_ForwardingTheMetadataOfTheCall_RunsInItsTenant()
    {
        await using var host = await StartAsync();

        var relayed = await EchoPluginHost.InvokeAsync(host, "echoes.relay", """{"operation":"tenants.echo","payload":{"name":"x"}}""", Tenant("acme"));

        Assert.True(relayed.Outcome.IsSuccess, relayed.Outcome.ToString());
        Assert.Equal("acme", EchoPluginHost.Payload(relayed).GetProperty("name").GetString());
    }

    /// <summary>
    /// 只带租户的 Baggage
    /// </summary>
    private static Dictionary<string, string> Tenant(string tenant) => new(StringComparer.Ordinal) { [BaggageKeys.Tenant] = tenant };

    /// <summary>
    /// 装配 EchoPlugin、内存多租户与宿主侧 tenants.echo 的宿主
    /// </summary>
    /// <remarks>
    /// acme 与 globex 两个租户都启用
    /// </remarks>
    private static Task<TestHost> StartAsync()
        => EchoPluginHost.StartAsync(
            static b => b.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.tenants"))
            {
                OnConfigure = static ctx => ctx.AddOperation<TenantOperation, PingRequest, PingResponse>(new OperationContract("tenants.echo"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse),
            }),
            extra: [("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme"), ("XiHan:MultiTenancy:Tenants:globex:DisplayName", "Globex")]);
}
