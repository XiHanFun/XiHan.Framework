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
using System.Xml.Schema;
using System.Xml.Serialization;

namespace XiHan.Framework.Utils.Serialization.Xml;

/// <summary>
/// XML 操作帮助类
/// 提供 XML 序列化、反序列化、节点操作、验证等功能
/// </summary>
public static class XmlHelper
{
    #region 序列化与反序列化

    /// <summary>
    /// 将对象序列化为 XML 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>XML 字符串</returns>
    /// <exception cref="ArgumentNullException">当对象为空时抛出</exception>
    /// <exception cref="InvalidOperationException">当序列化失败时抛出</exception>
    public static string Serialize<T>(T obj, XmlSerializeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(obj);

        options ??= new XmlSerializeOptions();

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = options.OmitXmlDeclaration,
            Indent = options.Indent,
            IndentChars = options.IndentChars,
            Encoding = options.Encoding,
            NewLineChars = options.NewLineChars,
            CheckCharacters = options.CheckCharacters
        };

        using var stream = new StringWriter();
        using var writer = XmlWriter.Create(stream, settings);

        var serializer = new XmlSerializer(typeof(T));
        var namespaces = new XmlSerializerNamespaces();

        if (options.OmitNamespaces)
        {
            namespaces.Add(string.Empty, string.Empty);
        }
        else if (options.CustomNamespaces?.Count > 0)
        {
            foreach (var ns in options.CustomNamespaces)
            {
                namespaces.Add(ns.Key, ns.Value);
            }
        }

        serializer.Serialize(writer, obj, namespaces);
        return stream.ToString();
    }

    /// <summary>
    /// 异步将对象序列化为 XML 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>XML 字符串</returns>
    public static async Task<string> SerializeAsync<T>(T obj, XmlSerializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Serialize(obj, options), cancellationToken);
    }

    /// <summary>
    /// 从 XML 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="xml">XML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    /// <exception cref="ArgumentException">当 XML 字符串为空时抛出</exception>
    /// <exception cref="InvalidOperationException">当反序列化失败时抛出</exception>
    public static T Deserialize<T>(string xml, XmlDeserializeOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException("XML 字符串不能为空", nameof(xml));
        }

        options ??= new XmlDeserializeOptions();

        try
        {
            using var reader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(reader, new XmlReaderSettings
            {
                IgnoreWhitespace = options.IgnoreWhitespace,
                IgnoreComments = options.IgnoreComments,
                CheckCharacters = options.CheckCharacters,
                CloseInput = true
            });

            var serializer = new XmlSerializer(typeof(T));
            var result = serializer.Deserialize(xmlReader);

            return result is T typedResult ? typedResult : throw new InvalidOperationException("反序列化失败：类型不匹配");
        }
        catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
        {
            throw new InvalidOperationException($"反序列化失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 异步从 XML 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="xml">XML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public static async Task<T> DeserializeAsync<T>(string xml, XmlDeserializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Deserialize<T>(xml, options), cancellationToken);
    }

    /// <summary>
    /// 从文件反序列化对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    public static T DeserializeFromFile<T>(string filePath, XmlDeserializeOptions? options = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在：{filePath}");
        }

        var xml = File.ReadAllText(filePath, Encoding.UTF8);
        return Deserialize<T>(xml, options);
    }

    /// <summary>
    /// 异步从文件反序列化对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public static async Task<T> DeserializeFromFileAsync<T>(string filePath, XmlDeserializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在：{filePath}");
        }

        var xml = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return await DeserializeAsync<T>(xml, options, cancellationToken);
    }

    /// <summary>
    /// 将对象序列化并保存到文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    public static void SerializeToFile<T>(T obj, string filePath, XmlSerializeOptions? options = null)
    {
        var xml = Serialize(obj, options);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, xml, Encoding.UTF8);
    }

    /// <summary>
    /// 异步将对象序列化并保存到文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task SerializeToFileAsync<T>(T obj, string filePath, XmlSerializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        var xml = await SerializeAsync(obj, options, cancellationToken);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(filePath, xml, Encoding.UTF8, cancellationToken);
    }

    #endregion 序列化与反序列化

    #region XML 节点操作

    /// <summary>
    /// 查询 XML 节点内容
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">XPath 表达式</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>节点内容，如果未找到则返回 null</returns>
    public static string? QueryNode(string xml, string xpath, XmlNamespaceManager? namespaceManager = null)
    {
        if (string.IsNullOrWhiteSpace(xml) || string.IsNullOrWhiteSpace(xpath))
        {
            return null;
        }

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var node = namespaceManager != null
                ? doc.SelectSingleNode(xpath, namespaceManager)
                : doc.SelectSingleNode(xpath);
            return node?.InnerText;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 查询 XML 节点集合
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">XPath 表达式</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>节点内容列表</returns>
    public static List<string> QueryNodes(string xml, string xpath, XmlNamespaceManager? namespaceManager = null)
    {
        if (string.IsNullOrWhiteSpace(xml) || string.IsNullOrWhiteSpace(xpath))
        {
            return [];
        }

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var nodes = namespaceManager != null
                ? doc.SelectNodes(xpath, namespaceManager)
                : doc.SelectNodes(xpath);
            return nodes?.Cast<XmlNode>().Select(n => n.InnerText).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 查询 XML 节点属性
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">XPath 表达式</param>
    /// <param name="attributeName">属性名</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>属性值，如果未找到则返回 null</returns>
    public static string? QueryNodeAttribute(string xml, string xpath, string attributeName, XmlNamespaceManager? namespaceManager = null)
    {
        if (string.IsNullOrWhiteSpace(xml) || string.IsNullOrWhiteSpace(xpath) || string.IsNullOrWhiteSpace(attributeName))
        {
            return null;
        }

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var node = namespaceManager != null
                ? doc.SelectSingleNode(xpath, namespaceManager)
                : doc.SelectSingleNode(xpath);
            return node?.Attributes?[attributeName]?.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 添加节点
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">父节点 XPath 表达式</param>
    /// <param name="nodeName">新节点名称</param>
    /// <param name="nodeValue">新节点值</param>
    /// <param name="attributes">节点属性</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>修改后的 XML 字符串</returns>
    /// <exception cref="InvalidOperationException">当找不到父节点时抛出</exception>
    public static string AddNode(string xml, string xpath, string nodeName, string? nodeValue = null,
        Dictionary<string, string>? attributes = null, XmlNamespaceManager? namespaceManager = null)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var parentNode = (namespaceManager != null
            ? doc.SelectSingleNode(xpath, namespaceManager)
            : doc.SelectSingleNode(xpath)) ?? throw new InvalidOperationException($"找不到节点：{xpath}");
        var newNode = doc.CreateElement(nodeName);

        if (!string.IsNullOrEmpty(nodeValue))
        {
            newNode.InnerText = nodeValue;
        }

        if (attributes?.Count > 0)
        {
            foreach (var attr in attributes)
            {
                var attribute = doc.CreateAttribute(attr.Key);
                attribute.Value = attr.Value;
                newNode.Attributes.Append(attribute);
            }
        }

        parentNode.AppendChild(newNode);
        return doc.OuterXml;
    }

    /// <summary>
    /// 修改节点值
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">节点 XPath 表达式</param>
    /// <param name="newValue">新值</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>修改后的 XML 字符串</returns>
    /// <exception cref="InvalidOperationException">当找不到节点时抛出</exception>
    public static string UpdateNode(string xml, string xpath, string newValue, XmlNamespaceManager? namespaceManager = null)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var node = (namespaceManager != null
            ? doc.SelectSingleNode(xpath, namespaceManager)
            : doc.SelectSingleNode(xpath)) ?? throw new InvalidOperationException($"找不到节点：{xpath}");
        node.InnerText = newValue;
        return doc.OuterXml;
    }

    /// <summary>
    /// 修改节点属性
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">节点 XPath 表达式</param>
    /// <param name="attributeName">属性名</param>
    /// <param name="attributeValue">属性值</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>修改后的 XML 字符串</returns>
    /// <exception cref="InvalidOperationException">当找不到节点时抛出</exception>
    public static string UpdateNodeAttribute(string xml, string xpath, string attributeName, string attributeValue, XmlNamespaceManager? namespaceManager = null)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var node = (namespaceManager != null
            ? doc.SelectSingleNode(xpath, namespaceManager)
            : doc.SelectSingleNode(xpath)) ?? throw new InvalidOperationException($"找不到节点：{xpath}");
        if (node.Attributes == null)
        {
            // 如果节点没有属性集合，创建一个
            var attr = doc.CreateAttribute(attributeName);
            attr.Value = attributeValue;
            node.Attributes?.Append(attr);
        }
        else
        {
            var existingAttr = node.Attributes[attributeName];
            if (existingAttr != null)
            {
                existingAttr.Value = attributeValue;
            }
            else
            {
                var attr = doc.CreateAttribute(attributeName);
                attr.Value = attributeValue;
                node.Attributes.Append(attr);
            }
        }

        return doc.OuterXml;
    }

    /// <summary>
    /// 删除节点
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">节点 XPath 表达式</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>修改后的 XML 字符串</returns>
    /// <exception cref="InvalidOperationException">当找不到节点时抛出</exception>
    public static string RemoveNode(string xml, string xpath, XmlNamespaceManager? namespaceManager = null)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var node = (namespaceManager != null
            ? doc.SelectSingleNode(xpath, namespaceManager)
            : doc.SelectSingleNode(xpath)) ?? throw new InvalidOperationException($"找不到节点：{xpath}");
        node.ParentNode?.RemoveChild(node);
        return doc.OuterXml;
    }

    /// <summary>
    /// 删除节点属性
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xpath">节点 XPath 表达式</param>
    /// <param name="attributeName">属性名</param>
    /// <param name="namespaceManager">命名空间管理器</param>
    /// <returns>修改后的 XML 字符串</returns>
    /// <exception cref="InvalidOperationException">当找不到节点时抛出</exception>
    public static string RemoveNodeAttribute(string xml, string xpath, string attributeName, XmlNamespaceManager? namespaceManager = null)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var node = (namespaceManager != null
            ? doc.SelectSingleNode(xpath, namespaceManager)
            : doc.SelectSingleNode(xpath)) ?? throw new InvalidOperationException($"找不到节点：{xpath}");
        node.Attributes?.RemoveNamedItem(attributeName);
        return doc.OuterXml;
    }

    #endregion XML 节点操作

    #region XML 验证

    /// <summary>
    /// 验证 XML 是否符合 XSD 架构
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="xsdSchema">XSD 架构字符串</param>
    /// <param name="validationErrors">验证错误信息</param>
    /// <returns>是否验证通过</returns>
    public static bool ValidateXmlWithXsd(string xml, string xsdSchema, out List<string> validationErrors)
    {
        var errors = new List<string>();
        validationErrors = errors;

        try
        {
            var schemas = new XmlSchemaSet();
            using var schemaReader = new StringReader(xsdSchema);
            using var xmlSchemaReader = XmlReader.Create(schemaReader);
            schemas.Add(null, xmlSchemaReader);

            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemas
            };

            settings.ValidationEventHandler += (sender, e) =>
            {
                errors.Add($"{e.Severity}: {e.Message}");
            };

            using var xmlReader = new StringReader(xml);
            using var validatingReader = XmlReader.Create(xmlReader, settings);

            while (validatingReader.Read()) { }

            return errors.Count == 0;
        }
        catch (Exception ex)
        {
            errors.Add($"验证异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 验证 XML 是否符合 DTD
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="validationErrors">验证错误信息</param>
    /// <returns>是否验证通过</returns>
    public static bool ValidateXmlWithDtd(string xml, out List<string> validationErrors)
    {
        var errors = new List<string>();
        validationErrors = errors;

        try
        {
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.DTD,
                DtdProcessing = DtdProcessing.Parse
            };

            settings.ValidationEventHandler += (sender, e) =>
            {
                errors.Add($"{e.Severity}: {e.Message}");
            };

            using var xmlReader = new StringReader(xml);
            using var validatingReader = XmlReader.Create(xmlReader, settings);

            while (validatingReader.Read()) { }

            return errors.Count == 0;
        }
        catch (Exception ex)
        {
            errors.Add($"验证异常: {ex.Message}");
            return false;
        }
    }

    #endregion XML 验证

    #region 辅助功能

    /// <summary>
    /// 格式化 XML 字符串
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="indent">是否缩进</param>
    /// <param name="indentChars">缩进字符</param>
    /// <returns>格式化后的 XML 字符串</returns>
    public static string FormatXml(string xml, bool indent = true, string indentChars = "  ")
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var settings = new XmlWriterSettings
            {
                Indent = indent,
                IndentChars = indentChars,
                OmitXmlDeclaration = false
            };

            using var writer = new StringWriter();
            using var xmlWriter = XmlWriter.Create(writer, settings);
            doc.Save(xmlWriter);
            return writer.ToString();
        }
        catch
        {
            return xml; // 如果格式化失败，返回原始字符串
        }
    }

    /// <summary>
    /// 压缩 XML 字符串（移除空白字符）
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <returns>压缩后的 XML 字符串</returns>
    public static string CompressXml(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var settings = new XmlWriterSettings
            {
                Indent = false,
                OmitXmlDeclaration = true
            };

            using var writer = new StringWriter();
            using var xmlWriter = XmlWriter.Create(writer, settings);
            doc.Save(xmlWriter);
            return writer.ToString();
        }
        catch
        {
            return xml;
        }
    }

    /// <summary>
    /// 检查 XML 是否有效
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="errorMessage">错误信息</param>
    /// <returns>是否有效</returns>
    public static bool IsValidXml(string xml, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(xml))
        {
            errorMessage = "XML 字符串为空";
            return false;
        }

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 检查 XML 是否有效（简化版本）
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <returns>是否有效</returns>
    public static bool IsValidXml(string xml)
    {
        return IsValidXml(xml, out _);
    }

    /// <summary>
    /// 转换 XML 为字典（扁平化）
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="separator">层级分隔符</param>
    /// <returns>扁平化的键值对字典</returns>
    public static Dictionary<string, string> XmlToDictionary(string xml, string separator = ".")
    {
        var result = new Dictionary<string, string>();

        try
        {
            var doc = XDocument.Parse(xml);
            FlattenXmlElement(doc.Root, string.Empty, result, separator);
        }
        catch
        {
            // 解析失败时返回空字典
        }

        return result;
    }

    /// <summary>
    /// 创建命名空间管理器
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="namespaces">命名空间映射</param>
    /// <returns>命名空间管理器</returns>
    public static XmlNamespaceManager CreateNamespaceManager(string xml, Dictionary<string, string>? namespaces = null)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var nsManager = new XmlNamespaceManager(doc.NameTable);

        if (namespaces?.Count > 0)
        {
            foreach (var ns in namespaces)
            {
                nsManager.AddNamespace(ns.Key, ns.Value);
            }
        }

        return nsManager;
    }

    /// <summary>
    /// XML 转 JSON 字符串
    /// </summary>
    /// <param name="xml">XML 字符串</param>
    /// <param name="indent">是否格式化 JSON</param>
    /// <returns>JSON 字符串</returns>
    public static string XmlToJson(string xml, bool indent = false)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var dict = XmlToDictionary(xml);

            // 这里可以使用 System.Text.Json 或 Newtonsoft.Json 进行转换
            // 为了保持独立性，这里提供一个简单的实现
            var json = new StringBuilder();
            json.Append('{');

            var first = true;
            foreach (var kvp in dict)
            {
                if (!first)
                {
                    json.Append(',');
                }

                if (indent)
                {
                    json.AppendLine();
                }

                json.Append($"\"{kvp.Key}\":\"{kvp.Value.Replace("\"", "\\\"")}\"");
                first = false;
            }

            if (indent)
            {
                json.AppendLine();
            }

            json.Append('}');

            return json.ToString();
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// 递归扁平化 XML 元素
    /// </summary>
    /// <param name="element">XML 元素</param>
    /// <param name="prefix">前缀</param>
    /// <param name="result">结果字典</param>
    /// <param name="separator">分隔符</param>
    private static void FlattenXmlElement(XElement? element, string prefix, Dictionary<string, string> result, string separator)
    {
        if (element == null)
        {
            return;
        }

        var currentPath = string.IsNullOrEmpty(prefix) ? element.Name.LocalName : $"{prefix}{separator}{element.Name.LocalName}";

        // 添加属性
        foreach (var attr in element.Attributes())
        {
            result[$"{currentPath}@{attr.Name.LocalName}"] = attr.Value;
        }

        // 如果有子元素，递归处理
        if (element.HasElements)
        {
            foreach (var child in element.Elements())
            {
                FlattenXmlElement(child, currentPath, result, separator);
            }
        }
        else
        {
            // 叶子节点，添加值
            result[currentPath] = element.Value;
        }
    }

    #endregion 辅助功能
}
