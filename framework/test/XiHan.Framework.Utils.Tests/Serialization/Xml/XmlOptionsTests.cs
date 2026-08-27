// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Serialization.Xml;

namespace XiHan.Framework.Utils.Tests.Serialization.Xml;

/// <summary>
/// XML 序列化与反序列化选项测试
/// </summary>
/// <remarks>
/// 除了锁死默认值与预设，还顺带验证缩进类开关确实作用到了输出上，
/// 避免出现"选项存在但没接进 XmlWriterSettings"的假开关。
/// </remarks>
public class XmlOptionsTests
{
    /// <summary>
    /// 序列化选项的默认值
    /// </summary>
    [Fact]
    public void SerializeOptions_Default_HasExpectedDefaultValues()
    {
        var options = new XmlSerializeOptions();

        Assert.False(options.OmitXmlDeclaration);
        Assert.True(options.Indent);
        Assert.Equal("  ", options.IndentChars);
        Assert.Same(Encoding.UTF8, options.Encoding);
        Assert.Equal(Environment.NewLine, options.NewLineChars);
        Assert.True(options.CheckCharacters);
        Assert.True(options.OmitNamespaces);
        Assert.Null(options.CustomNamespaces);
    }

    /// <summary>
    /// 序列化选项预设每次访问返回新实例
    /// </summary>
    [Fact]
    public void SerializeOptions_Presets_ReturnFreshInstanceEachTime()
    {
        Assert.NotSame(XmlSerializeOptions.Default, XmlSerializeOptions.Default);
        Assert.NotSame(XmlSerializeOptions.Compact, XmlSerializeOptions.Compact);
        Assert.NotSame(XmlSerializeOptions.Formatted, XmlSerializeOptions.Formatted);
    }

    /// <summary>
    /// 紧凑预设关闭声明与缩进
    /// </summary>
    [Fact]
    public void SerializeOptions_Compact_DisablesDeclarationAndIndent()
    {
        var options = XmlSerializeOptions.Compact;

        Assert.True(options.OmitXmlDeclaration);
        Assert.False(options.Indent);
        Assert.True(options.OmitNamespaces);
    }

    /// <summary>
    /// 格式化预设使用四空格缩进并保留命名空间
    /// </summary>
    [Fact]
    public void SerializeOptions_Formatted_UsesFourSpaceIndent()
    {
        var options = XmlSerializeOptions.Formatted;

        Assert.False(options.OmitXmlDeclaration);
        Assert.True(options.Indent);
        Assert.Equal("    ", options.IndentChars);
        Assert.False(options.OmitNamespaces);
    }

    /// <summary>
    /// 缩进字符设置会真实作用到输出
    /// </summary>
    [Fact]
    public void SerializeOptions_IndentChars_AffectOutput()
    {
        var options = new XmlSerializeOptions { OmitXmlDeclaration = true, Indent = true, IndentChars = "\t" };

        var xml = XmlHelper.Serialize(new XmlTestPerson { Name = "曦寒" }, options);

        Assert.Contains("\t<Name>曦寒</Name>", xml);
    }

    /// <summary>
    /// 反序列化选项的默认值
    /// </summary>
    [Fact]
    public void DeserializeOptions_Default_HasExpectedDefaultValues()
    {
        var options = new XmlDeserializeOptions();

        Assert.True(options.IgnoreWhitespace);
        Assert.True(options.IgnoreComments);
        Assert.True(options.CheckCharacters);
        Assert.True(options.IgnoreProcessingInstructions);
        Assert.False(options.ValidateXml);
        Assert.Equal(0L, options.MaxCharactersInDocument);
        Assert.Equal(0L, options.MaxCharactersFromEntities);
    }

    /// <summary>
    /// 反序列化选项预设每次访问返回新实例
    /// </summary>
    [Fact]
    public void DeserializeOptions_Presets_ReturnFreshInstanceEachTime()
    {
        Assert.NotSame(XmlDeserializeOptions.Default, XmlDeserializeOptions.Default);
        Assert.NotSame(XmlDeserializeOptions.Strict, XmlDeserializeOptions.Strict);
        Assert.NotSame(XmlDeserializeOptions.Lenient, XmlDeserializeOptions.Lenient);
    }

    /// <summary>
    /// 严格预设不忽略任何内容并开启校验
    /// </summary>
    [Fact]
    public void DeserializeOptions_Strict_KeepsEverythingAndValidates()
    {
        var options = XmlDeserializeOptions.Strict;

        Assert.False(options.IgnoreWhitespace);
        Assert.False(options.IgnoreComments);
        Assert.False(options.IgnoreProcessingInstructions);
        Assert.True(options.ValidateXml);
        Assert.True(options.CheckCharacters);
    }

    /// <summary>
    /// 宽松预设忽略所有非必要内容并关闭校验
    /// </summary>
    [Fact]
    public void DeserializeOptions_Lenient_IgnoresEverythingNonEssential()
    {
        var options = XmlDeserializeOptions.Lenient;

        Assert.True(options.IgnoreWhitespace);
        Assert.True(options.IgnoreComments);
        Assert.True(options.IgnoreProcessingInstructions);
        Assert.False(options.ValidateXml);
        Assert.False(options.CheckCharacters);
    }
}
