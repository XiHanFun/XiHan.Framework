#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XmlExtensions
// Guid:0e5b0a99-5644-45b1-95fa-9c89e083edba
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 8:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Xml;
using System.Xml.Linq;

namespace XiHan.Framework.Utils.Serialization.Xml;

/// <summary>
/// XML 扩展方法
/// </summary>
public static class XmlExtensions
{
    #region 对象扩展

    /// <summary>
    /// 将对象转换为 XML 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要转换的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>XML 字符串</returns>
    public static string ToXml<T>(this T obj, XmlSerializationOptions? options = null)
    {
        return XmlHelper.Serialize(obj, options);
    }

    /// <summary>
    /// 异步将对象转换为 XML 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要转换的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>XML 字符串</returns>
    public static async Task<string> ToXmlAsync<T>(this T obj, XmlSerializationOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await XmlHelper.SerializeAsync(obj, options, cancellationToken);
    }

    #endregion 对象扩展

    #region 字符串扩展

    /// <summary>
    /// 从 XML 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="xml">XML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    public static T FromXml<T>(this string xml, XmlDeserializationOptions? options = null)
    {
        return XmlHelper.Deserialize<T>(xml, options);
    }

    /// <summary>
    /// 异步从 XML 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="xml">XML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public static async Task<T> FromXmlAsync<T>(this string xml, XmlDeserializationOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await XmlHelper.DeserializeAsync<T>(xml, options, cancellationToken);
    }

    /// <summary>
    /// 检查字符串是否为有效的 XML
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <returns>是否有效</returns>
    public static bool IsValidXml(this string xml)
    {
        return XmlHelper.IsValidXml(xml);
    }

    /// <summary>
    /// 检查字符串是否为有效的 XML
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="errorMessage">错误信息</param>
    /// <returns>是否有效</returns>
    public static bool IsValidXml(this string xml, out string? errorMessage)
    {
        return XmlHelper.IsValidXml(xml, out errorMessage);
    }

    /// <summary>
    /// 格式化 XML 字符串
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="indent">是否缩进</param>
    /// <param name="indentChars">缩进字符</param>
    /// <returns>格式化后的 XML 字符串</returns>
    public static string FormatXml(this string xml, bool indent = true, string indentChars = "  ")
    {
        return XmlHelper.FormatXml(xml, indent, indentChars);
    }

    /// <summary>
    /// 压缩 XML 字符串
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <returns>压缩后的 XML 字符串</returns>
    public static string CompressXml(this string xml)
    {
        return XmlHelper.CompressXml(xml);
    }

    /// <summary>
    /// 查询 XML 节点内容
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">XPath 表达式</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>节点内容</returns>
    public static string? QueryNode(this string xml, string xpath, XmlNamespaceManager? namespaceManager = null)
    {
        return XmlHelper.QueryNode(xml, xpath, namespaceManager);
    }

    /// <summary>
    /// 查询 XML 节点集合
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">XPath 表达式</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>节点内容列表</returns>
    public static List<string> QueryNodes(this string xml, string xpath, XmlNamespaceManager? namespaceManager = null)
    {
        return XmlHelper.QueryNodes(xml, xpath, namespaceManager);
    }

    /// <summary>
    /// 查询 XML 节点属性
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">XPath 表达式</param>
    /// <param name="attributeName">属性名</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>属性值</returns>
    public static string? QueryNodeAttribute(this string xml, string xpath, string attributeName, XmlNamespaceManager? namespaceManager = null)
    {
        return XmlHelper.QueryNodeAttribute(xml, xpath, attributeName, namespaceManager);
    }

    /// <summary>
    /// 转换 XML 为字典
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="separator">层级分隔符</param>
    /// <returns>扁平化的键值对字典</returns>
    public static Dictionary<string, string> ToDictionary(this string xml, string separator = ".")
    {
        return XmlHelper.XmlToDictionary(xml, separator);
    }

    /// <summary>
    /// XML 转 JSON 字符串
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="indent">是否格式化 JSON</param>
    /// <returns>JSON 字符串</returns>
    public static string ToJson(this string xml, bool indent = false)
    {
        return XmlHelper.XmlToJson(xml, indent);
    }

    #endregion 字符串扩展

    #region XmlDocument 扩展

    /// <summary>
    /// 安全获取节点文本内容
    /// </summary>
    /// <param name="node">XML 节点</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>节点文本内容或默认值</returns>
    public static string GetTextSafe(this XmlNode? node, string defaultValue = "")
    {
        return node?.InnerText ?? defaultValue;
    }

    /// <summary>
    /// 安全获取属性值
    /// </summary>
    /// <param name="node">XML 节点</param>
    /// <param name="attributeName">属性名</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>属性值或默认值</returns>
    public static string GetAttributeSafe(this XmlNode? node, string attributeName, string defaultValue = "")
    {
        return node?.Attributes?[attributeName]?.Value ?? defaultValue;
    }

    /// <summary>
    /// 安全获取子节点
    /// </summary>
    /// <param name="node">XML 节点</param>
    /// <param name="childName">子节点名称</param>
    /// <returns>子节点或 null</returns>
    public static XmlNode? GetChildSafe(this XmlNode? node, string childName)
    {
        return node?.SelectSingleNode(childName);
    }

    /// <summary>
    /// 获取所有子节点的文本内容
    /// </summary>
    /// <param name="node">XML 节点</param>
    /// <param name="childName">子节点名称</param>
    /// <returns>子节点文本内容列表</returns>
    public static List<string> GetChildrenText(this XmlNode? node, string childName)
    {
        if (node == null)
        {
            return [];
        }

        var nodes = node.SelectNodes(childName);
        return nodes?.Cast<XmlNode>().Select(n => n.InnerText).ToList() ?? [];
    }

    #endregion XmlDocument 扩展

    #region XElement 扩展

    /// <summary>
    /// 安全获取元素值
    /// </summary>
    /// <param name="element">XML 元素</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>元素值或默认值</returns>
    public static string GetValueSafe(this XElement? element, string defaultValue = "")
    {
        return element?.Value ?? defaultValue;
    }

    /// <summary>
    /// 安全获取属性值
    /// </summary>
    /// <param name="element">XML 元素</param>
    /// <param name="attributeName">属性名</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>属性值或默认值</returns>
    public static string GetAttributeSafe(this XElement? element, string attributeName, string defaultValue = "")
    {
        return element?.Attribute(attributeName)?.Value ?? defaultValue;
    }

    /// <summary>
    /// 安全获取子元素
    /// </summary>
    /// <param name="element">XML 元素</param>
    /// <param name="childName">子元素名称</param>
    /// <returns>子元素或 null</returns>
    public static XElement? GetElementSafe(this XElement? element, string childName)
    {
        return element?.Element(childName);
    }

    /// <summary>
    /// 获取所有子元素的值
    /// </summary>
    /// <param name="element">XML 元素</param>
    /// <param name="childName">子元素名称</param>
    /// <returns>子元素值列表</returns>
    public static List<string> GetElementsValue(this XElement? element, string childName)
    {
        return element == null ? [] : [.. element.Elements(childName).Select(e => e.Value)];
    }

    /// <summary>
    /// 添加子元素
    /// </summary>
    /// <param name="element">父元素</param>
    /// <param name="name">子元素名称</param>
    /// <param name="value">子元素值</param>
    /// <param name="attributes">属性字典</param>
    /// <returns>新创建的子元素</returns>
    public static XElement AddElement(this XElement element, string name, object? value = null, Dictionary<string, string>? attributes = null)
    {
        var child = new XElement(name);

        if (value != null)
        {
            child.Value = value.ToString() ?? string.Empty;
        }

        if (attributes?.Count > 0)
        {
            foreach (var attr in attributes)
            {
                child.SetAttributeValue(attr.Key, attr.Value);
            }
        }

        element.Add(child);
        return child;
    }

    /// <summary>
    /// 设置或更新属性值
    /// </summary>
    /// <param name="element">XML 元素</param>
    /// <param name="name">属性名</param>
    /// <param name="value">属性值</param>
    /// <returns>XML 元素（支持链式调用）</returns>
    public static XElement SetAttribute(this XElement element, string name, object? value)
    {
        element.SetAttributeValue(name, value);
        return element;
    }

    /// <summary>
    /// 批量设置属性
    /// </summary>
    /// <param name="element">XML 元素</param>
    /// <param name="attributes">属性字典</param>
    /// <returns>XML 元素（支持链式调用）</returns>
    public static XElement SetAttributes(this XElement element, Dictionary<string, string> attributes)
    {
        foreach (var attr in attributes)
        {
            element.SetAttributeValue(attr.Key, attr.Value);
        }
        return element;
    }

    #endregion XElement 扩展
}
