// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Serialization.Xml;

namespace XiHan.Framework.Utils.Tests.Serialization.Xml;

/// <summary>
/// XmlHelper 序列化与反序列化契约测试
/// </summary>
/// <remarks>
/// 断言避开 XML 声明行（其 encoding 取决于底层 TextWriter，不是可靠契约），
/// 只锁定元素结构、命名空间开关、异常类型与往返一致性。
/// </remarks>
public class XmlHelperSerializationTests
{
    /// <summary>
    /// 默认选项省略命名空间声明，中文原样输出
    /// </summary>
    [Fact]
    public void Serialize_WithDefaultOptions_OmitsSchemaNamespacesAndKeepsChinese()
    {
        var xml = XmlHelper.Serialize(new XmlTestPerson { Name = "曦寒", Age = 18 });

        Assert.Contains("<Name>曦寒</Name>", xml);
        Assert.Contains("<Age>18</Age>", xml);
        Assert.DoesNotContain("xmlns:xsi", xml);
        Assert.DoesNotContain("xmlns:xsd", xml);
    }

    /// <summary>
    /// 紧凑选项不输出声明也不缩进
    /// </summary>
    [Fact]
    public void Serialize_WithCompactOptions_ProducesSingleLineWithoutDeclaration()
    {
        var xml = XmlHelper.Serialize(new XmlTestPerson { Name = "曦寒", Age = 18 }, XmlSerializeOptions.Compact);

        Assert.DoesNotContain("<?xml", xml);
        Assert.DoesNotContain("\n", xml);
        Assert.StartsWith("<XmlTestPerson>", xml);
        Assert.EndsWith("</XmlTestPerson>", xml);
    }

    /// <summary>
    /// 指定自定义命名空间时会写入根元素
    /// </summary>
    [Fact]
    public void Serialize_WithCustomNamespaces_DeclaresThemOnRoot()
    {
        var options = new XmlSerializeOptions
        {
            OmitNamespaces = false,
            CustomNamespaces = new Dictionary<string, string> { ["x"] = "urn:xihan:test" }
        };

        var xml = XmlHelper.Serialize(new XmlTestPerson { Name = "曦寒" }, options);

        Assert.Contains("urn:xihan:test", xml);
    }

    /// <summary>
    /// 序列化 null 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Serialize_WhenObjectNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            XmlHelper.Serialize<XmlTestPerson?>(null);
        });
    }

    /// <summary>
    /// 序列化再反序列化后各字段保持一致，null 成员仍为 null
    /// </summary>
    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesValues()
    {
        var source = new XmlTestPerson { Name = "曦寒", Age = 18, Nickname = null };

        var restored = XmlHelper.Deserialize<XmlTestPerson>(XmlHelper.Serialize(source));

        Assert.Equal("曦寒", restored.Name);
        Assert.Equal(18, restored.Age);
        Assert.Null(restored.Nickname);
    }

    /// <summary>
    /// 含集合的嵌套对象可完整往返
    /// </summary>
    [Fact]
    public void SerializeDeserialize_WithNestedCollection_RoundTrips()
    {
        var source = new XmlTestTeam
        {
            Title = "曦寒框架组",
            Members =
            [
                new XmlTestPerson { Name = "甲", Age = 20 },
                new XmlTestPerson { Name = "乙", Age = 30, Nickname = "小乙" }
            ]
        };

        var restored = XmlHelper.Deserialize<XmlTestTeam>(XmlHelper.Serialize(source));

        Assert.Equal("曦寒框架组", restored.Title);
        Assert.Equal(2, restored.Members.Count);
        Assert.Equal("甲", restored.Members[0].Name);
        Assert.Null(restored.Members[0].Nickname);
        Assert.Equal("小乙", restored.Members[1].Nickname);
        Assert.Equal(30, restored.Members[1].Age);
    }

    /// <summary>
    /// XML 保留字符被转义且能无损还原
    /// </summary>
    [Fact]
    public void SerializeDeserialize_WithReservedCharacters_EscapesAndRestores()
    {
        var source = new XmlTestPerson { Name = "<标签> & \"引号\" '单引号' 中文：曦寒", Age = 1 };

        var xml = XmlHelper.Serialize(source, XmlSerializeOptions.Compact);
        var restored = XmlHelper.Deserialize<XmlTestPerson>(xml);

        Assert.Contains("&lt;", xml);
        Assert.Contains("&amp;", xml);
        Assert.DoesNotContain("<标签>", xml);
        Assert.Equal(source.Name, restored.Name);
    }

    /// <summary>
    /// 空白 XML 字符串抛出 ArgumentException
    /// </summary>
    /// <param name="xml">待反序列化的字符串</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void Deserialize_WhenXmlBlank_ThrowsArgumentException(string xml)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            XmlHelper.Deserialize<XmlTestPerson>(xml);
        });

        Assert.Contains("XML 字符串不能为空", exception.Message);
    }

    /// <summary>
    /// XML 结构损坏时抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void Deserialize_WhenXmlMalformed_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            XmlHelper.Deserialize<XmlTestPerson>("<XmlTestPerson><Name>曦寒</XmlTestPerson>");
        });
    }

    /// <summary>
    /// 根元素与目标类型不匹配时抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void Deserialize_WhenRootElementMismatched_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            XmlHelper.Deserialize<XmlTestPerson>("<别的根节点><Name>曦寒</Name></别的根节点>");
        });
    }

    /// <summary>
    /// 宽松与严格反序列化选项都能处理带缩进和注释的文档
    /// </summary>
    [Fact]
    public void Deserialize_WithStrictAndLenientOptions_BothHandleWhitespaceAndComments()
    {
        const string Xml = """
            <XmlTestPerson>
              <!-- 人员信息 -->
              <Name>曦寒</Name>
              <Age>18</Age>
            </XmlTestPerson>
            """;

        Assert.Equal("曦寒", XmlHelper.Deserialize<XmlTestPerson>(Xml, XmlDeserializeOptions.Strict).Name);
        Assert.Equal("曦寒", XmlHelper.Deserialize<XmlTestPerson>(Xml, XmlDeserializeOptions.Lenient).Name);
    }

    /// <summary>
    /// TrySerialize 遇到 null 返回 false
    /// </summary>
    [Fact]
    public void TrySerialize_WhenObjectNull_ReturnsFalse()
    {
        var succeeded = XmlHelper.TrySerialize<XmlTestPerson?>(null, out var xml);

        Assert.False(succeeded);
        Assert.Null(xml);
    }

    /// <summary>
    /// TrySerialize 正常对象返回 true 且产出合法 XML
    /// </summary>
    [Fact]
    public void TrySerialize_WithValidObject_ReturnsTrueAndValidXml()
    {
        var succeeded = XmlHelper.TrySerialize(new XmlTestPerson { Name = "曦寒" }, out var xml);

        Assert.True(succeeded);
        Assert.NotNull(xml);
        Assert.True(XmlHelper.IsValidXml(xml!));
    }

    /// <summary>
    /// TryDeserialize 遇到损坏或空白 XML 返回 false
    /// </summary>
    [Fact]
    public void TryDeserialize_WhenXmlInvalidOrBlank_ReturnsFalse()
    {
        Assert.False(XmlHelper.TryDeserialize<XmlTestPerson>("<XmlTestPerson>", out var broken));
        Assert.Null(broken);

        Assert.False(XmlHelper.TryDeserialize<XmlTestPerson>("   ", out var blank));
        Assert.Null(blank);
    }

    /// <summary>
    /// TryDeserialize 正常 XML 返回 true
    /// </summary>
    [Fact]
    public void TryDeserialize_WithValidXml_ReturnsTrue()
    {
        var xml = XmlHelper.Serialize(new XmlTestPerson { Name = "曦寒", Age = 18 });

        var succeeded = XmlHelper.TryDeserialize<XmlTestPerson>(xml, out var person);

        Assert.True(succeeded);
        Assert.NotNull(person);
        Assert.Equal("曦寒", person!.Name);
    }
}
