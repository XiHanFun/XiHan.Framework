#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiXmlCommentsOperationTransformer
// Guid:3e5d8d7a-1c5e-4e5d-8d7a-1c5e3b1c9f3a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 16:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.XPath;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Helpers;

namespace XiHan.Framework.Web.Docs.Swagger;

/// <summary>
/// 动态 API XML 注释操作转换器
/// 用于从原始服务方法读取 XML 注释并应用到 OpenAPI 文档
/// </summary>
public class DynamicApiXmlCommentsOperationTransformer : IOpenApiOperationTransformer
{
    private readonly Dictionary<string, XDocument> _xmlDocuments = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiXmlCommentsOperationTransformer()
    {
        LoadXmlDocuments();
    }

    /// <summary>
    /// 转换操作
    /// </summary>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
        {
            return Task.CompletedTask;
        }

        var originalMethodAttr = controllerActionDescriptor.MethodInfo.GetCustomAttribute<OriginalMethodAttribute>();
        if (originalMethodAttr == null)
        {
            return Task.CompletedTask;
        }

        try
        {
            var originalMethod = originalMethodAttr.ServiceType.GetMethod(
                originalMethodAttr.MethodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                originalMethodAttr.ParameterTypes,
                null);

            if (originalMethod != null)
            {
                ApplyXmlComments(operation, originalMethodAttr.ServiceType, originalMethod);
            }
        }
        catch
        {
            // 忽略错误，不影响 OpenAPI 生成
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 应用 XML 注释
    /// </summary>
    private void ApplyXmlComments(OpenApiOperation operation, Type serviceType, MethodInfo methodInfo)
    {
        var dynamicApiDescription = DynamicApiAttributeMergeHelper.ResolveDescription(serviceType, methodInfo);
        var hasCustomDescription = !string.IsNullOrWhiteSpace(dynamicApiDescription);

        var memberNameCandidates = XmlCommentsNodeNameHelper.GetMemberNamesForMethod(methodInfo);
        var xmlDoc = GetXmlDocument(methodInfo.DeclaringType?.Assembly);

        if (xmlDoc == null)
        {
            if (hasCustomDescription && string.IsNullOrEmpty(operation.Summary))
            {
                operation.Summary = dynamicApiDescription;
            }
            return;
        }

        var memberNode = GetMethodMemberNode(xmlDoc, memberNameCandidates);
        var summaryNode = memberNode?.Element("summary");
        if (string.IsNullOrEmpty(operation.Summary))
        {
            if (hasCustomDescription)
            {
                operation.Summary = dynamicApiDescription;
            }
            else if (summaryNode != null)
            {
                operation.Summary = summaryNode.Value.Trim();
            }
        }

        var remarksNode = memberNode?.Element("remarks");
        if (remarksNode != null && string.IsNullOrEmpty(operation.Description))
        {
            operation.Description = remarksNode.Value.Trim();
        }

        var parameters = methodInfo.GetParameters();
        foreach (var parameter in parameters)
        {
            var paramNode = memberNode?
                .Elements("param")
                .FirstOrDefault(element => string.Equals(element.Attribute("name")?.Value, parameter.Name, StringComparison.Ordinal));
            if (paramNode == null)
            {
                continue;
            }

            var apiParam = operation.Parameters?.FirstOrDefault(p => p.Name == parameter.Name);
            if (apiParam != null && string.IsNullOrEmpty(apiParam.Description))
            {
                apiParam.Description = paramNode.Value.Trim();
            }
        }

        var returnsNode = memberNode?.Element("returns");
        if (returnsNode == null || operation.Responses == null)
        {
            return;
        }

        foreach (var response in operation.Responses.Values)
        {
            if (string.IsNullOrEmpty(response.Description))
            {
                response.Description = returnsNode.Value.Trim();
            }
        }
    }

    private static XElement? GetMethodMemberNode(XDocument xmlDoc, IEnumerable<string> memberNameCandidates)
    {
        foreach (var memberName in memberNameCandidates)
        {
            var node = xmlDoc.XPathSelectElement($"/doc/members/member[@name='{memberName}']");
            if (node != null)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// 加载 XML 文档
    /// </summary>
    private void LoadXmlDocuments()
    {
        var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
        foreach (var xmlFile in xmlFiles)
        {
            try
            {
                var doc = XDocument.Load(xmlFile);
                var assemblyName = Path.GetFileNameWithoutExtension(xmlFile);
                _xmlDocuments[assemblyName] = doc;
            }
            catch
            {
                // 忽略无法加载的 XML 文件
            }
        }
    }

    /// <summary>
    /// 获取程序集的 XML 文档
    /// </summary>
    private XDocument? GetXmlDocument(Assembly? assembly)
    {
        if (assembly == null)
        {
            return null;
        }

        var assemblyName = assembly.GetName().Name;
        if (assemblyName != null && _xmlDocuments.TryGetValue(assemblyName, out var doc))
        {
            return doc;
        }

        return null;
    }
}
