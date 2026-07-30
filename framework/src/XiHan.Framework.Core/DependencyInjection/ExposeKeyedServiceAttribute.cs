// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 暴露键值服务特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ExposeKeyedServiceAttribute<TServiceType> : Attribute, IExposedKeyedServiceTypesProvider
    where TServiceType : class
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceKey"></param>
    /// <exception cref="XiHanException"></exception>
    public ExposeKeyedServiceAttribute(object serviceKey)
    {
        if (serviceKey is null)
        {
            throw new XiHanException($"{nameof(serviceKey)} can not be null! Use {nameof(ExposeServicesAttribute)} instead.");
        }

        ServiceIdentifier = new ServiceIdentifier(serviceKey, typeof(TServiceType));
    }

    /// <summary>
    /// 服务标识
    /// </summary>
    public ServiceIdentifier ServiceIdentifier { get; }

    /// <summary>
    /// 获取暴露的服务类型
    /// </summary>
    /// <param name="targetType"></param>
    /// <returns></returns>
    public ServiceIdentifier[] GetExposedServiceTypes(Type targetType)
    {
        return [ServiceIdentifier];
    }
}
