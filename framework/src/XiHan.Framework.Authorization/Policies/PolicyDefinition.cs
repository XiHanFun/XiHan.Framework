#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PolicyDefinition
// Guid:a5b6c7d8-e9f0-1234-8901-123456789034
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.Policies;

/// <summary>
/// 策略定义
/// </summary>
public class PolicyDefinition
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public PolicyDefinition()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">策略名称</param>
    /// <param name="displayName">显示名称</param>
    /// <param name="description">描述</param>
    public PolicyDefinition(string name, string displayName, string? description = null)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>
    /// 策略名称（唯一标识）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 要求的角色列表（任意一个角色即可）
    /// </summary>
    public List<string> RequiredRoles { get; set; } = [];

    /// <summary>
    /// 要求的权限列表（所有权限都必须有）
    /// </summary>
    public List<string> RequiredPermissions { get; set; } = [];

    /// <summary>
    /// 要求的声明列表
    /// </summary>
    public Dictionary<string, string> RequiredClaims { get; set; } = [];

    /// <summary>
    /// 自定义要求列表
    /// </summary>
    public List<IAuthorizationRequirement> CustomRequirements { get; set; } = [];

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 额外属性
    /// </summary>
    public Dictionary<string, object>? Properties { get; set; }
}
