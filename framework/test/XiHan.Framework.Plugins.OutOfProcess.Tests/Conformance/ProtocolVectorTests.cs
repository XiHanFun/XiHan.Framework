// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Primitives;
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
        foreach (var valid in document.RootElement.GetProperty("valid").EnumerateArray())
        {
            var text = valid.GetProperty("text").GetString();
            Assert.True(PluginDeadlines.TryParse(text, out var parsed), text);
            Assert.Equal(Utc(valid.GetProperty("utc").GetString()!), parsed);
            Assert.Equal(TimeSpan.Zero, parsed.Offset);
        }

        foreach (var invalid in document.RootElement.GetProperty("invalid").EnumerateArray())
        {
            Assert.False(PluginDeadlines.TryParse(invalid.GetString(), out _), invalid.GetString());
        }

        foreach (var effective in document.RootElement.GetProperty("effective").EnumerateArray())
        {
            var grpc = effective.GetProperty("grpc");
            var expected = effective.GetProperty("expected");
            var actual = PluginDeadlines.Effective(effective.GetProperty("metadata").GetString(), grpc.ValueKind == JsonValueKind.Null ? null : Utc(grpc.GetString()!));
            Assert.Equal(expected.ValueKind == JsonValueKind.Null ? null : Utc(expected.GetString()!), actual);
        }

        foreach (var written in document.RootElement.GetProperty("written").EnumerateArray())
        {
            Assert.Equal(written.GetProperty("text").GetString(), PluginDeadlines.Format(Utc(written.GetProperty("utc").GetString()!)));
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
