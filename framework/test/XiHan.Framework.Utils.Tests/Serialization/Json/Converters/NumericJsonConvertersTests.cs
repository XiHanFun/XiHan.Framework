// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Utils.Tests.Serialization.Json.Converters;

/// <summary>
/// 数值类 JSON 转换器测试
/// </summary>
/// <remarks>
/// 这组转换器统一采用"宽进严出"：读取时数字与数字字符串都接受，读不出来回落到 0 / null，
/// 写入时除 long 外都写数字。long 必须写成字符串，这是为了规避 JavaScript Number 精度上限，
/// 属于对前端的显式契约，必须锁死。
/// </remarks>
public class NumericJsonConvertersTests
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
    /// int 写为数字，数字与数字字符串都能读回
    /// </summary>
    [Fact]
    public void IntConverter_ReadsNumberAndNumericString_WritesNumber()
    {
        var options = CreateOptions(new IntJsonConverter());

        Assert.Equal("42", JsonSerializer.Serialize(42, options));
        Assert.Equal(42, JsonSerializer.Deserialize<int>("42", options));
        Assert.Equal(42, JsonSerializer.Deserialize<int>("\"42\"", options));
    }

    /// <summary>
    /// int 遇到无法解析的标记回落为 0
    /// </summary>
    /// <param name="json">输入 JSON 片段</param>
    [Theory]
    [InlineData("true")]
    [InlineData("\"不是数字\"")]
    public void IntConverter_WhenTokenUnparsable_FallsBackToZero(string json)
    {
        var options = CreateOptions(new IntJsonConverter());

        Assert.Equal(0, JsonSerializer.Deserialize<int>(json, options));
    }

    /// <summary>
    /// 可空 int 的 null 与有值往返
    /// </summary>
    [Fact]
    public void IntNullableConverter_RoundTripsNullAndValue()
    {
        var options = CreateOptions(new IntNullableConverter());

        Assert.Equal("null", JsonSerializer.Serialize<int?>(null, options));
        Assert.Equal("7", JsonSerializer.Serialize<int?>(7, options));
        Assert.Null(JsonSerializer.Deserialize<int?>("null", options));
        Assert.Equal(7, JsonSerializer.Deserialize<int?>("\"7\"", options));
        Assert.Null(JsonSerializer.Deserialize<int?>("true", options));
    }

    /// <summary>
    /// long 必须写成字符串以规避前端精度丢失
    /// </summary>
    [Fact]
    public void LongConverter_WritesString_ToAvoidJavaScriptPrecisionLoss()
    {
        var options = CreateOptions(new LongJsonConverter());

        Assert.Equal("\"9007199254740993\"", JsonSerializer.Serialize(9007199254740993L, options));
    }

    /// <summary>
    /// long 从字符串与数字都能读回，超出 double 精度的值不失真
    /// </summary>
    [Fact]
    public void LongConverter_RoundTripsWithoutPrecisionLoss()
    {
        var options = CreateOptions(new LongJsonConverter());
        const long Value = 9007199254740993L;

        Assert.Equal(Value, JsonSerializer.Deserialize<long>(JsonSerializer.Serialize(Value, options), options));
        Assert.Equal(Value, JsonSerializer.Deserialize<long>("9007199254740993", options));
        Assert.Equal(0L, JsonSerializer.Deserialize<long>("true", options));
    }

    /// <summary>
    /// 可空 long 的 null 写为 null，有值写为字符串
    /// </summary>
    [Fact]
    public void LongNullableConverter_RoundTripsNullAndValue()
    {
        var options = CreateOptions(new LongNullableConverter());

        Assert.Equal("null", JsonSerializer.Serialize<long?>(null, options));
        Assert.Equal("\"5\"", JsonSerializer.Serialize<long?>(5L, options));
        Assert.Null(JsonSerializer.Deserialize<long?>("null", options));
        Assert.Equal(5L, JsonSerializer.Deserialize<long?>("\"5\"", options));
    }

    /// <summary>
    /// short 写为数字并接受数字字符串
    /// </summary>
    [Fact]
    public void ShortConverter_ReadsNumberAndNumericString_WritesNumber()
    {
        var options = CreateOptions(new ShortJsonConverter(), new ShortNullableConverter());

        Assert.Equal("7", JsonSerializer.Serialize<short>(7, options));
        Assert.Equal((short)7, JsonSerializer.Deserialize<short>("\"7\"", options));
        Assert.Equal((short)0, JsonSerializer.Deserialize<short>("true", options));
        Assert.Null(JsonSerializer.Deserialize<short?>("null", options));
    }

    /// <summary>
    /// byte 写为数字并接受数字字符串
    /// </summary>
    [Fact]
    public void ByteConverter_ReadsNumberAndNumericString_WritesNumber()
    {
        var options = CreateOptions(new ByteJsonConverter(), new ByteNullableConverter());

        Assert.Equal("7", JsonSerializer.Serialize<byte>(7, options));
        Assert.Equal((byte)7, JsonSerializer.Deserialize<byte>("\"7\"", options));
        Assert.Equal((byte)0, JsonSerializer.Deserialize<byte>("true", options));
        Assert.Null(JsonSerializer.Deserialize<byte?>("null", options));
    }

    /// <summary>
    /// uint 写为数字并接受数字字符串
    /// </summary>
    [Fact]
    public void UIntConverter_ReadsNumberAndNumericString_WritesNumber()
    {
        var options = CreateOptions(new UIntJsonConverter(), new UIntNullableConverter());

        Assert.Equal("7", JsonSerializer.Serialize(7u, options));
        Assert.Equal(7u, JsonSerializer.Deserialize<uint>("\"7\"", options));
        Assert.Equal(0u, JsonSerializer.Deserialize<uint>("true", options));
        Assert.Null(JsonSerializer.Deserialize<uint?>("null", options));
    }

    /// <summary>
    /// decimal 保持高精度往返，且写出的是 JSON 数字
    /// </summary>
    [Fact]
    public void DecimalConverter_KeepsFullPrecision()
    {
        var options = CreateOptions(new DecimalJsonConverter());

        Assert.Equal("12.5", JsonSerializer.Serialize(12.5m, options));

        var json = JsonSerializer.Serialize(decimal.MaxValue, options);
        Assert.Equal(decimal.MaxValue, JsonSerializer.Deserialize<decimal>(json, options));
    }

    /// <summary>
    /// decimal 接受数字字符串，无法解析时回落为 0
    /// </summary>
    [Fact]
    public void DecimalConverter_ReadsNumericStringAndFallsBackToZero()
    {
        var options = CreateOptions(new DecimalJsonConverter(), new DecimalNullableConverter());

        Assert.Equal(1234m, JsonSerializer.Deserialize<decimal>("\"1234\"", options));
        Assert.Equal(0m, JsonSerializer.Deserialize<decimal>("true", options));
        Assert.Null(JsonSerializer.Deserialize<decimal?>("null", options));
        Assert.Equal("null", JsonSerializer.Serialize<decimal?>(null, options));
    }

    /// <summary>
    /// double 写为数字并接受数字字符串
    /// </summary>
    [Fact]
    public void DoubleConverter_ReadsNumberAndNumericString_WritesNumber()
    {
        var options = CreateOptions(new DoubleJsonConverter(), new DoubleNullableConverter());

        Assert.Equal("1.5", JsonSerializer.Serialize(1.5d, options));
        Assert.Equal(1.5d, JsonSerializer.Deserialize<double>("1.5", options));
        Assert.Equal(1234d, JsonSerializer.Deserialize<double>("\"1234\"", options));
        Assert.Equal(0d, JsonSerializer.Deserialize<double>("true", options));
        Assert.Null(JsonSerializer.Deserialize<double?>("null", options));
    }

    /// <summary>
    /// float 写为数字并接受数字字符串
    /// </summary>
    [Fact]
    public void FloatConverter_ReadsNumberAndNumericString_WritesNumber()
    {
        var options = CreateOptions(new FloatJsonConverter(), new FloatNullableConverter());

        Assert.Equal("1.5", JsonSerializer.Serialize(1.5f, options));
        Assert.Equal(1.5f, JsonSerializer.Deserialize<float>("1.5", options));
        Assert.Equal(1234f, JsonSerializer.Deserialize<float>("\"1234\"", options));
        Assert.Equal(0f, JsonSerializer.Deserialize<float>("true", options));
        Assert.Null(JsonSerializer.Deserialize<float?>("null", options));
    }
}
