#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RemoteServiceAttribute
// Guid:00062b69-fe81-418b-8aff-e4bfae1c8700
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 04:09:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 远程服务特性
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Method)]
public class RemoteServiceAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isEnabled"></param>
    public RemoteServiceAttribute(bool isEnabled = true)
    {
        IsEnabled = isEnabled;
        IsMetadataEnabled = true;
    }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 是否启用元数据
    /// </summary>
    public bool IsMetadataEnabled { get; set; }

    /// <summary>
    /// 远程服务的组名
    /// 一个模块的所有服务的组名预计应相同
    /// 此名称也用于区分该组的服务端点
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 是否明确启用
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsExplicitlyEnabledFor(Type type)
    {
        var remoteServiceAttr = type.GetTypeInfo().GetSingleAttributeOrNull<RemoteServiceAttribute>();
        return remoteServiceAttr is not null && remoteServiceAttr.IsEnabledFor(type);
    }

    /// <summary>
    /// 是否明确禁用
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsExplicitlyDisabledFor(Type type)
    {
        var remoteServiceAttr = type.GetTypeInfo().GetSingleAttributeOrNull<RemoteServiceAttribute>();
        return remoteServiceAttr is not null && !remoteServiceAttr.IsEnabledFor(type);
    }

    /// <summary>
    /// 是否明确启用元数据
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsMetadataExplicitlyEnabledFor(Type type)
    {
        var remoteServiceAttr = type.GetTypeInfo().GetSingleAttributeOrNull<RemoteServiceAttribute>();
        return remoteServiceAttr is not null && remoteServiceAttr.IsMetadataEnabledFor(type);
    }

    /// <summary>
    /// 是否明确禁用元数据
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsMetadataExplicitlyDisabledFor(Type type)
    {
        var remoteServiceAttr = type.GetTypeInfo().GetSingleAttributeOrNull<RemoteServiceAttribute>();
        return remoteServiceAttr is not null && !remoteServiceAttr.IsMetadataEnabledFor(type);
    }

    /// <summary>
    /// 是否明确启用元数据
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public static bool IsMetadataExplicitlyEnabledFor(MethodInfo method)
    {
        var remoteServiceAttr = method.GetSingleAttributeOrNull<RemoteServiceAttribute>();
        return remoteServiceAttr is not null && remoteServiceAttr.IsMetadataEnabledFor(method);
    }

    /// <summary>
    /// 是否明确禁用元数据
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public static bool IsMetadataExplicitlyDisabledFor(MethodInfo method)
    {
        var remoteServiceAttr = method.GetSingleAttributeOrNull<RemoteServiceAttribute>();
        return remoteServiceAttr is not null && !remoteServiceAttr.IsMetadataEnabledFor(method);
    }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public virtual bool IsEnabledFor(Type type)
    {
        return IsEnabled;
    }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public virtual bool IsEnabledFor(MethodInfo method)
    {
        return IsEnabled;
    }

    /// <summary>
    /// 是否启用元数据
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public virtual bool IsMetadataEnabledFor(Type type)
    {
        return IsMetadataEnabled;
    }

    /// <summary>
    /// 是否启用元数据
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public virtual bool IsMetadataEnabledFor(MethodInfo method)
    {
        return IsMetadataEnabled;
    }
}
