#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OriginalMethodAttribute
// Guid:8d3e3b1c-9f3a-4e5d-8d7a-1c5e3b1c9f3a
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2025-01-26 下午 04:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.Attributes;

/// <summary>
/// 原始方法标记特性
/// 用于标记动态生成的控制器方法对应的原始服务方法
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class OriginalMethodAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodName">方法名称</param>
    /// <param name="parameterTypes">参数类型</param>
    public OriginalMethodAttribute(Type serviceType, string methodName, Type[] parameterTypes)
    {
        ServiceType = serviceType;
        MethodName = methodName;
        ParameterTypes = parameterTypes;
    }

    /// <summary>
    /// 服务类型
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// 方法名称
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// 参数类型
    /// </summary>
    public Type[] ParameterTypes { get; }
}
