// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 宿主经 <c>Configure</c> 下发给 Wasm 插件的绑定
/// </summary>
/// <remarks>
/// <c>spec/plugin/lifecycle.md</c> §2，ADR-0268、ADR-0285、ADR-0292。端到端：应用经 <c>AppBuilder.UseServiceBindingRoot</c> 指定绑定根目录，特性把它交给宿主，现场生成的 WAT 插件在 <c>xihan_configure</c> 里核对收到的请求，找得到期望的主值才启动成功。组装规则本身（类型的取值、没有主值的绑定、读失败）由进程外测试项目里的 <c>PluginConfigureRequestsTests</c> 核对，两个宿主共用那一处
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
    public async Task Binding_WithOnlyPropertyFiles_ReachesThePluginWithItsProperties()
    {
        // 只挂属性文件、没有 uri：修之前线协议带不了属性，这个绑定整个不下发，插件停在 Failed（ADR-0285）
        Mount("db", ("type", "connection:relational"), ("host", "db.internal"), ("username", "app"), ("password", "mounted-pw-3c1e"));
        using var plugin = WatPlugin.ExpectingBinding("db", "connection:relational", "\"password\": \"mounted-pw-3c1e\"");

        await using var host = await StartAsync(plugin, ["binding:db"]);

        var status = EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status;
        Assert.True(status.State == LifecycleState.Ready, $"The plugin must be configured with the properties of the mounted binding, but it is {status.State}: {status.Error}");
    }

    [Fact]
    public async Task Binding_PropertiesBesideTheMainValue_ReachThePlugin()
    {
        // 带 uri 的绑定里另放的凭据同样下发；配置来源的属性也一样（type、value 之外的子键）
        Mount("db", ("uri", "mounted-db-7f3a"), ("password", "mounted-pw-3c1e"));
        using var mounted = WatPlugin.ExpectingBinding("db", "connection:relational", "\"password\": \"mounted-pw-3c1e\"");
        await using (var host = await StartAsync(mounted, ["binding:db"]))
        {
            var status = EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status;
            Assert.True(status.State == LifecycleState.Ready, $"The property beside the main value must reach the plugin, but it is {status.State}: {status.Error}");
        }

        using var configured = WatPlugin.ExpectingBinding("db", "connection:relational", "\"accessKey\": \"configured-ak-88d1\"");
        await using (var host = await StartAsync(configured, ["binding:db"], ("XiHan:Bindings:db:value", "configured-db-91c2"), ("XiHan:Bindings:db:accessKey", "configured-ak-88d1")))
        {
            var status = EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status;
            Assert.True(status.State == LifecycleState.Ready, $"The configured property must reach the plugin, but it is {status.State}: {status.Error}");
        }
    }

    [Fact]
    public async Task Binding_WithTooManyProperties_FailsTheConfigureStep_WithoutNamingAnyValue()
    {
        // 超过上限不少发、不截断：配置这一步失败，插件停在 Failed；错误只带绑定名与上限的数字
        Mount("db", [("uri", "mounted-db-7f3a"), .. Enumerable.Range(0, 64).Select(static i => ($"p{i:D2}", "mounted-pw-3c1e"))]);
        using var plugin = WatPlugin.ExpectingBinding("db", "connection:relational", "mounted-db-7f3a");

        await using var host = await StartAsync(plugin, ["binding:db"]);

        var status = EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status;
        Assert.Equal(LifecycleState.Failed, status.State);
        Assert.Equal(PluginErrors.Unexpected, status.Error?.Code);
        Assert.Equal("binding 'db' has more than 64 properties", status.Error?.Details?["reason"]);
        Assert.DoesNotContain("mounted-pw-3c1e", status.Error!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("mounted-db-7f3a", status.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Binding_ThatIsRequiredButNotProvided_FailsTheConfigureStep()
    {
        // 根目录里没有这个绑定，清单也没把它标成可选：宿主不发 Configure，插件停在 Failed（ADR-0292；以前照发、由插件自己报失败）
        Mount("cache", ("uri", "mounted-db-7f3a"));
        using var plugin = WatPlugin.ExpectingBinding("db", "connection:relational", "mounted-db-7f3a");

        await using var host = await StartAsync(plugin, ["binding:db"]);

        var status = EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status;
        Assert.Equal(LifecycleState.Failed, status.State);
        Assert.Equal(PluginErrors.Unexpected, status.Error?.Code);
        Assert.Equal("binding 'db' is required but not provided", status.Error?.Details?["reason"]);
    }

    [Fact]
    public async Task Binding_OfAnotherType_FailsTheConfigureStep_WithBothTypes()
    {
        // 挂载的绑定声明了别的类型：以前照样下发来源的类型，插件拿着文档库的连接串去连关系库
        Mount("db", ("type", "connection:document"), ("uri", "mounted-db-7f3a"));
        using var plugin = WatPlugin.ExpectingBinding("db", "connection:relational", "mounted-db-7f3a");

        await using var host = await StartAsync(plugin, ["binding:db"]);

        var status = EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status;
        Assert.Equal(LifecycleState.Failed, status.State);
        Assert.Equal("binding 'db' is of type 'connection:document' but the manifest declares 'connection:relational'", status.Error?.Details?["reason"]);
        Assert.DoesNotContain("mounted-db-7f3a", status.Error!.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("XiHan:Bindings:db:value", "s3<high>cret", "fixed-db-2b7d")]
    [InlineData("XiHan:Bindings:db:password", "s3<low>cret", "fixed-pw-6e09")]
    public async Task Binding_WithALoneSurrogate_FailsTheConfigureStep_AndTheNextCallTriesAgain(string key, string value, string fixedValue)
    {
        // 主值或属性值有孤立的代理项：组装配置请求即失败，与进程外插件走同一处（PluginConfigureRequests，ADR-0321），不调 xihan_configure；
        // 插件停在 Failed、原因只带绑定名，宿主降级运行。启动阶段的失败 Wasm 宿主不在后台重试，下一次调用按需再试（ADR-0260、ADR-0325）。
        // 用例数据里只写占位：孤立的代理项不进测试用例的名字
        using var plugin = WatPlugin.ExpectingBinding("db", "connection:relational", fixedValue);
        await using var host = await StartAsync(plugin, ["binding:db"], Bindings(key, WithLoneSurrogates(value)));
        var plugins = EchoWasmPluginHost.PluginHost(host);

        var failed = plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow();
        AssertLoneSurrogateRejected(failed.Status.Error, failed.Status.State);
        Assert.Equal(0, failed.Restarts);
        Assert.Equal(LifecycleState.Degraded, host.App.State);

        // 下一次调用重建实例、再组装一次请求：照样失败，调用方拿到的是配置这一步的错误，不是插件不可用
        var call = await EchoWasmPluginHost.InvokeAsync(host, WatPlugin.Operation, "{}");
        AssertLoneSurrogateRejected(call.Outcome.Error, plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow().Status.State);

        // 改正绑定之后的那次调用照常配置并启动：插件拿到的是改正的值
        host.Configuration[key] = fixedValue;
        await EchoWasmPluginHost.InvokeAsync(host, WatPlugin.Operation, "{}");
        var status = plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow().Status;
        Assert.True(status.State == LifecycleState.Ready, $"The plugin must be configured with the corrected binding, but it is {status.State}: {status.Error}");
    }

    [Fact]
    public async Task Binding_WithALoneSurrogate_FailsTheRebuildAfterACrash_AndIsRetriedWithBackoff()
    {
        // 运行期崩溃后按 lifecycle.md §6 退避重建（wasm.md §5）：重建时绑定变成有孤立代理项的值，配置这一步同样失败、原因相同，接着按退避再试，不按需抢跑；
        // 改正之后的那次重建照常配置并启动
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var plugin = WatPlugin.ExpectingBindingAndTrapping("db", "connection:relational", "configured-db-91c2");
        await using var host = await StartAsync(plugin, ["binding:db"], clock, ("XiHan:Bindings:db:value", "configured-db-91c2"));
        var plugins = EchoWasmPluginHost.PluginHost(host);
        Assert.Equal(LifecycleState.Ready, plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow().Status.State);
        var configuration = host.Configuration;

        configuration["XiHan:Bindings:db:value"] = WithLoneSurrogates("s3<high>cret");
        Assert.Equal(PluginErrors.Crashed, (await EchoWasmPluginHost.InvokeAsync(host, WatPlugin.Operation, "{}")).Outcome.Error?.Code);

        // 推过第一次退避（1 秒）：重建在组装请求时失败，计一次重启，之后仍在退避里，调用不抢跑重建
        Assert.True(await RetriesAfterBackoffAsync(plugins, clock, TimeSpan.FromSeconds(10)), "The rebuild must start after the backoff.");
        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() => plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow().Status.Error?.Code == PluginErrors.Unexpected, TimeSpan.FromSeconds(10)));
        var retried = plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow();
        AssertLoneSurrogateRejected(retried.Status.Error, retried.Status.State);
        Assert.Equal(1, retried.Restarts);
        // 重建失败到排上下一次退避之间有一瞬：落在那一瞬的调用按需再试一次、同样失败，之后的调用都是不可用
        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() => EchoWasmPluginHost.InvokeAsync(host, WatPlugin.Operation, "{}").GetAwaiter().GetResult().Outcome.Error?.Code == PluginErrors.Unavailable, TimeSpan.FromSeconds(10)));
        Assert.Equal(1, plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow().Restarts);

        // 改正绑定之后，下一次退避（2 秒）的重建照常配置并启动
        configuration["XiHan:Bindings:db:value"] = "configured-db-91c2";
        Assert.True(await RetriesAfterBackoffAsync(plugins, clock, TimeSpan.FromSeconds(10)), "The rebuild must be retried after the next backoff.");
        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() => plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow().Status.State == LifecycleState.Ready, TimeSpan.FromSeconds(10)));
        Assert.Equal(2, plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow().Restarts);
    }

    /// <summary>
    /// 配置来源的绑定：给的是属性时另配一个主值
    /// </summary>
    private static (string Key, string Value)[] Bindings(string key, string value)
        => key.EndsWith(":value", StringComparison.Ordinal) ? [(key, value)] : [("XiHan:Bindings:db:value", "configured-db-91c2"), (key, value)];

    /// <summary>
    /// 断言是绑定有孤立代理项而配置失败的错误
    /// </summary>
    /// <remarks>
    /// 插件停在 Failed，原因照 ADR-0321，错误里不带任何值
    /// </remarks>
    private static void AssertLoneSurrogateRejected(Error? error, LifecycleState state)
    {
        Assert.Equal(LifecycleState.Failed, state);
        Assert.NotNull(error);
        Assert.Equal((PluginErrors.Unexpected, PluginMessages.ProcessFailed), (error.Code, error.MessageKey));
        Assert.Equal(("configure", "binding 'db' has a lone surrogate and cannot be encoded as UTF-8"), (error.Details?["phase"], error.Details?["reason"]));
        Assert.DoesNotContain("s3", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 把宿主时钟按秒推进，直到退避结束、开始一次重启
    /// </summary>
    /// <remarks>
    /// 重启计数在退避结束后、重试之前递增：计数没变就还在退避里，只在这时推进时钟。返回是否在 <paramref name="timeout"/> 内开始了重启
    /// </remarks>
    private static async Task<bool> RetriesAfterBackoffAsync(WasmPluginHost plugins, FakeTimeProvider clock, TimeSpan timeout)
    {
        var restarts = plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow().Restarts;
        return await EchoWasmPluginHost.WaitUntilAsync(() =>
        {
            if (plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow().Restarts == restarts)
            {
                clock.Advance(TimeSpan.FromSeconds(1));
            }

            return plugins.FindStatus(WatPlugin.PluginId).ValueOrThrow().Restarts != restarts;
        }, timeout);
    }

    /// <summary>
    /// 把占位 <c>&lt;high&gt;</c>、<c>&lt;low&gt;</c> 换成孤立的高、低代理项
    /// </summary>
    private static string WithLoneSurrogates(string text)
        => text.Replace("<high>", "\uD800", StringComparison.Ordinal).Replace("<low>", "\uDC00", StringComparison.Ordinal);

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
        => StartAsync(plugin, grants, null, extra);

    /// <summary>
    /// 启动装配了 Wasm 插件特性的宿主，可换上给定的时钟
    /// </summary>
    /// <remarks>
    /// 绑定根目录指向本用例的目录
    /// </remarks>
    private Task<TestHost> StartAsync(WatPlugin plugin, IReadOnlyList<string> grants, TimeProvider? clock, params (string Key, string Value)[] extra)
    {
        var configuration = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory),
            new("XiHan:Plugins:Wasm:EpochIntervalMilliseconds", "5"),
        };
        configuration.AddRange(grants.Select(static (grant, index) => new KeyValuePair<string, string?>($"XiHan:Plugins:Wasm:Grants:{WatPlugin.PluginId}:{index}", grant)));
        configuration.AddRange(extra.Select(static e => new KeyValuePair<string, string?>(e.Key, e.Value)));
        Action<IServiceCollection>? services = null;
        if (clock is { } given)
        {
            // 退避按宿主时钟等待：换成可推进的时钟，用例不必真的等
            services = s => s.AddSingleton(given);
        }

        return TestHost.StartAsync(b => b.UseServiceBindingRoot(_root).UseFeature<WasmPluginsFeature>(), configuration, services);
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
