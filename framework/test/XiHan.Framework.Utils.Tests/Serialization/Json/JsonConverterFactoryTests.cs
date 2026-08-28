// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using XiHan.Framework.Utils.Serialization.Json;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JsonConverterFactory 转换器装配测试
/// </summary>
/// <remarks>
/// 转换器数量与"每次返回新实例"是硬契约：数量变化意味着某类型悄悄失去了统一读写规则，
/// 而复用实例会让不同 JsonSerializerOptions 之间共享状态。
/// </remarks>
public class JsonConverterFactoryTests
{
    /// <summary>
    /// 数值转换器覆盖 8 种基础类型的可空与非可空形态
    /// </summary>
    [Fact]
    public void GetNumericConverters_CoversEveryNumericTypeAndItsNullable()
    {
        var converters = JsonConverterFactory.GetNumericConverters();

        Assert.Equal(16, converters.Count);
        Assert.Contains(converters, c => c is IntJsonConverter);
        Assert.Contains(converters, c => c is IntNullableConverter);
        Assert.Contains(converters, c => c is LongJsonConverter);
        Assert.Contains(converters, c => c is LongNullableConverter);
        Assert.Contains(converters, c => c is FloatJsonConverter);
        Assert.Contains(converters, c => c is FloatNullableConverter);
        Assert.Contains(converters, c => c is DoubleJsonConverter);
        Assert.Contains(converters, c => c is DoubleNullableConverter);
        Assert.Contains(converters, c => c is DecimalJsonConverter);
        Assert.Contains(converters, c => c is DecimalNullableConverter);
        Assert.Contains(converters, c => c is ByteJsonConverter);
        Assert.Contains(converters, c => c is ByteNullableConverter);
        Assert.Contains(converters, c => c is ShortJsonConverter);
        Assert.Contains(converters, c => c is ShortNullableConverter);
        Assert.Contains(converters, c => c is UIntJsonConverter);
        Assert.Contains(converters, c => c is UIntNullableConverter);
    }

    /// <summary>
    /// 日期时间转换器覆盖 4 种类型的可空与非可空形态
    /// </summary>
    [Fact]
    public void GetDateTimeConverters_CoversEveryDateTimeTypeAndItsNullable()
    {
        var converters = JsonConverterFactory.GetDateTimeConverters();

        Assert.Equal(8, converters.Count);
        Assert.Contains(converters, c => c is DateOnlyJsonConverter);
        Assert.Contains(converters, c => c is DateOnlyNullableConverter);
        Assert.Contains(converters, c => c is TimeOnlyJsonConverter);
        Assert.Contains(converters, c => c is TimeOnlyNullableConverter);
        Assert.Contains(converters, c => c is DateTimeJsonConverter);
        Assert.Contains(converters, c => c is DateTimeNullableConverter);
        Assert.Contains(converters, c => c is DateTimeOffsetJsonConverter);
        Assert.Contains(converters, c => c is DateTimeOffsetNullableConverter);
    }

    /// <summary>
    /// 通用转换器覆盖布尔与 Guid
    /// </summary>
    [Fact]
    public void GetCommonConverters_CoversBooleanAndGuid()
    {
        var converters = JsonConverterFactory.GetCommonConverters();

        Assert.Equal(4, converters.Count);
        Assert.Contains(converters, c => c is BooleanJsonConverter);
        Assert.Contains(converters, c => c is BooleanNullableConverter);
        Assert.Contains(converters, c => c is GuidJsonConverter);
        Assert.Contains(converters, c => c is GuidNullableConverter);
    }

    /// <summary>
    /// 全量转换器等于三组之和
    /// </summary>
    [Fact]
    public void GetAllConverters_EqualsSumOfEveryGroup()
    {
        var all = JsonConverterFactory.GetAllConverters();

        Assert.Equal(28, all.Count);
        Assert.Equal(
            JsonConverterFactory.GetNumericConverters().Count
            + JsonConverterFactory.GetDateTimeConverters().Count
            + JsonConverterFactory.GetCommonConverters().Count,
            all.Count);
    }

    /// <summary>
    /// 每次调用返回全新的列表与全新的转换器实例
    /// </summary>
    [Fact]
    public void GetAllConverters_ReturnsFreshInstancesEachCall()
    {
        var first = JsonConverterFactory.GetAllConverters();
        var second = JsonConverterFactory.GetAllConverters();

        Assert.NotSame(first, second);
        Assert.NotSame(first[0], second[0]);
    }

    /// <summary>
    /// 配置扩展把全量转换器写入同一个选项实例并返回自身
    /// </summary>
    [Fact]
    public void ConfigureConverters_AddsAllConvertersAndReturnsSameInstance()
    {
        var options = new JsonSerializerOptions();

        var configured = options.ConfigureConverters();

        Assert.Same(options, configured);
        Assert.Equal(28, configured.Converters.Count);
    }

    /// <summary>
    /// 预配置选项默认缩进、驼峰命名并使用宽松编码器
    /// </summary>
    [Fact]
    public void CreateOptions_UsesIndentedCamelCaseAndRelaxedEncoder()
    {
        var options = JsonConverterFactory.CreateOptions();

        Assert.True(options.WriteIndented);
        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.Same(JavaScriptEncoder.UnsafeRelaxedJsonEscaping, options.Encoder);
        Assert.Equal(28, options.Converters.Count);
    }

    /// <summary>
    /// 关闭驼峰后保持原始属性名
    /// </summary>
    [Fact]
    public void CreateOptions_WhenCamelCaseDisabled_KeepsOriginalNames()
    {
        var options = JsonConverterFactory.CreateOptions(camelCase: false);

        Assert.Null(options.PropertyNamingPolicy);

        var json = JsonSerializer.Serialize(new JsonSampleUser { Name = "曦寒" }, options);
        Assert.Contains("\"Name\"", json);
    }

    /// <summary>
    /// 预配置选项输出中文不转义
    /// </summary>
    [Fact]
    public void CreateOptions_KeepsChineseUnescaped()
    {
        var json = JsonSerializer.Serialize(new JsonSampleUser { Name = "曦寒" }, JsonConverterFactory.CreateOptions());

        Assert.Contains("曦寒", json);
        Assert.Contains("\"name\"", json);
    }

    /// <summary>
    /// 预配置选项把 long 写成字符串以避免前端精度丢失
    /// </summary>
    [Fact]
    public void CreateOptions_WritesLongAsString()
    {
        var json = JsonSerializer.Serialize(9007199254740993L, JsonConverterFactory.CreateOptions());

        Assert.Equal("\"9007199254740993\"", json);
    }

    /// <summary>
    /// 日期时间转换器使用调用方传入的格式
    /// </summary>
    [Fact]
    public void GetDateTimeConverters_UsesProvidedFormats()
    {
        var options = new JsonSerializerOptions();
        foreach (var converter in JsonConverterFactory.GetDateTimeConverters("yyyyMMdd", "HHmmss"))
        {
            options.Converters.Add(converter);
        }

        Assert.Equal("\"20240506\"", JsonSerializer.Serialize(new DateOnly(2024, 5, 6), options));
        Assert.Equal("\"070809\"", JsonSerializer.Serialize(new TimeOnly(7, 8, 9), options));
        Assert.Equal("\"20240506 070809\"", JsonSerializer.Serialize(new DateTime(2024, 5, 6, 7, 8, 9), options));
    }

    /// <summary>
    /// DateTimeOffset 固定按 ISO 8601 带偏移输出，不受日期时间格式参数影响
    /// </summary>
    /// <remarks>
    /// 源码注释明确承诺这一点（保留时区语义供前端标准库解析），属于对外契约而非实现细节。
    /// 编码器显式对齐框架标准路径（JsonConverterFactory.CreateOptions / JsonSerializeOptions 默认都是
    /// UnsafeRelaxedJsonEscaping）：不设的话会退回 JavaScriptEncoder.Default，把偏移量里的 '+'
    /// 转义成 U+002B 的六字符形式，断言到的就不是转换器输出而是编码器行为。
    /// </remarks>
    [Fact]
    public void GetDateTimeConverters_KeepsDateTimeOffsetInIso8601()
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        foreach (var converter in JsonConverterFactory.GetDateTimeConverters("yyyyMMdd", "HHmmss"))
        {
            options.Converters.Add(converter);
        }

        var json = JsonSerializer.Serialize(
            new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(8)),
            options);

        Assert.Equal("\"2024-05-06T07:08:09+08:00\"", json);
    }
}
