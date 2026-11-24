// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 宿主经 <c>Configure</c> 下发给 Wasm 插件的绑定（<c>spec/plugin/lifecycle.md</c> §2，ADR-0268）
/// </summary>
/// <remarks>
/// 端到端：应用经 <c>AppBuilder.UseServiceBindingRoot</c> 指定绑定根目录，特性把它交给宿主，现场生成的 WAT 插件在 <c>xihan_configure</c> 里核对收到的请求，找得到期望的主值才启动成功。组装规则本身（类型的取值、没有主值的绑定、读失败）由进程外测试项目里的 <c>PluginConfigureRequestsTests</c> 核对，两个宿主共用那一处
/// </remarks>
public sealed class WasmPluginBindingTests : IDisposable
{
    /// <summary>
    /// 本用例的 Service Binding 根目录
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xihan-plugin-bindings-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Binding_MountedUnderTheAppsServiceBindingRoot_ReachesThePlugin()
    {
        // 绑定只在目录里（Kubernetes 上就是这样），配置里一项都没有；根目录是应用指定的那一个，不是环境变量
        Mount("db", ("type", "connection:relational"), ("uri", "mounted-db-7f3a"));
        using var plugin = WatPlugin.ExpectingBinding("db", "connection:relational", "mounted-db-7f3a");

        await using var host = await StartAsync(plugin, ["binding:db"]);

        var status = EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status;
        Assert.True(status.State == LifecycleState.Ready, $"The plugin must be configured with the mounted binding, but it is {status.State}: {status.Error}");
    }

    [Fact]
    public async Task Binding_ThatIsNotProvided_IsNotDelivered()
    {
        // 对照：根目录里没有这个绑定，插件在 Configure 里找不到它、报失败，停在 Failed
        Mount("cache", ("uri", "mounted-db-7f3a"));
        using var plugin = WatPlugin.ExpectingBinding("db", "connection:relational", "mounted-db-7f3a");

        await using var host = await StartAsync(plugin, ["binding:db"]);

        var status = EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status;
        Assert.Equal(LifecycleState.Failed, status.State);
        Assert.Equal(PluginErrors.Unexpected, status.Error?.Code);
    }

    [Fact]
    public async Task Binding_InConfiguration_ReplacesTheMountedOneOfTheSameName()
    {
        Mount("db", ("uri", "mounted-db-7f3a"));
        using var plugin = WatPlugin.ExpectingBinding("db", "connection:relational", "configured-db-91c2");

        await using var host = await StartAsync(plugin, ["binding:db"], ("XiHan:Bindings:db:value", "configured-db-91c2"));

        var status = EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status;
        Assert.True(status.State == LifecycleState.Ready, $"The configured binding must replace the mounted one, but the plugin is {status.State}: {status.Error}");
    }

    [Fact]
    public async Task Binding_DeclaredButNotGranted_RejectsThePluginAtDiscovery()
    {
        // 目录里有这个绑定也不行：能不能拿到由 binding:<名> 的授予决定，与来源无关（ADR-0177）
        Mount("db", ("uri", "mounted-db-7f3a"));
        using var plugin = WatPlugin.ExpectingBinding("db", "connection:relational", "mounted-db-7f3a");

        await using var host = await StartAsync(plugin, []);

        var status = EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow();
        Assert.Equal(LifecycleState.Failed, status.Status.State);
        Assert.Equal(PluginErrors.PermissionDenied, status.Status.Error?.Code);
        Assert.Contains("binding:db", status.Status.Error?.Message, StringComparison.Ordinal);
        // 发现时被拒：没有实例化，插件代码一行没跑
        Assert.Null(status.AbiVersion);
    }

    [Fact]
    public async Task Binding_ProvidedAfterTheHostStarted_ReachesAPluginLoadedAtRuntime()
    {
        // 绑定在配置插件的那一刻解析：宿主启动时还没有的绑定，运行期加载的插件照样拿得到，不用重启宿主
        using var plugin = WatPlugin.ExpectingBinding("db", "connection:relational", "mounted-later-55e0");
        await using var host = await TestHost.StartAsync(b => b.UseServiceBindingRoot(_root).UseFeature<WasmPluginsFeature>(), [new("XiHan:Plugins:Wasm:EpochIntervalMilliseconds", "5")]);
        Mount("db", ("uri", "mounted-later-55e0"));

        var loaded = await host.Services.GetRequiredService<IPluginHost>().LoadAsync(new PluginSource(plugin.Directory, new PluginPermissions(["binding:db"])), CancellationToken.None);

        Assert.True(loaded.IsSuccess, loaded.ToString());
        Assert.Equal(LifecycleState.Ready, loaded.ValueOrThrow().State);
    }

    /// <summary>
    /// 启动装配了 Wasm 插件特性的宿主，绑定根目录指向本用例的目录
    /// </summary>
    private Task<TestHost> StartAsync(WatPlugin plugin, IReadOnlyList<string> grants, params (string Key, string Value)[] extra)
    {
        var configuration = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory),
            new("XiHan:Plugins:Wasm:EpochIntervalMilliseconds", "5"),
        };
        configuration.AddRange(grants.Select(static (grant, index) => new KeyValuePair<string, string?>($"XiHan:Plugins:Wasm:Grants:{WatPlugin.PluginId}:{index}", grant)));
        configuration.AddRange(extra.Select(static e => new KeyValuePair<string, string?>(e.Key, e.Value)));
        return TestHost.StartAsync(b => b.UseServiceBindingRoot(_root).UseFeature<WasmPluginsFeature>(), configuration);
    }

    /// <summary>
    /// 在绑定根下放一个绑定目录：每个文件是一个属性
    /// </summary>
    private void Mount(string name, params (string File, string Content)[] files)
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        foreach (var (file, content) in files)
        {
            File.WriteAllText(Path.Combine(directory, file), content);
        }
    }
}
