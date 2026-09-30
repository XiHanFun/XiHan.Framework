// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.SqlSugar.Entities;

namespace XiHan.Framework.Authorization.SqlSugar.Mapping;

/// <summary>
/// 角色定义与实体的映射
/// </summary>
public static class RoleMapper
{
    /// <summary>
    /// 角色定义映射为实体
    /// </summary>
    /// <param name="definition">角色定义</param>
    /// <param name="basicId">主键</param>
    /// <returns>角色实体</returns>
    public static SysAuthzRole ToEntity(RoleDefinition definition, long basicId)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new SysAuthzRole(basicId)
        {
            RoleId = definition.Id,
            RoleName = definition.Name,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            IsEnabled = definition.IsEnabled,
            IsDefault = definition.IsDefault,
            IsStatic = definition.IsStatic,
            SortOrder = definition.Order,
            CreatedTime = definition.CreatedTime,
            LastModifiedTime = definition.LastModifiedTime,
            Properties = JsonColumn.SerializeOrNull(definition.Properties)
        };
    }

    /// <summary>
    /// 实体映射为角色定义
    /// </summary>
    /// <remarks>
    /// 创建时间与修改时间标记为 UTC。
    /// </remarks>
    /// <param name="entity">角色实体</param>
    /// <returns>角色定义</returns>
    public static RoleDefinition ToDefinition(SysAuthzRole entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new RoleDefinition(entity.RoleId, entity.RoleName, entity.DisplayName, entity.Description)
        {
            IsEnabled = entity.IsEnabled,
            IsDefault = entity.IsDefault,
            IsStatic = entity.IsStatic,
            Order = entity.SortOrder,
            CreatedTime = AsUtc(entity.CreatedTime),
            LastModifiedTime = entity.LastModifiedTime.HasValue ? AsUtc(entity.LastModifiedTime.Value) : null,
            Properties = JsonColumn.DeserializeOrNull<Dictionary<string, object>>(entity.Properties)
        };
    }

    /// <summary>
    /// 把时间标记为 UTC，不改变刻度
    /// </summary>
    /// <param name="value">时间</param>
    /// <returns>标记为 UTC 的时间</returns>
    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
