// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JsonExtensions 扩展方法测试
/// </summary>
/// <remarks>
/// 扩展方法是对 JsonHelper 的语法糖，这里重点验证"入口选对了、空值安全、默认值语义正确"，
/// 不重复 JsonHelper 自身的深度用例。
/// </remarks>
public class JsonExtensionsTests
{
    private const string SampleJson = """
        {
          "user": { "name": "曦寒", "age": 18 },
          "tags": [ "甲", "乙" ]
        }
        """;

    /// <summary>
    /// 对象转 JSON 走驼峰命名
    /// </summary>
    [Fact]
    public void ToJson_OnObject_UsesCamelCase()
    {
        var json = new JsonSampleUser { Name = "曦寒", IsActive = true }.ToJson();

        Assert.Contains("\"name\"", json);
        Assert.Contains("\"isActive\"", json);
    }

    /// <summary>
    /// 泛型反序列化还原对象
    /// </summary>
    [Fact]
    public void FromJson_Generic_RestoresObject()
    {
        var user = "{\"name\":\"曦寒\",\"age\":18}".FromJson<JsonSampleUser>();

        Assert.Equal("曦寒", user.Name);
        Assert.Equal(18, user.Age);
    }

    /// <summary>
    /// 非泛型反序列化返回 JsonElement
    /// </summary>
    [Fact]
    public void FromJson_NonGeneric_ReturnsJsonElement()
    {
        var result = "{\"name\":\"曦寒\"}".FromJson();

        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal("曦寒", element.GetProperty("name").GetString());
    }

    /// <summary>
    /// 字符串合法性判断与错误信息输出
    /// </summary>
    [Fact]
    public void IsValidJson_Extension_ReportsValidity()
    {
        Assert.True("{\"a\":1}".IsValidJson());
        Assert.False("{\"a\":".IsValidJson());

        var valid = "{\"a\":".IsValidJson(out var errorMessage);
        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    /// <summary>
    /// 格式化与压缩扩展方法产出正确形态
    /// </summary>
    [Fact]
    public void FormatJson_And_CompressJson_Extensions_Work()
    {
        var formatted = "{\"a\":1}".FormatJson();
        var compressed = "{\n  \"a\" : 1\n}".CompressJson();

        Assert.Contains("\n", formatted);
        Assert.Equal("{\"a\":1}", compressed);
    }

    /// <summary>
    /// 节点查询扩展方法与帮助类结果一致
    /// </summary>
    [Fact]
    public void QueryNode_Extensions_ReturnSameResultAsHelper()
    {
        Assert.Equal("曦寒", SampleJson.QueryNode("$.user.name"));
        Assert.Equal(new[] { "甲", "乙" }, SampleJson.QueryNodes("$.tags.*"));
    }

    /// <summary>
    /// 字符串转扁平化字典
    /// </summary>
    [Fact]
    public void ToDictionary_Extension_FlattensJson()
    {
        var dict = SampleJson.ToDictionary();

        Assert.Equal("曦寒", dict["user.name"]);
        Assert.Equal("甲", dict["tags.0"]);
    }

    /// <summary>
    /// TryFromJson 在合法与非法输入下的返回值
    /// </summary>
    [Fact]
    public void TryFromJson_ReflectsParseResult()
    {
        Assert.True("{\"name\":\"曦寒\"}".TryFromJson<JsonSampleUser>(out var user));
        Assert.NotNull(user);
        Assert.Equal("曦寒", user!.Name);

        Assert.False("{\"name\":".TryFromJson<JsonSampleUser>(out var broken));
        Assert.Null(broken);

        Assert.False("   ".TryFromJson<JsonSampleUser>(out var blank));
        Assert.Null(blank);
    }

    /// <summary>
    /// 字典转 JSON 保留原始键
    /// </summary>
    [Fact]
    public void ToJson_OnDictionaries_KeepsOriginalKeys()
    {
        var objectJson = new Dictionary<string, object> { ["Key"] = "值" }.ToJson();
        var stringJson = new Dictionary<string, string> { ["Key"] = "值" }.ToJson();

        Assert.Equal("值", JsonHelper.QueryNode(objectJson, "Key"));
        Assert.Equal("值", JsonHelper.QueryNode(stringJson, "Key"));
    }

    /// <summary>
    /// 取值扩展在键缺失时返回默认值
    /// </summary>
    [Fact]
    public void GetValueOrDefault_WhenKeyMissing_ReturnsDefault()
    {
        var dict = new Dictionary<string, string> { ["a"] = "1" };

        Assert.Equal("1", dict.GetValueOrDefault("a", "默认"));
        Assert.Equal("默认", dict.GetValueOrDefault("b", "默认"));
    }

    /// <summary>
    /// 嵌套键的读写走扁平化后的完整键
    /// </summary>
    [Fact]
    public void NestedValue_ReadAndWrite_UseFlattenedKey()
    {
        var dict = new Dictionary<string, string>();

        dict.SetNestedValue("server.host", "localhost");

        Assert.Equal("localhost", dict["server.host"]);
        Assert.Equal("localhost", dict.GetNestedValue("server.host", "默认"));
        Assert.Equal("默认", dict.GetNestedValue("server.port", "默认"));
    }

    /// <summary>
    /// 合并字典时按 overwrite 决定同名键归属，且不修改原字典
    /// </summary>
    [Fact]
    public void Merge_RespectsOverwriteFlagAndKeepsSourceIntact()
    {
        var source = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };
        var other = new Dictionary<string, string> { ["b"] = "3", ["c"] = "4" };

        var overwritten = source.Merge(other);
        var preserved = source.Merge(other, false);

        Assert.Equal("3", overwritten["b"]);
        Assert.Equal("2", preserved["b"]);
        Assert.Equal("4", preserved["c"]);
        Assert.Equal("2", source["b"]);
    }

    /// <summary>
    /// JsonNode 安全取值在 null 时回落到默认值
    /// </summary>
    [Fact]
    public void GetValueSafe_WhenNodeNull_ReturnsDefault()
    {
        Assert.Equal("默认", ((JsonNode?)null).GetValueSafe("默认"));
        Assert.Equal("曦寒", JsonNode.Parse("\"曦寒\"").GetValueSafe());
    }

    /// <summary>
    /// JsonObject 安全取属性在缺失时回落到默认值
    /// </summary>
    [Fact]
    public void GetPropertySafe_WhenPropertyMissing_ReturnsDefault()
    {
        var jsonObject = JsonNode.Parse("{\"a\":\"1\"}") as JsonObject;

        Assert.Equal("1", jsonObject.GetPropertySafe("a", "默认"));
        Assert.Equal("默认", jsonObject.GetPropertySafe("b", "默认"));
        Assert.Equal("默认", ((JsonObject?)null).GetPropertySafe("a", "默认"));
    }

    /// <summary>
    /// JsonArray 安全取元素在越界或负数下标时回落到默认值
    /// </summary>
    [Fact]
    public void GetElementSafe_WhenIndexOutOfRange_ReturnsDefault()
    {
        var jsonArray = JsonNode.Parse("[\"甲\",\"乙\"]") as JsonArray;

        Assert.Equal("甲", jsonArray.GetElementSafe(0, "默认"));
        Assert.Equal("默认", jsonArray.GetElementSafe(2, "默认"));
        Assert.Equal("默认", jsonArray.GetElementSafe(-1, "默认"));
        Assert.Equal("默认", ((JsonArray?)null).GetElementSafe(0, "默认"));
    }

    /// <summary>
    /// 字符串转指定类型的成功路径
    /// </summary>
    [Fact]
    public void TryConvertTo_ForSupportedTypes_Succeeds()
    {
        Assert.True("文本".TryConvertTo<string>(out var text));
        Assert.Equal("文本", text);

        Assert.True("true".TryConvertTo<bool>(out var flag));
        Assert.True(flag);

        Assert.True("123".TryConvertTo<int>(out var number));
        Assert.Equal(123, number);

        Assert.True("123".TryConvertTo<double>(out var real));
        Assert.Equal(123d, real);

        Assert.True("123".TryConvertTo<decimal>(out var money));
        Assert.Equal(123m, money);

        Assert.True("2024-05-06".TryConvertTo<DateTime>(out var date));
        Assert.Equal(new DateTime(2024, 5, 6), date);

        // 未特判的类型走 Convert.ChangeType 兜底
        Assert.True("123".TryConvertTo<long>(out var big));
        Assert.Equal(123L, big);
    }

    /// <summary>
    /// 可空目标类型按其基础类型解析
    /// </summary>
    [Fact]
    public void TryConvertTo_ForNullableTarget_UsesUnderlyingType()
    {
        Assert.True("123".TryConvertTo<int?>(out var number));
        Assert.Equal(123, number);
    }

    /// <summary>
    /// 空字符串与不可解析的值返回 false
    /// </summary>
    [Fact]
    public void TryConvertTo_WhenValueBlankOrUnparsable_ReturnsFalse()
    {
        Assert.False("".TryConvertTo<int>(out _));
        Assert.False("不是数字".TryConvertTo<int>(out _));
        Assert.False("不是布尔".TryConvertTo<bool>(out _));
    }

    /// <summary>
    /// 转换失败时返回调用方给定的默认值
    /// </summary>
    [Fact]
    public void ConvertToOrDefault_WhenParseFails_ReturnsGivenDefault()
    {
        Assert.Equal(-1, "不是数字".ConvertToOrDefault(-1));
        Assert.Equal(123, "123".ConvertToOrDefault(-1));
    }
}
