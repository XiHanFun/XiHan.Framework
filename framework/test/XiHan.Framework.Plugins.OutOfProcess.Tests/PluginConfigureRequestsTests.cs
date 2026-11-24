// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;
using Binding = XiHan.Framework.Primitives.Manifests.Binding;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 两个宿主共用的 <c>Configure</c> 请求组装：下发给插件的绑定与内核按同一套来源解析
/// </summary>
/// <remarks>
/// 规则出自 <c>spec/plugin/lifecycle.md</c> §2、RFC-0016 与 ADR-0268：先读 Service Binding 目录、再读配置节，同名时配置整体覆盖目录；只下发清单声明的绑定；线协议的绑定只有名字、类型与主值。每个用例用自己的临时目录作绑定根，不碰环境变量
/// </remarks>
public sealed class PluginConfigureRequestsTests : IDisposable
{
    /// <summary>
    /// 插件的清单 ID
    /// </summary>
    private const string PluginId = "xihan.test.bound";

    /// <summary>
    /// 本用例的 Service Binding 根目录
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xihan-plugin-bindings-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 收集组装时写的日志
    /// </summary>
    private readonly LogCapture _logs = new();

    /// <inheritdoc />
    public void Dispose()
    {
        _logs.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Bindings_GivenOnlyByTheServiceBindingDirectory_AreDelivered()
    {
        // Kubernetes 上绑定只经 SERVICE_BINDING_ROOT 挂载（app-manifest.md「映射」）：配置里一项都没有
        Mount("db", ("type", "connection:relational\n"), ("uri", "postgres://db/app\n"), ("username", "app"));

        var request = Build(Manifest(new Binding("db", "connection:relational")), Configuration());

        var binding = Assert.Single(request.Bindings);
        Assert.Equal(("db", "connection:relational", "postgres://db/app"), (binding.Name, binding.Type, binding.Value));
    }

    [Fact]
    public void Bindings_InConfiguration_ReplaceTheDirectoryBindingOfTheSameName()
    {
        Mount("db", ("type", "connection:document"), ("uri", "mongodb://mounted/app"));
        Mount("cache", ("type", "cache:distributed"), ("uri", "mounted:6379"));

        var request = Build(
            Manifest(new Binding("db", "connection:relational"), new Binding("cache", "cache:distributed")),
            Configuration(("XiHan:Bindings:db:value", "Host=configured")));

        // 同名时配置整体覆盖目录：主值是配置的；配置没写类型，目录里的类型不合并进来，退回清单声明的类型
        var db = request.Bindings.Single(static b => b.Name == "db");
        Assert.Equal(("connection:relational", "Host=configured"), (db.Type, db.Value));
        // 配置里没有的名字照旧来自目录
        var cache = request.Bindings.Single(static b => b.Name == "cache");
        Assert.Equal(("cache:distributed", "mounted:6379"), (cache.Type, cache.Value));
    }

    [Fact]
    public void Bindings_NotDeclaredByTheManifest_AreNotDelivered()
    {
        Mount("db", ("uri", "postgres://db/app"));
        Mount("secrets", ("uri", "vault://mounted"));

        var request = Build(
            Manifest(new Binding("db", "connection:relational")),
            Configuration(("XiHan:Bindings:queue:value", "amqp://configured")));

        // 两种来源里的其余绑定都不是这个插件的：清单没声明就不下发
        Assert.Equal(["db"], request.Bindings.Select(static b => b.Name));
    }

    [Fact]
    public void Bindings_TakeTheirTypeFromTheSource_AndFallBackToTheManifest()
    {
        Mount("typed", ("type", "cache:distributed"), ("uri", "mounted:6379"));
        Mount("untyped", ("uri", "postgres://db/app"));

        var request = Build(
            Manifest(new Binding("typed", "cache:distributed"), new Binding("untyped", "connection:relational"), new Binding("configured", "queue"), new Binding("scalar", "blob")),
            Configuration(
                ("XiHan:Bindings:configured:type", "queue"),
                ("XiHan:Bindings:configured:value", "amqp://configured"),
                ("XiHan:Bindings:scalar", "https://s3.example.com")));

        Assert.Equal(
            [("typed", "cache:distributed", "mounted:6379"), ("untyped", "connection:relational", "postgres://db/app"), ("configured", "queue", "amqp://configured"), ("scalar", "blob", "https://s3.example.com")],
            request.Bindings.Select(static b => (b.Name, b.Type, b.Value)));
    }

    [Fact]
    public void Bindings_InConfiguration_AreReadTheWayTheKernelReadsThem()
    {
        // 与内核同一个解析器：绑定名与 type / value 子键区分大小写（RFC-0016「资源绑定与配置节」第 4 条）。
        // 全大写环境变量写出来的键内核读不到主值，下发给插件时也一样；原先这里经配置的索引器读、不区分大小写
        var request = Build(
            Manifest(new Binding("db", "connection:relational"), new Binding("cache", "cache:distributed"), new Binding("queue", "queue")),
            Configuration(
                ("XiHan:Bindings:DB:value", "Host=upper-name"),
                ("XiHan:Bindings:cache:VALUE", "upper-key:6379"),
                ("xihan:bindings:queue:value", "amqp://lower-section")));

        // 节名 XiHan:Bindings 本身照配置的规则不区分大小写；绑定名与子键按字面比
        var queue = Assert.Single(request.Bindings);
        Assert.Equal(("queue", "queue", "amqp://lower-section"), (queue.Name, queue.Type, queue.Value));
    }

    [Fact]
    public void Bindings_WithoutAMainValue_AreNotDelivered_AndTheHostSaysSo()
    {
        // 只有属性文件、没有 uri：线协议的绑定只带主值，属性送不过去，这个绑定对插件等于没有
        Mount("db", ("type", "connection:relational"), ("host", "db.internal"), ("username", "app"), ("password", "s3cret"));
        Mount("cache", ("uri", "mounted:6379"), ("password", "s3cret"));

        var request = Build(Manifest(new Binding("db", "connection:relational"), new Binding("cache", "cache:distributed")), Configuration());

        // 有主值的照常下发，它的其余属性不随请求走
        var cache = Assert.Single(request.Bindings);
        Assert.Equal(("cache", "mounted:6379"), (cache.Name, cache.Value));
        var warning = Assert.Single(_logs.Entries, static e => e.Level == LogLevel.Warning);
        Assert.Contains("'db'", warning.Message, StringComparison.Ordinal);
        Assert.Contains(PluginId, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret", string.Join('\n', _logs.Entries.Select(static e => e.Message)), StringComparison.Ordinal);
    }

    [Fact]
    public void Bindings_MissingServiceBindingRoot_IsNotAFailure()
    {
        // 根目录不存在是常态（不在 Kubernetes 上部署）：只是没有目录来源，配置来源照常
        Assert.False(Directory.Exists(_root));

        var built = PluginConfigureRequests.Build(
            Manifest(new Binding("db", "connection:relational"), new Binding("cache", "cache:distributed")),
            Configuration(("XiHan:Bindings:cache:value", "configured:6379")),
            _root,
            "work",
            _logs.CreateLogger("test"));

        Assert.True(built.IsSuccess, built.ToString());
        var cache = Assert.Single(built.ValueOrThrow().Bindings);
        Assert.Equal(("cache", "cache:distributed", "configured:6379"), (cache.Name, cache.Type, cache.Value));
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public void Bindings_AreResolvedAgainOnEveryRequest()
    {
        // 配置插件的那一刻解析：运行期加载或重启的插件拿到的是那时的绑定，挂载的 Secret 轮换后不用重启宿主
        Mount("db", ("uri", "postgres://db/before"));
        var manifest = Manifest(new Binding("db", "connection:relational"));
        var configuration = Configuration();

        Assert.Equal("postgres://db/before", Assert.Single(Build(manifest, configuration).Bindings).Value);

        File.WriteAllText(Path.Combine(_root, "db", "uri"), "postgres://db/after");
        Assert.Equal("postgres://db/after", Assert.Single(Build(manifest, configuration).Bindings).Value);

        configuration["XiHan:Bindings:db:value"] = "Host=configured";
        Assert.Equal("Host=configured", Assert.Single(Build(manifest, configuration).Bindings).Value);
    }

    [Fact]
    public void Bindings_ThatCannotBeRead_FailTheRequestInsteadOfDeliveringFewer()
    {
        Mount("db", ("uri", "postgres://db/app"));
        using var unreadable = Unreadable("db", "password");

        var built = PluginConfigureRequests.Build(Manifest(new Binding("db", "connection:relational")), Configuration(), _root, "work", _logs.CreateLogger("test"));

        // 读失败不当成「没有绑定」：少给一个绑定插件会带着缺的连接启动，这里让配置这一步失败、由重启策略再试
        Assert.Equal(PluginErrors.Unexpected, built.Error?.Code);
        Assert.Contains(PluginId, built.Error!.Message, StringComparison.Ordinal);
        // 错误里只有异常类型，路径与异常消息只进日志
        Assert.DoesNotContain(_root, built.Error.Message, StringComparison.Ordinal);
        var logged = Assert.Single(_logs.Entries);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.IsAssignableFrom<IOException>(logged.Exception);
    }

    [Fact]
    public void Bindings_ThePluginDidNotDeclare_AreNotRead()
    {
        // 一个插件的配置只取决于它自己声明的绑定：别的绑定读不出来（正在轮换、权限不对）不让它失败，宿主也不为它去读别人的凭据
        Mount("db", ("uri", "postgres://db/app"));
        Mount("secrets", ("uri", "vault://mounted"));
        using var unreadable = Unreadable("secrets", "token");

        var built = PluginConfigureRequests.Build(Manifest(new Binding("db", "connection:relational")), Configuration(), _root, "work", _logs.CreateLogger("test"));

        Assert.True(built.IsSuccess, built.ToString());
        Assert.Equal(["db"], built.ValueOrThrow().Bindings.Select(static b => b.Name));
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public void Request_CarriesTheConfigurationSectionGrantsAndWorkDirectory()
    {
        var manifest = TestManifests.Create(PluginId, kind: ManifestKind.Plugin) with
        {
            Permissions = ["capability:kernel.pipeline"],
            Contributes = new Contributions { Config = new ConfigContribution("XiHan:Bound") },
        };

        var request = Build(manifest, Configuration(("XiHan:Bound:Greeting", "hello")));

        Assert.Equal("""{"Greeting":"hello"}""", request.Configuration);
        Assert.Equal(["capability:kernel.pipeline"], request.GrantedPermissions);
        Assert.Equal("work", request.WorkDirectory);
        Assert.Empty(request.Bindings);
    }

    [Fact]
    public void Preflight_RejectsABindingThatIsDeclaredButNotGranted()
    {
        // 能不能拿到绑定由授予决定、与来源无关：没有 binding:<名> 的插件在发现时就被拒，走不到组装请求这一步（ADR-0177）
        var manifest = Manifest(new Binding("db", "connection:relational"), new Binding("cache", "cache:distributed")) with { Entry = "plugin" };

        var denied = PluginPreflight.Check(new PluginSource(_root, new PluginPermissions(["binding:cache"])), manifest, static _ => true);
        var granted = PluginPreflight.Check(new PluginSource(_root, new PluginPermissions(["binding:cache", "binding:db"])), manifest, static _ => true);

        Assert.Equal(PluginErrors.PermissionDenied, denied.Error?.Code);
        Assert.Contains("binding:db", denied.Error!.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("binding:cache", denied.Error.Message, StringComparison.Ordinal);
        Assert.True(granted.IsSuccess, granted.ToString());
    }

    /// <summary>
    /// 以本用例的绑定根组装请求，要求成功
    /// </summary>
    private ConfigureRequest Build(Manifest manifest, IConfiguration configuration)
        => PluginConfigureRequests.Build(manifest, configuration, _root, "work", _logs.CreateLogger("test")).ValueOrThrow();

    /// <summary>
    /// 声明了这些绑定的插件清单
    /// </summary>
    private static Manifest Manifest(params Binding[] bindings)
        => TestManifests.Create(PluginId, kind: ManifestKind.Plugin, bindings: bindings);

    /// <summary>
    /// 内存里的宿主配置
    /// </summary>
    private static IConfigurationRoot Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    /// <summary>
    /// 在一个绑定目录里放一个读不出来的属性文件
    /// </summary>
    /// <remarks>
    /// Windows 上是独占打开的文件，释放返回值即放开；其他系统上是指向不存在目标的符号链接（Secret 轮换到一半时就是这样）
    /// </remarks>
    private FileStream? Unreadable(string binding, string file)
    {
        var path = Path.Combine(_root, binding, file);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(path, "s3cret");
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        File.CreateSymbolicLink(path, Path.Combine(_root, binding, "..missing", file));
        return null;
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
