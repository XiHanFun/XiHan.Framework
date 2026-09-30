// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.SqlSugar.Entities;

namespace XiHan.Framework.Authorization.SqlSugar.Mapping;

/// <summary>
/// 权限定义与实体的映射
/// </summary>
public static class PermissionMapper
{
    /// <summary>
    /// 权限定义映射为实体
    /// </summary>
    /// <param name="definition">权限定义</param>
    /// <param name="basicId">主键</param>
    /// <returns>权限定义实体</returns>
    public static SysAuthzPermission ToEntity(PermissionDefinition definition, long basicId)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new SysAuthzPermission(basicId)
        {
            PermissionName = definition.Name,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            ParentName = definition.ParentName,
            Tag = definition.Tag,
            IsEnabled = definition.IsEnabled,
            SortOrder = definition.Order,
            Properties = JsonColumn.SerializeOrNull(definition.Properties)
        };
    }

    /// <summary>
    /// 实体映射为权限定义
    /// </summary>
    /// <param name="entity">权限定义实体</param>
    /// <returns>权限定义</returns>
    public static PermissionDefinition ToDefinition(SysAuthzPermission entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new PermissionDefinition(entity.PermissionName, entity.DisplayName, entity.Description)
        {
            ParentName = entity.ParentName,
            Tag = entity.Tag,
            IsEnabled = entity.IsEnabled,
            Order = entity.SortOrder,
            Properties = JsonColumn.DeserializeOrNull<Dictionary<string, object>>(entity.Properties)
        };
    }
}
