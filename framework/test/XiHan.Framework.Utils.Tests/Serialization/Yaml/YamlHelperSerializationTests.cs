// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Utils.Serialization.Yaml;

namespace XiHan.Framework.Utils.Tests.Serialization.Yaml;

/// <summary>
/// YamlHelper 对象序列化与格式互转测试
/// </summary>
/// <remarks>
/// 该实现以 JSON 为中转：对象 → JSON → YAML，YAML → 扁平字典 → 嵌套结构 → JSON → 对象。
/// 因此属性名走的是 JSON 驼峰策略，标量类型也在中转时被还原（数字/布尔/null）。
/// </remarks>
public class YamlHelperSerializationTests
{
    /// <summary>
    /// 构造一份嵌套配置
    /// </summary>
    private static YamlSampleConfig CreateSampleConfig()
    {
        return new YamlSampleConfig
        {
            Name = "曦寒",
            Enabled = true,
            Server = new YamlSampleServer { Host = "localhost", Port = 8080 }
        };
    }

    /// <summary>
    /// 对象序列化为驼峰键的缩进 YAML
    /// </summary>
    [Fact]
    public void Serialize_ProducesCamelCaseKeysAndIndentedNesting()
    {
        var yaml = YamlHelper.Serialize(CreateSampleConfig());

        Assert.Contains("name: 曦寒", yaml);
        Assert.Contains("enabled: true", yaml);
        Assert.Contains("server:", yaml);
        Assert.Contains("  host: localhost", yaml);
        Assert.Contains("  port: 8080", yaml);
    }

    /// <summary>
    /// 缩进大小由序列化选项控制
    /// </summary>
    [Fact]
    public void Serialize_WithCustomIndentSize_UsesIt()
    {
        var yaml = YamlHelper.Serialize(CreateSampleConfig(), new YamlSerializeOptions { IndentSize = 4 });

        Assert.Contains("    host: localhost", yaml);
    }

    /// <summary>
    /// 集合成员序列化为短横线列表
    /// </summary>
    [Fact]
    public void Serialize_WithCollection_ProducesDashList()
    {
        var yaml = YamlHelper.Serialize(new YamlSampleTagged { Name = "曦寒", Tags = ["甲", "乙"] });

        Assert.Contains("tags:", yaml);
        Assert.Contains("  - 甲", yaml);
        Assert.Contains("  - 乙", yaml);
    }

    /// <summary>
    /// 序列化 null 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Serialize_WhenObjectNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            YamlHelper.Serialize<YamlSampleConfig?>(null);
        });
    }

    /// <summary>
    /// 标量与嵌套对象经 YAML 往返后保持一致
    /// </summary>
    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesScalarsAndNestedObject()
    {
        var source = CreateSampleConfig();

        var restored = YamlHelper.Deserialize<YamlSampleConfig>(YamlHelper.Serialize(source));

        Assert.Equal("曦寒", restored.Name);
        Assert.True(restored.Enabled);
        Assert.NotNull(restored.Server);
        Assert.Equal("localhost", restored.Server.Host);
        Assert.Equal(8080, restored.Server.Port);
    }

    /// <summary>
    /// 反序列化忽略目标类型上不存在的键
    /// </summary>
    [Fact]
    public void Deserialize_WhenKeyUnknown_IgnoresIt()
    {
        var config = YamlHelper.Deserialize<YamlSampleConfig>("unknown: 值\nname: 曦寒");

        Assert.Equal("曦寒", config.Name);
        Assert.False(config.Enabled);
    }

    /// <summary>
    /// 空白 YAML 抛出 ArgumentException
    /// </summary>
    /// <param name="yaml">待反序列化的字符串</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void Deserialize_WhenYamlBlank_ThrowsArgumentException(string yaml)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            YamlHelper.Deserialize<YamlSampleConfig>(yaml);
        });

        Assert.Contains("YAML 字符串不能为空", exception.Message);
    }

    /// <summary>
    /// TrySerialize 遇到 null 返回 false
    /// </summary>
    [Fact]
    public void TrySerialize_WhenObjectNull_ReturnsFalse()
    {
        var succeeded = YamlHelper.TrySerialize<YamlSampleConfig?>(null, out var yaml);

        Assert.False(succeeded);
        Assert.Null(yaml);
    }

    /// <summary>
    /// TrySerialize 正常对象返回 true
    /// </summary>
    [Fact]
    public void TrySerialize_WithValidObject_ReturnsTrue()
    {
        var succeeded = YamlHelper.TrySerialize(CreateSampleConfig(), out var yaml);

        Assert.True(succeeded);
        Assert.NotNull(yaml);
        Assert.Contains("name: 曦寒", yaml!);
    }

    /// <summary>
    /// TryDeserialize 遇到空白 YAML 返回 false
    /// </summary>
    [Fact]
    public void TryDeserialize_WhenYamlBlank_ReturnsFalse()
    {
        var succeeded = YamlHelper.TryDeserialize<YamlSampleConfig>("   ", out var config);

        Assert.False(succeeded);
        Assert.Null(config);
    }

    /// <summary>
    /// TryDeserialize 正常 YAML 返回 true
    /// </summary>
    [Fact]
    public void TryDeserialize_WithValidYaml_ReturnsTrue()
    {
        var succeeded = YamlHelper.TryDeserialize<YamlSampleConfig>(
            YamlHelper.Serialize(CreateSampleConfig()),
            out var config);

        Assert.True(succeeded);
        Assert.NotNull(config);
        Assert.Equal("曦寒", config!.Name);
    }

    /// <summary>
    /// JSON 转 YAML 保留标量类型写法与中文
    /// </summary>
    [Fact]
    public void JsonToYaml_KeepsScalarLiteralsAndChinese()
    {
        var yaml = YamlHelper.JsonToYaml("{\"count\":18,\"enabled\":true,\"name\":\"曦寒\",\"nothing\":null}");

        Assert.Contains("count: 18", yaml);
        Assert.Contains("enabled: true", yaml);
        Assert.Contains("name: 曦寒", yaml);
        Assert.Contains("nothing: null", yaml);
    }

    /// <summary>
    /// JSON 非法时转 YAML 抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void JsonToYaml_WhenJsonInvalid_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            YamlHelper.JsonToYaml("不是 JSON");
        });

        Assert.Contains("JSON 转 YAML 失败", exception.Message);
    }

    /// <summary>
    /// YAML 转 JSON 还原层级并把标量还原为 JSON 原生类型
    /// </summary>
    [Fact]
    public void YamlToJson_RestoresHierarchyAndScalarTypes()
    {
        const string Yaml = """
            name: 曦寒
            age: 18
            enabled: true
            server:
              host: localhost
              port: 8080
            """;

        var json = YamlHelper.YamlToJson(Yaml);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("曦寒", root.GetProperty("name").GetString());
        Assert.Equal(18, root.GetProperty("age").GetInt32());
        Assert.True(root.GetProperty("enabled").GetBoolean());
        Assert.Equal("localhost", root.GetProperty("server").GetProperty("host").GetString());
        Assert.Equal(8080, root.GetProperty("server").GetProperty("port").GetInt32());

        // 编码器为宽松模式，中文必须原样输出
        Assert.Contains("曦寒", json);
        Assert.DoesNotContain("\\u", json);
    }

    /// <summary>
    /// JSON 与 YAML 互转后语义保持一致
    /// </summary>
    [Fact]
    public void JsonToYaml_ThenYamlToJson_KeepsScalarSemantics()
    {
        const string SourceJson = """{"name":"曦寒","age":18,"enabled":true}""";

        var roundTripped = YamlHelper.YamlToJson(YamlHelper.JsonToYaml(SourceJson));

        using var document = JsonDocument.Parse(roundTripped);
        var root = document.RootElement;

        Assert.Equal("曦寒", root.GetProperty("name").GetString());
        Assert.Equal(18, root.GetProperty("age").GetInt32());
        Assert.True(root.GetProperty("enabled").GetBoolean());
    }
}
