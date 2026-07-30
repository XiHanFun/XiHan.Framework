// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
