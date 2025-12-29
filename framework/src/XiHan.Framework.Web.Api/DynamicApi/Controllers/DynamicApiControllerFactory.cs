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

using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using XiHan.Framework.Web.Api.DynamicApi.Configuration;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Helpers;

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
                    $"{controllerName}DynamicController",
                    TypeAttributes.Public | TypeAttributes.Class,
                    typeof(ControllerBase));

                // 添加 ApiController 特性
                AddApiControllerAttribute(typeBuilder);

                // 添加 Route 特性
                AddRouteAttribute(typeBuilder, routeTemplate);

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

        var count = 0;
        foreach (var serviceMethod in serviceMethods)
        {
            try
            {
                CreateActionMethod(typeBuilder, serviceMethod, serviceField, convention, options);
                count++;
                logger?.LogDebug("已创建操作方法: {MethodName}", serviceMethod.Name);
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
    /// 创建动作方法
    /// </summary>
    private static void CreateActionMethod(TypeBuilder typeBuilder, MethodInfo serviceMethod, FieldBuilder serviceField,
        IDynamicApiConvention? convention, DynamicApiOptions? options)
    {
        // 应用约定获取方法信息
        var context = new DynamicApiConventionContext
        {
            ServiceType = serviceField.FieldType,
            MethodInfo = serviceMethod
        };

        convention?.Apply(context);

        // 如果方法被禁用，跳过
        if (!context.IsEnabled)
        {
            return;
        }

        var parameters = serviceMethod.GetParameters();
        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

        var methodBuilder = typeBuilder.DefineMethod(
            context.ActionName ?? serviceMethod.Name,
            MethodAttributes.Public | MethodAttributes.Virtual,
            serviceMethod.ReturnType,
            parameterTypes);

        // 添加 HTTP 方法特性（带路由模板）
        AddHttpMethodAttribute(methodBuilder, context.HttpMethod ?? "POST", context.RouteTemplate);

        // 获取路由模板中的参数名称
        var routeParameterNames = ExtractRouteParameterNames(context.RouteTemplate ?? string.Empty);

        // 添加参数及参数特性
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramBuilder = methodBuilder.DefineParameter(i + 1, parameters[i].Attributes, parameters[i].Name);

            // 添加参数绑定特性
            AddParameterBindingAttribute(paramBuilder, parameters[i], context.HttpMethod ?? "POST", i, parameters.Length, routeParameterNames);
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
    /// 添加参数绑定特性
    /// </summary>
    private static void AddParameterBindingAttribute(ParameterBuilder paramBuilder, ParameterInfo paramInfo,
        string httpMethod, int parameterIndex, int totalParameters, HashSet<string> routeParameterNames)
    {
        // 根据 HTTP 方法和参数类型决定绑定源
        Type? bindingAttributeType = null;

        // 检查参数是否在路由模板中
        var isInRoute = paramInfo.Name != null && routeParameterNames.Contains(paramInfo.Name);

        if (IsSimpleType(paramInfo.ParameterType))
        {
            // 如果参数在路由模板中，使用 [FromRoute]
            if (isInRoute)
            {
                bindingAttributeType = typeof(FromRouteAttribute);
            }
            else
            {
                // 不在路由中的简单类型参数使用 [FromQuery]
                bindingAttributeType = typeof(FromQueryAttribute);
            }
        }
        else
        {
            // 复杂类型：从 Body 绑定
            bindingAttributeType = typeof(FromBodyAttribute);
        }

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
    /// 提取路由模板中的参数名称
    /// </summary>
    private static HashSet<string> ExtractRouteParameterNames(string routeTemplate)
    {
        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(routeTemplate))
        {
            return parameterNames;
        }

        // 使用正则表达式提取 {paramName} 格式的参数
        var regex = new Regex(@"\{([^}:?]+)[\?:]?[^}]*\}");
        var matches = regex.Matches(routeTemplate);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                parameterNames.Add(match.Groups[1].Value);
            }
        }

        return parameterNames;
    }

    /// <summary>
    /// 判断是否是简单类型
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        return TypeHelper.IsSimpleType(type);
    }

    #endregion
}
