#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExposeServicesAttribute
// Guid:28012959-fd57-43ec-90df-715812cbb5f7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:42:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Text;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 暴露服务特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ExposeServicesAttribute : Attribute, IExposedServiceTypesProvider
{
    private const string DefaultInterfaceNameInitial = "I";

    /// <summary>
    /// 服务类型
    /// </summary>
    public Type[] ServiceTypes { get; }

    /// <summary>
    /// 是否包含默认服务
    /// </summary>
    public bool IncludeDefaults { get; init; }

    /// <summary>
    /// 是否包含自身
    /// </summary>
    public bool IncludeSelf { get; init; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceTypes"></param>
    public ExposeServicesAttribute(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }

    /// <summary>
    /// 获取暴露的服务类型
    /// </summary>
    /// <param name="targetType"></param>
    /// <returns></returns>
    public Type[] GetExposedServiceTypes(Type targetType)
    {
        var serviceList = ServiceTypes.ToList();

        if (IncludeDefaults)
        {
            foreach (var type in GetDefaultServices(targetType))
            {
                _ = serviceList.AddIfNotContains(type);
            }

            if (IncludeSelf)
            {
                _ = serviceList.AddIfNotContains(targetType);
            }
        }
        else if (IncludeSelf)
        {
            _ = serviceList.AddIfNotContains(targetType);
        }

        return [.. serviceList];
    }

    /// <summary>
    /// 获取默认服务
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static List<Type> GetDefaultServices(Type type)
    {
        List<Type> serviceTypes = [];

        foreach (var interfaceType in type.GetTypeInfo().GetInterfaces())
        {
            var interfaceName = interfaceType.Name;
            if (interfaceType.IsGenericType)
            {
                interfaceName = interfaceType.Name.Left(interfaceType.Name.IndexOf('`'));
            }

            if (interfaceName.StartsWith(DefaultInterfaceNameInitial))
            {
                interfaceName = interfaceName.Right(interfaceName.Length - 1);
            }

            if (type.Name.EndsWith(interfaceName))
            {
                serviceTypes.Add(interfaceType);
            }
        }

        return serviceTypes;
    }
}
