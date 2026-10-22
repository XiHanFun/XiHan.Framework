// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Plugins.Protocol;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 回放 spec/conformance/vectors/plugin 下的向量，以及选项解析。
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
