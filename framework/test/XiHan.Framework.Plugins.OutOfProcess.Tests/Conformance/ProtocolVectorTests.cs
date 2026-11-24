// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;
using ConfigureRequest = XiHan.Framework.Plugins.Protocol.V1.ConfigureRequest;
using ManifestBinding = XiHan.Framework.Primitives.Manifests.Binding;
using ProtoBinding = XiHan.Framework.Plugins.Protocol.V1.Binding;
using ProtoError = XiHan.Framework.Plugins.Protocol.V1.Error;
using ProtoStatus = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Conformance;

/// <summary>
/// 回放 spec/conformance/vectors/plugin 下的向量，以及选项解析
/// </summary>
public sealed class ProtocolVectorTests
{
    [Fact]
    public void HandshakeLines_FollowTheVectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "handshake-lines.json")));
        foreach (var valid in document.RootElement.GetProperty("valid").EnumerateArray())
        {
            var line = valid.GetProperty("line").GetString();
            Assert.True(PluginEndpoint.TryParseHandshakeLine(line, out var endpoint), line);
            Assert.Equal(valid.GetProperty("network").GetString(), endpoint.Network);
            Assert.Equal(valid.GetProperty("address").GetString(), endpoint.Address);
            Assert.Equal(line, endpoint.ToHandshakeLine());
        }

        foreach (var invalid in document.RootElement.GetProperty("invalid").EnumerateArray())
        {
            Assert.False(PluginEndpoint.TryParseHandshakeLine(invalid.GetString(), out _), invalid.GetString());
        }
    }

    [Fact]
    public void ProtocolVersions_FollowTheVectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "protocol-versions.json")));
        foreach (var @case in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var host = @case.GetProperty("host").EnumerateArray().Select(static v => v.GetUInt32());
            var plugin = @case.GetProperty("plugin").EnumerateArray().Select(static v => v.GetUInt32());
            var selected = PluginProtocol.Negotiate(host, plugin);
            if (@case.TryGetProperty("selected", out var expected))
            {
                Assert.Equal(expected.GetUInt32(), selected.ValueOrThrow());
            }
            else
            {
                Assert.False(selected.HasValue, @case.ToString());
                Assert.Equal(PluginErrors.InvalidHandshake, @case.GetProperty("error").GetString());
            }
        }
    }

    [Fact]
    public void Endpoint_RejectsNonLoopbackTcp_AndParsesEnvironmentForm()
    {
        Assert.Throws<ArgumentException>(static () => new PluginEndpoint("tcp", "10.0.0.5:1"));
        Assert.Throws<ArgumentException>(static () => new PluginEndpoint("udp", "x"));
        Assert.True(PluginEndpoint.TryParse("npipe \\\\.\\pipe\\xihan-abc", out var pipe));
        Assert.Equal("npipe \\\\.\\pipe\\xihan-abc", pipe.ToString());
        Assert.False(PluginEndpoint.TryParse("unix", out _));
    }

    [Theory]
    [InlineData("127.1:50051")]
    [InlineData("0177.0.0.1:50051")]
    [InlineData("[0:0:0:0:0:0:0:1]:50051")]
    [InlineData("127.0.0.1:050051")]
    [InlineData("[::ffff:127.0.0.1]:50051")]
    public void Endpoint_RejectsLoopbackSpellingsThatIPEndPointAccepts(string address)
    {
        // 修之前用 IPEndPoint.TryParse + IsLoopback：这些写法都被当成回环地址（ADR-0237）
        Assert.False(PluginEndpoint.TryParse("tcp " + address, out _), address);
        Assert.Throws<ArgumentException>(() => new PluginEndpoint(PluginEndpoint.Tcp, address));
    }

    [Fact]
    public void HostAddresses_FollowTheVectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "host-addresses.json")));
        foreach (var valid in document.RootElement.GetProperty("valid").EnumerateArray())
        {
            var value = valid.GetProperty("value").GetString();
            Assert.True(PluginEndpoint.TryParseList(value, out var endpoints), value);
            var expected = valid.GetProperty("entries").EnumerateArray().Select(static e => new PluginEndpoint(e.GetProperty("network").GetString()!, e.GetProperty("address").GetString()!)).ToList();
            Assert.Equal(expected, endpoints);
            Assert.Equal(value, PluginEndpoint.FormatList(endpoints));
        }

        foreach (var invalid in document.RootElement.GetProperty("invalid").EnumerateArray())
        {
            Assert.False(PluginEndpoint.TryParseList(invalid.GetString(), out _), invalid.GetString());
        }

        // 选择是插件一侧的事：解析整个列表，取第一个 network 在支持集合里的项
        foreach (var selection in document.RootElement.GetProperty("selection").EnumerateArray())
        {
            var value = selection.GetProperty("value").ValueKind == JsonValueKind.Null ? null : selection.GetProperty("value").GetString();
            var supported = selection.GetProperty("supported").EnumerateArray().Select(static n => n.GetString()!).ToHashSet(StringComparer.Ordinal);
            var selected = PluginEndpoint.TryParseList(value, out var endpoints) ? endpoints.FirstOrDefault(e => supported.Contains(e.Network)) : null;
            var expected = selection.GetProperty("selected");
            if (expected.ValueKind == JsonValueKind.Null)
            {
                Assert.Null(selected);
            }
            else
            {
                Assert.Equal(new PluginEndpoint(expected.GetProperty("network").GetString()!, expected.GetProperty("address").GetString()!), selected);
            }
        }
    }

    [Fact]
    public void HostAddresses_CannotListAnAddressWithASemicolon()
    {
        // 握手行的地址可以含 ';'，列表里的不行：它是分隔符
        Assert.True(PluginEndpoint.TryParseHandshakeLine("xihan-plugin/1 unix /tmp/a;b/plugin.sock", out var endpoint));
        Assert.Throws<ArgumentException>(() => PluginEndpoint.FormatList([endpoint]));
        Assert.Throws<ArgumentException>(static () => PluginEndpoint.FormatList([]));
    }

    [Fact]
    public void Deadlines_FollowTheVectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "deadlines.json")));
        // 文法本身由原语 DeadlineText 回放（原语层的测试）；这里走插件协议自己的读写点：元数据的 deadline 怎样成为生效的截止时间、上下文的截止时间怎样写进元数据
        var invalidTexts = document.RootElement.GetProperty("invalid").EnumerateArray().Select(static text => text.GetString()).ToList();
        Assert.NotEmpty(invalidTexts);
        foreach (var valid in document.RootElement.GetProperty("valid").EnumerateArray())
        {
            var text = valid.GetProperty("text").GetString();
            var read = PluginDeadlines.Effective(text, null);
            Assert.True(read.HasValue, text);
            Assert.Equal(Utc(valid.GetProperty("utc").GetString()!), read.Value);
            Assert.Equal(TimeSpan.Zero, read.Value.Offset);
        }

        foreach (var invalid in invalidTexts)
        {
            Assert.False(PluginDeadlines.Effective(invalid, null).HasValue, invalid);
        }

        // ADR-0276 补的宽松写法与并了两个时刻的值都在回放之列：HTTP 入口以前照收的，插件协议一侧从来读不出
        Assert.Contains("Wed, 18 Nov 2026 03:25:00 +0000", invalidTexts);
        Assert.Contains("2026-11-18T03:25:00Z,2026-11-18T03:25:05Z", invalidTexts);

        foreach (var effective in document.RootElement.GetProperty("effective").EnumerateArray())
        {
            var grpc = effective.GetProperty("grpc");
            var expected = effective.GetProperty("expected");
            var actual = PluginDeadlines.Effective(effective.GetProperty("metadata").GetString(), grpc.ValueKind == JsonValueKind.Null ? null : Utc(grpc.GetString()!));
            Assert.Equal(expected.ValueKind == JsonValueKind.Null ? null : Utc(expected.GetString()!), actual);
        }

        foreach (var written in document.RootElement.GetProperty("written").EnumerateArray())
        {
            var context = TestInvocationContext.Create(deadline: Utc(written.GetProperty("utc").GetString()!));
            Assert.Equal(written.GetProperty("text").GetString(), PluginMetadata.FromContext(context).Deadline);
        }
    }

    [Fact]
    public void Deadlines_AreNotReadLeniently()
    {
        // 修之前 DateTimeOffset.TryParse 读得出这些：空格分隔、没有偏移按 UTC、RFC 1123、只有日期（ADR-0237）
        foreach (var text in new[] { "2026-11-18 03:25:00Z", "2026-11-18T03:25:00", "Wed, 18 Nov 2026 03:25:00 GMT", "2026-11-18" })
        {
            Assert.Null(PluginDeadlines.Effective(text, null));
        }
    }

    [Fact]
    public void PluginMessages_MatchTheConstantsAndTheEnglishCatalog()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "plugin-messages.json")));
        var keys = Constants(typeof(PluginMessages)).ToHashSet(StringComparer.Ordinal);
        var codes = Constants(typeof(PluginErrors)).Append(KernelErrors.UnknownOperation).ToHashSet(StringComparer.Ordinal);
        var failures = new List<string>();
        foreach (var message in document.RootElement.GetProperty("messages").EnumerateArray())
        {
            var key = message.GetProperty("key").GetString()!;
            var code = message.GetProperty("code").GetString()!;
            var template = message.GetProperty("template").GetString()!;
            var arguments = message.GetProperty("arguments").EnumerateArray().Select(static a => a.GetString()!).ToList();
            if (!keys.Contains(key))
            {
                failures.Add($"{key}: not a constant of 'PluginMessages'");
            }

            if (!codes.Contains(code))
            {
                failures.Add($"{key}: code '{code}' is not a constant of 'PluginErrors'");
            }

            var catalogued = PluginMessages.Catalog.Find(key, "en-US");
            if (!catalogued.HasValue || catalogued.ValueOrThrow() != template)
            {
                failures.Add($"{key}: en-US template is '{(catalogued.HasValue ? catalogued.ValueOrThrow() : "<missing>")}', the vector has '{template}'");
            }

            var placeholders = MessageTemplate.PlaceholdersOf(template);
            if (!placeholders.SequenceEqual(arguments, StringComparer.Ordinal))
            {
                failures.Add($"{key}: placeholders [{string.Join(", ", placeholders)}] differ from arguments [{string.Join(", ", arguments)}]");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void PluginMessages_FixedReasons_AreWhatTheHostWrites()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "plugin-messages.json")));
        var reasons = document.RootElement.GetProperty("reasons").EnumerateArray().Select(static r => r.GetProperty("reason").GetString()!).ToHashSet(StringComparer.Ordinal);

        // 宿主读插件写来的错误时写出的三种 reason（invocation.md §3）
        var invalidCode = PluginErrorMapper.FromProto(new ProtoError { Code = "not a code" }, "xihan.samples.echo");
        Assert.Contains("invalid error code '{code}'", reasons);
        Assert.Equal("invalid error code 'not a code'", invalidCode.Details!["reason"]);
        Assert.Equal("invoke", invalidCode.Details!["phase"]);

        var withoutError = PluginErrorMapper.FromStatus(new ProtoStatus { Ok = false }, "xihan.samples.echo", "echoes.create").Error!;
        Assert.Contains((string)withoutError.Details!["reason"]!, reasons);

        var unmapped = PluginErrorMapper.FromRpc(new RpcException(new Grpc.Core.Status(StatusCode.DataLoss, "boom")), "xihan.samples.echo", "echoes.create", LifecycleState.Ready);
        Assert.Contains("{status}: {message}", reasons);
        Assert.Equal("DataLoss: boom", unmapped.Details!["reason"]);
    }

    [Fact]
    public void ErrorReading_FollowsTheVectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "error-reading.json")));
        var pluginId = document.RootElement.GetProperty("pluginId").GetString()!;
        foreach (var @case in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var name = @case.GetProperty("name").GetString()!;
            var read = PluginErrorMapper.FromProto(ProtoErrorOf(@case.GetProperty("error")), pluginId);
            AssertError(@case.GetProperty("expected"), read, name);
        }

        // 宿主读插件：Status.ok 为假却没有 error，与整个 status 缺失相同；插件读宿主的几项由插件 SDK 回放
        foreach (var @case in document.RootElement.GetProperty("statusWithoutError").EnumerateArray().Where(static c => c.GetProperty("reader").GetString() == "host"))
        {
            var phase = @case.GetProperty("phase").GetString()!;
            AssertError(@case.GetProperty("expected"), PluginErrorMapper.FromStatus(new ProtoStatus { Ok = false }, pluginId, phase).Error, "ok false without an error, phase " + phase);
            AssertError(@case.GetProperty("expected"), PluginErrorMapper.FromStatus(null, pluginId, phase).Error, "no status, phase " + phase);
        }
    }

    [Fact]
    public void BindingProperties_NamesAndLimits_FollowTheVectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "binding-properties.json")));
        var limits = document.RootElement.GetProperty("limits");
        Assert.Equal(PluginBindingProperties.MaxCount, limits.GetProperty("maxProperties").GetInt32());
        Assert.Equal(PluginBindingProperties.MaxNameLength, limits.GetProperty("maxNameLength").GetInt32());
        Assert.Equal(PluginBindingProperties.MaxBytes, limits.GetProperty("maxBytes").GetInt32());

        var names = document.RootElement.GetProperty("names");
        foreach (var valid in names.GetProperty("valid").EnumerateArray())
        {
            Assert.True(PluginBindingProperties.IsValidName(valid.GetString()!), valid.GetRawText());
        }

        foreach (var invalid in names.GetProperty("invalid").EnumerateArray())
        {
            Assert.False(PluginBindingProperties.IsValidName(invalid.GetString()!), invalid.GetRawText());
        }
    }

    [Fact]
    public void BindingProperties_Delivery_FollowsTheVectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "binding-properties.json")));
        var pluginId = document.RootElement.GetProperty("pluginId").GetString()!;
        var missingRoot = Path.Combine(Path.GetTempPath(), "xihan-no-bindings-" + Guid.NewGuid().ToString("N"));
        foreach (var @case in document.RootElement.GetProperty("delivery").EnumerateArray())
        {
            var name = @case.GetProperty("name").GetString()!;
            var declared = @case.GetProperty("declared").EnumerateArray().Select(static d => new ManifestBinding(d.GetProperty("name").GetString()!, d.GetProperty("type").GetString()!)).ToList();

            // 解析出的绑定经配置来源交给真实的组装：来源没声明类型、没给主值的就不写那个键
            var values = new List<KeyValuePair<string, string?>>();
            foreach (var resolved in @case.GetProperty("resolved").EnumerateObject())
            {
                var section = BindingResolver.ConfigurationSection + ":" + resolved.Name;
                if (resolved.Value.GetProperty("type").GetString() is { Length: > 0 } type)
                {
                    values.Add(new(section + ":type", type));
                }

                if (resolved.Value.GetProperty("value").ValueKind != JsonValueKind.Null)
                {
                    values.Add(new(section + ":value", resolved.Value.GetProperty("value").GetString()));
                }

                values.AddRange(PropertiesOf(resolved.Value.GetProperty("properties")).Select(p => new KeyValuePair<string, string?>(section + ":" + p.Key, p.Value)));
            }

            using var logs = new LogCapture();
            var built = PluginConfigureRequests.Build(
                TestManifests.Create(pluginId, kind: ManifestKind.Plugin, bindings: declared),
                new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
                missingRoot,
                "work",
                logs.CreateLogger("test"));

            if (@case.TryGetProperty("error", out var error))
            {
                // 不发 Configure：按配置这一步失败处理，宿主只记一条 Error
                Assert.True(built.Error is not null, name);
                Assert.True(error.GetProperty("code").GetString() == built.Error.Code, $"{name}: code '{built.Error.Code}'");
                Assert.True(error.GetProperty("key").GetString() == built.Error.MessageKey, $"{name}: key '{built.Error.MessageKey}'");
                using var details = JsonDocument.Parse(PluginJson.SerializeDetails(built.Error.Details!));
                Assert.True(SameJson(error.GetProperty("details"), details.RootElement), $"{name}: details {details.RootElement.GetRawText()}");
                Assert.True(logs.Entries.Count == 1 && logs.Entries[0].Level == LogLevel.Error, $"{name}: logs {logs.Entries.Count}");
                continue;
            }

            Assert.True(built.IsSuccess, $"{name}: {built}");
            var expected = @case.GetProperty("bindings").EnumerateArray().ToList();
            var actual = built.ValueOrThrow().Bindings;
            Assert.True(expected.Count == actual.Count, $"{name}: delivered {actual.Count} bindings");
            foreach (var (want, got) in expected.Zip(actual))
            {
                Assert.True(
                    (want.GetProperty("name").GetString(), want.GetProperty("type").GetString(), want.GetProperty("value").GetString()) == (got.Name, got.Type, got.Value),
                    $"{name}: delivered '{got.Name}' of type '{got.Type}'");
                Assert.True(SameProperties(PropertiesOf(want.GetProperty("properties")), got.Properties), $"{name}: properties of '{got.Name}'");
            }

            // 记了 Warning 的正是主值与属性都没有的那些绑定，此外没有别的日志
            var warned = @case.GetProperty("warnings").EnumerateArray().Select(static w => w.GetString()!).ToList();
            Assert.True(logs.Entries.Count == warned.Count && logs.Entries.All(static e => e.Level == LogLevel.Warning), $"{name}: logs {logs.Entries.Count}");
            Assert.All(warned, binding => Assert.Contains(logs.Entries, e => e.Message.Contains($"'{binding}'", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void BindingProperties_Reading_IsWhatTheHostWrites()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "binding-properties.json")));
        foreach (var @case in document.RootElement.GetProperty("reading").EnumerateArray())
        {
            var name = @case.GetProperty("name").GetString()!;
            var request = new ConfigureRequest { Configuration = "{}" };
            foreach (var binding in @case.GetProperty("bindings").EnumerateArray())
            {
                var proto = new ProtoBinding { Name = binding.GetProperty("name").GetString()!, Type = binding.GetProperty("type").GetString()!, Value = binding.GetProperty("value").GetString()! };
                foreach (var property in binding.GetProperty("properties").EnumerateObject())
                {
                    proto.Properties.Add(property.Name, property.Value.GetString()!);
                }

                request.Bindings.Add(proto);
            }

            // 向量的字节与 JSON 文本就是宿主写出的：进程外载体发字节，Wasm 载体发 proto3 JSON
            Assert.True(@case.GetProperty("hex").GetString() == Convert.ToHexStringLower(request.ToByteArray()), $"{name}: bytes {Convert.ToHexStringLower(request.ToByteArray())}");
            Assert.True(@case.GetProperty("json").GetString() == Encoding.UTF8.GetString(PluginJsonCodec.Encode(request)), $"{name}: json {Encoding.UTF8.GetString(PluginJsonCodec.Encode(request))}");

            // 读回来是同一个请求：没有属性的绑定读到空表，只差大小写的属性名是两项，值为空的属性照样在
            var fromBytes = ConfigureRequest.Parser.ParseFrom(Convert.FromHexString(@case.GetProperty("hex").GetString()!));
            var fromJson = PluginJsonCodec.Decode<ConfigureRequest>(Encoding.UTF8.GetBytes(@case.GetProperty("json").GetString()!));
            Assert.True(request.Equals(fromBytes), $"{name}: the bytes read as {fromBytes}");
            Assert.True(request.Equals(fromJson), $"{name}: the JSON text read as {fromJson}");
            foreach (var (want, got) in @case.GetProperty("bindings").EnumerateArray().Zip(fromBytes.Bindings))
            {
                Assert.True(SameProperties(PropertiesOf(want.GetProperty("properties")), got.Properties), $"{name}: properties of '{got.Name}'");
            }
        }
    }

    [Fact]
    public void BindingProperties_FixedReasons_AreWhatTheHostWrites()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(EchoPluginHost.VectorsDirectory, "plugin-messages.json")));
        var reasons = document.RootElement.GetProperty("reasons").EnumerateArray()
            .Where(static r => r.GetProperty("key").GetString() == PluginMessages.ProcessFailed && r.GetProperty("phase").GetString() == "configure")
            .Select(static r => r.GetProperty("reason").GetString()!.Replace("{binding}", "db", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        // 组装 Configure 时属性不能下发的三种 reason（lifecycle.md §2）：只带绑定名与上限的数字
        var tooMany = Enumerable.Range(0, PluginBindingProperties.MaxCount + 1).ToDictionary(static i => $"p{i}", static _ => "v", StringComparer.Ordinal);
        Assert.Contains(PluginBindingProperties.FindViolation("db", tooMany).ValueOrThrow(), reasons);
        Assert.Contains(PluginBindingProperties.FindViolation("db", new Dictionary<string, string> { ["user name"] = "v" }).ValueOrThrow(), reasons);
        Assert.Contains(PluginBindingProperties.FindViolation("db", new Dictionary<string, string> { ["blob"] = new('v', PluginBindingProperties.MaxBytes) }).ValueOrThrow(), reasons);
        Assert.False(PluginBindingProperties.FindViolation("db", new Dictionary<string, string> { ["username"] = "app" }).HasValue);
    }

    /// <summary>
    /// 向量里一个绑定的属性：值是字符串，或 <c>{repeat, count}</c>（把 repeat 重复 count 次）
    /// </summary>
    private static Dictionary<string, string> PropertiesOf(JsonElement properties)
        => properties.EnumerateObject().ToDictionary(
            static p => p.Name,
            static p => p.Value.ValueKind == JsonValueKind.String
                ? p.Value.GetString()!
                : string.Concat(Enumerable.Repeat(p.Value.GetProperty("repeat").GetString()!, p.Value.GetProperty("count").GetInt32())),
            StringComparer.Ordinal);

    /// <summary>
    /// 两组属性逐项相同
    /// </summary>
    /// <remarks>
    /// 属性名按字面比，各项的先后不计
    /// </remarks>
    private static bool SameProperties(Dictionary<string, string> expected, MapField<string, string> actual)
        => expected.Count == actual.Count && expected.All(p => actual.TryGetValue(p.Key, out var value) && value == p.Value);

    /// <summary>
    /// 向量里的线上 Error
    /// </summary>
    private static ProtoError ProtoErrorOf(JsonElement error)
    {
        var proto = new ProtoError
        {
            Code = error.GetProperty("code").GetString()!,
            MessageKey = error.GetProperty("message_key").GetString()!,
            Text = error.GetProperty("text").GetString()!,
            Details = error.GetProperty("details").GetString()!,
        };
        if (error.TryGetProperty("inner", out var inner) && inner.ValueKind == JsonValueKind.Object)
        {
            proto.Inner = ProtoErrorOf(inner);
        }

        return proto;
    }

    /// <summary>
    /// 读入后的错误与向量的期望一致
    /// </summary>
    /// <remarks>
    /// <c>details</c> 按 JSON 值比较：写回 JSON 再逐值比较，数按双精度比较
    /// </remarks>
    private static void AssertError(JsonElement expected, Error? actual, string name)
    {
        Assert.True(actual is not null, name);
        Assert.Equal(expected.GetProperty("code").GetString(), actual.Code);
        Assert.True(expected.GetProperty("key").GetString() == actual.MessageKey, $"{name}: key '{actual.MessageKey}'");
        Assert.True(expected.GetProperty("text").GetString() == actual.Text, $"{name}: text '{actual.Text}'");
        var details = expected.GetProperty("details");
        if (details.ValueKind == JsonValueKind.Null)
        {
            Assert.True(actual.Details is null, $"{name}: details {(actual.Details is null ? string.Empty : PluginJson.SerializeDetails(actual.Details))}");
        }
        else
        {
            Assert.True(actual.Details is not null, $"{name}: no details");
            using var written = JsonDocument.Parse(PluginJson.SerializeDetails(actual.Details));
            Assert.True(SameJson(details, written.RootElement), $"{name}: details {written.RootElement.GetRawText()}, want {details.GetRawText()}");
        }

        var inner = expected.GetProperty("inner");
        if (inner.ValueKind == JsonValueKind.Null)
        {
            Assert.True(actual.Inner is null, $"{name}: unexpected inner");
        }
        else
        {
            AssertError(inner, actual.Inner, name + " (inner)");
        }
    }

    /// <summary>
    /// 两个 JSON 值相等
    /// </summary>
    /// <remarks>
    /// 对象成员顺序不计，数按双精度比较（<c>1e+20</c> 与 <c>1E+20</c>、<c>0</c> 与 <c>0.0</c> 相等）
    /// </remarks>
    private static bool SameJson(JsonElement left, JsonElement right) => (left.ValueKind, right.ValueKind) switch
    {
        (JsonValueKind.Object, JsonValueKind.Object) => left.EnumerateObject().Count() == right.EnumerateObject().Count()
            && left.EnumerateObject().All(member => right.TryGetProperty(member.Name, out var other) && SameJson(member.Value, other)),
        (JsonValueKind.Array, JsonValueKind.Array) => left.GetArrayLength() == right.GetArrayLength()
            && left.EnumerateArray().Zip(right.EnumerateArray()).All(static pair => SameJson(pair.First, pair.Second)),
        (JsonValueKind.Number, JsonValueKind.Number) => left.GetDouble().Equals(right.GetDouble()),
        (JsonValueKind.String, JsonValueKind.String) => left.GetString() == right.GetString(),
        var (a, b) => a == b && a is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null,
    };

    /// <summary>
    /// 向量里精确到纳秒的 UTC 时刻
    /// </summary>
    /// <remarks>
    /// 比 100 纳秒更细的部分截断，与规范对实现时钟精度的要求一致
    /// </remarks>
    private static DateTimeOffset Utc(string text)
    {
        var whole = DateTime.ParseExact(text[..19], "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        var ticks = long.Parse(text.AsSpan(20, 7), NumberStyles.None, CultureInfo.InvariantCulture);
        return new DateTimeOffset(whole.Ticks + ticks, TimeSpan.Zero);
    }

    /// <summary>
    /// 类型上全部字符串常量的值
    /// </summary>
    private static IEnumerable<string> Constants(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(static field => (string)field.GetRawConstantValue()!);

    [Fact]
    public void Options_FromConfiguration_ParseDirectoriesGrantsAndTimings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Plugins:OutOfProcess:Directories:0"] = "plugins/a",
            ["XiHan:Plugins:OutOfProcess:Directories:1"] = "plugins/b",
            ["XiHan:Plugins:OutOfProcess:Grants:xihan.samples.echo:0"] = "capability:kernel.pipeline",
            ["XiHan:Plugins:OutOfProcess:Grants:xihan.samples.echo:1"] = "capability:messaging.queue",
            ["XiHan:Plugins:OutOfProcess:HandshakeTimeoutSeconds"] = "2.5",
            ["XiHan:Plugins:OutOfProcess:RestartLimit"] = "0",
            ["XiHan:Plugins:OutOfProcess:HealthFailures"] = "2",
        }).Build();

        var options = OutOfProcessPluginOptions.FromConfiguration(configuration);

        Assert.Equal(["plugins/a", "plugins/b"], options.Directories);
        Assert.True(options.GrantsFor("xihan.samples.echo").IsGranted("capability:messaging.queue"));
        Assert.False(options.GrantsFor("other").IsGranted("capability:kernel.pipeline"));
        Assert.Equal(TimeSpan.FromSeconds(2.5), options.HandshakeTimeout);
        Assert.Equal(0, options.RestartLimit);
        Assert.Equal(2, options.HealthFailures);
        Assert.Equal(TimeSpan.FromSeconds(30), options.StartTimeout);
        Assert.Null(options.StopGrace);
        Assert.Throws<ArgumentException>(() => OutOfProcessPluginOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Plugins:OutOfProcess:HealthFailures"] = "-1" }).Build()));
    }
}
