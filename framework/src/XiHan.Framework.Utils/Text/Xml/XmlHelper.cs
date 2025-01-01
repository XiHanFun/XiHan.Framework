#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XmlHelper
// Guid:8f5fc4c6-1697-4e5b-b426-7832f7502485
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 7:44:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace XiHan.Framework.Utils.Text.Xml;

/// <summary>
/// XmlHelper
/// </summary>
public static class XmlHelper
{
    #region 序列化与反序列化

    /// <summary>
    /// 将对象序列化为 XML 字符串。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <param name="omitXmlDeclaration"></param>
    /// <param name="indent"></param>
    /// <returns></returns>
    public static string Serialize<T>(T obj, bool omitXmlDeclaration = false, bool indent = true)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = omitXmlDeclaration, Indent = indent, Encoding = Encoding.UTF8
        };

        using var stream = new StringWriter();
        using var writer = XmlWriter.Create(stream, settings);
        var serializer = new XmlSerializer(typeof(T));
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add(string.Empty, string.Empty); // 去掉命名空间
        serializer.Serialize(writer, obj, namespaces);
        return stream.ToString();
    }

    /// <summary>
    /// 从 XML 字符串反序列化为对象。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="xml"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static T Deserialize<T>(string xml)
    {
        using var reader = new StringReader(xml);
        var serializer = new XmlSerializer(typeof(T));
        var objectValue = serializer.Deserialize(reader);

        return objectValue is T result ? result : throw new Exception("反序列化失败");
    }

    #endregion 序列化与反序列化

    #region XML 节点操作

    /// <summary>
    /// 查询 XML 节点内容。
    /// </summary>
    /// <param name="xml"></param>
    /// <param name="xpath"></param>
    /// <returns></returns>
    public static string? QueryNode(string xml, string xpath)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var node = doc.SelectSingleNode(xpath);
        return node?.InnerText;
    }

    /// <summary>
    /// 查询 XML 节点集合。
    /// </summary>
    /// <param name="xml"></param>
    /// <param name="xpath"></param>
    /// <returns></returns>
    public static List<string> QueryNodes(string xml, string xpath)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var nodes = doc.SelectNodes(xpath);
        return nodes == null ? [] : nodes.Cast<XmlNode>().Select(n => n.InnerText).ToList();
    }

    /// <summary>
    /// 添加节点。
    /// </summary>
    /// <param name="xml"></param>
    /// <param name="xpath"></param>
    /// <param name="nodeName"></param>
    /// <param name="nodeValue"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string AddNode(string xml, string xpath, string nodeName, string nodeValue)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var parentNode = doc.SelectSingleNode(xpath);
        if (parentNode == null)
        {
            throw new Exception($"找不到节点：{xpath}");
        }

        var newNode = doc.CreateElement(nodeName);
        newNode.InnerText = nodeValue;
        _ = parentNode.AppendChild(newNode);
        return doc.OuterXml;
    }

    /// <summary>
    /// 修改节点值。
    /// </summary>
    /// <param name="xml"></param>
    /// <param name="xpath"></param>
    /// <param name="newValue"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string UpdateNode(string xml, string xpath, string newValue)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var node = doc.SelectSingleNode(xpath);
        if (node == null)
        {
            throw new Exception($"找不到节点：{xpath}");
        }

        node.InnerText = newValue;
        return doc.OuterXml;
    }

    /// <summary>
    /// 删除节点。
    /// </summary>
    /// <param name="xml"></param>
    /// <param name="xpath"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string RemoveNode(string xml, string xpath)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var node = doc.SelectSingleNode(xpath);
        if (node == null)
        {
            throw new Exception($"找不到节点：{xpath}");
        }

        _ = node.ParentNode?.RemoveChild(node);
        return doc.OuterXml;
    }

    #endregion XML 节点操作

    #region 辅助功能

    /// <summary>
    /// 格式化 XML 字符串。
    /// </summary>
    /// <param name="xml"></param>
    /// <returns></returns>
    public static string FormatXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.ToString();
    }

    /// <summary>
    /// 检查 XML 是否有效。
    /// </summary>
    /// <param name="xml"></param>
    /// <returns></returns>
    public static bool IsValidXml(string xml)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion 辅助功能
}
