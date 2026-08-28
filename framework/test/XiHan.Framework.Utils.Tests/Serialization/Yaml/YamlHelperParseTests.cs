// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Serialization.Yaml;

namespace XiHan.Framework.Utils.Tests.Serialization.Yaml;

/// <summary>
/// YamlHelper 解析与字典互转测试
/// </summary>
/// <remarks>
/// 该实现是逐行正则的轻量解析器，不是完整 YAML 语法树：
/// ParseYaml 只做一层键值对，层级由 ParseNestedYaml 通过缩进栈还原为点号键。
/// 这里按其真实契约断言，并覆盖注释、类型转换、引号与特殊字符往返。
/// </remarks>
public class YamlHelperParseTests
{
    /// <summary>
    /// 解析扁平键值对，默认开启类型规范化
    /// </summary>
    [Fact]
    public void ParseYaml_WithFlatDocument_ReturnsEveryPair()
    {
        const string Yaml = """
            name: 曦寒
            age: 18
            active: true
            empty: null
            tilde: ~
            """;

        var dict = YamlHelper.ParseYaml(Yaml);

        Assert.Equal("曦寒", dict["name"]);
        Assert.Equal("18", dict["age"]);
        Assert.Equal("true", dict["active"]);
        Assert.Equal("null", dict["empty"]);
        Assert.Equal("null", dict["tilde"]);
    }

    /// <summary>
    /// 引号包裹的值会被剥离引号，值内的冒号不影响键的切分
    /// </summary>
    [Fact]
    public void ParseYaml_WithQuotedValue_StripsQuotesAndKeepsColon()
    {
        var dict = YamlHelper.ParseYaml("message: \"含: 冒号的值\"");

        Assert.Equal("含: 冒号的值", dict["message"]);
    }

    /// <summary>
    /// 默认忽略注释行，关闭后含冒号的注释会被当作键值对
    /// </summary>
    [Fact]
    public void ParseYaml_IgnoreComments_ControlsCommentLines()
    {
        const string Yaml = """
            # 注释: 注释值
            name: 曦寒
            """;

        var ignored = YamlHelper.ParseYaml(Yaml);
        var kept = YamlHelper.ParseYaml(Yaml, new YamlParseOptions { IgnoreComments = false });

        Assert.Single(ignored);
        Assert.Equal("曦寒", ignored["name"]);
        Assert.Equal(2, kept.Count);
        Assert.Equal("注释值", kept["# 注释"]);
    }

    /// <summary>
    /// 关闭类型转换后保留原始字面量
    /// </summary>
    [Fact]
    public void ParseYaml_ConvertTypes_ControlsValueNormalization()
    {
        const string Yaml = """
            code: 018
            active: TRUE
            """;

        var converted = YamlHelper.ParseYaml(Yaml);
        var raw = YamlHelper.ParseYaml(Yaml, new YamlParseOptions { ConvertTypes = false });

        Assert.Equal("18", converted["code"]);
        Assert.Equal("true", converted["active"]);
        Assert.Equal("018", raw["code"]);
        Assert.Equal("TRUE", raw["active"]);
    }

    /// <summary>
    /// 没有冒号的行被跳过，空白文档返回空字典
    /// </summary>
    [Fact]
    public void ParseYaml_WhenLineHasNoKeyValue_SkipsIt()
    {
        var dict = YamlHelper.ParseYaml("这是一行没有分隔符的文本\nname: 曦寒");

        Assert.Single(dict);
        Assert.Equal("曦寒", dict["name"]);
        Assert.Empty(YamlHelper.ParseYaml("   "));
    }

    /// <summary>
    /// 多层级 YAML 按缩进还原为点号分隔的扁平键
    /// </summary>
    [Fact]
    public void ParseNestedYaml_FlattensHierarchyWithDottedKeys()
    {
        const string Yaml = """
            name: 曦寒
            server:
              host: localhost
              port: 8080
              auth:
                user: admin
            database: mysql
            """;

        var dict = YamlHelper.ParseNestedYaml(Yaml);

        Assert.Equal("曦寒", dict["name"]);
        Assert.Equal("localhost", dict["server.host"]);
        Assert.Equal("8080", dict["server.port"]);
        Assert.Equal("admin", dict["server.auth.user"]);
        Assert.Equal("mysql", dict["database"]);
    }

    /// <summary>
    /// 支持自定义键层级分隔符
    /// </summary>
    [Fact]
    public void ParseNestedYaml_WithCustomSeparator_UsesIt()
    {
        const string Yaml = """
            server:
              host: localhost
            """;

        var dict = YamlHelper.ParseNestedYaml(Yaml, new YamlParseOptions { KeySeparator = "/" });

        Assert.Equal("localhost", dict["server/host"]);
    }

    /// <summary>
    /// 空白文档解析为空字典
    /// </summary>
    [Fact]
    public void ParseNestedYaml_WhenBlank_ReturnsEmpty()
    {
        Assert.Empty(YamlHelper.ParseNestedYaml("   "));
    }

    /// <summary>
    /// 字典转 YAML 时按键排序，数字型字符串加引号以保持字符串语义
    /// </summary>
    [Fact]
    public void ConvertToYaml_SortsKeysAndQuotesAmbiguousValues()
    {
        var yaml = YamlHelper.ConvertToYaml(new Dictionary<string, string>
        {
            ["name"] = "曦寒",
            ["age"] = "18"
        });

        Assert.Contains("age: \"18\"", yaml);
        Assert.Contains("name: 曦寒", yaml);
        Assert.True(yaml.IndexOf("age:", StringComparison.Ordinal) < yaml.IndexOf("name:", StringComparison.Ordinal));
    }

    /// <summary>
    /// 空字典转 YAML 得到空字符串
    /// </summary>
    [Fact]
    public void ConvertToYaml_WhenDictionaryEmpty_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, YamlHelper.ConvertToYaml([]));
    }

    /// <summary>
    /// 开启文档标记时输出首尾标记
    /// </summary>
    [Fact]
    public void ConvertToYaml_WithDocumentMarkers_WrapsDocument()
    {
        var yaml = YamlHelper.ConvertToYaml(
            new Dictionary<string, string> { ["name"] = "曦寒" },
            new YamlSerializeOptions { IncludeDocumentMarkers = true });

        Assert.StartsWith("---", yaml);
        Assert.Contains("...", yaml);
    }

    /// <summary>
    /// 头部注释逐行输出为 YAML 注释
    /// </summary>
    [Fact]
    public void ConvertToYaml_WithHeaderComment_EmitsCommentLines()
    {
        var yaml = YamlHelper.ConvertToYaml(
            new Dictionary<string, string> { ["name"] = "曦寒" },
            new YamlSerializeOptions { HeaderComment = "生成的配置\n请勿手工编辑" });

        Assert.Contains("# 生成的配置", yaml);
        Assert.Contains("# 请勿手工编辑", yaml);
    }

    /// <summary>
    /// 强制加引号时非数字非布尔的字符串都被引号包裹
    /// </summary>
    [Fact]
    public void ConvertToYaml_WithForceQuoteStrings_QuotesPlainStrings()
    {
        var yaml = YamlHelper.ConvertToYaml(
            new Dictionary<string, string> { ["name"] = "曦寒" },
            new YamlSerializeOptions { ForceQuoteStrings = true });

        Assert.Contains("name: \"曦寒\"", yaml);
    }

    /// <summary>
    /// 字典经 YAML 往返后完全一致，含冒号、井号、换行与空值等特殊内容
    /// </summary>
    /// <remarks>
    /// 这些字符正是 NeedsQuotes / EscapeYamlString 的分支入口，
    /// 往返一致才说明转义与反转义是配套的。
    /// </remarks>
    [Fact]
    public void ConvertToYaml_ThenParseYaml_RoundTripsSpecialCharacters()
    {
        var source = new Dictionary<string, string>
        {
            ["name"] = "曦寒",
            ["url"] = "http://example.com/path",
            ["note"] = "含#井号",
            ["multi"] = "第一行\n第二行",
            ["tab"] = "前\t后",
            ["blank"] = string.Empty,
            ["number"] = "18",
            ["chinesePunctuation"] = "你好，世界！这是全角：冒号"
        };

        var restored = YamlHelper.ParseYaml(YamlHelper.ConvertToYaml(source));

        Assert.Equal(source.Count, restored.Count);
        foreach (var pair in source)
        {
            Assert.Equal(pair.Value, restored[pair.Key]);
        }
    }

    /// <summary>
    /// 格式化会按键排序并规范化输出
    /// </summary>
    [Fact]
    public void FormatYaml_NormalizesOrdering()
    {
        var formatted = YamlHelper.FormatYaml("b: 乙\na: 甲");

        Assert.StartsWith("a: 甲", formatted);
        Assert.Contains("b: 乙", formatted);
    }

    /// <summary>
    /// 合法性判断：空白为无效并给出专用错误信息
    /// </summary>
    [Fact]
    public void IsValidYaml_WhenBlank_ReturnsFalseWithBlankMessage()
    {
        var valid = YamlHelper.IsValidYaml("   ", out var errorMessage);

        Assert.False(valid);
        Assert.Equal("YAML 字符串为空", errorMessage);
    }

    /// <summary>
    /// 合法性判断：正常键值对文档为有效
    /// </summary>
    [Fact]
    public void IsValidYaml_WithKeyValueDocument_ReturnsTrue()
    {
        Assert.True(YamlHelper.IsValidYaml("name: 曦寒"));
    }
}
