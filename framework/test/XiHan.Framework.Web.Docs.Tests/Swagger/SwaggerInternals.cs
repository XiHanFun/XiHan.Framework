// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace XiHan.Framework.Web.Docs.Tests.Swagger;

/// <summary>
/// 被测项目内部 Swagger 辅助类的反射外观
/// </summary>
/// <remarks>
/// XmlCommentsNodeNameHelper 与 DynamicApiSwaggerGroupHelper 在源码里都是 internal，
/// 被测项目又没有声明 InternalsVisibleTo，测试工程无法直接引用；本批任务不允许改动 src，
/// 所以这里用反射包一层强类型外观，让用例本身保持可读，也把"源码重命名"的失败点集中到这一个文件。
/// 类型全名、方法名、字段名均与源码逐字对应。
/// </remarks>
internal static class SwaggerInternals
{
    private const string XmlCommentsNodeNameHelperTypeName = "XiHan.Framework.Web.Docs.Swagger.XmlCommentsNodeNameHelper";

    private const string SwaggerGroupHelperTypeName = "XiHan.Framework.Web.Docs.Swagger.DynamicApiSwaggerGroupHelper";

    private const string GroupDefinitionTypeName = SwaggerGroupHelperTypeName + "+DynamicApiDocGroupDefinition";

    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

    private const BindingFlags InternalStatic = BindingFlags.NonPublic | BindingFlags.Static;

    private static readonly Assembly DocsAssembly = typeof(XiHanWebDocsModule).Assembly;

    private static readonly Type XmlCommentsNodeNameHelperType = GetRequiredType(XmlCommentsNodeNameHelperTypeName);

    private static readonly Type SwaggerGroupHelperType = GetRequiredType(SwaggerGroupHelperTypeName);

    private static readonly Type GroupDefinitionType = GetRequiredType(GroupDefinitionTypeName);

    private static readonly MethodInfo GetMemberNameForMethodMethod =
        GetRequiredMethod(XmlCommentsNodeNameHelperType, "GetMemberNameForMethod", PublicStatic);

    private static readonly MethodInfo GetMemberNamesForMethodMethod =
        GetRequiredMethod(XmlCommentsNodeNameHelperType, "GetMemberNamesForMethod", PublicStatic);

    private static readonly MethodInfo GetMemberNameForTypeMethod =
        GetRequiredMethod(XmlCommentsNodeNameHelperType, "GetMemberNameForType", PublicStatic);

    private static readonly MethodInfo GetGroupNamesMethod =
        GetRequiredMethod(SwaggerGroupHelperType, "GetGroupNames", InternalStatic);

    private static readonly MethodInfo GetGroupDefinitionsFromAttributesMethod =
        GetRequiredMethod(SwaggerGroupHelperType, "GetGroupDefinitionsFromAttributes", InternalStatic);

    private static readonly MethodInfo GetGroupNamesFromAttributesMethod =
        GetRequiredMethod(SwaggerGroupHelperType, "GetGroupNamesFromAttributes", InternalStatic);

    private static readonly PropertyInfo GroupProperty =
        GetRequiredProperty(GroupDefinitionType, "Group");

    private static readonly PropertyInfo DisplayNameProperty =
        GetRequiredProperty(GroupDefinitionType, "DisplayName");

    private static readonly PropertyInfo OrderProperty =
        GetRequiredProperty(GroupDefinitionType, "Order");

    /// <summary>
    /// 默认文档名称常量
    /// </summary>
    public static string DefaultDocName => (string)GetRequiredField(SwaggerGroupHelperType, "DefaultDocName").GetValue(null)!;

    /// <summary>
    /// 默认文档标题常量
    /// </summary>
    public static string DefaultDocTitle => (string)GetRequiredField(SwaggerGroupHelperType, "DefaultDocTitle").GetValue(null)!;

    /// <summary>
    /// 获取方法的首个成员名候选
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <returns>XML 文档成员名</returns>
    public static string GetMemberNameForMethod(MethodInfo method)
    {
        return (string)InvokeStatic(GetMemberNameForMethodMethod, method)!;
    }

    /// <summary>
    /// 获取方法的全部成员名候选
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <returns>按优先级排列的成员名候选</returns>
    public static IReadOnlyList<string> GetMemberNamesForMethod(MethodInfo method)
    {
        return (IReadOnlyList<string>)InvokeStatic(GetMemberNamesForMethodMethod, method)!;
    }

    /// <summary>
    /// 获取类型的成员名
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>XML 文档成员名</returns>
    public static string GetMemberNameForType(Type type)
    {
        return (string)InvokeStatic(GetMemberNameForTypeMethod, type)!;
    }

    /// <summary>
    /// 从 ApiExplorer 分组集合提取分组名
    /// </summary>
    /// <param name="provider">分组集合提供器</param>
    /// <returns>去重排序后的分组名</returns>
    public static IReadOnlyList<string> GetGroupNames(IApiDescriptionGroupCollectionProvider provider)
    {
        return (IReadOnlyList<string>)InvokeStatic(GetGroupNamesMethod, provider)!;
    }

    /// <summary>
    /// 扫描特性得到的分组定义
    /// </summary>
    /// <returns>分组定义列表</returns>
    public static IReadOnlyList<DocGroupDefinition> GetGroupDefinitionsFromAttributes()
    {
        var definitions = (IEnumerable)InvokeStatic(GetGroupDefinitionsFromAttributesMethod)!;

        var result = new List<DocGroupDefinition>();
        foreach (var definition in definitions)
        {
            result.Add(new DocGroupDefinition(
                (string)GroupProperty.GetValue(definition)!,
                (string)DisplayNameProperty.GetValue(definition)!,
                (int)OrderProperty.GetValue(definition)!));
        }

        return result;
    }

    /// <summary>
    /// 扫描特性得到的分组名（仅文档键）
    /// </summary>
    /// <returns>分组名列表</returns>
    public static IReadOnlyList<string> GetGroupNamesFromAttributes()
    {
        return (IReadOnlyList<string>)InvokeStatic(GetGroupNamesFromAttributesMethod)!;
    }

    private static Type GetRequiredType(string typeName)
    {
        return DocsAssembly.GetType(typeName, throwOnError: true)
            ?? throw new InvalidOperationException($"未能在被测程序集中找到类型：{typeName}。");
    }

    private static MethodInfo GetRequiredMethod(Type declaringType, string methodName, BindingFlags bindingFlags)
    {
        return declaringType.GetMethod(methodName, bindingFlags)
            ?? throw new InvalidOperationException($"未能在 {declaringType.FullName} 上找到方法：{methodName}。");
    }

    private static FieldInfo GetRequiredField(Type declaringType, string fieldName)
    {
        return declaringType.GetField(fieldName, InternalStatic)
            ?? throw new InvalidOperationException($"未能在 {declaringType.FullName} 上找到字段：{fieldName}。");
    }

    private static PropertyInfo GetRequiredProperty(Type declaringType, string propertyName)
    {
        return declaringType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"未能在 {declaringType.FullName} 上找到属性：{propertyName}。");
    }

    // 反射调用会把业务异常包成 TargetInvocationException，这里还原原始异常，否则用例断言的异常类型全对不上
    private static object? InvokeStatic(MethodInfo method, params object?[] arguments)
    {
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    /// <summary>
    /// 分组定义的测试侧投影
    /// </summary>
    /// <param name="Group">分组键（文档名）</param>
    /// <param name="DisplayName">分组显示名</param>
    /// <param name="Order">合并顺序</param>
    internal sealed record DocGroupDefinition(string Group, string DisplayName, int Order);
}
