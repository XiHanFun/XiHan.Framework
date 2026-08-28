// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Serialization.Yaml;

namespace XiHan.Framework.Utils.Tests.Serialization.Yaml;

/// <summary>
/// YAML 序列化、反序列化与解析选项测试
/// </summary>
/// <remarks>
/// 三套选项的默认值直接决定配置文件的读写口径（是否转换类型、层级分隔符、是否忽略注释），
/// 一旦漂移会让既有配置被静默改读，因此逐项锁死。
/// </remarks>
public class YamlOptionsTests
{
    /// <summary>
    /// 序列化选项的默认值
    /// </summary>
    [Fact]
    public void SerializeOptions_Default_HasExpectedDefaultValues()
    {
        var options = new YamlSerializeOptions();

        Assert.Equal(2, options.IndentSize);
        Assert.False(options.IncludeDocumentMarkers);
        Assert.Null(options.HeaderComment);
        Assert.False(options.ForceQuoteStrings);
        Assert.True(options.SortKeys);
        Assert.Equal(80, options.MaxLineLength);
        Assert.False(options.UseFlowStyle);
        Assert.Equal("- ", options.ArrayPrefix);
    }

    /// <summary>
    /// 序列化选项预设每次访问返回新实例
    /// </summary>
    [Fact]
    public void SerializeOptions_Presets_ReturnFreshInstanceEachTime()
    {
        Assert.NotSame(YamlSerializeOptions.Default, YamlSerializeOptions.Default);
        Assert.NotSame(YamlSerializeOptions.Compact, YamlSerializeOptions.Compact);
        Assert.NotSame(YamlSerializeOptions.Formatted, YamlSerializeOptions.Formatted);
        Assert.NotSame(YamlSerializeOptions.Strict, YamlSerializeOptions.Strict);
    }

    /// <summary>
    /// 紧凑预设使用最小缩进与流式样式
    /// </summary>
    [Fact]
    public void SerializeOptions_Compact_UsesMinimalIndent()
    {
        var options = YamlSerializeOptions.Compact;

        Assert.Equal(1, options.IndentSize);
        Assert.False(options.IncludeDocumentMarkers);
        Assert.False(options.SortKeys);
        Assert.True(options.UseFlowStyle);
    }

    /// <summary>
    /// 格式化预设带文档标记与头部注释
    /// </summary>
    [Fact]
    public void SerializeOptions_Formatted_AddsMarkersAndHeaderComment()
    {
        var options = YamlSerializeOptions.Formatted;

        Assert.Equal(4, options.IndentSize);
        Assert.True(options.IncludeDocumentMarkers);
        Assert.True(options.SortKeys);
        Assert.False(options.UseFlowStyle);
        Assert.False(string.IsNullOrWhiteSpace(options.HeaderComment));
    }

    /// <summary>
    /// 严格预设强制字符串加引号
    /// </summary>
    [Fact]
    public void SerializeOptions_Strict_ForcesQuotedStrings()
    {
        var options = YamlSerializeOptions.Strict;

        Assert.Equal(2, options.IndentSize);
        Assert.True(options.IncludeDocumentMarkers);
        Assert.True(options.ForceQuoteStrings);
        Assert.True(options.SortKeys);
    }

    /// <summary>
    /// 反序列化选项的默认值
    /// </summary>
    [Fact]
    public void DeserializeOptions_Default_HasExpectedDefaultValues()
    {
        var options = new YamlDeserializeOptions();

        Assert.True(options.IgnoreComments);
        Assert.True(options.ConvertTypes);
        Assert.Equal(".", options.KeySeparator);
        Assert.False(options.AllowDuplicateKeys);
        Assert.True(options.CaseSensitive);
        Assert.Equal(100, options.MaxNestingDepth);
        Assert.True(options.IgnoreUnknownProperties);
        Assert.True(options.UseDefaultValues);
    }

    /// <summary>
    /// 反序列化选项预设每次访问返回新实例
    /// </summary>
    [Fact]
    public void DeserializeOptions_Presets_ReturnFreshInstanceEachTime()
    {
        Assert.NotSame(YamlDeserializeOptions.Default, YamlDeserializeOptions.Default);
        Assert.NotSame(YamlDeserializeOptions.Strict, YamlDeserializeOptions.Strict);
        Assert.NotSame(YamlDeserializeOptions.Lenient, YamlDeserializeOptions.Lenient);
    }

    /// <summary>
    /// 严格反序列化预设不忽略注释也不容错
    /// </summary>
    [Fact]
    public void DeserializeOptions_Strict_TurnsOffTolerance()
    {
        var options = YamlDeserializeOptions.Strict;

        Assert.False(options.IgnoreComments);
        Assert.True(options.ConvertTypes);
        Assert.False(options.AllowDuplicateKeys);
        Assert.True(options.CaseSensitive);
        Assert.False(options.IgnoreUnknownProperties);
        Assert.False(options.UseDefaultValues);
    }

    /// <summary>
    /// 宽松反序列化预设打开全部容错并关闭类型转换
    /// </summary>
    [Fact]
    public void DeserializeOptions_Lenient_TurnsOnTolerance()
    {
        var options = YamlDeserializeOptions.Lenient;

        Assert.True(options.IgnoreComments);
        Assert.False(options.ConvertTypes);
        Assert.True(options.AllowDuplicateKeys);
        Assert.False(options.CaseSensitive);
        Assert.True(options.IgnoreUnknownProperties);
        Assert.True(options.UseDefaultValues);
    }

    /// <summary>
    /// 解析选项的默认值
    /// </summary>
    [Fact]
    public void ParseOptions_Default_HasExpectedDefaultValues()
    {
        var options = new YamlParseOptions();

        Assert.True(options.IgnoreComments);
        Assert.True(options.ConvertTypes);
        Assert.Equal(".", options.KeySeparator);
        Assert.True(options.IgnoreEmptyLines);
        Assert.False(options.StrictMode);
        Assert.Equal(100, options.MaxNestingDepth);
    }

    /// <summary>
    /// 解析选项预设每次访问返回新实例
    /// </summary>
    [Fact]
    public void ParseOptions_Presets_ReturnFreshInstanceEachTime()
    {
        Assert.NotSame(YamlParseOptions.Default, YamlParseOptions.Default);
        Assert.NotSame(YamlParseOptions.Strict, YamlParseOptions.Strict);
        Assert.NotSame(YamlParseOptions.Lenient, YamlParseOptions.Lenient);
    }

    /// <summary>
    /// 严格解析预设不忽略注释与空行
    /// </summary>
    [Fact]
    public void ParseOptions_Strict_KeepsCommentsAndEmptyLines()
    {
        var options = YamlParseOptions.Strict;

        Assert.False(options.IgnoreComments);
        Assert.True(options.ConvertTypes);
        Assert.True(options.StrictMode);
        Assert.False(options.IgnoreEmptyLines);
    }

    /// <summary>
    /// 宽松解析预设忽略注释与空行且不转换类型
    /// </summary>
    [Fact]
    public void ParseOptions_Lenient_IgnoresCommentsAndKeepsRawValues()
    {
        var options = YamlParseOptions.Lenient;

        Assert.True(options.IgnoreComments);
        Assert.False(options.ConvertTypes);
        Assert.False(options.StrictMode);
        Assert.True(options.IgnoreEmptyLines);
    }
}
