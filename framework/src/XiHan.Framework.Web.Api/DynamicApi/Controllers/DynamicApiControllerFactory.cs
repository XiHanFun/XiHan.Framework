#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiControllerFactory
// Guid:j0k1l2m3-n4o5-4p6q-7r8s-9t0u1v2w3x4y
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Reflection.Emit;
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Configuration;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Exceptions;
using XiHan.Framework.Web.Api.DynamicApi.Helpers;
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
    private static readonly Lock LockObject = new();

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
        // 使用双重检查锁定模式
        if (ControllerTypeCache.TryGetValue(serviceType, out var cachedType))
        {
            return cachedType;
        }

        lock (LockObject)
        {
            // 再次检查，防止并发创建
            if (ControllerTypeCache.TryGetValue(serviceType, out cachedType))
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

                // 添加 ApiExplorerSettings 特性（用于文档分组）
                AddApiExplorerSettingsAttribute(typeBuilder, serviceType);

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
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public static void ClearCache()
    {
        lock (LockObject)
        {
            ControllerTypeCache.Clear();
        }
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
        // 从服务类型获取 DynamicApiAttribute
        var dynamicApiAttr = serviceType.GetCustomAttribute<DynamicApiAttribute>();
        if (dynamicApiAttr == null)
        {
            return;
        }

        // 只处理 VisibleInApiExplorer = false 的情况
        // GroupName 通过 Swagger OperationFilter 处理，不使用 ApiExplorerSettings
        if (!dynamicApiAttr.VisibleInApiExplorer)
        {
            var attributeType = typeof(Microsoft.AspNetCore.Mvc.ApiExplorerSettingsAttribute);
            var constructor = attributeType.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                var ignoreApiProperty = attributeType.GetProperty("IgnoreApi");
                if (ignoreApiProperty != null)
                {
                    var attributeBuilder = new CustomAttributeBuilder(
                        constructor,
                        [],
                        [ignoreApiProperty],
                        [true]);

                    typeBuilder.SetCustomAttribute(attributeBuilder);
                }
            }
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

        // 过滤掉被隐藏的方法和重载方法，只保留每个方法名的最派生版本
        var filteredMethods = FilterOverloadedAndHiddenMethods(serviceMethods, serviceType, logger);

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
                    CreateActionMethod(typeBuilder, serviceMethod, serviceField, convention, options, null);
                    count++;
                    logger?.LogDebug("已创建操作方法: {MethodName}", serviceMethod.Name);
                }
                else
                {
                    // 为每个版本创建一个独立的 Action
                    foreach (var version in versions)
                    {
                        CreateActionMethod(typeBuilder, serviceMethod, serviceField, convention, options, version);
                        count++;
                        logger?.LogDebug("已创建操作方法: {MethodName} (版本: {Version})", serviceMethod.Name, version);
                    }
                }
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
        var versions = new HashSet<string>();

        // 1. 获取方法级别的 MapToApiVersionAttribute
        var mapToVersionAttrs = methodInfo.GetCustomAttributes<MapToApiVersionAttribute>();
        foreach (var attr in mapToVersionAttrs)
        {
            if (!string.IsNullOrEmpty(attr.Version))
            {
                versions.Add(attr.Version);
            }
        }

        // 2. 获取方法级别的 DynamicApiAttribute.Version
        var dynamicApiAttrs = methodInfo.GetCustomAttributes<DynamicApiAttribute>();
        foreach (var attr in dynamicApiAttrs)
        {
            if (!string.IsNullOrEmpty(attr.Version))
            {
                versions.Add(attr.Version);
            }
        }

        // 3. 获取方法级别的 ApiVersionAttribute
        var apiVersionAttrs = methodInfo.GetCustomAttributes<ApiVersionAttribute>();
        foreach (var attr in apiVersionAttrs)
        {
            if (!string.IsNullOrEmpty(attr.Version))
            {
                versions.Add(attr.Version);
            }
        }

        // 如果方法级别没有版本号，尝试从类级别获取
        if (versions.Count == 0)
        {
            // 4. 类级别的 DynamicApiAttribute.Version
            var classDynamicApiAttr = serviceType.GetCustomAttribute<DynamicApiAttribute>();
            if (!string.IsNullOrEmpty(classDynamicApiAttr?.Version))
            {
                versions.Add(classDynamicApiAttr.Version);
            }

            // 5. 类级别的 ApiVersionAttribute（可能有多个）
            var classApiVersionAttrs = serviceType.GetCustomAttributes<ApiVersionAttribute>();
            foreach (var attr in classApiVersionAttrs)
            {
                if (!string.IsNullOrEmpty(attr.Version))
                {
                    versions.Add(attr.Version);
                }
            }
        }

        // 如果还是没有版本号，使用默认版本
        if (versions.Count == 0 && !string.IsNullOrEmpty(options?.DefaultApiVersion))
        {
            versions.Add(options.DefaultApiVersion);
        }

        return [.. versions];
    }

    /// <summary>
    /// 过滤重载和被隐藏的方法，只保留每个方法名的最派生版本
    /// </summary>
    /// <remarks>
    /// 当基类和派生类有相同名称的方法（不管参数是否相同）时，
    /// 只保留声明类型最接近实际服务类型的那个版本，避免生成重复的 API 路由。
    /// </remarks>
    private static IEnumerable<MethodInfo> FilterOverloadedAndHiddenMethods(IEnumerable<MethodInfo> methods, Type serviceType, ILogger? logger = null)
    {
        var methodGroups = methods.GroupBy(m => m.Name);
        var result = new List<MethodInfo>();

        foreach (var group in methodGroups)
        {
            if (group.Count() == 1)
            {
                result.Add(group.First());
            }
            else
            {
                // 如果有多个同名方法（重载），选择声明类型最接近服务类型的那个
                var mostDerived = group
                    .OrderBy(m => GetDistanceFromType(m.DeclaringType, serviceType))
                    .First();

                result.Add(mostDerived);

                var skipped = group.Where(m => m != mostDerived).ToList();
                if (skipped.Count != 0)
                {
                    logger?.LogDebug("检测到方法重载/隐藏: {MethodName}，已选择 {DeclaringType} 中的版本，跳过 {SkippedCount} 个版本",
                        group.Key, mostDerived.DeclaringType?.Name, skipped.Count);
                }
            }
        }

        return result;
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
    /// <param name="specificVersion">指定的版本号（为多版本方法生成独立Action时使用）</param>
    private static void CreateActionMethod(TypeBuilder typeBuilder, MethodInfo serviceMethod, FieldBuilder serviceField,
        IDynamicApiConvention? convention, DynamicApiOptions? options, string? specificVersion = null)
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

        // 使用参数分析器分析参数
        List<ParameterDescriptor> parameterDescriptors;
        try
        {
            parameterDescriptors = DynamicApiParameterAnalyzer.Analyze(serviceMethod, httpMethod);
        }
        catch (DynamicApiException ex)
        {
            throw new InvalidOperationException(
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

        // 添加原始方法标记特性（用于 Swagger 读取 XML 注释）
        AddOriginalMethodAttribute(methodBuilder, serviceMethod);

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
        // 根据参数来源选择绑定特性类型
        var bindingAttributeType = descriptor.Source switch
        {
            ParameterSource.Route => typeof(FromRouteAttribute),
            ParameterSource.Query => typeof(FromQueryAttribute),
            ParameterSource.Body => typeof(FromBodyAttribute),
            ParameterSource.Services => typeof(FromServicesAttribute),
            ParameterSource.Header => typeof(FromHeaderAttribute),
            ParameterSource.Form => typeof(FromFormAttribute),
            _ => null
        };

        if (bindingAttributeType != null)
        {
            var constructor = bindingAttributeType.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                paramBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, []));
            }
        }
    }

    /// <summary>
    /// 添加方法级别的 ApiExplorerSettings 特性
    /// </summary>
    private static void AddMethodApiExplorerSettingsAttribute(MethodBuilder methodBuilder, MethodInfo serviceMethod)
    {
        // 从服务方法获取 DynamicApiAttribute
        var dynamicApiAttr = serviceMethod.GetCustomAttribute<DynamicApiAttribute>();
        if (dynamicApiAttr == null)
        {
            return;
        }

        // 只处理 VisibleInApiExplorer = false 的情况
        // GroupName 通过 Swagger OperationFilter 处理，不使用 ApiExplorerSettings
        if (!dynamicApiAttr.VisibleInApiExplorer)
        {
            var attributeType = typeof(Microsoft.AspNetCore.Mvc.ApiExplorerSettingsAttribute);
            var constructor = attributeType.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                var ignoreApiProperty = attributeType.GetProperty("IgnoreApi");
                if (ignoreApiProperty != null)
                {
                    var attributeBuilder = new CustomAttributeBuilder(
                        constructor,
                        [],
                        [ignoreApiProperty],
                        [true]);

                    methodBuilder.SetCustomAttribute(attributeBuilder);
                }
            }
        }
    }

    /// <summary>
    /// 添加原始方法标记特性
    /// </summary>
    private static void AddOriginalMethodAttribute(MethodBuilder methodBuilder, MethodInfo serviceMethod)
    {
        var attributeType = typeof(Attributes.OriginalMethodAttribute);
        var constructor = attributeType.GetConstructor([typeof(Type), typeof(string), typeof(Type[])]);
        if (constructor != null)
        {
            var parameterTypes = serviceMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            var attributeBuilder = new CustomAttributeBuilder(
                constructor,
                [serviceMethod.DeclaringType!, serviceMethod.Name, parameterTypes]);

            methodBuilder.SetCustomAttribute(attributeBuilder);
        }
    }

    #endregion
}
