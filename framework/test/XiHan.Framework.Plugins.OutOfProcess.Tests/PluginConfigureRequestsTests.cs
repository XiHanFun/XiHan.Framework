// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;
using Binding = XiHan.Framework.Primitives.Manifests.Binding;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 两个宿主共用的 <c>Configure</c> 请求组装：下发给插件的绑定与内核按同一套来源解析
/// </summary>
/// <remarks>
/// 规则出自 <c>spec/plugin/lifecycle.md</c> §2、RFC-0016、ADR-0268 与 ADR-0285：先读 Service Binding 目录、再读配置节，同名时配置整体覆盖目录；只下发清单声明的绑定；绑定连同属性一起下发，属性名有文法、数量与字节数有上限，属性值不进日志与错误。每个用例用自己的临时目录作绑定根，不碰环境变量
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
        // 属性与内核交给特性的是同一份：type 文件不是属性，uri 既是主值也是属性，内容去掉首尾空白
        Assert.Equal(new Dictionary<string, string> { ["uri"] = "postgres://db/app", ["username"] = "app" }, binding.Properties);
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
        Assert.Equal(["cache", "queue"], request.Bindings.Select(static b => b.Name));
        var queue = request.Bindings[1];
        Assert.Equal(("queue", "queue", "amqp://lower-section"), (queue.Name, queue.Type, queue.Value));
        // 大写的 VALUE 不是主值的键：内核把它读成一个名叫 VALUE 的属性，下发给插件的也是这样（主值为空、属性 VALUE），
        // 与宿主进程里的特性看到的一致；修之前属性不下发，这个绑定整个到不了插件
        var cache = request.Bindings[0];
        Assert.Equal(string.Empty, cache.Value);
        Assert.Equal(new Dictionary<string, string> { ["VALUE"] = "upper-key:6379" }, cache.Properties);
    }

    [Fact]
    public void Bindings_WithOnlyProperties_AreDeliveredWithAnEmptyValue()
    {
        // 只有属性文件、没有 uri（Service Binding 目录里常见的形态）：修之前线协议带不了属性，这个绑定整个不下发（ADR-0268），
        // 现在照常下发，主值为空、属性都在（ADR-0285）
        Mount("db", ("type", "connection:relational"), ("host", "db.internal"), ("port", "5432\n"), ("username", "app"), ("password", "s3cret"));
        Mount("cache", ("uri", "mounted:6379"), ("password", "c4che"));

        var request = Build(Manifest(new Binding("db", "connection:relational"), new Binding("cache", "cache:distributed")), Configuration());

        var db = request.Bindings.Single(static b => b.Name == "db");
        Assert.Equal(("connection:relational", string.Empty), (db.Type, db.Value));
        Assert.Equal(new Dictionary<string, string> { ["host"] = "db.internal", ["port"] = "5432", ["username"] = "app", ["password"] = "s3cret" }, db.Properties);
        // 带 uri 的绑定里另放的凭据同样到得了插件
        var cache = request.Bindings.Single(static b => b.Name == "cache");
        Assert.Equal("mounted:6379", cache.Value);
        Assert.Equal(new Dictionary<string, string> { ["uri"] = "mounted:6379", ["password"] = "c4che" }, cache.Properties);
        // 下发成功不记日志：属性值没有理由出现在宿主的任何输出里
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public void Bindings_WithNeitherAValueNorProperties_AreNotDelivered_AndTheHostSaysSo()
    {
        // 来源只给了类型（或一个空目录）：没有任何能交给插件的内容
        Mount("db", ("type", "connection:relational"));
        Mount("empty");
        Mount("cache", ("uri", "mounted:6379"), ("password", "s3cret"));

        var request = Build(
            Manifest(new Binding("db", "connection:relational"), new Binding("empty", "queue"), new Binding("cache", "cache:distributed"), new Binding("typed", "blob")),
            Configuration(("XiHan:Bindings:typed:type", "blob")));

        Assert.Equal(["cache"], request.Bindings.Select(static b => b.Name));
        var warnings = _logs.Entries.Where(static e => e.Level == LogLevel.Warning).ToList();
        Assert.Equal(3, warnings.Count);
        Assert.Equal(_logs.Entries.Count, warnings.Count);
        Assert.All(["'db'", "'empty'", "'typed'"], name => Assert.Contains(warnings, w => w.Message.Contains(name, StringComparison.Ordinal) && w.Message.Contains(PluginId, StringComparison.Ordinal)));
        Assert.DoesNotContain("s3cret", string.Join('\n', _logs.Entries.Select(static e => e.Message)), StringComparison.Ordinal);
    }

    [Fact]
    public void Properties_InConfiguration_AreTheKeysBesideTypeAndValue_CaseSensitive()
    {
        // 配置来源：type、value 之外有值的子键是属性，原样下发——不折叠大小写、不改写，值为空的照发；嵌套的节不是属性
        var request = Build(
            Manifest(new Binding("objectstorage", "blob")),
            Configuration(
                ("XiHan:Bindings:objectstorage:type", "blob"),
                ("XiHan:Bindings:objectstorage:value", "https://s3.internal"),
                ("XiHan:Bindings:objectstorage:accessKey", "AKIAEXAMPLE"),
                ("XiHan:Bindings:objectstorage:SecretKey", " s3cret "),
                ("XiHan:Bindings:objectstorage:sasl.username", string.Empty),
                ("XiHan:Bindings:objectstorage:nested:child", "ignored")));

        var binding = Assert.Single(request.Bindings);
        Assert.Equal(("blob", "https://s3.internal"), (binding.Type, binding.Value));
        Assert.Equal(new Dictionary<string, string> { ["accessKey"] = "AKIAEXAMPLE", ["SecretKey"] = " s3cret ", ["sasl.username"] = string.Empty }, binding.Properties);
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public void Properties_InConfiguration_ReplaceTheMountedOnesAsAWhole()
    {
        // 同名时配置整体覆盖目录：属性不逐个合并，目录里的 password 不会漏到配置给出的绑定上
        Mount("db", ("uri", "postgres://mounted/app"), ("username", "mounted"), ("password", "s3cret"));

        var request = Build(
            Manifest(new Binding("db", "connection:relational")),
            Configuration(("XiHan:Bindings:db:username", "configured")));

        var binding = Assert.Single(request.Bindings);
        Assert.Equal(string.Empty, binding.Value);
        Assert.Equal(new Dictionary<string, string> { ["username"] = "configured" }, binding.Properties);
    }

    [Fact]
    public void Properties_AreResolvedAgainOnEveryRequest()
    {
        // 挂载的 Secret 轮换后，重启或再加载的插件拿到的是新的凭据属性，不用重启宿主
        Mount("db", ("username", "app"), ("password", "before"));
        var manifest = Manifest(new Binding("db", "connection:relational"));

        Assert.Equal("before", Assert.Single(Build(manifest, Configuration()).Bindings).Properties["password"]);

        File.WriteAllText(Path.Combine(_root, "db", "password"), "after");
        File.Delete(Path.Combine(_root, "db", "username"));
        Assert.Equal(new Dictionary<string, string> { ["password"] = "after" }, Assert.Single(Build(manifest, Configuration()).Bindings).Properties);
    }

    [Fact]
    public void Properties_AtTheLimits_AreDelivered()
    {
        // 64 个属性；名字 253 个字符；名与值合计正好 262144 个 UTF-8 字节——三个上限各踩在线上
        var longName = new string('n', PluginBindingProperties.MaxNameLength);
        var request = Build(
            Manifest(new Binding("many", "queue"), new Binding("named", "queue"), new Binding("large", "queue"), new Binding("wide", "queue")),
            Configuration([
                .. Enumerable.Range(0, PluginBindingProperties.MaxCount).Select(static i => ($"XiHan:Bindings:many:p{i:D2}", "v")),
                ($"XiHan:Bindings:named:{longName}", "v"),
                // 主值不计入上限
                ("XiHan:Bindings:large:value", "amqp://mq.internal"),
                ("XiHan:Bindings:large:blob", new string('v', PluginBindingProperties.MaxBytes - 4)),
                // 非 ASCII 的值按 UTF-8 字节计：87380 个三字节字符加 4 字节的名字
                ("XiHan:Bindings:wide:blob", new string('值', (PluginBindingProperties.MaxBytes - 4) / 3))]));

        Assert.Equal([64, 1, 1, 1], request.Bindings.Select(static b => b.Properties.Count));
        Assert.Equal("v", request.Bindings[1].Properties[longName]);
        Assert.Equal(PluginBindingProperties.MaxBytes - 4, request.Bindings[2].Properties["blob"].Length);
        Assert.Equal(PluginBindingProperties.MaxBytes, 4 + Encoding.UTF8.GetByteCount(request.Bindings[3].Properties["blob"]));
        Assert.Empty(_logs.Entries);
    }

    [Theory]
    [InlineData("count", "binding 'db' has more than 64 properties")]
    [InlineData("name-space", "binding 'db' has a property name that is not valid")]
    [InlineData("name-dot", "binding 'db' has a property name that is not valid")]
    [InlineData("name-unicode", "binding 'db' has a property name that is not valid")]
    [InlineData("name-long", "binding 'db' has a property name that is not valid")]
    [InlineData("bytes", "the properties of binding 'db' exceed 262144 bytes")]
    [InlineData("bytes-utf8", "the properties of binding 'db' exceed 262144 bytes")]
    public void Properties_BeyondTheLimits_FailTheRequestInsteadOfDeliveringFewer(string violation, string reason)
    {
        // 不少发、不截断：插件带着缺的凭据启动比启动失败更难查，所以让配置这一步失败
        (string Key, string Value)[] offending = violation switch
        {
            "count" => [.. Enumerable.Range(0, PluginBindingProperties.MaxCount + 1).Select(static i => ($"XiHan:Bindings:db:p{i:D2}", "s3cret"))],
            "name-space" => [("XiHan:Bindings:db:user name", "s3cret")],
            "name-dot" => [("XiHan:Bindings:db:.hidden", "s3cret")],
            "name-unicode" => [("XiHan:Bindings:db:口令", "s3cret")],
            "name-long" => [("XiHan:Bindings:db:" + new string('n', PluginBindingProperties.MaxNameLength + 1), "s3cret")],
            // password 的名与值共 14 字节，blob 的名 4 字节：合计比上限多 1 字节
            "bytes" => [("XiHan:Bindings:db:password", "s3cret"), ("XiHan:Bindings:db:blob", new string('v', PluginBindingProperties.MaxBytes - 14 - 4 + 1))],
            // 按 UTF-16 码元计只有 87385 个，按字节计超过上限
            "bytes-utf8" => [("XiHan:Bindings:db:blob", new string('值', ((PluginBindingProperties.MaxBytes - 4) / 3) + 1))],
            _ => throw new ArgumentOutOfRangeException(nameof(violation)),
        };

        var built = PluginConfigureRequests.Build(
            Manifest(new Binding("cache", "cache:distributed"), new Binding("db", "connection:relational")),
            Configuration([("XiHan:Bindings:cache:value", "configured:6379"), ("XiHan:Bindings:db:value", "Host=db;Password=s3cret"), .. offending]),
            _root,
            "work",
            _logs.CreateLogger("test"));

        Assert.Equal(PluginErrors.Unexpected, built.Error?.Code);
        Assert.Equal(PluginMessages.ProcessFailed, built.Error!.MessageKey);
        Assert.Equal(PluginId, built.Error.Details!["id"]);
        Assert.Equal("configure", built.Error.Details["phase"]);
        Assert.Equal(reason, built.Error.Details["reason"]);
        // 错误与日志只带绑定名与上限的数字：属性名、属性值与主值都不出现
        var logged = Assert.Single(_logs.Entries);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Contains(reason, logged.Message, StringComparison.Ordinal);
        Assert.Contains(PluginId, logged.Message, StringComparison.Ordinal);
        foreach (var secret in new[] { "s3cret", "user name", ".hidden", "口令", "nnnn", "vvvv", "值值" })
        {
            Assert.DoesNotContain(secret, logged.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, built.Error.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(secret, PluginJson.SerializeDetails(built.Error.Details), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Properties_AreCheckedInManifestOrder_CountBeforeNamesBeforeBytes()
    {
        (string, string)[] tooManyWithABadName = [.. Enumerable.Range(0, PluginBindingProperties.MaxCount).Select(static i => ($"XiHan:Bindings:cache:p{i:D2}", "v")), ("XiHan:Bindings:cache:user name", "v")];
        (string, string)[] badNameAndTooLarge = [("XiHan:Bindings:db:user name", "v"), ("XiHan:Bindings:db:blob", new string('v', PluginBindingProperties.MaxBytes))];

        // 清单里 cache 在前：两个绑定都不合时报 cache 的；它既超过数量又有不合文法的名字，报数量
        var first = PluginConfigureRequests.Build(Manifest(new Binding("cache", "cache:distributed"), new Binding("db", "connection:relational")), Configuration([.. tooManyWithABadName, .. badNameAndTooLarge]), _root, "work", _logs.CreateLogger("test"));
        Assert.Equal("binding 'cache' has more than 64 properties", first.Error?.Details!["reason"]);

        // 属性名先于字节数
        var second = PluginConfigureRequests.Build(Manifest(new Binding("db", "connection:relational")), Configuration(badNameAndTooLarge), _root, "work", _logs.CreateLogger("test"));
        Assert.Equal("binding 'db' has a property name that is not valid", second.Error?.Details!["reason"]);
    }

    [Fact]
    public void Properties_OfBindingsThePluginDidNotDeclare_AreNotChecked()
    {
        // 别的绑定属性不合文法不关这个插件的事：宿主只读、只核对清单声明的那几个
        var request = Build(
            Manifest(new Binding("db", "connection:relational")),
            Configuration(("XiHan:Bindings:db:username", "app"), ("XiHan:Bindings:other:user name", "s3cret")));

        Assert.Equal(new Dictionary<string, string> { ["username"] = "app" }, Assert.Single(request.Bindings).Properties);
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public void Properties_DifferingOnlyInCase_AreBothDelivered_WhereTheFileSystemKeepsThemApart()
    {
        // 属性名区分大小写（RFC-0016 第 4 条）：大小写敏感的文件系统上两个文件是两个属性；Windows 与 macOS 的缺省文件系统上它们是同一个文件
        Mount("db", ("username", "lower"));
        File.WriteAllText(Path.Combine(_root, "db", "Username"), "upper");
        var caseSensitive = Directory.EnumerateFiles(Path.Combine(_root, "db")).Count() == 2;

        var properties = Assert.Single(Build(Manifest(new Binding("db", "connection:relational")), Configuration()).Bindings).Properties;

        Assert.Equal(
            caseSensitive ? new Dictionary<string, string> { ["username"] = "lower", ["Username"] = "upper" } : new Dictionary<string, string> { ["username"] = "upper" },
            properties);
    }

    [Fact]
    public void Properties_AreSkippedByAPluginBuiltAgainstTheEarlierMessage()
    {
        // 协议版本不变（规范的 MINOR）：宿主照发 properties，按较早的规范生成代码的插件把字段 4 当未知字段跳过，
        // name、type、value 读到的与以前一样——只是会收到以前不下发的、主值为空的绑定
        Mount("db", ("host", "db.internal"), ("password", "s3cret"));
        Mount("cache", ("uri", "mounted:6379"), ("password", "c4che"));

        var request = Build(Manifest(new Binding("db", "connection:relational"), new Binding("cache", "cache:distributed")), Configuration());

        Assert.Equal([("db", "connection:relational", string.Empty), ("cache", "cache:distributed", "mounted:6379")], ReadWithoutProperties(request.ToByteArray()));
        // Wasm 载体的 JSON 文本同理：只认三个成员的读法读到的不变（wasm.md §2：未知字段忽略）
        using var json = JsonDocument.Parse(PluginJsonCodec.Encode(request));
        var cache = json.RootElement.GetProperty("bindings")[1];
        Assert.Equal(("cache", "cache:distributed", "mounted:6379"), (cache.GetProperty("name").GetString(), cache.GetProperty("type").GetString(), cache.GetProperty("value").GetString()));
        Assert.Equal("c4che", cache.GetProperty("properties").GetProperty("password").GetString());
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
    /// 按较早的消息定义读请求字节里的绑定
    /// </summary>
    /// <remarks>
    /// 只认 <c>Binding</c> 的 name、type、value（字段 1 到 3）。按较早的规范生成的消息类型就是这样读 <c>ConfigureRequest</c> 的：不认识的字段整个跳过
    /// </remarks>
    private static List<(string Name, string Type, string Value)> ReadWithoutProperties(byte[] request)
    {
        var bindings = new List<(string Name, string Type, string Value)>();
        var input = new CodedInputStream(request);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            // ConfigureRequest.bindings 是字段 3（长度前缀）
            if (tag != ((3 << 3) | 2))
            {
                input.SkipLastField();
                continue;
            }

            var message = new CodedInputStream(input.ReadBytes().ToByteArray());
            var (name, type, value) = (string.Empty, string.Empty, string.Empty);
            uint field;
            while ((field = message.ReadTag()) != 0)
            {
                switch (field)
                {
                    case (1 << 3) | 2:
                        name = message.ReadString();
                        break;
                    case (2 << 3) | 2:
                        type = message.ReadString();
                        break;
                    case (3 << 3) | 2:
                        value = message.ReadString();
                        break;
                    default:
                        message.SkipLastField();
                        break;
                }
            }

            bindings.Add((name, type, value));
        }

        return bindings;
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
