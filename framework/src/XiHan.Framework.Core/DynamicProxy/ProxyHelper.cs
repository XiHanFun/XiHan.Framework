#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxyHelper
// Guid:af0fb8a5-355b-4ca0-97ed-645b385e1f5a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 20:13:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Core.DynamicProxy;

/// <summary>
/// 动态代理帮助类
/// </summary>
public static class ProxyHelper
{
    // Castle动态代理的命名空间
    private const string ProxyNamespace = "Castle.Proxies";

    /// <summary>
    /// 是否为代理对象
    /// </summary>
    public static bool IsProxy(object obj)
    {
        return obj.GetType().Namespace == ProxyNamespace;
    }

    /// <summary>
    /// 获取代理对象的目标对象
    /// 它支持 Castle 动态代理
    /// </summary>
    public static object UnProxy(object obj)
    {
        if (obj.GetType().Namespace != ProxyNamespace)
        {
            return obj;
        }

        var targetField = obj.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(f => f.Name == "__target");

        return targetField == null ? obj : targetField.GetValue(obj)!;
    }

    /// <summary>
    /// 获取未代理的类型
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static Type GetUnProxiedType(object obj)
    {
        if (obj.GetType().Namespace == ProxyNamespace)
        {
            var target = UnProxy(obj);
            return target == obj ? obj.GetType().GetTypeInfo().BaseType! : target.GetType();
        }

        return obj.GetType();
    }
}
