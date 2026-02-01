#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RoleDefinition
// Guid:c1d2e3f4-a5b6-7890-4567-123456789030
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.Roles;

/// <summary>
/// 角色定义
/// </summary>
public class RoleDefinition
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public RoleDefinition()
    {
        CreatedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">角色ID</param>
    /// <param name="name">角色名称</param>
    /// <param name="displayName">显示名称</param>
    /// <param name="description">描述</param>
    public RoleDefinition(string id, string name, string displayName, string? description = null)
    {
        Id = id;
        Name = name;
        DisplayName = displayName;
        Description = description;
        CreatedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 角色ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 角色名称（唯一标识）
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
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否为默认角色
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 是否为静态角色（不可删除）
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime? LastModifiedTime { get; set; }

    /// <summary>
    /// 额外属性
    /// </summary>
    public Dictionary<string, object>? Properties { get; set; }
}
