// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Utils.Serialization.Yaml;

namespace XiHan.Framework.Utils.Tests.Serialization.Yaml;

/// <summary>
/// YamlExtensions 扩展方法测试
/// </summary>
/// <remarks>
/// 字典类扩展是配置读取的主力入口（按前缀取子树、合并、批量改键值），
/// 这里重点验证它们都返回新字典、不修改原字典。
/// </remarks>
public class YamlExtensionsTests
{
    /// <summary>
    /// 构造一份层级化的扁平配置字典
    /// </summary>
    private static Dictionary<string, string> CreateFlatConfig()
    {
        return new Dictionary<string, string>
        {
            ["server.host"] = "localhost",
            ["server.port"] = "8080",
            ["app.name"] = "曦寒"
        };
    }

    /// <summary>
    /// 对象转 YAML 与字符串还原对象
    /// </summary>
    [Fact]
    public void ToYaml_And_FromYaml_RoundTrip()
    {
        var source = new YamlSampleConfig
        {
            Name = "曦寒",
            Enabled = true,
            Server = new YamlSampleServer { Host = "localhost", Port = 8080 }
        };

        var yaml = source.ToYaml();
        var restored = yaml.FromYaml<YamlSampleConfig>();

        Assert.Contains("name: 曦寒", yaml);
        Assert.Equal("曦寒", restored.Name);
        Assert.True(restored.Enabled);
        Assert.Equal(8080, restored.Server.Port);
    }

    /// <summary>
    /// 字典转 YAML 输出键值行
    /// </summary>
    [Fact]
    public void ToYaml_OnDictionary_ProducesKeyValueLines()
    {
        var yaml = new Dictionary<string, string>
        {
            ["age"] = "18",
            ["name"] = "曦寒"
        }.ToYaml();

        Assert.Contains("age: \"18\"", yaml);
        Assert.Contains("name: 曦寒", yaml);
    }

    /// <summary>
    /// 合法性判断与错误信息输出
    /// </summary>
    [Fact]
    public void IsValidYaml_Extension_ReportsValidity()
    {
        Assert.True("name: 曦寒".IsValidYaml());

        var valid = "   ".IsValidYaml(out var errorMessage);
        Assert.False(valid);
        Assert.Equal("YAML 字符串为空", errorMessage);
    }

    /// <summary>
    /// 格式化扩展按键排序输出
    /// </summary>
    [Fact]
    public void FormatYaml_Extension_NormalizesOrdering()
    {
        var formatted = "b: 乙\na: 甲".FormatYaml();

        Assert.StartsWith("a: 甲", formatted);
        Assert.Contains("b: 乙", formatted);
    }

    /// <summary>
    /// 解析扩展与嵌套解析扩展的层级差异
    /// </summary>
    [Fact]
    public void ParseExtensions_FlatVersusNested()
    {
        const string Yaml = """
            server:
              host: localhost
            """;

        var flat = Yaml.ParseYaml();
        var nested = Yaml.ParseNestedYaml();

        // 扁平解析不识别缩进层级，子键会退化成顶层键
        Assert.Equal("localhost", flat["host"]);
        Assert.Equal("localhost", nested["server.host"]);
    }

    /// <summary>
    /// YAML 转 JSON 扩展还原层级与标量类型
    /// </summary>
    [Fact]
    public void ToJson_Extension_RestoresHierarchy()
    {
        const string Yaml = """
            name: 曦寒
            server:
              port: 8080
            """;

        using var document = JsonDocument.Parse(Yaml.ToJson());
        var root = document.RootElement;

        Assert.Equal("曦寒", root.GetProperty("name").GetString());
        Assert.Equal(8080, root.GetProperty("server").GetProperty("port").GetInt32());
    }

    /// <summary>
    /// 取值扩展在键缺失时返回默认值
    /// </summary>
    [Fact]
    public void GetValueOrDefault_WhenKeyMissing_ReturnsDefault()
    {
        var dict = CreateFlatConfig();

        Assert.Equal("localhost", dict.GetValueOrDefault("server.host", "默认"));
        Assert.Equal("默认", dict.GetValueOrDefault("server.missing", "默认"));
    }

    /// <summary>
    /// 嵌套键的读写走扁平化后的完整键
    /// </summary>
    [Fact]
    public void NestedValue_ReadAndWrite_UseFlattenedKey()
    {
        var dict = CreateFlatConfig();

        dict.SetNestedValue("server.timeout", "30");

        Assert.Equal("30", dict["server.timeout"]);
        Assert.Equal("30", dict.GetNestedValue("server.timeout", "默认"));
        Assert.Equal("默认", dict.GetNestedValue("server.nothing", "默认"));
    }

    /// <summary>
    /// 按前缀取子配置，可选择是否剥离前缀
    /// </summary>
    [Fact]
    public void GetByPrefix_ReturnsMatchingSubset()
    {
        var dict = CreateFlatConfig();

        var kept = dict.GetByPrefix("server");
        var stripped = dict.GetByPrefix("server", ".", true);

        Assert.Equal(2, kept.Count);
        Assert.Equal("localhost", kept["server.host"]);
        Assert.Equal(2, stripped.Count);
        Assert.Equal("localhost", stripped["host"]);
        Assert.Equal("8080", stripped["port"]);
    }

    /// <summary>
    /// 前缀已带分隔符时不会重复追加
    /// </summary>
    [Fact]
    public void GetByPrefix_WhenPrefixAlreadyEndsWithSeparator_StillMatches()
    {
        var dict = CreateFlatConfig();

        Assert.Equal(2, dict.GetByPrefix("server.").Count);
    }

    /// <summary>
    /// 合并字典时按 overwrite 决定同名键归属，且不修改原字典
    /// </summary>
    [Fact]
    public void Merge_RespectsOverwriteFlagAndKeepsSourceIntact()
    {
        var dict = CreateFlatConfig();
        var other = new Dictionary<string, string> { ["server.port"] = "9090", ["app.version"] = "1.0" };

        var overwritten = dict.Merge(other);
        var preserved = dict.Merge(other, false);

        Assert.Equal("9090", overwritten["server.port"]);
        Assert.Equal("8080", preserved["server.port"]);
        Assert.Equal("1.0", preserved["app.version"]);
        Assert.Equal("8080", dict["server.port"]);
    }

    /// <summary>
    /// 过滤返回满足条件的新字典
    /// </summary>
    [Fact]
    public void Filter_ReturnsMatchingEntriesOnly()
    {
        var dict = CreateFlatConfig();

        var filtered = dict.Filter(kvp => kvp.Key.StartsWith("server", StringComparison.Ordinal));

        Assert.Equal(2, filtered.Count);
        Assert.Equal(3, dict.Count);
    }

    /// <summary>
    /// 批量转换值与键都返回新字典
    /// </summary>
    [Fact]
    public void TransformValues_And_TransformKeys_ReturnNewDictionaries()
    {
        var dict = CreateFlatConfig();

        var upperValues = dict.TransformValues(value => value.ToUpperInvariant());
        var upperKeys = dict.TransformKeys(key => key.ToUpperInvariant());

        Assert.Equal("LOCALHOST", upperValues["server.host"]);
        Assert.Equal("localhost", upperKeys["SERVER.HOST"]);
        Assert.Equal("localhost", dict["server.host"]);
    }

    /// <summary>
    /// 字符串转指定类型的成功与失败路径
    /// </summary>
    [Fact]
    public void TryConvertTo_ReflectsParseResult()
    {
        Assert.True("8080".TryConvertTo<int>(out var port));
        Assert.Equal(8080, port);

        Assert.True("true".TryConvertTo<bool>(out var flag));
        Assert.True(flag);

        Assert.False("不是数字".TryConvertTo<int>(out _));
        Assert.False("".TryConvertTo<int>(out _));
    }

    /// <summary>
    /// 转换失败时返回调用方给定的默认值
    /// </summary>
    [Fact]
    public void ConvertToOrDefault_WhenParseFails_ReturnsGivenDefault()
    {
        Assert.Equal(-1, "不是数字".ConvertToOrDefault(-1));
        Assert.Equal(8080, "8080".ConvertToOrDefault(-1));
    }
}
