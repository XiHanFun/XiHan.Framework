// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

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
    public void ParseDetails_ThatFailTheCommonJsonCheckOrAreNotAnObject_AreNoDetails(string json)
    {
        // 修之前重复成员取最后一个，转义出的孤立代理项在读值时抛出 InvalidOperationException
        Assert.Null(PluginJson.ParseDetails(json));
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
        Assert.Equal("""{"Empty":null,"List":["a","b"],"Padded":{"0":"a","01":"b"}}""", PluginJson.SerializeSection(section));
    }

    [Fact]
    public void SerializeSection_RootIsAlwaysAnObject()
    {
        var section = Configuration(("Plugin:0", "a"), ("Plugin:1", "b")).GetSection("Plugin");

        // 修之前子项名是 0、1 的根节写成数组，插件读到的配置节不是对象就进入 Failed
        Assert.Equal("""{"0":"a","1":"b"}""", PluginJson.SerializeSection(section));
        Assert.Equal("{}", PluginJson.SerializeSection(Configuration().GetSection("Missing")));
    }

    /// <summary>
    /// 内存配置
    /// </summary>
    private static IConfigurationRoot Configuration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();
}
