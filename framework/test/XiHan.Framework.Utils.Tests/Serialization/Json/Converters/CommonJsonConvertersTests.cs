// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Utils.Tests.Serialization.Json.Converters;

/// <summary>
/// 布尔与 Guid JSON 转换器测试
/// </summary>
/// <remarks>
/// 这两个转换器的价值在于兼容前端常见的松散写法（"true" 字符串、0/1 数字、大小写混排的 Guid），
/// 读不出来时统一回落到 false / Guid.Empty 而不是抛异常。
/// </remarks>
public class CommonJsonConvertersTests
{
    /// <summary>
    /// 用给定转换器构造一个干净的序列化选项
    /// </summary>
    /// <param name="converters">要挂载的转换器</param>
    private static JsonSerializerOptions CreateOptions(params JsonConverter[] converters)
    {
        var options = new JsonSerializerOptions();
        foreach (var converter in converters)
        {
            options.Converters.Add(converter);
        }

        return options;
    }

    /// <summary>
    /// 布尔值写为 JSON 布尔字面量
    /// </summary>
    [Fact]
    public void BooleanConverter_WritesJsonBoolean()
    {
        var options = CreateOptions(new BooleanJsonConverter());

        Assert.Equal("true", JsonSerializer.Serialize(true, options));
        Assert.Equal("false", JsonSerializer.Serialize(false, options));
    }

    /// <summary>
    /// 布尔值兼容布尔字面量、布尔字符串与 0/1 数字
    /// </summary>
    /// <param name="json">输入 JSON 片段</param>
    /// <param name="expected">期望结果</param>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"true\"", true)]
    [InlineData("\"False\"", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("-1", true)]
    [InlineData("\"不是布尔\"", false)]
    public void BooleanConverter_ReadsLooseRepresentations(string json, bool expected)
    {
        var options = CreateOptions(new BooleanJsonConverter());

        Assert.Equal(expected, JsonSerializer.Deserialize<bool>(json, options));
    }

    /// <summary>
    /// 可空布尔的 null 与有值往返
    /// </summary>
    [Fact]
    public void BooleanNullableConverter_RoundTripsNullAndValue()
    {
        var options = CreateOptions(new BooleanNullableConverter());

        Assert.Equal("null", JsonSerializer.Serialize<bool?>(null, options));
        Assert.Equal("true", JsonSerializer.Serialize<bool?>(true, options));
        Assert.Null(JsonSerializer.Deserialize<bool?>("null", options));
        Assert.True(JsonSerializer.Deserialize<bool?>("1", options));
        Assert.False(JsonSerializer.Deserialize<bool?>("\"false\"", options));
    }

    /// <summary>
    /// Guid 写为标准短横线字符串并可无损读回
    /// </summary>
    [Fact]
    public void GuidConverter_RoundTripsAsDashedString()
    {
        var options = CreateOptions(new GuidJsonConverter());
        var value = Guid.NewGuid();

        var json = JsonSerializer.Serialize(value, options);

        Assert.Equal($"\"{value}\"", json);
        Assert.Equal(value, JsonSerializer.Deserialize<Guid>(json, options));
    }

    /// <summary>
    /// Guid 大小写与花括号形式都能读回
    /// </summary>
    [Fact]
    public void GuidConverter_AcceptsUpperCaseAndBracedForms()
    {
        var options = CreateOptions(new GuidJsonConverter());
        var value = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

        Assert.Equal(value, JsonSerializer.Deserialize<Guid>("\"3F2504E0-4F89-11D3-9A0C-0305E82C3301\"", options));
        Assert.Equal(value, JsonSerializer.Deserialize<Guid>("\"{3f2504e0-4f89-11d3-9a0c-0305e82c3301}\"", options));
    }

    /// <summary>
    /// Guid 无法解析时回落为 Guid.Empty
    /// </summary>
    /// <param name="json">输入 JSON 片段</param>
    [Theory]
    [InlineData("\"不是Guid\"")]
    [InlineData("123")]
    [InlineData("true")]
    public void GuidConverter_WhenUnparsable_FallsBackToEmpty(string json)
    {
        var options = CreateOptions(new GuidJsonConverter());

        Assert.Equal(Guid.Empty, JsonSerializer.Deserialize<Guid>(json, options));
    }

    /// <summary>
    /// 可空 Guid 的 null 与有值往返，非法字符串读为 null
    /// </summary>
    [Fact]
    public void GuidNullableConverter_RoundTripsNullAndValue()
    {
        var options = CreateOptions(new GuidNullableConverter());
        var value = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

        Assert.Equal("null", JsonSerializer.Serialize<Guid?>(null, options));
        Assert.Equal($"\"{value}\"", JsonSerializer.Serialize<Guid?>(value, options));
        Assert.Null(JsonSerializer.Deserialize<Guid?>("null", options));
        Assert.Null(JsonSerializer.Deserialize<Guid?>("\"不是Guid\"", options));
        Assert.Equal(value, JsonSerializer.Deserialize<Guid?>($"\"{value}\"", options));
    }
}
