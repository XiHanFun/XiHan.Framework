#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiXmlCommentsHelper
// Guid:0f2bcbe3-f1ee-4d2e-9ea7-c9f85775a5b2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/03 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using System.Xml.Linq;
using System.Xml.XPath;

namespace XiHan.Framework.Web.Api.DynamicApi.Helpers;

/// <summary>
/// Dynamic API XML 注释辅助
/// </summary>
public static class DynamicApiXmlCommentsHelper
{
    private static readonly Dictionary<string, XDocument?> XmlDocumentCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock XmlDocumentCacheLock = new();

    /// <summary>
    /// 获取类型 summary
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>类型的 summary</returns>
    public static string? GetTypeSummary(Type type)
    {
        var xmlDocument = GetXmlDocument(type.Assembly);
        if (xmlDocument == null)
        {
            return null;
        }

        if (type.IsConstructedGenericType)
        {
            type = type.GetGenericTypeDefinition();
        }

        var typeName = (type.FullName ?? type.Name).Replace("+", ".");
        var memberName = $"T:{typeName}";
        var summaryNode = xmlDocument.XPathSelectElement($"/doc/members/member[@name='{memberName}']/summary");
        return NormalizeXmlText(summaryNode?.Value);
    }

    /// <summary>
    /// 清空缓存
    /// </summary>
    public static void ClearCache()
    {
        XmlDocumentCache.Clear();
    }

    /// <summary>
    /// 获取程序集对应的 XML 文档
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>XML 文档</returns>
    private static XDocument? GetXmlDocument(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        if (XmlDocumentCache.TryGetValue(assemblyName, out var cached))
        {
            return cached;
        }

        lock (XmlDocumentCacheLock)
        {
            if (XmlDocumentCache.TryGetValue(assemblyName, out cached))
            {
                return cached;
            }

            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml");
            if (!File.Exists(xmlPath))
            {
                XmlDocumentCache[assemblyName] = null;
                return null;
            }

            try
            {
                cached = XDocument.Load(xmlPath);
            }
            catch
            {
                cached = null;
            }

            XmlDocumentCache[assemblyName] = cached;
            return cached;
        }
    }

    /// <summary>
    /// 规范化 XML 文本
    /// </summary>
    /// <param name="text">XML 文本</param>
    /// <returns>规范化后的 XML 文本</returns>
    private static string NormalizeXmlText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Join(' ', text
            .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }
}
