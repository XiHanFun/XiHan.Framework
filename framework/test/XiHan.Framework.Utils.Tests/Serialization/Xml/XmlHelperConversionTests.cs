// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Serialization.Xml;

namespace XiHan.Framework.Utils.Tests.Serialization.Xml;

/// <summary>
/// XmlHelper 校验、格式化与格式互转测试
/// </summary>
public class XmlHelperConversionTests
{
    private const string AgeSchema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="age" type="xs:int" />
        </xs:schema>
        """;

    /// <summary>
    /// 合法 XML 校验通过
    /// </summary>
    [Fact]
    public void IsValidXml_WithValidXml_ReturnsTrue()
    {
        Assert.True(XmlHelper.IsValidXml("<a><b>1</b></a>"));
        Assert.True(XmlHelper.IsValidXml("<a/>"));
    }

    /// <summary>
    /// 非法 XML 校验失败并给出错误信息
    /// </summary>
    [Fact]
    public void IsValidXml_WithInvalidXml_ReturnsFalseWithMessage()
    {
        var valid = XmlHelper.IsValidXml("<a><b></a>", out var errorMessage);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    /// <summary>
    /// 空白字符串校验失败并给出专用错误信息
    /// </summary>
    [Fact]
    public void IsValidXml_WhenBlank_ReturnsFalseWithBlankMessage()
    {
        var valid = XmlHelper.IsValidXml("   ", out var errorMessage);

        Assert.False(valid);
        Assert.Equal("XML 字符串为空", errorMessage);
    }

    /// <summary>
    /// 格式化输出带缩进并保留内容
    /// </summary>
    [Fact]
    public void FormatXml_IndentsWithoutChangingContent()
    {
        var formatted = XmlHelper.FormatXml("<a><b>内容</b></a>");

        Assert.Contains("\n", formatted);
        Assert.Contains("<b>内容</b>", formatted);
    }

    /// <summary>
    /// 支持自定义缩进字符
    /// </summary>
    [Fact]
    public void FormatXml_WithCustomIndentChars_UsesThem()
    {
        var formatted = XmlHelper.FormatXml("<a><b>1</b></a>", true, "\t");

        Assert.Contains("\t<b>1</b>", formatted);
    }

    /// <summary>
    /// XML 非法时格式化原样返回
    /// </summary>
    [Fact]
    public void FormatXml_WhenInvalid_ReturnsOriginal()
    {
        const string Source = "<不闭合";

        Assert.Equal(Source, XmlHelper.FormatXml(Source));
    }

    /// <summary>
    /// 压缩移除缩进与声明
    /// </summary>
    [Fact]
    public void CompressXml_RemovesWhitespaceAndDeclaration()
    {
        var compressed = XmlHelper.CompressXml("<a>\n  <b>1</b>\n</a>");

        Assert.Equal("<a><b>1</b></a>", compressed);
    }

    /// <summary>
    /// XML 非法时压缩原样返回
    /// </summary>
    [Fact]
    public void CompressXml_WhenInvalid_ReturnsOriginal()
    {
        const string Source = "<不闭合";

        Assert.Equal(Source, XmlHelper.CompressXml(Source));
    }

    /// <summary>
    /// 转字典时元素按层级扁平化，属性用 @ 标记
    /// </summary>
    [Fact]
    public void XmlToDictionary_FlattensElementsAndAttributes()
    {
        var dict = XmlHelper.XmlToDictionary("<root><item id=\"1\"><name>曦寒</name></item></root>");

        Assert.Equal("1", dict["root.item@id"]);
        Assert.Equal("曦寒", dict["root.item.name"]);
    }

    /// <summary>
    /// 支持自定义层级分隔符
    /// </summary>
    [Fact]
    public void XmlToDictionary_WithCustomSeparator_UsesIt()
    {
        var dict = XmlHelper.XmlToDictionary("<root><name>曦寒</name></root>", "/");

        Assert.Equal("曦寒", dict["root/name"]);
    }

    /// <summary>
    /// XML 非法时转字典返回空字典
    /// </summary>
    [Fact]
    public void XmlToDictionary_WhenInvalid_ReturnsEmpty()
    {
        Assert.Empty(XmlHelper.XmlToDictionary("<不闭合"));
    }

    /// <summary>
    /// XML 转 JSON 使用扁平化键，中文不转义
    /// </summary>
    [Fact]
    public void XmlToJson_ProducesFlattenedJsonWithChinese()
    {
        var json = XmlHelper.XmlToJson("<root><name>曦寒</name></root>");

        Assert.Contains("root.name", json);
        Assert.Contains("曦寒", json);
        Assert.DoesNotContain("\\u", json);
    }

    /// <summary>
    /// XML 非法时转 JSON 返回空对象
    /// </summary>
    [Fact]
    public void XmlToJson_WhenInvalid_ReturnsEmptyObject()
    {
        Assert.Equal("{}", XmlHelper.XmlToJson("<不闭合"));
    }

    /// <summary>
    /// JSON 转 XML 生成对应元素
    /// </summary>
    [Fact]
    public void JsonToXml_BuildsElementsForEveryProperty()
    {
        var xml = XmlHelper.JsonToXml("{\"name\":\"曦寒\",\"age\":18,\"ok\":true}");

        Assert.Contains("<name>曦寒</name>", xml);
        Assert.Contains("<age>18</age>", xml);
        Assert.Contains("<ok>true</ok>", xml);
    }

    /// <summary>
    /// JSON 嵌套对象与数组分别转为嵌套元素与重复元素
    /// </summary>
    [Fact]
    public void JsonToXml_HandlesNestedObjectsAndArrays()
    {
        var xml = XmlHelper.JsonToXml("{\"server\":{\"host\":\"localhost\"},\"tags\":[\"甲\",\"乙\"]}");

        Assert.Contains("<host>localhost</host>", xml);
        Assert.Contains("<tags>甲</tags>", xml);
        Assert.Contains("<tags>乙</tags>", xml);
    }

    /// <summary>
    /// 使用自定义根元素名
    /// </summary>
    [Fact]
    public void JsonToXml_WithCustomRootName_UsesIt()
    {
        var xml = XmlHelper.JsonToXml("{\"name\":\"曦寒\"}", "config");

        Assert.Contains("<config>", xml);
        Assert.Contains("</config>", xml);
    }

    /// <summary>
    /// 非法 XML 元素名被清洗为合法名称
    /// </summary>
    /// <remarks>
    /// 空格转下划线、非法字符丢弃、数字开头补前导下划线，否则生成的 XML 会直接不可解析。
    /// </remarks>
    [Fact]
    public void JsonToXml_SanitizesInvalidElementNames()
    {
        var xml = XmlHelper.JsonToXml("{\"my key\":\"a\",\"123\":\"b\"}");

        Assert.Contains("<my_key>a</my_key>", xml);
        Assert.Contains("<_123>b</_123>", xml);
        Assert.True(XmlHelper.IsValidXml(xml));
    }

    /// <summary>
    /// JSON 非法或为空对象时返回空的根元素
    /// </summary>
    /// <param name="json">输入 JSON</param>
    [Theory]
    [InlineData("不是 JSON")]
    [InlineData("{}")]
    public void JsonToXml_WhenInvalidOrEmpty_ReturnsEmptyRoot(string json)
    {
        Assert.Equal("<root></root>", XmlHelper.JsonToXml(json));
    }

    /// <summary>
    /// 符合 XSD 的文档校验通过且无错误
    /// </summary>
    [Fact]
    public void ValidateXmlWithXsd_WhenDocumentMatchesSchema_ReturnsTrue()
    {
        var valid = XmlHelper.ValidateXmlWithXsd("<age>18</age>", AgeSchema, out var errors);

        Assert.True(valid);
        Assert.Empty(errors);
    }

    /// <summary>
    /// 数据类型不符时校验失败并给出错误明细
    /// </summary>
    [Fact]
    public void ValidateXmlWithXsd_WhenDataTypeMismatch_ReturnsFalseWithErrors()
    {
        var valid = XmlHelper.ValidateXmlWithXsd("<age>不是数字</age>", AgeSchema, out var errors);

        Assert.False(valid);
        Assert.NotEmpty(errors);
    }

    /// <summary>
    /// XSD 本身非法时校验失败并记录异常信息
    /// </summary>
    [Fact]
    public void ValidateXmlWithXsd_WhenSchemaInvalid_ReturnsFalseWithExceptionInfo()
    {
        var valid = XmlHelper.ValidateXmlWithXsd("<age>18</age>", "不是架构", out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("验证异常"));
    }

    /// <summary>
    /// 符合内部 DTD 的文档校验通过
    /// </summary>
    [Fact]
    public void ValidateXmlWithDtd_WhenDocumentMatchesDtd_ReturnsTrue()
    {
        const string Xml = """
            <!DOCTYPE note [<!ELEMENT note (#PCDATA)>]>
            <note>内容</note>
            """;

        var valid = XmlHelper.ValidateXmlWithDtd(Xml, out var errors);

        Assert.True(valid);
        Assert.Empty(errors);
    }

    /// <summary>
    /// 违反 DTD 内容模型时校验失败
    /// </summary>
    [Fact]
    public void ValidateXmlWithDtd_WhenContentViolatesDtd_ReturnsFalse()
    {
        const string Xml = """
            <!DOCTYPE note [<!ELEMENT note EMPTY>]>
            <note>内容</note>
            """;

        var valid = XmlHelper.ValidateXmlWithDtd(Xml, out var errors);

        Assert.False(valid);
        Assert.NotEmpty(errors);
    }

    /// <summary>
    /// XML 结构损坏时 DTD 校验失败并记录异常信息
    /// </summary>
    [Fact]
    public void ValidateXmlWithDtd_WhenXmlMalformed_ReturnsFalseWithExceptionInfo()
    {
        var valid = XmlHelper.ValidateXmlWithDtd("<note>", out var errors);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("验证异常"));
    }
}
