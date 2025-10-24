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
using Microsoft.AspNetCore.Mvc;

namespace XiHan.Framework.Web.Api.DynamicApi.Controllers;

/// <summary>
/// 动态 API 控制器工厂
/// </summary>
public static class DynamicApiControllerFactory
{
    private static readonly AssemblyBuilder AssemblyBuilder;
    private static readonly ModuleBuilder ModuleBuilder;
    private static readonly Dictionary<Type, Type> ControllerTypeCache = new();

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
    /// <returns>控制器类型</returns>
    public static Type? CreateControllerType(Type serviceType)
    {
        if (ControllerTypeCache.TryGetValue(serviceType, out var cachedType))
        {
            return cachedType;
        }

        try
        {
            var controllerName = $"{serviceType.Name}Controller";
            var typeBuilder = ModuleBuilder.DefineType(
                controllerName,
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(ControllerBase));

            // 添加 ApiController 特性
            typeBuilder.SetCustomAttribute(
                new CustomAttributeBuilder(
                    typeof(ApiControllerAttribute).GetConstructor(Type.EmptyTypes)!,
                    []));

            // 添加 Route 特性
            var routeConstructor = typeof(RouteAttribute).GetConstructor([typeof(string)])!;
            typeBuilder.SetCustomAttribute(
                new CustomAttributeBuilder(
                    routeConstructor,
                    ["api/[controller]"]));

            // 添加服务字段
            var serviceField = typeBuilder.DefineField(
                "_service",
                serviceType,
                FieldAttributes.Private | FieldAttributes.InitOnly);

            // 创建构造函数
            CreateConstructor(typeBuilder, serviceType, serviceField);

            // 创建动作方法
            CreateActionMethods(typeBuilder, serviceType, serviceField);

            var controllerType = typeBuilder.CreateType();
            if (controllerType != null)
            {
                ControllerTypeCache[serviceType] = controllerType;
            }

            return controllerType;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 创建构造函数
    /// </summary>
    private static void CreateConstructor(TypeBuilder typeBuilder, Type serviceType, FieldBuilder serviceField)
    {
        var constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            [serviceType]);

        var il = constructor.GetILGenerator();

        // 调用基类构造函数
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(ControllerBase).GetConstructor(Type.EmptyTypes)!);

        // 设置服务字段
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, serviceField);
        il.Emit(OpCodes.Ret);
    }

    /// <summary>
    /// 创建动作方法
    /// </summary>
    private static void CreateActionMethods(TypeBuilder typeBuilder, Type serviceType, FieldBuilder serviceField)
    {
        var serviceMethods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object));

        foreach (var serviceMethod in serviceMethods)
        {
            CreateActionMethod(typeBuilder, serviceMethod, serviceField);
        }
    }

    /// <summary>
    /// 创建动作方法
    /// </summary>
    private static void CreateActionMethod(TypeBuilder typeBuilder, MethodInfo serviceMethod, FieldBuilder serviceField)
    {
        var parameters = serviceMethod.GetParameters();
        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

        var methodBuilder = typeBuilder.DefineMethod(
            serviceMethod.Name,
            MethodAttributes.Public | MethodAttributes.Virtual,
            serviceMethod.ReturnType,
            parameterTypes);

        // 添加参数名称
        for (var i = 0; i < parameters.Length; i++)
        {
            methodBuilder.DefineParameter(i + 1, parameters[i].Attributes, parameters[i].Name);
        }

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
    /// 清除缓存
    /// </summary>
    public static void ClearCache()
    {
        ControllerTypeCache.Clear();
    }
}

