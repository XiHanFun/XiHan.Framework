// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Serialization.Yaml;
using XiHan.Framework.Utils.Text.Yaml;

namespace XiHan.Framework.Utils.Tests.Serialization.Yaml;

/// <summary>
/// YamlHelper 格式化与序列化选项生效性测试
/// </summary>
/// <remarks>
/// 修复前 FormatYaml 会把层级压平成点号键（a:\n  b: 1 变成 a.b: 1），
/// 且 SortKeys / ArrayPrefix / UseFlowStyle / MaxLineLength 四个选项全无读取点，
/// IsValidYaml 则对任何非空输入都返回 true。这里逐项锁死修复后的语义。
/// </remarks>
public class YamlHelperFormatAndOptionEffectTests
{
    /// <summary>
    /// 格式化保持层级结构，不再压平成点号键
    /// </summary>
    [Fact]
    public void FormatYaml_WithNestedDocument_KeepsHierarchy()
    {
        const string Yaml = """
            server:
              host: localhost
              port: 8080
            """;

        var formatted = YamlHelper.FormatYaml(Yaml);

        Assert.Contains("server:", formatted);
        Assert.Contains("  host: localhost", formatted);
        Assert.DoesNotContain("server.host", formatted);
        // 值一律按字符串处理，数字字面量加引号以保住字符串语义
        Assert.Contains("  port: \"8080\"", formatted);
    }

    /// <summary>
    /// 格式化保留序列的短横线列表
    /// </summary>
    [Fact]
    public void FormatYaml_WithSequence_KeepsDashList()
    {
        const string Yaml = """
            tags:
              - 甲
              - 乙
            """;

        var formatted = YamlHelper.FormatYaml(Yaml);

        Assert.Contains("tags:", formatted);
        Assert.Contains("  - 甲", formatted);
        Assert.Contains("  - 乙", formatted);
        Assert.DoesNotContain("tags.0", formatted);
    }

    /// <summary>
    /// 格式化仍支持文档标记与头部注释
    /// </summary>
    [Fact]
    public void FormatYaml_WithDocumentMarkersAndHeaderComment_WrapsDocument()
    {
        var formatted = YamlHelper.FormatYaml(
            "name: 曦寒",
            new YamlSerializeOptions { IncludeDocumentMarkers = true, HeaderComment = "测试注释" });

        Assert.StartsWith("---", formatted);
        Assert.Contains("# 测试注释", formatted);
        Assert.Contains("name: 曦寒", formatted);
        Assert.Contains("...", formatted);
    }

    /// <summary>
    /// 关闭排序后格式化保持原文档的键顺序
    /// </summary>
    [Fact]
    public void FormatYaml_WhenSortKeysDisabled_KeepsSourceOrder()
    {
        var formatted = YamlHelper.FormatYaml("b: 乙\na: 甲", new YamlSerializeOptions { SortKeys = false });

        Assert.StartsWith("b: 乙", formatted);
        Assert.True(formatted.IndexOf("b:", StringComparison.Ordinal) < formatted.IndexOf("a:", StringComparison.Ordinal));
    }

    /// <summary>
    /// 关闭排序后字典转 YAML 保持插入顺序
    /// </summary>
    [Fact]
    public void ConvertToYaml_WhenSortKeysDisabled_KeepsInsertionOrder()
    {
        var yaml = YamlHelper.ConvertToYaml(
            new Dictionary<string, string> { ["b"] = "乙", ["a"] = "甲" },
            new YamlSerializeOptions { SortKeys = false });

        Assert.StartsWith("b: 乙", yaml);
        Assert.True(yaml.IndexOf("b:", StringComparison.Ordinal) < yaml.IndexOf("a:", StringComparison.Ordinal));
    }

    /// <summary>
    /// 对象输出按 SortKeys 决定键顺序
    /// </summary>
    /// <remarks>
    /// 用 JSON 文本作输入，键顺序由文档本身确定（name 在前、enabled 在后），
    /// 排序开关是否生效就能一眼看出来，不依赖反射拿到的属性顺序。
    /// </remarks>
    [Fact]
    public void JsonToYaml_SortKeys_ControlsKeyOrder()
    {
        const string Json = """{"name":"曦寒","enabled":true}""";

        var sorted = YamlHelper.JsonToYaml(Json);
        var unsorted = YamlHelper.JsonToYaml(Json, new YamlSerializeOptions { SortKeys = false });

        Assert.StartsWith("enabled: true", sorted);
        Assert.StartsWith("name: 曦寒", unsorted);
    }

    /// <summary>
    /// 自定义数组项前缀会被真正使用，且仍能读回来
    /// </summary>
    [Fact]
    public void Serialize_WithCustomArrayPrefix_UsesItAndStillRoundTrips()
    {
        var options = new YamlSerializeOptions { ArrayPrefix = "-   " };

        var yaml = YamlHelper.Serialize(new YamlSampleTagged { Name = "曦寒", Tags = ["甲"] }, options);
        var restored = YamlHelper.Deserialize<YamlSampleTagged>(yaml);

        Assert.Contains("  -   甲", yaml);
        Assert.Single(restored.Tags);
        Assert.Equal("甲", restored.Tags[0]);
    }

    /// <summary>
    /// 开启流式样式后集合折成单行，且能原样读回
    /// </summary>
    [Fact]
    public void Serialize_WithFlowStyle_FoldsCollectionIntoSingleLine()
    {
        var source = new YamlSampleConfig
        {
            Name = "曦寒",
            Enabled = true,
            Server = new YamlSampleServer { Host = "localhost", Port = 8080 }
        };

        var yaml = YamlHelper.Serialize(source, YamlSerializeOptions.Compact);
        var restored = YamlHelper.Deserialize<YamlSampleConfig>(yaml);

        Assert.Contains("server: {host: localhost, port: 8080}", yaml);
        Assert.Equal("曦寒", restored.Name);
        Assert.True(restored.Enabled);
        Assert.Equal("localhost", restored.Server.Host);
        Assert.Equal(8080, restored.Server.Port);
    }

    /// <summary>
    /// 流式列表同样能折行并读回
    /// </summary>
    [Fact]
    public void Serialize_WithFlowStyle_FoldsSequenceIntoSingleLine()
    {
        var yaml = YamlHelper.Serialize(
            new YamlSampleTagged { Name = "曦寒", Tags = ["甲", "乙"] },
            YamlSerializeOptions.Compact);

        var restored = YamlHelper.Deserialize<YamlSampleTagged>(yaml);

        Assert.Contains("tags: [甲, 乙]", yaml);
        Assert.Equal(2, restored.Tags.Count);
        Assert.Equal("甲", restored.Tags[0]);
        Assert.Equal("乙", restored.Tags[1]);
    }

    /// <summary>
    /// 边界：折出来的行超过 MaxLineLength 时回退为块式
    /// </summary>
    [Fact]
    public void Serialize_WithFlowStyle_WhenExceedsMaxLineLength_FallsBackToBlockStyle()
    {
        var source = new YamlSampleConfig
        {
            Name = "曦寒",
            Enabled = true,
            Server = new YamlSampleServer { Host = "localhost", Port = 8080 }
        };

        var yaml = YamlHelper.Serialize(source, new YamlSerializeOptions { UseFlowStyle = true, MaxLineLength = 10 });

        Assert.DoesNotContain("{", yaml);
        Assert.Contains("  host: localhost", yaml);
        Assert.Contains("  port: 8080", yaml);
    }

    /// <summary>
    /// 合法性判断：没有任何键值对的散文不再被判为合法
    /// </summary>
    [Fact]
    public void IsValidYaml_WithProseWithoutKeyValue_ReturnsFalse()
    {
        var valid = YamlHelper.IsValidYaml("这是一段没有任何键值对的中文散文", out var errorMessage);

        Assert.False(valid);
        Assert.Equal("未解析到任何 YAML 键值对或序列项", errorMessage);
    }

    /// <summary>
    /// 合法性判断：纯序列文档为合法
    /// </summary>
    [Fact]
    public void IsValidYaml_WithSequenceDocument_ReturnsTrue()
    {
        Assert.True(YamlHelper.IsValidYaml("- 甲\n- 乙"));
    }

    /// <summary>
    /// 合法性判断：嵌套文档为合法
    /// </summary>
    [Fact]
    public void IsValidYaml_WithNestedDocument_ReturnsTrue()
    {
        Assert.True(YamlHelper.IsValidYaml("server:\n  host: localhost"));
    }
}
