// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Serialization.Dynamic;

namespace XiHan.Framework.Serialization.Tests.Dynamic;

/// <summary>
/// <see cref="DynamicJsonHelper"/> 反序列化与序列化的测试
/// </summary>
public class DynamicJsonHelperTests
{
    /// <summary>
    /// 反序列化 JSON 对象字符串应得到 DynamicJsonObject
    /// </summary>
    [Fact]
    public void Deserialize_ObjectJson_ReturnsDynamicJsonObject()
    {
        const string json = """{"name":"张三","age":30}""";

        object? result = DynamicJsonHelper.Deserialize(json);
        var obj = Assert.IsType<DynamicJsonObject>(result);

        Assert.Equal("张三", obj.GetValue<string>("name"));
        Assert.Equal(30, obj.GetValue<int>("age"));
        Assert.True(obj.ContainsKey("name"));
        Assert.Equal(2, obj.Count);
    }

    /// <summary>
    /// 反序列化 JSON 数组字符串应得到 DynamicJsonArray
    /// </summary>
    [Fact]
    public void Deserialize_ArrayJson_ReturnsDynamicJsonArray()
    {
        const string json = """[1,2,3]""";

        object? result = DynamicJsonHelper.Deserialize(json);
        var array = Assert.IsType<DynamicJsonArray>(result);

        Assert.Equal(3, array.Count);
        Assert.Equal(new[] { 1, 2, 3 }, array.ToObject<int>());
    }

    /// <summary>
    /// 反序列化标量 JSON 字符串应得到 DynamicJsonValue
    /// </summary>
    /// <param name="json">标量 JSON 字符串</param>
    /// <param name="expectedRaw">期望的原始字符串表示</param>
    [Theory]
    [InlineData("42", "42")]
    [InlineData("true", "true")]
    [InlineData("\"hello\"", "hello")]
    public void Deserialize_ScalarJson_ReturnsDynamicJsonValue(string json, string expectedRaw)
    {
        object? result = DynamicJsonHelper.Deserialize(json);
        var value = Assert.IsType<DynamicJsonValue>(result);

        Assert.Equal(expectedRaw, value.ToString());
    }

    /// <summary>
    /// 反序列化空字符串或空白字符串时应抛出 ArgumentException
    /// </summary>
    /// <param name="json">空或空白字符串</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_EmptyOrWhitespace_ThrowsArgumentException(string json)
    {
        Assert.Throws<ArgumentException>(() => { _ = DynamicJsonHelper.Deserialize(json); });
    }

    /// <summary>
    /// 反序列化非法 JSON 时应抛出 JsonException
    /// </summary>
    /// <param name="json">非法 JSON 字符串</param>
    [Theory]
    [InlineData("{ invalid")]
    [InlineData("[1, 2")]
    [InlineData("not json")]
    public void Deserialize_InvalidJson_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => { _ = DynamicJsonHelper.Deserialize(json); });
    }

    /// <summary>
    /// 反序列化 JSON null 字面量应返回 null
    /// </summary>
    [Fact]
    public void Deserialize_NullLiteral_ReturnsNull()
    {
        object? result = DynamicJsonHelper.Deserialize("null");

        Assert.Null(result);
    }

    /// <summary>
    /// 异步序列化后再异步反序列化，应保持值一致
    /// </summary>
    [Fact]
    public async Task SerializeAsync_DeserializeAsync_Roundtrip()
    {
        var obj = new DynamicJsonObject();
        obj.SetValue("key", "value");

        var json = await DynamicJsonHelper.SerializeAsync(obj, false, TestContext.Current.CancellationToken);
        object? result = await DynamicJsonHelper.DeserializeAsync(json, TestContext.Current.CancellationToken);
        var reparsed = Assert.IsType<DynamicJsonObject>(result);

        Assert.Equal("value", reparsed.GetValue<string>("key"));
    }

    /// <summary>
    /// 将 JsonNode 转换为动态对象时，应按节点类型包装为对应的动态类型
    /// </summary>
    [Fact]
    public void JsonNode_ToDynamic_ReturnsWrappedTypes()
    {
        var objectNode = JsonNode.Parse("""{"a":1}""");
        var arrayNode = JsonNode.Parse("""[1,2]""");
        var valueNode = JsonNode.Parse("42");

        Assert.IsType<DynamicJsonObject>((object?)objectNode!.ToDynamic());
        Assert.IsType<DynamicJsonArray>((object?)arrayNode!.ToDynamic());
        Assert.IsType<DynamicJsonValue>((object?)valueNode!.ToDynamic());
    }

    /// <summary>
    /// 从普通对象创建动态 JSON 对象，应保留属性值（默认按驼峰命名）
    /// </summary>
    [Fact]
    public void FromObject_ReturnsDynamicJsonObject()
    {
        var source = new { Name = "张三", Age = 30 };

        object? result = DynamicJsonHelper.FromObject(source);
        var obj = Assert.IsType<DynamicJsonObject>(result);

        Assert.Equal("张三", obj.GetValue<string>("name"));
        Assert.Equal(30, obj.GetValue<int>("age"));
    }
}
