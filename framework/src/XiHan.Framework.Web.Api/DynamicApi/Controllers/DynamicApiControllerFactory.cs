#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiControllerFactory
// Guid:e1c77bb7-c59c-4893-97d0-bf37c4e71f6a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Reflection.Emit;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Exceptions;
using XiHan.Framework.Web.Api.DynamicApi.Helpers;
using XiHan.Framework.Web.Api.DynamicApi.Options;
using XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

namespace XiHan.Framework.Web.Api.DynamicApi.Controllers;

/// <summary>
/// 动态 API 控制器工厂
/// </summary>
public static class DynamicApiControllerFactory
{
    private static readonly AssemblyBuilder AssemblyBuilder;
    private static readonly ModuleBuilder ModuleBuilder;
    private static readonly Dictionary<Type, Type> ControllerTypeCache = [];

    static DynamicApiControllerFactory()
    {
        var assemblyName = new AssemblyName("XiHan.Framework.Web.Api.DynamicControllers");
        AssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder = AssemblyBuilder.DefineDynamicModule("MainModule");
    }

    /// <summary>
    /// 创建控制器类型
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="convention">动态 API 约定</param>
    /// <param name="options">动态 API 配置选项</param>
    /// <param name="logger">日志记录器</param>
    /// <returns>控制器类型</returns>
    public static Type? CreateControllerType(Type serviceType, IDynamicApiConvention? convention = null,
        DynamicApiOptions? options = null, ILogger? logger = null)
    {
        // 首先检查缓存，避免重复创建控制器类型
        if (ControllerTypeCache.TryGetValue(serviceType, out var cachedType))
        {
            return cachedType;
        }

        try
        {
            logger?.LogDebug("正在为服务创建动态控制器: {ServiceType}", serviceType.FullName);

            // 应用约定获取控制器信息
            var context = new DynamicApiConventionContext
            {
                ServiceType = serviceType
            };

            convention?.Apply(context);

            // 如果约定禁用了动态 API，返回 null
            if (!context.IsEnabled)
            {
                logger?.LogDebug("服务已禁用动态 API: {ServiceName}", serviceType.Name);
                return null;
            }

            var controllerName = context.ControllerName ?? serviceType.Name;
            var routeTemplate = context.RouteTemplate ?? "api/[controller]";

            logger?.LogInformation("正在生成控制器 '{ControllerName}'，路由模板: '{RouteTemplate}'",
                controllerName, routeTemplate);

            // 创建控制器类型
            var typeBuilder = ModuleBuilder.DefineType(
                $"{controllerName}Controller",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(ControllerBase));

            // 添加 ApiController 特性
            AddApiControllerAttribute(typeBuilder);

            // 添加 Route 特性
            AddRouteAttribute(typeBuilder, routeTemplate);

            // 添加 ApiExplorerSettings 特性
            AddApiExplorerSettingsAttribute(typeBuilder, serviceType);

            // 复制类级别的授权相关特性（[Authorize] / [AllowAnonymous]）
            foreach (var attrBuilder in BuildAuthorizationAttributeBuilders(serviceType))
            {
                typeBuilder.SetCustomAttribute(attrBuilder);
            }

            // 添加服务字段
            var serviceField = DefineServiceField(typeBuilder, serviceType);

            // 创建构造函数
            CreateConstructor(typeBuilder, serviceType, serviceField);

            // 创建动作方法
            var methodCount = CreateActionMethods(typeBuilder, serviceType, serviceField, convention, options, logger);

            // 创建类型
            var controllerType = typeBuilder.CreateType();
            if (controllerType != null)
            {
                ControllerTypeCache[serviceType] = controllerType;
                logger?.LogInformation("成功为服务 '{ServiceName}' 创建了动态控制器，包含 {MethodCount} 个操作方法", serviceType.Name, methodCount);
            }

            return controllerType;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "为服务 '{ServiceName}' 创建动态控制器失败", serviceType.Name);
            return null;
        }
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public static void ClearCache()
    {
        ControllerTypeCache.Clear();
        DynamicApiXmlCommentsHelper.ClearCache();
    }

    #region 辅助方法

    /// <summary>
    /// 添加 ApiController 特性
    /// </summary>
    private static void AddApiControllerAttribute(TypeBuilder typeBuilder)
    {
        var constructor = typeof(ApiControllerAttribute).GetConstructor(Type.EmptyTypes);
        if (constructor != null)
        {
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, []));
        }
    }

    /// <summary>
    /// 添加 Route 特性
    /// </summary>
    private static void AddRouteAttribute(TypeBuilder typeBuilder, string routeTemplate)
    {
        var constructor = typeof(RouteAttribute).GetConstructor([typeof(string)]);
        if (constructor != null)
        {
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, [routeTemplate]));
        }
    }

    /// <summary>
    /// 添加 ApiExplorerSettings 特性
    /// </summary>
    private static void AddApiExplorerSettingsAttribute(TypeBuilder typeBuilder, Type serviceType)
    {
        var classAttributes = DynamicApiAttributeMergeHelper.GetOrderedClassAttributes(serviceType);
        if (classAttributes.Count == 0)
        {
            return;
        }

        var groupName = DynamicApiAttributeMergeHelper.ResolveStringFromAttributes(
            classAttributes,
            attribute => attribute.Group);
        var ignoreApi = classAttributes.Any(attribute => !attribute.VisibleInApiExplorer);
        var attributeBuilder = BuildApiExplorerSettingsAttribute(groupName, ignoreApi);
        if (attributeBuilder != null)
        {
            typeBuilder.SetCustomAttribute(attributeBuilder);
        }
    }

    /// <summary>
    /// 定义服务字段
    /// </summary>
    private static FieldBuilder DefineServiceField(TypeBuilder typeBuilder, Type serviceType)
    {
        return typeBuilder.DefineField(
            "_service",
            serviceType,
            FieldAttributes.Private | FieldAttributes.InitOnly);
    }

    /// <summary>
    /// 创建构造函数
    /// </summary>
    private static void CreateConstructor(TypeBuilder typeBuilder, Type serviceType, FieldBuilder serviceField)
    {
        var constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            [serviceType]);

        // 添加参数名称
        constructor.DefineParameter(1, ParameterAttributes.None, "service");

        var il = constructor.GetILGenerator();

        // 调用基类构造函数
        il.Emit(OpCodes.Ldarg_0);
        var baseConstructor = typeof(ControllerBase).GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (baseConstructor != null)
        {
            il.Emit(OpCodes.Call, baseConstructor);
        }
        else
        {
            // 如果找不到无参构造函数，尝试调用 Object 的构造函数
            var objectConstructor = typeof(object).GetConstructor(Type.EmptyTypes);
            il.Emit(OpCodes.Call, objectConstructor!);
        }

        // 设置服务字段
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, serviceField);

        // 返回
        il.Emit(OpCodes.Ret);
    }

    /// <summary>
    /// 创建动作方法
    /// </summary>
    /// <returns>创建的方法数量</returns>
    private static int CreateActionMethods(TypeBuilder typeBuilder, Type serviceType, FieldBuilder serviceField,
        IDynamicApiConvention? convention, DynamicApiOptions? options, ILogger? logger = null)
    {
        var serviceMethods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(TypeHelper.ShouldExposeAsApi);

        // 仅过滤同签名的隐藏方法，保留合法重载
        var filteredMethods = FilterHiddenMethodsBySignature(serviceMethods, serviceType, logger);
        var endpointRouteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var count = 0;
        foreach (var serviceMethod in filteredMethods)
        {
            try
            {
                // 获取方法的所有版本号
                var versions = GetMethodVersions(serviceMethod, serviceType, options);

                if (versions.Count == 0)
                {
                    // 没有指定版本，创建默认的 Action
                    CreateActionMethod(typeBuilder, serviceMethod, serviceField, convention, options, endpointRouteMap, null);
                    count++;
                    logger?.LogDebug("已创建操作方法: {MethodName}", serviceMethod.Name);
                }
                else
                {
                    // 为每个版本创建一个独立的 Action
                    foreach (var version in versions)
                    {
                        CreateActionMethod(typeBuilder, serviceMethod, serviceField, convention, options, endpointRouteMap, version);
                        count++;
                        logger?.LogDebug("已创建操作方法: {MethodName} (版本: {Version})", serviceMethod.Name, version);
                    }
                }
            }
            catch (DynamicApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "创建操作方法 '{MethodName}' 失败: {ErrorMessage}",
                    serviceMethod.Name, ex.Message);
            }
        }

        return count;
    }

    /// <summary>
    /// 获取方法的所有版本号
    /// </summary>
    private static List<string> GetMethodVersions(MethodInfo methodInfo, Type serviceType, DynamicApiOptions? options)
    {
        var versions = DynamicApiAttributeMergeHelper
            .ResolveVersions(serviceType, methodInfo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 如果还是没有版本号，使用默认版本
        if (versions.Count == 0 && !string.IsNullOrEmpty(options?.DefaultApiVersion))
        {
            versions.Add(options.DefaultApiVersion);
        }

        return [.. versions];
    }

    /// <summary>
    /// 过滤被隐藏的方法，仅对同签名方法保留最派生版本
    /// </summary>
    /// <remarks>
    /// 同名不同参数的合法重载会被保留。
    /// </remarks>
    private static IEnumerable<MethodInfo> FilterHiddenMethodsBySignature(IEnumerable<MethodInfo> methods, Type serviceType, ILogger? logger = null)
    {
        var methodGroups = methods.GroupBy(GetMethodSignatureKey, StringComparer.Ordinal);
        var result = new List<MethodInfo>();

        foreach (var group in methodGroups)
        {
            if (group.Count() == 1)
            {
                result.Add(group.First());
            }
            else
            {
                // 同签名出现多个版本（多为隐藏基类方法），选择最派生版本
                var mostDerived = group
                    .OrderBy(m => GetDistanceFromType(m.DeclaringType, serviceType))
                    .First();

                result.Add(mostDerived);

                var skipped = group.Where(m => m != mostDerived).ToList();
                if (skipped.Count != 0)
                {
                    logger?.LogDebug("检测到同签名隐藏方法: {MethodSignature}，已选择 {DeclaringType} 中的版本，跳过 {SkippedCount} 个版本",
                        group.Key, mostDerived.DeclaringType?.Name, skipped.Count);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 获取方法签名键（方法名 + 参数类型列表）
    /// </summary>
    private static string GetMethodSignatureKey(MethodInfo method)
    {
        var parameterTypeNames = method
            .GetParameters()
            .Select(parameter => GetTypeSignatureName(parameter.ParameterType));

        return $"{method.Name}({string.Join(",", parameterTypeNames)})";
    }

    /// <summary>
    /// 获取类型签名名称
    /// </summary>
    private static string GetTypeSignatureName(Type type)
    {
        if (type.IsByRef)
        {
            return $"{GetTypeSignatureName(type.GetElementType()!)}&";
        }

        if (type.IsArray)
        {
            return $"{GetTypeSignatureName(type.GetElementType()!)}[]";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var genericTypeName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var genericArgs = type.GetGenericArguments().Select(GetTypeSignatureName);
        return $"{genericTypeName}[{string.Join(",", genericArgs)}]";
    }

    /// <summary>
    /// 获取声明类型到服务类型的距离
    /// </summary>
    /// <returns>距离值，越小表示越接近服务类型</returns>
    private static int GetDistanceFromType(Type? declaringType, Type serviceType)
    {
        if (declaringType == null)
        {
            return int.MaxValue;
        }

        var distance = 0;
        var currentType = serviceType;

        while (currentType != null && currentType != typeof(object))
        {
            if (currentType == declaringType)
            {
                return distance;
            }
            distance++;
            currentType = currentType.BaseType;
        }

        return int.MaxValue;
    }

    /// <summary>
    /// 创建动作方法
    /// </summary>
    /// <param name="typeBuilder">类型构建器</param>
    /// <param name="serviceMethod">服务方法</param>
    /// <param name="serviceField">服务字段</param>
    /// <param name="convention">动态API约定</param>
    /// <param name="options">配置选项</param>
    /// <param name="endpointRouteMap">端点映射（用于检测冲突）</param>
    /// <param name="specificVersion">指定的版本号（为多版本方法生成独立Action时使用）</param>
    private static void CreateActionMethod(TypeBuilder typeBuilder, MethodInfo serviceMethod, FieldBuilder serviceField,
        IDynamicApiConvention? convention, DynamicApiOptions? options, Dictionary<string, string> endpointRouteMap, string? specificVersion = null)
    {
        // 应用约定获取方法信息
        var context = new DynamicApiConventionContext
        {
            ServiceType = serviceField.FieldType,
            MethodInfo = serviceMethod
        };

        // 如果指定了特定版本，强制使用该版本
        if (!string.IsNullOrEmpty(specificVersion))
        {
            context.ApiVersion = specificVersion;
        }

        convention?.Apply(context);

        // 如果指定了特定版本，确保使用该版本（约定可能会覆盖）
        if (!string.IsNullOrEmpty(specificVersion))
        {
            context.ApiVersion = specificVersion;
        }

        // 如果方法被禁用，跳过
        if (!context.IsEnabled)
        {
            return;
        }

        var httpMethod = context.HttpMethod ?? "POST";
        EnsureUniqueEndpointRoute(endpointRouteMap, serviceMethod, httpMethod, context.RouteTemplate, specificVersion);

        // 使用参数分析器分析参数
        List<ParameterDescriptor> parameterDescriptors;
        try
        {
            parameterDescriptors = DynamicApiParameterAnalyzer.Analyze(serviceMethod, httpMethod);
        }
        catch (DynamicApiException ex)
        {
            throw new DynamicApiException(
                $"分析方法 '{serviceMethod.DeclaringType?.Name}.{serviceMethod.Name}' 的参数时失败: {ex.Message}",
                ex);
        }

        var parameters = serviceMethod.GetParameters();
        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

        // 生成 Action 方法名称
        // 如果指定了版本号，添加版本后缀以区分不同版本的方法
        var actionName = context.ActionName ?? serviceMethod.Name;
        if (!string.IsNullOrEmpty(specificVersion))
        {
            // 将版本号转换为合法的方法名后缀（去除点号等特殊字符）
            var versionSuffix = specificVersion.Replace(".", "").Replace("-", "");
            actionName = $"{actionName}_V{versionSuffix}";
        }

        var methodBuilder = typeBuilder.DefineMethod(
            actionName,
            MethodAttributes.Public | MethodAttributes.Virtual,
            serviceMethod.ReturnType,
            parameterTypes);

        // 添加 HTTP 方法特性（带路由模板）
        AddHttpMethodAttribute(methodBuilder, httpMethod, context.RouteTemplate);

        // 添加方法级别的 ApiExplorerSettings 特性
        AddMethodApiExplorerSettingsAttribute(methodBuilder, serviceMethod);

        // 添加 Tags 特性（用于 OpenAPI 分组）
        AddTagsAttribute(methodBuilder, serviceMethod, serviceField.FieldType);

        // 添加原始方法标记特性（用于读取 XML 注释）
        AddOriginalMethodAttribute(methodBuilder, serviceMethod, serviceField.FieldType);

        // 复制方法级别的授权相关特性（[Authorize] / [AllowAnonymous]）
        foreach (var attrBuilder in BuildAuthorizationAttributeBuilders(serviceMethod))
        {
            methodBuilder.SetCustomAttribute(attrBuilder);
        }

        // 添加参数及参数特性
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramBuilder = methodBuilder.DefineParameter(i + 1, parameters[i].Attributes, parameters[i].Name);

            // 使用参数描述符添加参数绑定特性
            var descriptor = parameterDescriptors.FirstOrDefault(d => d.Name == parameters[i].Name);
            if (descriptor != null)
            {
                AddParameterBindingAttribute(paramBuilder, descriptor);
            }
        }

        // 生成 IL 代码
        var il = methodBuilder.GetILGenerator();

        // 加载服务实例
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, serviceField);

        // 加载所有参数
        for (var i = 0; i < parameters.Length; i++)
        {
            il.Emit(OpCodes.Ldarg, i + 1);
        }

        // 调用服务方法
        il.Emit(OpCodes.Callvirt, serviceMethod);
        il.Emit(OpCodes.Ret);
    }

    /// <summary>
    /// 确保端点路由唯一，避免运行期出现路由歧义
    /// </summary>
    private static void EnsureUniqueEndpointRoute(
        Dictionary<string, string> endpointRouteMap,
        MethodInfo serviceMethod,
        string httpMethod,
        string? routeTemplate,
        string? specificVersion)
    {
        var normalizedRoute = string.IsNullOrWhiteSpace(routeTemplate)
            ? string.Empty
            : routeTemplate.Trim().Trim('/');
        var normalizedHttpMethod = httpMethod.ToUpperInvariant();
        var endpointKey = $"{normalizedHttpMethod}:{normalizedRoute}";
        var currentMethodDisplay = BuildMethodDisplayName(serviceMethod, specificVersion);

        if (endpointRouteMap.TryGetValue(endpointKey, out var existingMethodDisplay))
        {
            throw new DynamicApiException(
                $"检测到动态 API 路由冲突：{normalizedHttpMethod} '{normalizedRoute}' 同时映射到 '{existingMethodDisplay}' 和 '{currentMethodDisplay}'。请为重载方法指定不同的 Name/RouteTemplate，或调整 HTTP 方法。");
        }

        endpointRouteMap[endpointKey] = currentMethodDisplay;
    }

    /// <summary>
    /// 构建方法展示名称（用于冲突提示）
    /// </summary>
    private static string BuildMethodDisplayName(MethodInfo methodInfo, string? specificVersion)
    {
        var parameters = string.Join(", ", methodInfo.GetParameters().Select(parameter =>
            $"{parameter.ParameterType.Name} {parameter.Name}"));
        var versionSuffix = string.IsNullOrWhiteSpace(specificVersion) ? string.Empty : $" [v{specificVersion}]";
        return $"{methodInfo.DeclaringType?.Name}.{methodInfo.Name}({parameters}){versionSuffix}";
    }

    /// <summary>
    /// 添加 HTTP 方法特性
    /// </summary>
    private static void AddHttpMethodAttribute(MethodBuilder methodBuilder, string httpMethod, string? routeTemplate = null)
    {
        var attributeType = httpMethod.ToUpperInvariant() switch
        {
            "GET" => typeof(HttpGetAttribute),
            "POST" => typeof(HttpPostAttribute),
            "PUT" => typeof(HttpPutAttribute),
            "DELETE" => typeof(HttpDeleteAttribute),
            "PATCH" => typeof(HttpPatchAttribute),
            "HEAD" => typeof(HttpHeadAttribute),
            "OPTIONS" => typeof(HttpOptionsAttribute),
            _ => typeof(HttpPostAttribute)
        };

        // 如果有路由模板，使用带路由参数的构造函数；否则使用无参构造函数
        if (!string.IsNullOrWhiteSpace(routeTemplate))
        {
            var constructor = attributeType.GetConstructor([typeof(string)]);
            if (constructor != null)
            {
                methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, [routeTemplate]));
                return;
            }
        }

        var emptyConstructor = attributeType.GetConstructor(Type.EmptyTypes);
        if (emptyConstructor != null)
        {
            methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(emptyConstructor, []));
        }
    }

    /// <summary>
    /// 添加参数绑定特性（基于参数描述符）
    /// </summary>
    private static void AddParameterBindingAttribute(ParameterBuilder paramBuilder, ParameterDescriptor descriptor)
    {
        if (descriptor.Type == typeof(CancellationToken))
        {
            return;
        }

        // 显式绑定特性优先，保留 Name 等绑定元数据
        if (descriptor.ParameterInfo != null &&
            ParameterSourceResolver.TryGetExplicitBinding(descriptor.ParameterInfo, out var explicitSource, out var explicitBindingName))
        {
            var explicitBindingBuilder = BuildBindingAttribute(explicitSource, explicitBindingName);
            if (explicitBindingBuilder != null)
            {
                paramBuilder.SetCustomAttribute(explicitBindingBuilder);
                return;
            }
        }

        var inferredBindingBuilder = BuildBindingAttribute(descriptor.Source);
        if (inferredBindingBuilder != null)
        {
            paramBuilder.SetCustomAttribute(inferredBindingBuilder);
        }
    }

    /// <summary>
    /// 按参数来源构建参数绑定特性
    /// </summary>
    private static CustomAttributeBuilder? BuildBindingAttribute(ParameterSource source, string? name = null)
    {
        var attributeType = source switch
        {
            ParameterSource.Route => typeof(FromRouteAttribute),
            ParameterSource.Query => typeof(FromQueryAttribute),
            ParameterSource.Body => typeof(FromBodyAttribute),
            ParameterSource.Services => typeof(FromServicesAttribute),
            ParameterSource.Header => typeof(FromHeaderAttribute),
            ParameterSource.Form => typeof(FromFormAttribute),
            _ => null
        };

        return attributeType == null ? null : BuildBindingAttribute(attributeType, name);
    }

    /// <summary>
    /// 构建参数绑定特性
    /// </summary>
    private static CustomAttributeBuilder? BuildBindingAttribute(Type attributeType, string? name = null)
    {
        var constructor = attributeType.GetConstructor(Type.EmptyTypes);
        if (constructor == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return new CustomAttributeBuilder(constructor, []);
        }

        var nameProperty = attributeType.GetProperty(nameof(FromQueryAttribute.Name));
        if (nameProperty == null)
        {
            return new CustomAttributeBuilder(constructor, []);
        }

        return new CustomAttributeBuilder(
            constructor,
            [],
            [nameProperty],
            [name]);
    }

    /// <summary>
    /// 添加方法级别的 ApiExplorerSettings 特性
    /// </summary>
    private static void AddMethodApiExplorerSettingsAttribute(MethodBuilder methodBuilder, MethodInfo serviceMethod)
    {
        var methodAttributes = DynamicApiAttributeMergeHelper.GetOrderedMethodAttributes(serviceMethod);
        if (methodAttributes.Count == 0)
        {
            return;
        }

        var groupName = DynamicApiAttributeMergeHelper.ResolveStringFromAttributes(
            methodAttributes,
            attribute => attribute.Group);
        var ignoreApi = methodAttributes.Any(attribute => !attribute.VisibleInApiExplorer);

        var attributeBuilder = BuildApiExplorerSettingsAttribute(groupName, ignoreApi);
        if (attributeBuilder != null)
        {
            methodBuilder.SetCustomAttribute(attributeBuilder);
        }
    }

    /// <summary>
    /// 构建 ApiExplorerSettings 特性
    /// </summary>
    private static CustomAttributeBuilder? BuildApiExplorerSettingsAttribute(string? groupName, bool? ignoreApi)
    {
        var hasGroupName = !string.IsNullOrWhiteSpace(groupName);
        var shouldIgnoreApi = ignoreApi == true;

        if (!hasGroupName && !shouldIgnoreApi)
        {
            return null;
        }

        var attributeType = typeof(ApiExplorerSettingsAttribute);
        var constructor = attributeType.GetConstructor(Type.EmptyTypes);
        if (constructor == null)
        {
            return null;
        }

        var namedProperties = new List<PropertyInfo>();
        var propertyValues = new List<object?>();

        if (hasGroupName)
        {
            var groupNameProperty = attributeType.GetProperty(nameof(ApiExplorerSettingsAttribute.GroupName));
            if (groupNameProperty != null)
            {
                namedProperties.Add(groupNameProperty);
                propertyValues.Add(groupName!.Trim());
            }
        }

        if (shouldIgnoreApi)
        {
            var ignoreApiProperty = attributeType.GetProperty(nameof(ApiExplorerSettingsAttribute.IgnoreApi));
            if (ignoreApiProperty != null)
            {
                namedProperties.Add(ignoreApiProperty);
                propertyValues.Add(true);
            }
        }

        if (namedProperties.Count == 0)
        {
            return null;
        }

        return new CustomAttributeBuilder(
            constructor,
            [],
            [.. namedProperties],
            [.. propertyValues]);
    }

    /// <summary>
    /// 添加 Tags 特性（用于 OpenAPI 分组）
    /// </summary>
    private static void AddTagsAttribute(MethodBuilder methodBuilder, MethodInfo serviceMethod, Type serviceType)
    {
        var tags = DynamicApiAttributeMergeHelper.ResolveTags(serviceType, serviceMethod).ToList();
        if (tags.Count == 0)
        {
            var typeSummary = DynamicApiXmlCommentsHelper.GetTypeSummary(serviceType);
            if (!string.IsNullOrWhiteSpace(typeSummary))
            {
                tags.Add(typeSummary);
            }
        }

        // 如果有 Tag，添加 Tags 特性
        if (tags.Count != 0)
        {
            // 使用 ASP.NET Core 内置的 TagsAttribute
            var attributeType = typeof(TagsAttribute);
            var constructor = attributeType.GetConstructor([typeof(string[])]);
            if (constructor != null)
            {
                var attributeBuilder = new CustomAttributeBuilder(
                    constructor,
                    [tags.ToArray()]);

                methodBuilder.SetCustomAttribute(attributeBuilder);
            }
        }
    }

    /// <summary>
    /// 添加原始方法标记特性
    /// </summary>
    private static void AddOriginalMethodAttribute(MethodBuilder methodBuilder, MethodInfo serviceMethod, Type serviceType)
    {
        var attributeType = typeof(OriginalMethodAttribute);
        var constructor = attributeType.GetConstructor([typeof(Type), typeof(string), typeof(Type[])]);
        if (constructor != null)
        {
            var parameterTypes = serviceMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            var attributeBuilder = new CustomAttributeBuilder(
                constructor,
                [serviceType, serviceMethod.Name, parameterTypes]);

            methodBuilder.SetCustomAttribute(attributeBuilder);
        }
    }

    /// <summary>
    /// 构建授权相关特性（[AllowAnonymous] / [Authorize]），用于复制到动态生成的控制器或动作方法
    /// </summary>
    private static List<CustomAttributeBuilder> BuildAuthorizationAttributeBuilders(MemberInfo source)
    {
        var builders = new List<CustomAttributeBuilder>();

        if (source.GetCustomAttribute<AllowAnonymousAttribute>() != null)
        {
            var ctor = typeof(AllowAnonymousAttribute).GetConstructor(Type.EmptyTypes);
            if (ctor != null)
            {
                builders.Add(new CustomAttributeBuilder(ctor, []));
            }
        }

        foreach (var attr in source.GetCustomAttributes<AuthorizeAttribute>())
        {
            var builder = BuildAuthorizeAttributeBuilder(attr);
            if (builder != null)
            {
                builders.Add(builder);
            }
        }

        return builders;
    }

    /// <summary>
    /// 构建 [Authorize] 特性（支持 Policy、Roles、AuthenticationSchemes）
    /// </summary>
    private static CustomAttributeBuilder? BuildAuthorizeAttributeBuilder(AuthorizeAttribute source)
    {
        var type = typeof(AuthorizeAttribute);

        ConstructorInfo? ctor;
        object[] ctorArgs;

        if (!string.IsNullOrEmpty(source.Policy))
        {
            ctor = type.GetConstructor([typeof(string)]);
            ctorArgs = [source.Policy];
        }
        else
        {
            ctor = type.GetConstructor(Type.EmptyTypes);
            ctorArgs = [];
        }

        if (ctor == null)
        {
            return null;
        }

        var namedProps = new List<PropertyInfo>();
        var propValues = new List<object>();

        if (!string.IsNullOrEmpty(source.Roles))
        {
            var prop = type.GetProperty(nameof(AuthorizeAttribute.Roles));
            if (prop != null)
            {
                namedProps.Add(prop);
                propValues.Add(source.Roles);
            }
        }

        if (!string.IsNullOrEmpty(source.AuthenticationSchemes))
        {
            var prop = type.GetProperty(nameof(AuthorizeAttribute.AuthenticationSchemes));
            if (prop != null)
            {
                namedProps.Add(prop);
                propValues.Add(source.AuthenticationSchemes);
            }
        }

        return namedProps.Count > 0
            ? new CustomAttributeBuilder(ctor, ctorArgs, [.. namedProps], [.. propValues])
            : new CustomAttributeBuilder(ctor, ctorArgs);
    }

    #endregion
}
