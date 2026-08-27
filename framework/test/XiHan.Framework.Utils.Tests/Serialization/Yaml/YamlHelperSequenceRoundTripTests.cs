// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Utils.Serialization.Yaml;
using XiHan.Framework.Utils.Text.Yaml;

namespace XiHan.Framework.Utils.Tests.Serialization.Yaml;

/// <summary>
/// YamlHelper 序列（集合）往返测试
/// </summary>
/// <remarks>
/// 修复前 Serialize 会把集合写成短横线列表，而解析侧的键值对正则匹配不到 `- item`，
/// 于是 Deserialize(Serialize(obj)) 会把集合成员全部静默丢掉、属性停留在默认值。
/// 这里锁死"写得出去就读得回来"，并用下标键的反例守住"以 0/1 为键的映射不能被当成数组"。
/// </remarks>
public class YamlHelperSequenceRoundTripTests
{
    /// <summary>
    /// 字符串集合往返后成员与顺序都不丢
    /// </summary>
    [Fact]
    public void SerializeDeserialize_WithStringCollection_RestoresEveryItem()
    {
        var source = new YamlSampleTagged { Name = "曦寒", Tags = ["甲", "乙", "丙"] };

        var restored = YamlHelper.Deserialize<YamlSampleTagged>(YamlHelper.Serialize(source));

        Assert.Equal("曦寒", restored.Name);
        Assert.Equal(3, restored.Tags.Count);
        Assert.Equal("甲", restored.Tags[0]);
        Assert.Equal("乙", restored.Tags[1]);
        Assert.Equal("丙", restored.Tags[2]);
    }

    /// <summary>
    /// 对象集合往返后每一项的字段都不丢
    /// </summary>
    [Fact]
    public void SerializeDeserialize_WithObjectCollection_RestoresEveryItem()
    {
        var source = new YamlSequenceCluster
        {
            Name = "曦寒集群",
            Servers =
            [
                new YamlSequenceServer { Host = "localhost", Port = 8080 },
                new YamlSequenceServer { Host = "127.0.0.1", Port = 9090 }
            ]
        };

        var restored = YamlHelper.Deserialize<YamlSequenceCluster>(YamlHelper.Serialize(source));

        Assert.Equal("曦寒集群", restored.Name);
        Assert.Equal(2, restored.Servers.Count);
        Assert.Equal("localhost", restored.Servers[0].Host);
        Assert.Equal(8080, restored.Servers[0].Port);
        Assert.Equal("127.0.0.1", restored.Servers[1].Host);
        Assert.Equal(9090, restored.Servers[1].Port);
    }

    /// <summary>
    /// 空集合往返后仍是空集合（边界：不能凭空造出成员）
    /// </summary>
    [Fact]
    public void SerializeDeserialize_WhenCollectionEmpty_RestoresEmptyCollection()
    {
        var source = new YamlSampleTagged { Name = "曦寒", Tags = [] };

        var restored = YamlHelper.Deserialize<YamlSampleTagged>(YamlHelper.Serialize(source));

        Assert.Equal("曦寒", restored.Name);
        Assert.Empty(restored.Tags);
    }

    /// <summary>
    /// 根级序列可还原为列表
    /// </summary>
    [Fact]
    public void Deserialize_WithRootSequence_RestoresList()
    {
        const string Yaml = """
            - 甲
            - 乙
            """;

        var restored = YamlHelper.Deserialize<List<string>>(Yaml);

        Assert.Equal(2, restored.Count);
        Assert.Equal("甲", restored[0]);
        Assert.Equal("乙", restored[1]);
    }

    /// <summary>
    /// 嵌套解析把序列展开成下标键
    /// </summary>
    [Fact]
    public void ParseNestedYaml_WithScalarSequence_ProducesIndexedKeys()
    {
        const string Yaml = """
            tags:
              - 甲
              - 乙
            """;

        var dict = YamlHelper.ParseNestedYaml(Yaml);

        Assert.Equal(2, dict.Count);
        Assert.Equal("甲", dict["tags.0"]);
        Assert.Equal("乙", dict["tags.1"]);
    }

    /// <summary>
    /// 序列与父键同缩进（块序列的常见写法）时仍挂在父键下
    /// </summary>
    /// <remarks>
    /// tags:\n- 甲 是合法 YAML，短横线可以与父键同缩进；
    /// 缩进栈若按普通行的规则把父键弹掉，这些成员就会跑到根上去。
    /// </remarks>
    [Fact]
    public void ParseNestedYaml_WithFlushSequence_AttachesItemsToParentKey()
    {
        const string Yaml = """
            tags:
            - 甲
            - 乙
            name: 曦寒
            """;

        var dict = YamlHelper.ParseNestedYaml(Yaml);

        Assert.Equal("甲", dict["tags.0"]);
        Assert.Equal("乙", dict["tags.1"]);
        Assert.Equal("曦寒", dict["name"]);
    }

    /// <summary>
    /// 对象序列的下标键继续向下展开
    /// </summary>
    [Fact]
    public void ParseNestedYaml_WithObjectSequence_ProducesIndexedKeys()
    {
        const string Yaml = """
            servers:
              -
                host: localhost
                port: 8080
              -
                host: 127.0.0.1
                port: 9090
            """;

        var dict = YamlHelper.ParseNestedYaml(Yaml);

        Assert.Equal("localhost", dict["servers.0.host"]);
        Assert.Equal("8080", dict["servers.0.port"]);
        Assert.Equal("127.0.0.1", dict["servers.1.host"]);
        Assert.Equal("9090", dict["servers.1.port"]);
    }

    /// <summary>
    /// 自定义键分隔符对序列下标同样生效
    /// </summary>
    [Fact]
    public void ParseNestedYaml_WithCustomSeparator_AppliesToSequenceIndex()
    {
        const string Yaml = """
            tags:
              - 甲
            """;

        var dict = YamlHelper.ParseNestedYaml(Yaml, new YamlParseOptions { KeySeparator = "/" });

        Assert.Equal("甲", dict["tags/0"]);
    }

    /// <summary>
    /// YAML 转 JSON 时序列还原为 JSON 数组
    /// </summary>
    [Fact]
    public void YamlToJson_WithSequence_ProducesJsonArray()
    {
        const string Yaml = """
            tags:
              - 甲
              - 乙
            """;

        using var document = JsonDocument.Parse(YamlHelper.YamlToJson(Yaml));
        var tags = document.RootElement.GetProperty("tags");

        Assert.Equal(JsonValueKind.Array, tags.ValueKind);
        Assert.Equal(2, tags.GetArrayLength());
        Assert.Equal("甲", tags[0].GetString());
        Assert.Equal("乙", tags[1].GetString());
    }

    /// <summary>
    /// 反例：以数字为键的映射不是序列，不能被还原成数组
    /// </summary>
    /// <remarks>
    /// 序列在扁平字典里与"0、1 为键的对象"同形，只按形状猜就会误判，
    /// 因此实现按解析阶段记录的序列路径还原，这条用例守住该边界。
    /// </remarks>
    [Fact]
    public void YamlToJson_WithNumericMapKeys_KeepsJsonObject()
    {
        const string Yaml = """
            items:
              0: 甲
              1: 乙
            """;

        using var document = JsonDocument.Parse(YamlHelper.YamlToJson(Yaml));
        var items = document.RootElement.GetProperty("items");

        Assert.Equal(JsonValueKind.Object, items.ValueKind);
        Assert.Equal("甲", items.GetProperty("0").GetString());
        Assert.Equal("乙", items.GetProperty("1").GetString());
    }

    /// <summary>
    /// 反例：ParseYaml 仍是一层解析，短横线行照旧跳过
    /// </summary>
    /// <remarks>序列还原只发生在 ParseNestedYaml / Deserialize 一侧，扁平解析的既有契约不变。</remarks>
    [Fact]
    public void ParseYaml_WithSequence_StillSkipsDashLines()
    {
        const string Yaml = """
            tags:
              - 甲
            """;

        var dict = YamlHelper.ParseYaml(Yaml);

        Assert.Single(dict);
        Assert.Equal(string.Empty, dict["tags"]);
    }
}

/// <summary>
/// 序列往返测试用服务端配置
/// </summary>
public sealed class YamlSequenceServer
{
    /// <summary>
    /// 主机名
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; }
}

/// <summary>
/// 序列往返测试用集群配置，含对象集合
/// </summary>
public sealed class YamlSequenceCluster
{
    /// <summary>
    /// 集群名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 服务端集合
    /// </summary>
    public List<YamlSequenceServer> Servers { get; set; } = [];
}
