#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiAttribute
// Guid:e5f6g7h8-i9j0-4k1l-2m3n-4o5p6q7r8s9t
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Attributes;

/// <summary>
/// 动态 API 特性
/// 标记类或方法以配置动态 API 行为
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class DynamicApiAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiAttribute()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isEnabled">是否启用</param>
    public DynamicApiAttribute(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// 路由模板
    /// </summary>
    public string? RouteTemplate { get; set; }

    /// <summary>
    /// API 名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 是否启用动态 API
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// API 版本
    /// </summary>
    public string? Version { get; set; }
}
