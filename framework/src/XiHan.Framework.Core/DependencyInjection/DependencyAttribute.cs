#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DependencyAttribute
// Guid:44e20997-029f-44eb-8b3c-1db93711b7f2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:41:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 依赖特性
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class DependencyAttribute : Attribute
{
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
}
