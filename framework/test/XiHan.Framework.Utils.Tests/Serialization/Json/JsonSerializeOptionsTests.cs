// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JsonSerializeOptions 默认值、预设与系统选项映射测试
/// </summary>
/// <remarks>
/// 预设是对外承诺的语义（紧凑 / 格式化 / 严格 / WebApi），一旦漂移会静默改变全站输出格式，
/// 因此逐项锁死；ToSystemOptions 是这批开关唯一的落地路径，必须逐字段验证映射。
/// </remarks>
public class JsonSerializeOptionsTests
{
    /// <summary>
    /// 默认实例的各项默认值
    /// </summary>
    [Fact]
    public void Default_HasExpectedDefaultValues()
    {
        var options = new JsonSerializeOptions();

        Assert.True(options.WriteIndented);
        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.False(options.IgnoreNullValues);
        Assert.False(options.IgnoreReadOnlyProperties);
        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.True(options.AllowTrailingCommas);
        Assert.True(options.ReadCommentHandling);
        Assert.Same(JavaScriptEncoder.UnsafeRelaxedJsonEscaping, options.Encoder);
        Assert.Equal(64, options.MaxDepth);
        Assert.Equal(JsonNumberHandling.AllowReadingFromString, options.NumberHandling);
        Assert.Equal(JsonIgnoreCondition.Never, options.DefaultIgnoreCondition);
        Assert.Same(Encoding.UTF8, options.Encoding);
        Assert.Null(options.CustomConverters);
    }

    /// <summary>
    /// 预设属性每次访问都返回新实例
    /// </summary>
    /// <remarks>
    /// 预设是可变对象，若返回同一实例，调用方的就地修改会污染全局；这里锁死"每次新建"的语义。
    /// </remarks>
    [Fact]
    public void Presets_ReturnFreshInstanceEachTime()
    {
        Assert.NotSame(JsonSerializeOptions.Default, JsonSerializeOptions.Default);
        Assert.NotSame(JsonSerializeOptions.Compact, JsonSerializeOptions.Compact);
        Assert.NotSame(JsonSerializeOptions.Formatted, JsonSerializeOptions.Formatted);
        Assert.NotSame(JsonSerializeOptions.Strict, JsonSerializeOptions.Strict);
        Assert.NotSame(JsonSerializeOptions.WebApi, JsonSerializeOptions.WebApi);
    }

    /// <summary>
    /// 紧凑预设：不缩进、驼峰、忽略只读属性
    /// </summary>
    [Fact]
    public void Compact_HasCompactSettings()
    {
        var options = JsonSerializeOptions.Compact;

        Assert.False(options.WriteIndented);
        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.True(options.IgnoreNullValues);
        Assert.True(options.IgnoreReadOnlyProperties);
    }

    /// <summary>
    /// 格式化预设：缩进、驼峰、保留只读属性、宽松编码器
    /// </summary>
    [Fact]
    public void Formatted_HasFormattedSettings()
    {
        var options = JsonSerializeOptions.Formatted;

        Assert.True(options.WriteIndented);
        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.False(options.IgnoreNullValues);
        Assert.False(options.IgnoreReadOnlyProperties);
        Assert.Same(JavaScriptEncoder.UnsafeRelaxedJsonEscaping, options.Encoder);
    }

    /// <summary>
    /// 严格预设：保持原始命名、大小写敏感、不允许尾随逗号
    /// </summary>
    [Fact]
    public void Strict_HasStrictSettings()
    {
        var options = JsonSerializeOptions.Strict;

        Assert.True(options.WriteIndented);
        Assert.Null(options.PropertyNamingPolicy);
        Assert.False(options.PropertyNameCaseInsensitive);
        Assert.False(options.AllowTrailingCommas);
        Assert.False(options.IgnoreReadOnlyProperties);
    }

    /// <summary>
    /// WebApi 预设：不缩进、驼峰、宽松编码器并挂载全量自定义转换器
    /// </summary>
    [Fact]
    public void WebApi_CarriesAllCustomConverters()
    {
        var options = JsonSerializeOptions.WebApi;

        Assert.False(options.WriteIndented);
        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.Same(JavaScriptEncoder.UnsafeRelaxedJsonEscaping, options.Encoder);
        Assert.NotNull(options.CustomConverters);
        Assert.Equal(28, options.CustomConverters!.Count);
    }

    /// <summary>
    /// ToSystemOptions 逐字段映射到系统选项
    /// </summary>
    [Fact]
    public void ToSystemOptions_MapsEveryField()
    {
        var options = new JsonSerializeOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
            PropertyNameCaseInsensitive = false,
            AllowTrailingCommas = false,
            ReadCommentHandling = false,
            Encoder = JavaScriptEncoder.Default,
            MaxDepth = 16,
            NumberHandling = JsonNumberHandling.WriteAsString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyProperties = true
        };

        var system = options.ToSystemOptions();

        Assert.False(system.WriteIndented);
        Assert.Same(JsonNamingPolicy.KebabCaseLower, system.PropertyNamingPolicy);
        Assert.False(system.PropertyNameCaseInsensitive);
        Assert.False(system.AllowTrailingCommas);
        Assert.Equal(JsonCommentHandling.Disallow, system.ReadCommentHandling);
        Assert.Same(JavaScriptEncoder.Default, system.Encoder);
        Assert.Equal(16, system.MaxDepth);
        Assert.Equal(JsonNumberHandling.WriteAsString, system.NumberHandling);
        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, system.DefaultIgnoreCondition);
        Assert.True(system.IgnoreReadOnlyProperties);
    }

    /// <summary>
    /// 允许注释时映射为跳过注释
    /// </summary>
    [Fact]
    public void ToSystemOptions_WhenReadCommentHandlingTrue_MapsToSkip()
    {
        var system = new JsonSerializeOptions { ReadCommentHandling = true }.ToSystemOptions();

        Assert.Equal(JsonCommentHandling.Skip, system.ReadCommentHandling);
    }

    /// <summary>
    /// 自定义转换器被复制进系统选项
    /// </summary>
    [Fact]
    public void ToSystemOptions_CopiesCustomConverters()
    {
        var converter = new IntJsonConverter();
        var options = new JsonSerializeOptions { CustomConverters = [converter] };

        var system = options.ToSystemOptions();

        Assert.Single(system.Converters);
        Assert.Same(converter, system.Converters[0]);
    }

    /// <summary>
    /// 每次调用产出独立的系统选项实例
    /// </summary>
    [Fact]
    public void ToSystemOptions_ReturnsNewInstanceEachCall()
    {
        var options = new JsonSerializeOptions();

        Assert.NotSame(options.ToSystemOptions(), options.ToSystemOptions());
    }
}
