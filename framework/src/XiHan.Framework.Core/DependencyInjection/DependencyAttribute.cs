// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 依赖特性
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class DependencyAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DependencyAttribute()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="lifetime"></param>
    public DependencyAttribute(ServiceLifetime lifetime)
    {
        Lifetime = lifetime;
    }

    /// <summary>
    /// 生命周期
    /// </summary>
    public virtual ServiceLifetime? Lifetime { get; set; }

    /// <summary>
    /// 是否尝试注册
    /// </summary>
    public virtual bool TryRegister { get; set; }

    /// <summary>
    /// 是否替换服务
    /// </summary>
    public virtual bool ReplaceServices { get; set; }
}
