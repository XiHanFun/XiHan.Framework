// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JsonDeserializeOptions 默认值、预设与系统选项映射测试
/// </summary>
public class JsonDeserializeOptionsTests
{
    /// <summary>
    /// 默认实例的各项默认值
    /// </summary>
    [Fact]
    public void Default_HasExpectedDefaultValues()
    {
        var options = new JsonDeserializeOptions();

        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.True(options.AllowTrailingCommas);
        Assert.True(options.ReadCommentHandling);
        Assert.True(options.IgnoreUnknownProperties);
        Assert.True(options.UseDefaultValues);
        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.Equal(JsonNumberHandling.AllowReadingFromString, options.NumberHandling);
        Assert.Equal(64, options.MaxDepth);
        Assert.Same(JavaScriptEncoder.UnsafeRelaxedJsonEscaping, options.Encoder);
        Assert.Equal(JsonIgnoreCondition.Never, options.DefaultIgnoreCondition);
        Assert.Null(options.CustomConverters);
        Assert.Equal(JsonErrorHandling.ThrowException, options.ErrorHandling);
        Assert.True(options.ValidateJson);
        Assert.Equal(0L, options.MaxStringLength);
        Assert.Equal(0L, options.MaxArrayLength);
    }

    /// <summary>
    /// 预设属性每次访问都返回新实例
    /// </summary>
    [Fact]
    public void Presets_ReturnFreshInstanceEachTime()
    {
        Assert.NotSame(JsonDeserializeOptions.Default, JsonDeserializeOptions.Default);
        Assert.NotSame(JsonDeserializeOptions.Strict, JsonDeserializeOptions.Strict);
        Assert.NotSame(JsonDeserializeOptions.Lenient, JsonDeserializeOptions.Lenient);
        Assert.NotSame(JsonDeserializeOptions.WebApi, JsonDeserializeOptions.WebApi);
    }

    /// <summary>
    /// 严格预设关闭所有容错开关并使用严格数字处理
    /// </summary>
    [Fact]
    public void Strict_TurnsOffEveryToleranceSwitch()
    {
        var options = JsonDeserializeOptions.Strict;

        Assert.False(options.PropertyNameCaseInsensitive);
        Assert.False(options.AllowTrailingCommas);
        Assert.False(options.ReadCommentHandling);
        Assert.False(options.IgnoreUnknownProperties);
        Assert.False(options.UseDefaultValues);
        Assert.Equal(JsonNumberHandling.Strict, options.NumberHandling);
    }

    /// <summary>
    /// 宽松预设打开所有容错开关并允许命名浮点字面量
    /// </summary>
    [Fact]
    public void Lenient_TurnsOnEveryToleranceSwitch()
    {
        var options = JsonDeserializeOptions.Lenient;

        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.True(options.AllowTrailingCommas);
        Assert.True(options.ReadCommentHandling);
        Assert.True(options.IgnoreUnknownProperties);
        Assert.True(options.UseDefaultValues);
        Assert.Equal(
            JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            options.NumberHandling);
    }

    /// <summary>
    /// WebApi 预设使用驼峰命名并挂载全量自定义转换器
    /// </summary>
    [Fact]
    public void WebApi_CarriesAllCustomConverters()
    {
        var options = JsonDeserializeOptions.WebApi;

        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.True(options.AllowTrailingCommas);
        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.NotNull(options.CustomConverters);
        Assert.Equal(28, options.CustomConverters!.Count);
    }

    /// <summary>
    /// ToSystemOptions 逐字段映射到系统选项
    /// </summary>
    [Fact]
    public void ToSystemOptions_MapsEveryField()
    {
        var options = new JsonDeserializeOptions
        {
            PropertyNameCaseInsensitive = false,
            AllowTrailingCommas = false,
            ReadCommentHandling = false,
            PropertyNamingPolicy = null,
            NumberHandling = JsonNumberHandling.Strict,
            MaxDepth = 8,
            Encoder = JavaScriptEncoder.Default,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var system = options.ToSystemOptions();

        Assert.False(system.PropertyNameCaseInsensitive);
        Assert.False(system.AllowTrailingCommas);
        Assert.Equal(JsonCommentHandling.Disallow, system.ReadCommentHandling);
        Assert.Null(system.PropertyNamingPolicy);
        Assert.Equal(JsonNumberHandling.Strict, system.NumberHandling);
        Assert.Equal(8, system.MaxDepth);
        Assert.Same(JavaScriptEncoder.Default, system.Encoder);
        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, system.DefaultIgnoreCondition);
    }

    /// <summary>
    /// 允许注释时映射为跳过注释
    /// </summary>
    [Fact]
    public void ToSystemOptions_WhenReadCommentHandlingTrue_MapsToSkip()
    {
        var system = new JsonDeserializeOptions { ReadCommentHandling = true }.ToSystemOptions();

        Assert.Equal(JsonCommentHandling.Skip, system.ReadCommentHandling);
    }

    /// <summary>
    /// 自定义转换器被复制进系统选项
    /// </summary>
    [Fact]
    public void ToSystemOptions_CopiesCustomConverters()
    {
        var converter = new GuidJsonConverter();
        var options = new JsonDeserializeOptions { CustomConverters = [converter] };

        var system = options.ToSystemOptions();

        Assert.Single(system.Converters);
        Assert.Same(converter, system.Converters[0]);
    }

    /// <summary>
    /// 错误处理枚举的数值必须稳定
    /// </summary>
    /// <remarks>
    /// 该枚举会被配置文件与持久化选项引用，数值漂移会让历史配置静默改变语义。
    /// </remarks>
    /// <param name="expected">期望数值</param>
    /// <param name="handling">枚举项</param>
    [Theory]
    [InlineData(0, JsonErrorHandling.ThrowException)]
    [InlineData(1, JsonErrorHandling.Ignore)]
    [InlineData(2, JsonErrorHandling.UseDefault)]
    [InlineData(3, JsonErrorHandling.Log)]
    public void JsonErrorHandling_NumericValuesAreStable(int expected, JsonErrorHandling handling)
    {
        Assert.Equal(expected, (int)handling);
    }
}
