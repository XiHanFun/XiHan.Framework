#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkHelper
// Guid:80046b25-b091-471f-b381-0ac928d5731f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 20:23:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Attributes;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元帮助类
/// </summary>
public static class UnitOfWorkHelper
{
    /// <summary>
    /// 是否工作单元类型
    /// </summary>
    /// <param name="implementationType"></param>
    /// <returns></returns>
    public static bool IsUnitOfWorkType(TypeInfo implementationType)
    {
        // 明确定义了 UnitOfWorkAttribute
        if (HasUnitOfWorkAttribute(implementationType) || AnyMethodHasUnitOfWorkAttribute(implementationType))
        {
            return true;
        }

        // 传统类
        if (typeof(IUnitOfWorkEnabled).GetTypeInfo().IsAssignableFrom(implementationType))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 是否工作单元方法
    /// </summary>
    /// <param name="methodInfo"></param>
    /// <param name="unitOfWorkAttribute"></param>
    /// <returns></returns>
    public static bool IsUnitOfWorkMethod([NotNull] MethodInfo methodInfo, out UnitOfWorkAttribute? unitOfWorkAttribute)
    {
        CheckHelper.NotNull(methodInfo, nameof(methodInfo));

        // 方法声明
        var attrs = methodInfo.GetCustomAttributes(true).OfType<UnitOfWorkAttribute>().ToArray();
        if (attrs.Length != 0)
        {
            unitOfWorkAttribute = attrs.First();
            return !unitOfWorkAttribute.IsDisabled;
        }

        if (methodInfo.DeclaringType != null)
        {
            // 类声明
            attrs = methodInfo.DeclaringType.GetTypeInfo().GetCustomAttributes(true).OfType<UnitOfWorkAttribute>().ToArray();
            if (attrs.Length != 0)
            {
                unitOfWorkAttribute = attrs.First();
                return !unitOfWorkAttribute.IsDisabled;
            }

            // 传统类
            if (typeof(IUnitOfWorkEnabled).GetTypeInfo().IsAssignableFrom(methodInfo.DeclaringType))
            {
                unitOfWorkAttribute = null;
                return true;
            }
        }

        unitOfWorkAttribute = null;
        return false;
    }

    /// <summary>
    /// 获取工作单元特性
    /// </summary>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    public static UnitOfWorkAttribute? GetUnitOfWorkAttributeOrNull(MethodInfo methodInfo)
    {
        var attrs = methodInfo.GetCustomAttributes(true).OfType<UnitOfWorkAttribute>().ToArray();
        if (attrs.Length > 0)
        {
            return attrs[0];
        }

        if (methodInfo.DeclaringType != null)
        {
            attrs = methodInfo.DeclaringType.GetTypeInfo().GetCustomAttributes(true).OfType<UnitOfWorkAttribute>().ToArray();
            if (attrs.Length > 0)
            {
                return attrs[0];
            }
        }

        return null;
    }

    /// <summary>
    /// 是否有工作单元特性
    /// </summary>
    /// <param name="implementationType"></param>
    /// <returns></returns>
    private static bool AnyMethodHasUnitOfWorkAttribute(TypeInfo implementationType)
    {
        return implementationType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(HasUnitOfWorkAttribute);
    }

    /// <summary>
    /// 是否有工作单元特性
    /// </summary>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    private static bool HasUnitOfWorkAttribute(MemberInfo methodInfo)
    {
        return methodInfo.IsDefined(typeof(UnitOfWorkAttribute), true);
    }
}
