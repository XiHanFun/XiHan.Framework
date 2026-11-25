// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Plugins.Protocol.Mapping;
using ProtoError = XiHan.Framework.Plugins.Protocol.V1.Error;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 协议里 JSON 文本字段的读写：错误细节与配置节
/// </summary>
/// <remarks>
/// <c>invocation.md</c> §3、<c>lifecycle.md</c> §2（ADR-0237）
/// </remarks>
public sealed class PluginJsonTests
{
    [Theory]
    [InlineData("""{"id":"a","id":"b"}""")]
    [InlineData("""{"id":"a","nested":{"x":1,"x":2}}""")]
    [InlineData("""{"id":"\ud800"}""")]
    [InlineData("""{"id":"a",}""")]
    [InlineData("""["id"]""")]
    [InlineData(" ")]
    [InlineData("{}")]
    [InlineData("""{"id":"a","n":1e400}""")]
    [InlineData("""{"id":"a","x":[{"n":-1e400}]}""")]
    public void ParseDetails_ThatFailTheCommonJsonCheckOrAreNotAnObject_AreNoDetails(string json)
    {
        // 修之前重复成员取最后一个，转义出的孤立代理项在读值时抛出 InvalidOperationException
        Assert.Null(PluginJson.ParseDetails(json));
    }

    [Fact]
    public void ParseDetails_WithALoneSurrogateInTheText_AreNoDetails()
    {
        // 字符串里的孤立代理项编码不成 UTF-8（RFC-0001「JSON 文本」，ADR-0301）：修之前宽松的编码把它换成 U+FFFD，读出一份本不合法的细节
        // 不用 InlineData：理论数据经序列化交给测试时孤立代理项已被换掉，到不了被测的方法
        string[] texts = ["{\"id\":\"\uD800\"}", "{\"id\":\"a\uDC00\"}", "{\"\uD83D\":\"a\"}"];
        foreach (var json in texts)
        {
            Assert.Contains(json, char.IsSurrogate);
            Assert.Null(PluginJson.ParseDetails(json));
        }
    }

    [Fact]
    public void ParseDetails_WithASurrogatePair_KeepTheCharacter()
    {
        var details = PluginJson.ParseDetails("{\"id\":\"\uD83D\uDE00\"}");

        Assert.NotNull(details);
        Assert.Equal("\uD83D\uDE00", details["id"]);
    }

    [Fact]
    public void FromProto_WithALoneSurrogateInTheDetails_KeepsTheErrorWithoutDetails()
    {
        // 细节当作没有，错误本身照常读入，与通不过共同检查的细节相同
        var error = PluginErrorMapper.FromProto(new ProtoError { Code = "ECHO_VALIDATION_001", MessageKey = "echo.text.empty", Details = "{\"id\":\"\uD800\"}" }, "xihan.samples.echo");

        Assert.Equal("ECHO_VALIDATION_001", error.Code);
        Assert.Equal("echo.text.empty", error.MessageKey);
        Assert.Null(error.Details);
    }

    [Fact]
    public void ParseDetails_OfAnObject_FollowTheValueModel()
    {
        var details = PluginJson.ParseDetails("""{"id":"a","count":2,"ratio":2.0,"ok":true,"none":null}""");

        Assert.NotNull(details);
        Assert.Equal("a", details["id"]);
        Assert.Equal(2L, details["count"]);
        Assert.Equal(2.0, details["ratio"]);
        Assert.Equal(true, details["ok"]);
        Assert.Null(details["none"]);
    }

    [Fact]
    public void FromProto_WithDuplicateDetails_KeepsTheErrorWithoutDetails()
    {
        var error = PluginErrorMapper.FromProto(new ProtoError { Code = "ECHO_VALIDATION_001", MessageKey = "echo.text.empty", Details = """{"id":"a","id":"b"}""" }, "xihan.samples.echo");

        Assert.Equal("ECHO_VALIDATION_001", error.Code);
        Assert.Equal("echo.text.empty", error.MessageKey);
        Assert.Null(error.Details);
    }

    [Fact]
    public void SerializeSection_WritesArraysOnlyForExactIndexes()
    {
        var section = Configuration(("Plugin:List:0", "a"), ("Plugin:List:1", "b"), ("Plugin:Padded:0", "a"), ("Plugin:Padded:01", "b"), ("Plugin:Empty", null)).GetSection("Plugin");

        // 修之前 "01" 按 int.TryParse 读成 1，Padded 也写成了数组
        Assert.Equal("""{"Empty":null,"List":["a","b"],"Padded":{"0":"a","01":"b"}}""", Serialized(section));
    }

    [Fact]
    public void SerializeSection_RootIsAlwaysAnObject()
    {
        var section = Configuration(("Plugin:0", "a"), ("Plugin:1", "b")).GetSection("Plugin");

        // 修之前子项名是 0、1 的根节写成数组，插件读到的配置节不是对象就进入 Failed
        Assert.Equal("""{"0":"a","1":"b"}""", Serialized(section));
        Assert.Equal("{}", Serialized(Configuration().GetSection("Missing")));
    }

    [Fact]
    public void SerializeSection_RefusesALoneSurrogate_InAKeyOrAValue()
    {
        // 写出器缺省把孤立的代理项写成转义的 U+FFFD（ADR-0315）：键与值都核对，数组的元素也一样；代理对照常写出
        Assert.False(PluginJson.TrySerializeSection(Configuration(("Plugin:Name", "a\uD800")).GetSection("Plugin"), out _));
        Assert.False(PluginJson.TrySerializeSection(Configuration(("Plugin:N\uDC00ame", "a")).GetSection("Plugin"), out _));
        Assert.False(PluginJson.TrySerializeSection(Configuration(("Plugin:List:0", "a"), ("Plugin:List:1", "\uDC00")).GetSection("Plugin"), out _));
        using var paired = JsonDocument.Parse(Serialized(Configuration(("Plugin:Name", "😀")).GetSection("Plugin")));
        Assert.Equal("😀", paired.RootElement.GetProperty("Name").GetString());
    }

    /// <summary>
    /// 写出配置节，要求写得出
    /// </summary>
    private static string Serialized(IConfigurationSection section)
    {
        Assert.True(PluginJson.TrySerializeSection(section, out var json));
        return json;
    }

    /// <summary>
    /// 内存配置
    /// </summary>
    private static IConfigurationRoot Configuration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();
}
