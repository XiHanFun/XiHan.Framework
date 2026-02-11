#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiXmlCommentsOperationFilter
// Guid:3e5d8d7a-1c5e-4e5d-8d7a-1c5e3b1c9f3a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 16:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.XPath;
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;

namespace XiHan.Framework.Web.Docs.Swagger;

/// <summary>
/// 动态 API XML 注释操作过滤器
/// 用于从原始服务方法读取 XML 注释并应用到 Swagger 文档
/// </summary>
public class DynamicApiXmlCommentsOperationFilter : IOperationFilter
{
    private readonly Dictionary<string, XDocument> _xmlDocuments = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiXmlCommentsOperationFilter()
    {
        // 加载所有程序集的 XML 文档
        LoadXmlDocuments();
    }

    /// <summary>
    /// 应用过滤器
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // 检查是否有 OriginalMethodAttribute
        var originalMethodAttr = context.MethodInfo.GetCustomAttribute<OriginalMethodAttribute>();
        if (originalMethodAttr == null)
        {
            return;
        }

        try
        {
            // 获取原始方法
            var originalMethod = originalMethodAttr.ServiceType.GetMethod(
                originalMethodAttr.MethodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                originalMethodAttr.ParameterTypes,
                null);

            if (originalMethod == null)
            {
                return;
            }

            // 读取并应用 XML 注释
            ApplyXmlComments(operation, originalMethod, context);
        }
        catch
        {
            // 忽略错误，不影响 Swagger 生成
        }
    }

    /// <summary>
    /// 应用 XML 注释
    /// </summary>
    private void ApplyXmlComments(OpenApiOperation operation, MethodInfo methodInfo, OperationFilterContext context)
    {
        // 获取 DynamicApiAttribute 的 Description（优先级最高）
        var dynamicApiAttr = methodInfo.GetCustomAttribute<DynamicApiAttribute>();
        var hasCustomDescription = dynamicApiAttr != null && !string.IsNullOrEmpty(dynamicApiAttr.Description);

        var memberName = XmlCommentsNodeNameHelper.GetMemberNameForMethod(methodInfo);
        var xmlDoc = GetXmlDocument(methodInfo.DeclaringType?.Assembly);

        if (xmlDoc == null)
        {
            // 如果没有 XML 文档，但有自定义描述，仍然应用自定义描述
            if (hasCustomDescription && string.IsNullOrEmpty(operation.Summary))
            {
                operation.Summary = dynamicApiAttr!.Description;
            }
            return;
        }

        // 获取方法的 summary 注释
        var summaryNode = xmlDoc.XPathSelectElement($"/doc/members/member[@name='{memberName}']/summary");
        if (string.IsNullOrEmpty(operation.Summary))
        {
            // 优先使用 DynamicApiAttribute.Description
            if (hasCustomDescription)
            {
                operation.Summary = dynamicApiAttr!.Description;
            }
            // 否则使用 XML summary
            else if (summaryNode != null)
            {
                operation.Summary = summaryNode.Value.Trim();
            }
        }

        // 获取方法的 remarks 注释
        var remarksNode = xmlDoc.XPathSelectElement($"/doc/members/member[@name='{memberName}']/remarks");
        if (remarksNode != null && string.IsNullOrEmpty(operation.Description))
        {
            operation.Description = remarksNode.Value.Trim();
        }

        // 应用参数注释
        var parameters = methodInfo.GetParameters();
        foreach (var parameter in parameters)
        {
            var paramNode = xmlDoc.XPathSelectElement($"/doc/members/member[@name='{memberName}']/param[@name='{parameter.Name}']");
            if (paramNode != null)
            {
                var apiParam = operation.Parameters?.FirstOrDefault(p => p.Name == parameter.Name);
                if (apiParam != null && string.IsNullOrEmpty(apiParam.Description))
                {
                    apiParam.Description = paramNode.Value.Trim();
                }
            }
        }

        // 应用返回值注释
        var returnsNode = xmlDoc.XPathSelectElement($"/doc/members/member[@name='{memberName}']/returns");
        if (returnsNode != null && operation.Responses != null)
        {
            foreach (var response in operation.Responses.Values)
            {
                if (string.IsNullOrEmpty(response.Description))
                {
                    response.Description = returnsNode.Value.Trim();
                }
            }
        }
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
