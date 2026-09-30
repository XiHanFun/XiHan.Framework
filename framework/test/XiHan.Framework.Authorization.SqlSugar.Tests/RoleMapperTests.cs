// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.SqlSugar.Entities;
using XiHan.Framework.Authorization.SqlSugar.Mapping;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 角色映射器测试
/// </summary>
public class RoleMapperTests
{
    /// <summary>
    /// 角色定义映射为实体时逐字段对应
    /// </summary>
    [Fact]
    public void 角色定义映射为实体时逐字段对应()
    {
        var createdTime = new DateTime(2026, 9, 28, 1, 2, 3, DateTimeKind.Utc);
        var definition = new RoleDefinition("r1", "editor", "编辑", "可编辑内容")
        {
            IsEnabled = false,
            IsDefault = true,
            IsStatic = true,
            Order = 5,
            CreatedTime = createdTime,
            LastModifiedTime = createdTime.AddHours(1)
        };

        var entity = RoleMapper.ToEntity(definition, 42L);

        Assert.Equal(42L, entity.BasicId);
        Assert.Equal("r1", entity.RoleId);
        Assert.Equal("editor", entity.RoleName);
        Assert.Equal("编辑", entity.DisplayName);
        Assert.Equal("可编辑内容", entity.Description);
        Assert.False(entity.IsEnabled);
        Assert.True(entity.IsDefault);
        Assert.True(entity.IsStatic);
        Assert.Equal(5, entity.SortOrder);
        Assert.Equal(createdTime, entity.CreatedTime);
        Assert.Equal(createdTime.AddHours(1), entity.LastModifiedTime);
        Assert.Null(entity.Properties);
    }

    /// <summary>
    /// 实体映射回角色定义时逐字段对应且时间标记为 UTC
    /// </summary>
    [Fact]
    public void 实体映射回角色定义时逐字段对应且时间标记为UTC()
    {
        var stored = new DateTime(2026, 9, 28, 1, 2, 3, DateTimeKind.Unspecified);
        var entity = new SysAuthzRole(1L)
        {
            RoleId = "r1",
            RoleName = "editor",
            DisplayName = "编辑",
            Description = "可编辑内容",
            IsEnabled = true,
            IsDefault = true,
            IsStatic = false,
            SortOrder = 5,
            CreatedTime = stored,
            LastModifiedTime = stored.AddHours(1)
        };

        var definition = RoleMapper.ToDefinition(entity);

        Assert.Equal("r1", definition.Id);
        Assert.Equal("editor", definition.Name);
        Assert.Equal("编辑", definition.DisplayName);
        Assert.Equal("可编辑内容", definition.Description);
        Assert.True(definition.IsEnabled);
        Assert.True(definition.IsDefault);
        Assert.False(definition.IsStatic);
        Assert.Equal(5, definition.Order);
        Assert.Equal(DateTimeKind.Utc, definition.CreatedTime.Kind);
        Assert.Equal(stored.Ticks, definition.CreatedTime.Ticks);
        Assert.NotNull(definition.LastModifiedTime);
        Assert.Equal(DateTimeKind.Utc, definition.LastModifiedTime.Value.Kind);
        Assert.Equal(stored.AddHours(1).Ticks, definition.LastModifiedTime.Value.Ticks);
    }

    /// <summary>
    /// 未修改过的角色读回时修改时间为空
    /// </summary>
    [Fact]
    public void 未修改过的角色读回时修改时间为空()
    {
        var entity = new SysAuthzRole(1L)
        {
            RoleId = "r1",
            RoleName = "editor",
            DisplayName = "编辑",
            CreatedTime = DateTime.UtcNow
        };

        Assert.Null(RoleMapper.ToDefinition(entity).LastModifiedTime);
    }
}
