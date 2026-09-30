// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.SqlSugar.Entities;

namespace XiHan.Framework.Authorization.SqlSugar.Mapping;

/// <summary>
/// 策略定义与实体的映射
/// </summary>
/// <remarks>
/// 不映射 <see cref="PolicyDefinition.CustomRequirements"/>。
/// </remarks>
public static class PolicyMapper
{
    /// <summary>
    /// 策略定义映射为实体
    /// </summary>
    /// <param name="definition">策略定义</param>
    /// <param name="basicId">主键</param>
    /// <returns>策略实体</returns>
    /// <exception cref="ArgumentException">要求集合为空引用</exception>
    public static SysAuthzPolicy ToEntity(PolicyDefinition definition, long basicId)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new SysAuthzPolicy(basicId)
        {
            PolicyName = definition.Name,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            RequiredRoles = JsonColumn.SerializeOrNull(definition.RequiredRoles)
                ?? throw new ArgumentException("策略的 RequiredRoles 不能为空引用", nameof(definition)),
            RequiredPermissions = JsonColumn.SerializeOrNull(definition.RequiredPermissions)
                ?? throw new ArgumentException("策略的 RequiredPermissions 不能为空引用", nameof(definition)),
            RequiredClaims = JsonColumn.SerializeOrNull(definition.RequiredClaims)
                ?? throw new ArgumentException("策略的 RequiredClaims 不能为空引用", nameof(definition)),
            IsEnabled = definition.IsEnabled,
            Properties = JsonColumn.SerializeOrNull(definition.Properties)
        };
    }

    /// <summary>
    /// 实体映射为策略定义
    /// </summary>
    /// <remarks>
    /// 集合列为空时返回空集合。
    /// </remarks>
    /// <param name="entity">策略实体</param>
    /// <returns>策略定义</returns>
    public static PolicyDefinition ToDefinition(SysAuthzPolicy entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new PolicyDefinition(entity.PolicyName, entity.DisplayName, entity.Description)
        {
            RequiredRoles = JsonColumn.DeserializeOrNull<List<string>>(entity.RequiredRoles) ?? [],
            RequiredPermissions = JsonColumn.DeserializeOrNull<List<string>>(entity.RequiredPermissions) ?? [],
            RequiredClaims = JsonColumn.DeserializeOrNull<Dictionary<string, string>>(entity.RequiredClaims) ?? [],
            IsEnabled = entity.IsEnabled,
            Properties = JsonColumn.DeserializeOrNull<Dictionary<string, object>>(entity.Properties)
        };
    }
}
