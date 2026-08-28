// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Utils.Tests.Serialization.Json.Converters;

/// <summary>
/// 测试用状态枚举
/// </summary>
/// <remarks>
/// 数值刻意不连续，用来验证转换器写出的是底层数值本身而不是序号。
/// </remarks>
public enum ConverterSampleStatus
{
    /// <summary>
    /// 待处理
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 生效中
    /// </summary>
    Active = 1,

    /// <summary>
    /// 已停用
    /// </summary>
    Disabled = 9
}

/// <summary>
/// 测试用订单，同时含默认枚举属性与标注了数值转换器的枚举属性
/// </summary>
public sealed class ConverterSampleOrder
{
    /// <summary>
    /// 走全局枚举策略的状态
    /// </summary>
    public ConverterSampleStatus PlainStatus { get; set; }

    /// <summary>
    /// 显式覆盖为数值形式的状态
    /// </summary>
    [JsonConverter(typeof(NumericEnumConverter<ConverterSampleStatus>))]
    public ConverterSampleStatus NumericStatus { get; set; }
}

/// <summary>
/// NumericEnumConverter 枚举数值转换器测试
/// </summary>
public class NumericEnumConverterTests
{
    /// <summary>
    /// 挂载了数值枚举转换器的序列化选项
    /// </summary>
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new NumericEnumConverter<ConverterSampleStatus>());
        return options;
    }

    /// <summary>
    /// 枚举写出为底层数值
    /// </summary>
    [Fact]
    public void Write_ProducesUnderlyingNumericValue()
    {
        var options = CreateOptions();

        Assert.Equal("9", JsonSerializer.Serialize(ConverterSampleStatus.Disabled, options));
        Assert.Equal("0", JsonSerializer.Serialize(ConverterSampleStatus.Pending, options));
    }

    /// <summary>
    /// 读取兼容数字、名称字符串与数字字符串三种来源
    /// </summary>
    /// <param name="json">输入 JSON 片段</param>
    /// <param name="expected">期望枚举值</param>
    [Theory]
    [InlineData("9", ConverterSampleStatus.Disabled)]
    [InlineData("1", ConverterSampleStatus.Active)]
    [InlineData("\"Disabled\"", ConverterSampleStatus.Disabled)]
    [InlineData("\"disabled\"", ConverterSampleStatus.Disabled)]
    [InlineData("\"9\"", ConverterSampleStatus.Disabled)]
    public void Read_AcceptsNumberAndNameAndNumericString(string json, ConverterSampleStatus expected)
    {
        var options = CreateOptions();

        Assert.Equal(expected, JsonSerializer.Deserialize<ConverterSampleStatus>(json, options));
    }

    /// <summary>
    /// 无法识别的标记抛出 JsonException
    /// </summary>
    /// <param name="json">输入 JSON 片段</param>
    [Theory]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("\"不存在的名称\"")]
    public void Read_WhenTokenUnsupported_ThrowsJsonException(string json)
    {
        var options = CreateOptions();

        Assert.Throws<JsonException>(() =>
        {
            JsonSerializer.Deserialize<ConverterSampleStatus>(json, options);
        });
    }

    /// <summary>
    /// 属性上的转换器覆盖全局的字符串枚举策略
    /// </summary>
    /// <remarks>
    /// 这是该转换器存在的唯一理由：全局按名称序列化时，个别字段仍需要数字形式。
    /// </remarks>
    [Fact]
    public void PropertyLevelConverter_OverridesGlobalStringEnumPolicy()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());

        var json = JsonSerializer.Serialize(
            new ConverterSampleOrder
            {
                PlainStatus = ConverterSampleStatus.Disabled,
                NumericStatus = ConverterSampleStatus.Disabled
            },
            options);

        Assert.Contains("\"PlainStatus\":\"Disabled\"", json);
        Assert.Contains("\"NumericStatus\":9", json);
    }

    /// <summary>
    /// 属性级数值枚举可以完整往返
    /// </summary>
    [Fact]
    public void PropertyLevelConverter_RoundTrips()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());

        var source = new ConverterSampleOrder
        {
            PlainStatus = ConverterSampleStatus.Active,
            NumericStatus = ConverterSampleStatus.Disabled
        };

        var restored = JsonSerializer.Deserialize<ConverterSampleOrder>(JsonSerializer.Serialize(source, options), options);

        Assert.NotNull(restored);
        Assert.Equal(ConverterSampleStatus.Active, restored!.PlainStatus);
        Assert.Equal(ConverterSampleStatus.Disabled, restored.NumericStatus);
    }

    /// <summary>
    /// 枚举数值必须稳定，避免持久化数据被静默改写语义
    /// </summary>
    /// <param name="expected">期望数值</param>
    /// <param name="status">枚举项</param>
    [Theory]
    [InlineData(0, ConverterSampleStatus.Pending)]
    [InlineData(1, ConverterSampleStatus.Active)]
    [InlineData(9, ConverterSampleStatus.Disabled)]
    public void EnumNumericValues_AreStable(int expected, ConverterSampleStatus status)
    {
        Assert.Equal(expected, (int)status);
    }
}
