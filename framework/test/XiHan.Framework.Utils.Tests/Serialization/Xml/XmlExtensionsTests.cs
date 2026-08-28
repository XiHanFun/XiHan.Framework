// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Xml;
using System.Xml.Linq;
using XiHan.Framework.Utils.Serialization.Xml;

namespace XiHan.Framework.Utils.Tests.Serialization.Xml;

/// <summary>
/// XmlExtensions 扩展方法测试
/// </summary>
/// <remarks>
/// 安全取值类扩展的核心承诺是"接收者为 null 也不炸"，因此每个都补了 null 接收者用例。
/// </remarks>
public class XmlExtensionsTests
{
    private const string SampleXml = """
        <team title="曦寒框架组"><member id="1">甲</member><member id="2">乙</member></team>
        """;

    /// <summary>
    /// 对象转 XML 与字符串还原对象
    /// </summary>
    [Fact]
    public void ToXml_And_FromXml_RoundTrip()
    {
        var xml = new XmlTestPerson { Name = "曦寒", Age = 18 }.ToXml(XmlSerializeOptions.Compact);

        var restored = xml.FromXml<XmlTestPerson>();

        Assert.Contains("<Name>曦寒</Name>", xml);
        Assert.Equal("曦寒", restored.Name);
        Assert.Equal(18, restored.Age);
    }

    /// <summary>
    /// 字符串合法性判断与错误信息输出
    /// </summary>
    [Fact]
    public void IsValidXml_Extension_ReportsValidity()
    {
        Assert.True(SampleXml.IsValidXml());
        Assert.False("<不闭合".IsValidXml());

        var valid = "<不闭合".IsValidXml(out var errorMessage);
        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    /// <summary>
    /// 格式化与压缩扩展方法产出正确形态
    /// </summary>
    [Fact]
    public void FormatXml_And_CompressXml_Extensions_Work()
    {
        var formatted = "<a><b>1</b></a>".FormatXml();
        var compressed = "<a>\n  <b>1</b>\n</a>".CompressXml();

        Assert.Contains("\n", formatted);
        Assert.Equal("<a><b>1</b></a>", compressed);
    }

    /// <summary>
    /// 节点与属性查询扩展方法与帮助类结果一致
    /// </summary>
    [Fact]
    public void QueryExtensions_ReturnSameResultAsHelper()
    {
        Assert.Equal("甲", SampleXml.QueryNode("/team/member[1]"));
        Assert.Equal(new[] { "甲", "乙" }, SampleXml.QueryNodes("/team/member"));
        Assert.Equal("曦寒框架组", SampleXml.QueryNodeAttribute("/team", "title"));
    }

    /// <summary>
    /// 字符串转扁平化字典与 JSON
    /// </summary>
    [Fact]
    public void ToDictionary_And_ToJson_Extensions_Work()
    {
        var dict = "<root><name>曦寒</name></root>".ToDictionary();
        var json = "<root><name>曦寒</name></root>".ToJson();

        Assert.Equal("曦寒", dict["root.name"]);
        Assert.Contains("root.name", json);
        Assert.Contains("曦寒", json);
    }

    /// <summary>
    /// XmlNode 安全取值在 null 接收者与缺失目标下回落到默认值
    /// </summary>
    [Fact]
    public void XmlNodeExtensions_AreNullSafe()
    {
        var document = new XmlDocument();
        document.LoadXml(SampleXml);
        var root = document.DocumentElement;

        Assert.Equal("曦寒框架组", root.GetAttributeSafe("title", "默认"));
        Assert.Equal("默认", root.GetAttributeSafe("nothing", "默认"));
        Assert.Equal("甲", root.GetChildSafe("member").GetTextSafe("默认"));
        Assert.Equal(new[] { "甲", "乙" }, root.GetChildrenText("member"));

        Assert.Equal("默认", ((XmlNode?)null).GetTextSafe("默认"));
        Assert.Equal("默认", ((XmlNode?)null).GetAttributeSafe("title", "默认"));
        Assert.Null(((XmlNode?)null).GetChildSafe("member"));
        Assert.Empty(((XmlNode?)null).GetChildrenText("member"));
    }

    /// <summary>
    /// XElement 安全取值在 null 接收者与缺失目标下回落到默认值
    /// </summary>
    [Fact]
    public void XElementExtensions_AreNullSafe()
    {
        var element = XElement.Parse(SampleXml);

        Assert.Equal("曦寒框架组", element.GetAttributeSafe("title", "默认"));
        Assert.Equal("默认", element.GetAttributeSafe("nothing", "默认"));
        Assert.Equal("甲", element.GetElementSafe("member").GetValueSafe("默认"));
        Assert.Equal(new[] { "甲", "乙" }, element.GetElementsValue("member"));

        Assert.Equal("默认", ((XElement?)null).GetValueSafe("默认"));
        Assert.Equal("默认", ((XElement?)null).GetAttributeSafe("title", "默认"));
        Assert.Null(((XElement?)null).GetElementSafe("member"));
        Assert.Empty(((XElement?)null).GetElementsValue("member"));
    }

    /// <summary>
    /// 添加子元素时同时写入值与属性，并返回新建的子元素
    /// </summary>
    [Fact]
    public void AddElement_WritesValueAndAttributes_ReturnsChild()
    {
        var parent = new XElement("root");

        var child = parent.AddElement("item", "值", new Dictionary<string, string> { ["id"] = "1" });

        Assert.Equal("值", child.Value);
        Assert.Equal("1", child.GetAttributeSafe("id", "默认"));
        Assert.Single(parent.Elements("item"));
    }

    /// <summary>
    /// 设置属性支持链式调用并返回原元素
    /// </summary>
    [Fact]
    public void SetAttribute_And_SetAttributes_SupportChaining()
    {
        var element = new XElement("root");

        var result = element
            .SetAttribute("a", 1)
            .SetAttributes(new Dictionary<string, string> { ["b"] = "2", ["c"] = "3" });

        Assert.Same(element, result);
        Assert.Equal("1", element.GetAttributeSafe("a", "默认"));
        Assert.Equal("2", element.GetAttributeSafe("b", "默认"));
        Assert.Equal("3", element.GetAttributeSafe("c", "默认"));
    }

    /// <summary>
    /// 设置同名属性会覆盖旧值
    /// </summary>
    [Fact]
    public void SetAttribute_WhenAttributeExists_OverwritesValue()
    {
        var element = new XElement("root");

        element.SetAttribute("a", "旧值").SetAttribute("a", "新值");

        Assert.Equal("新值", element.GetAttributeSafe("a", "默认"));
    }
}
