﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceIdentifier
// Guid:4dee945f-fd45-48a7-a712-6db53a691bd5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:30:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务标识符
/// </summary>
public readonly struct ServiceIdentifier : IEquatable<ServiceIdentifier>
{
    /// <summary>
    /// 服务 Key
    /// </summary>
    public object? ServiceKey { get; }

    /// <summary>
    /// 服务类型
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceType"></param>
    public ServiceIdentifier(Type serviceType)
    {
        ServiceType = serviceType;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceKey"></param>
    /// <param name="serviceType"></param>
    public ServiceIdentifier(object? serviceKey, Type serviceType)
    {
        ServiceKey = serviceKey;
        ServiceType = serviceType;
    }

    /// <summary>
    /// 是否相等
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(ServiceIdentifier other)
    {
        return ServiceKey == null && other.ServiceKey == null
            ? ServiceType == other.ServiceType
            : ServiceKey != null && other.ServiceKey != null && ServiceType == other.ServiceType && ServiceKey.Equals(other.ServiceKey);
    }

    /// <summary>
    /// 是否相等
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj)
    {
        return obj is ServiceIdentifier identifier && Equals(identifier);
    }

    /// <summary>
    /// 获取 HashCode
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        if (ServiceKey == null)
        {
            return ServiceType.GetHashCode();
        }

        unchecked
        {
            return ServiceType.GetHashCode() * 397 ^ ServiceKey.GetHashCode();
        }
    }

    /// <summary>
    /// 相等操作符
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator ==(ServiceIdentifier left, ServiceIdentifier right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// 不相等操作符
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator !=(ServiceIdentifier left, ServiceIdentifier right)
    {
        return !(left == right);
    }
}
