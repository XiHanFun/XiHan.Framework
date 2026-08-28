// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Xml;
using XiHan.Framework.Utils.Serialization.Xml;

namespace XiHan.Framework.Utils.Tests.Serialization.Xml;

/// <summary>
/// XmlHelper 节点查询与增删改测试
/// </summary>
/// <remarks>
/// 查询类方法吞掉所有异常并返回 null / 空集合，修改类方法则在找不到节点时显式抛出，
/// 两种风格并存是有意为之，这里分别锁定。
/// </remarks>
public class XmlHelperNodeTests
{
    private const string SampleXml = """
        <team title="曦寒框架组"><member id="1"><name>甲</name></member><member id="2"><name>乙</name></member></team>
        """;

    /// <summary>
    /// 按 XPath 查询单个节点文本
    /// </summary>
    [Fact]
    public void QueryNode_WithXPath_ReturnsInnerText()
    {
        Assert.Equal("甲", XmlHelper.QueryNode(SampleXml, "/team/member[1]/name"));
        Assert.Equal("乙", XmlHelper.QueryNode(SampleXml, "/team/member[2]/name"));
    }

    /// <summary>
    /// 节点不存在时返回 null
    /// </summary>
    [Fact]
    public void QueryNode_WhenNodeMissing_ReturnsNull()
    {
        Assert.Null(XmlHelper.QueryNode(SampleXml, "/team/nothing"));
    }

    /// <summary>
    /// XML 非法或参数为空白时返回 null 而不抛异常
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">XPath 表达式</param>
    [Theory]
    [InlineData("<不闭合", "/team")]
    [InlineData("", "/team")]
    [InlineData("   ", "/team")]
    [InlineData("<team/>", "")]
    [InlineData("<team/>", "   ")]
    public void QueryNode_WhenInputInvalid_ReturnsNull(string xml, string xpath)
    {
        Assert.Null(XmlHelper.QueryNode(xml, xpath));
    }

    /// <summary>
    /// 查询节点集合返回全部匹配项的文本
    /// </summary>
    [Fact]
    public void QueryNodes_WithXPath_ReturnsAllInnerTexts()
    {
        Assert.Equal(new[] { "甲", "乙" }, XmlHelper.QueryNodes(SampleXml, "/team/member/name"));
    }

    /// <summary>
    /// XML 非法或 XPath 语法错误时返回空集合
    /// </summary>
    [Fact]
    public void QueryNodes_WhenInputInvalid_ReturnsEmpty()
    {
        Assert.Empty(XmlHelper.QueryNodes("<不闭合", "/team/member"));
        Assert.Empty(XmlHelper.QueryNodes(SampleXml, "///["));
        Assert.Empty(XmlHelper.QueryNodes(SampleXml, "   "));
    }

    /// <summary>
    /// 查询节点属性值
    /// </summary>
    [Fact]
    public void QueryNodeAttribute_ReturnsAttributeValue()
    {
        Assert.Equal("曦寒框架组", XmlHelper.QueryNodeAttribute(SampleXml, "/team", "title"));
        Assert.Equal("2", XmlHelper.QueryNodeAttribute(SampleXml, "/team/member[2]", "id"));
    }

    /// <summary>
    /// 属性不存在或参数为空白时返回 null
    /// </summary>
    [Fact]
    public void QueryNodeAttribute_WhenMissingOrBlank_ReturnsNull()
    {
        Assert.Null(XmlHelper.QueryNodeAttribute(SampleXml, "/team", "nothing"));
        Assert.Null(XmlHelper.QueryNodeAttribute(SampleXml, "/team/nothing", "title"));
        Assert.Null(XmlHelper.QueryNodeAttribute(SampleXml, "/team", "   "));
    }

    /// <summary>
    /// 添加带值与属性的新节点
    /// </summary>
    [Fact]
    public void AddNode_AppendsChildWithValueAndAttributes()
    {
        var updated = XmlHelper.AddNode(
            SampleXml,
            "/team",
            "member",
            "丙",
            new Dictionary<string, string> { ["id"] = "3" });

        Assert.Equal(new[] { "甲", "乙", "丙" }, XmlHelper.QueryNodes(updated, "/team/member"));
        Assert.Equal("3", XmlHelper.QueryNodeAttribute(updated, "/team/member[3]", "id"));
    }

    /// <summary>
    /// 父节点不存在时添加节点抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void AddNode_WhenParentMissing_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            XmlHelper.AddNode(SampleXml, "/nothing", "member");
        });

        Assert.Contains("找不到节点", exception.Message);
    }

    /// <summary>
    /// 修改类方法不吞掉 XML 解析异常
    /// </summary>
    /// <remarks>
    /// 与查询类方法的"静默返回 null"不同，修改类方法遇到非法 XML 直接抛 XmlException，
    /// 调用方必须自行兜底，这里把该差异固定下来。
    /// </remarks>
    [Fact]
    public void AddNode_WhenXmlMalformed_ThrowsXmlException()
    {
        Assert.Throws<XmlException>(() =>
        {
            XmlHelper.AddNode("<不闭合", "/team", "member");
        });
    }

    /// <summary>
    /// 更新节点文本
    /// </summary>
    [Fact]
    public void UpdateNode_ReplacesInnerText()
    {
        var updated = XmlHelper.UpdateNode(SampleXml, "/team/member[1]/name", "丁");

        Assert.Equal("丁", XmlHelper.QueryNode(updated, "/team/member[1]/name"));
        Assert.Equal("乙", XmlHelper.QueryNode(updated, "/team/member[2]/name"));
    }

    /// <summary>
    /// 更新不存在的节点抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void UpdateNode_WhenNodeMissing_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            XmlHelper.UpdateNode(SampleXml, "/team/nothing", "值");
        });
    }

    /// <summary>
    /// 更新已存在的属性并新增不存在的属性
    /// </summary>
    [Fact]
    public void UpdateNodeAttribute_UpdatesExistingAndAddsMissing()
    {
        var updated = XmlHelper.UpdateNodeAttribute(SampleXml, "/team", "title", "新名称");
        updated = XmlHelper.UpdateNodeAttribute(updated, "/team", "owner", "曦寒");

        Assert.Equal("新名称", XmlHelper.QueryNodeAttribute(updated, "/team", "title"));
        Assert.Equal("曦寒", XmlHelper.QueryNodeAttribute(updated, "/team", "owner"));
    }

    /// <summary>
    /// 更新不存在节点的属性抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void UpdateNodeAttribute_WhenNodeMissing_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            XmlHelper.UpdateNodeAttribute(SampleXml, "/nothing", "title", "值");
        });
    }

    /// <summary>
    /// 删除节点后剩余节点保持不变
    /// </summary>
    [Fact]
    public void RemoveNode_RemovesTargetOnly()
    {
        var updated = XmlHelper.RemoveNode(SampleXml, "/team/member[1]");

        Assert.Equal(new[] { "乙" }, XmlHelper.QueryNodes(updated, "/team/member"));
    }

    /// <summary>
    /// 删除不存在的节点抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void RemoveNode_WhenNodeMissing_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            XmlHelper.RemoveNode(SampleXml, "/team/nothing");
        });
    }

    /// <summary>
    /// 删除节点属性
    /// </summary>
    [Fact]
    public void RemoveNodeAttribute_RemovesAttribute()
    {
        var updated = XmlHelper.RemoveNodeAttribute(SampleXml, "/team", "title");

        Assert.Null(XmlHelper.QueryNodeAttribute(updated, "/team", "title"));
        Assert.Equal(new[] { "甲", "乙" }, XmlHelper.QueryNodes(updated, "/team/member"));
    }

    /// <summary>
    /// 删除不存在节点的属性抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void RemoveNodeAttribute_WhenNodeMissing_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            XmlHelper.RemoveNodeAttribute(SampleXml, "/nothing", "title");
        });
    }

    /// <summary>
    /// 命名空间管理器可用于带前缀的 XPath 查询
    /// </summary>
    [Fact]
    public void CreateNamespaceManager_EnablesPrefixedXPathQuery()
    {
        const string Xml = """<root xmlns:a="urn:xihan:test"><a:value>内容</a:value></root>""";

        var namespaceManager = XmlHelper.CreateNamespaceManager(
            Xml,
            new Dictionary<string, string> { ["a"] = "urn:xihan:test" });

        Assert.Equal("urn:xihan:test", namespaceManager.LookupNamespace("a"));
        Assert.Equal("内容", XmlHelper.QueryNode(Xml, "/root/a:value", namespaceManager));
        // 不带命名空间管理器时前缀无法解析，按契约返回 null
        Assert.Null(XmlHelper.QueryNode(Xml, "/root/a:value"));
    }

    /// <summary>
    /// 未提供映射时命名空间管理器可正常创建但没有额外前缀
    /// </summary>
    [Fact]
    public void CreateNamespaceManager_WithoutMappings_ReturnsEmptyManager()
    {
        var namespaceManager = XmlHelper.CreateNamespaceManager(SampleXml);

        Assert.NotNull(namespaceManager);
        Assert.Null(namespaceManager.LookupNamespace("a"));
    }
}
