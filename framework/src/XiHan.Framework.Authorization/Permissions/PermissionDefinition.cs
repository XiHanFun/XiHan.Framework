// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Permissions;

/// <summary>
/// 权限定义
/// </summary>
public class PermissionDefinition
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public PermissionDefinition()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">权限名称</param>
    /// <param name="displayName">显示名称</param>
    /// <param name="description">描述</param>
    public PermissionDefinition(string name, string displayName, string? description = null)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>
    /// 权限名称（唯一标识）
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
    /// 父权限名称
    /// </summary>
    public string? ParentName { get; set; }

    /// <summary>
    /// 分组名称
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 排序
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 额外属性
    /// </summary>
    public Dictionary<string, object>? Properties { get; set; }
}
