// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JsonHelper 校验与辅助转换测试
/// </summary>
/// <remarks>
/// 格式化/压缩类方法的断言以"语义等价"为主（用 CompareJson 判定），
/// 只有压缩这种输出唯一的场景才锁死字面量。
/// </remarks>
public class JsonHelperValidationTests
{
    /// <summary>
    /// 合法 JSON 校验通过
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    [InlineData("\"文本\"")]
    public void IsValidJson_WithValidJson_ReturnsTrue(string json)
    {
        Assert.True(JsonHelper.IsValidJson(json));
    }

    /// <summary>
    /// 非法 JSON 校验失败并给出错误信息
    /// </summary>
    [Fact]
    public void IsValidJson_WithInvalidJson_ReturnsFalseWithMessage()
    {
        var valid = JsonHelper.IsValidJson("{\"a\":", out var errorMessage);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    /// <summary>
    /// 空白字符串校验失败并给出专用错误信息
    /// </summary>
    [Fact]
    public void IsValidJson_WhenBlank_ReturnsFalseWithBlankMessage()
    {
        var valid = JsonHelper.IsValidJson("   ", out var errorMessage);

        Assert.False(valid);
        Assert.Equal("JSON 字符串为空", errorMessage);
    }

    /// <summary>
    /// 根节点类型校验按实际 ValueKind 判定
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="expectedKind">期望的根类型</param>
    /// <param name="expectedResult">期望的校验结果</param>
    [Theory]
    [InlineData("{\"a\":1}", JsonValueKind.Object, true)]
    [InlineData("[1,2]", JsonValueKind.Array, true)]
    [InlineData("{\"a\":1}", JsonValueKind.Array, false)]
    [InlineData("[1,2]", JsonValueKind.Object, false)]
    [InlineData("\"文本\"", JsonValueKind.String, true)]
    public void ValidateStructure_MatchesRootKind(string json, JsonValueKind expectedKind, bool expectedResult)
    {
        Assert.Equal(expectedResult, JsonHelper.ValidateStructure(json, expectedKind));
    }

    /// <summary>
    /// JSON 非法时结构校验返回 false
    /// </summary>
    [Fact]
    public void ValidateStructure_WhenJsonInvalid_ReturnsFalse()
    {
        Assert.False(JsonHelper.ValidateStructure("{不是 JSON", JsonValueKind.Object));
    }

    /// <summary>
    /// 必需属性齐全时校验通过且缺失列表为空
    /// </summary>
    [Fact]
    public void ValidateRequiredProperties_WhenAllPresent_ReturnsValid()
    {
        var (isValid, missing) = JsonHelper.ValidateRequiredProperties(
            "{\"name\":\"曦寒\",\"age\":18}",
            ["name", "age"]);

        Assert.True(isValid);
        Assert.Empty(missing);
    }

    /// <summary>
    /// 缺少必需属性时列出全部缺失项
    /// </summary>
    [Fact]
    public void ValidateRequiredProperties_WhenMissing_ListsMissingProperties()
    {
        var (isValid, missing) = JsonHelper.ValidateRequiredProperties(
            "{\"name\":\"曦寒\"}",
            ["name", "age", "city"]);

        Assert.False(isValid);
        Assert.Equal(new[] { "age", "city" }, missing);
    }

    /// <summary>
    /// 根节点不是对象时视为全部必需属性缺失
    /// </summary>
    [Fact]
    public void ValidateRequiredProperties_WhenRootNotObject_ReturnsAllMissing()
    {
        var (isValid, missing) = JsonHelper.ValidateRequiredProperties("[1,2]", ["name"]);

        Assert.False(isValid);
        Assert.Equal(new[] { "name" }, missing);
    }

    /// <summary>
    /// JSON 非法时必需属性校验返回全部缺失
    /// </summary>
    [Fact]
    public void ValidateRequiredProperties_WhenJsonInvalid_ReturnsAllMissing()
    {
        var (isValid, missing) = JsonHelper.ValidateRequiredProperties("{不是 JSON", ["name"]);

        Assert.False(isValid);
        Assert.Single(missing);
    }

    /// <summary>
    /// 格式化输出带换行且与原文语义等价
    /// </summary>
    [Fact]
    public void FormatJson_ProducesIndentedButEquivalentJson()
    {
        const string Source = "{\"a\":1,\"b\":{\"c\":\"文本\"}}";

        var formatted = JsonHelper.FormatJson(Source);

        Assert.Contains("\n", formatted);
        Assert.True(JsonHelper.CompareJson(Source, formatted));
    }

    /// <summary>
    /// 关闭缩进时格式化输出为紧凑形式
    /// </summary>
    [Fact]
    public void FormatJson_WhenIndentFalse_ProducesCompactJson()
    {
        var compact = JsonHelper.FormatJson("{\n  \"a\": 1\n}", false);

        Assert.Equal("{\"a\":1}", compact);
    }

    /// <summary>
    /// JSON 非法时格式化原样返回
    /// </summary>
    [Fact]
    public void FormatJson_WhenInvalid_ReturnsOriginal()
    {
        const string Source = "{不是 JSON";

        Assert.Equal(Source, JsonHelper.FormatJson(Source));
    }

    /// <summary>
    /// 压缩移除所有多余空白
    /// </summary>
    [Fact]
    public void CompressJson_RemovesWhitespace()
    {
        Assert.Equal("{\"a\":1,\"b\":[1,2]}", JsonHelper.CompressJson("{\n  \"a\" : 1,\n  \"b\" : [ 1, 2 ]\n}"));
    }

    /// <summary>
    /// JSON 非法时压缩原样返回
    /// </summary>
    [Fact]
    public void CompressJson_WhenInvalid_ReturnsOriginal()
    {
        const string Source = "[1,2";

        Assert.Equal(Source, JsonHelper.CompressJson(Source));
    }

    /// <summary>
    /// 结构化比较忽略属性顺序与空白
    /// </summary>
    [Fact]
    public void CompareJson_IgnoresPropertyOrderAndWhitespace()
    {
        Assert.True(JsonHelper.CompareJson("{\"a\":1,\"b\":2}", "{ \"b\" : 2 , \"a\" : 1 }"));
    }

    /// <summary>
    /// 值不同、属性数量不同、数组顺序不同均判定为不相等
    /// </summary>
    /// <param name="left">左侧 JSON</param>
    /// <param name="right">右侧 JSON</param>
    [Theory]
    [InlineData("{\"a\":1}", "{\"a\":2}")]
    [InlineData("{\"a\":1}", "{\"a\":1,\"b\":2}")]
    [InlineData("[1,2]", "[2,1]")]
    [InlineData("[1,2]", "[1,2,3]")]
    [InlineData("{\"a\":1}", "[1]")]
    [InlineData("{不是 JSON", "{}")]
    public void CompareJson_WhenDifferent_ReturnsFalse(string left, string right)
    {
        Assert.False(JsonHelper.CompareJson(left, right));
    }

    /// <summary>
    /// 哈希对格式差异不敏感，对内容差异敏感
    /// </summary>
    [Fact]
    public void ComputeHash_IsStableAcrossFormattingAndSensitiveToContent()
    {
        var hash1 = JsonHelper.ComputeHash("{\"a\":1}");
        var hash2 = JsonHelper.ComputeHash("{\n  \"a\" : 1\n}");
        var hash3 = JsonHelper.ComputeHash("{\"a\":2}");

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(hash1, hash3);
        Assert.Matches("^[0-9A-F]{64}$", hash1);
    }

    /// <summary>
    /// 克隆产出与原文等价的紧凑 JSON
    /// </summary>
    [Fact]
    public void CloneJson_ProducesEquivalentCompactJson()
    {
        var clone = JsonHelper.CloneJson("{ \"a\" : 1 }");

        Assert.Equal("{\"a\":1}", clone);
    }

    /// <summary>
    /// JSON 非法时克隆原样返回
    /// </summary>
    [Fact]
    public void CloneJson_WhenInvalid_ReturnsOriginal()
    {
        const string Source = "{不是 JSON";

        Assert.Equal(Source, JsonHelper.CloneJson(Source));
    }

    /// <summary>
    /// 转字典时嵌套对象与数组均被扁平化
    /// </summary>
    [Fact]
    public void JsonToDictionary_FlattensNestedStructure()
    {
        var dict = JsonHelper.JsonToDictionary("{\"user\":{\"name\":\"曦寒\"},\"tags\":[\"甲\",\"乙\"],\"count\":3}");

        Assert.Equal("曦寒", dict["user.name"]);
        Assert.Equal("甲", dict["tags.0"]);
        Assert.Equal("乙", dict["tags.1"]);
        Assert.Equal("3", dict["count"]);
    }

    /// <summary>
    /// 支持自定义层级分隔符
    /// </summary>
    [Fact]
    public void JsonToDictionary_WithCustomSeparator_UsesIt()
    {
        var dict = JsonHelper.JsonToDictionary("{\"user\":{\"name\":\"曦寒\"}}", "/");

        Assert.Equal("曦寒", dict["user/name"]);
    }

    /// <summary>
    /// JSON 非法时转字典返回空字典
    /// </summary>
    [Fact]
    public void JsonToDictionary_WhenInvalid_ReturnsEmpty()
    {
        Assert.Empty(JsonHelper.JsonToDictionary("{不是 JSON"));
    }

    /// <summary>
    /// 字典转 JSON 不改写字典键
    /// </summary>
    /// <remarks>
    /// 命名策略只作用于对象属性名，字典键由 DictionaryKeyPolicy 控制且未配置，
    /// 所以帕斯卡键必须原样保留，否则配置类场景会取不到值。
    /// </remarks>
    [Fact]
    public void DictionaryToJson_KeepsDictionaryKeysUnchanged()
    {
        var json = JsonHelper.DictionaryToJson(new Dictionary<string, object>
        {
            ["Key"] = "值",
            ["Count"] = 3
        });

        Assert.Equal("值", JsonHelper.QueryNode(json, "Key"));
        Assert.Equal("3", JsonHelper.QueryNode(json, "Count"));
    }

    /// <summary>
    /// 合并时第二份 JSON 覆盖同名键
    /// </summary>
    [Fact]
    public void MergeJson_WhenOverwrite_TakesSecondValue()
    {
        var merged = JsonHelper.MergeJson("{\"a\":\"1\",\"b\":\"2\"}", "{\"b\":\"3\",\"c\":\"4\"}");

        Assert.Equal("1", JsonHelper.QueryNode(merged, "a"));
        Assert.Equal("3", JsonHelper.QueryNode(merged, "b"));
        Assert.Equal("4", JsonHelper.QueryNode(merged, "c"));
    }

    /// <summary>
    /// 不覆盖时保留第一份 JSON 的同名键值
    /// </summary>
    [Fact]
    public void MergeJson_WhenNotOverwrite_KeepsFirstValue()
    {
        var merged = JsonHelper.MergeJson("{\"a\":\"1\",\"b\":\"2\"}", "{\"b\":\"3\",\"c\":\"4\"}", false);

        Assert.Equal("2", JsonHelper.QueryNode(merged, "b"));
        Assert.Equal("4", JsonHelper.QueryNode(merged, "c"));
    }

    /// <summary>
    /// 合并会按层级重建嵌套结构，两侧的子键都保留
    /// </summary>
    [Fact]
    public void MergeJson_WithNestedObjects_MergesByLeafKey()
    {
        var merged = JsonHelper.MergeJson("{\"s\":{\"x\":\"1\"}}", "{\"s\":{\"y\":\"2\"}}");

        Assert.Equal("1", JsonHelper.QueryNode(merged, "s.x"));
        Assert.Equal("2", JsonHelper.QueryNode(merged, "s.y"));
    }
}
