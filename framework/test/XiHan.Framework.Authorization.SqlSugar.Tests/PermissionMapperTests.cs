// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.SqlSugar.Entities;
using XiHan.Framework.Authorization.SqlSugar.Mapping;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 权限映射器测试
/// </summary>
public class PermissionMapperTests
{
    /// <summary>
    /// 权限定义映射为实体时逐字段对应
    /// </summary>
    [Fact]
    public void 权限定义映射为实体时逐字段对应()
    {
        var definition = new PermissionDefinition("User.Create", "创建用户", "允许创建用户")
        {
            ParentName = "User",
            Tag = "用户",
            IsEnabled = false,
            Order = 7
        };

        var entity = PermissionMapper.ToEntity(definition, 42L);

        Assert.Equal(42L, entity.BasicId);
        Assert.Equal("User.Create", entity.PermissionName);
        Assert.Equal("创建用户", entity.DisplayName);
        Assert.Equal("允许创建用户", entity.Description);
        Assert.Equal("User", entity.ParentName);
        Assert.Equal("用户", entity.Tag);
        Assert.False(entity.IsEnabled);
        Assert.Equal(7, entity.SortOrder);
        Assert.Null(entity.Properties);
    }

    /// <summary>
    /// 实体映射回权限定义时逐字段对应
    /// </summary>
    [Fact]
    public void 实体映射回权限定义时逐字段对应()
    {
        var entity = new SysAuthzPermission(1L)
        {
            PermissionName = "User.Delete",
            DisplayName = "删除用户",
            Description = "允许删除用户",
            ParentName = "User",
            Tag = "用户",
            IsEnabled = true,
            SortOrder = 9
        };

        var definition = PermissionMapper.ToDefinition(entity);

        Assert.Equal("User.Delete", definition.Name);
        Assert.Equal("删除用户", definition.DisplayName);
        Assert.Equal("允许删除用户", definition.Description);
        Assert.Equal("User", definition.ParentName);
        Assert.Equal("用户", definition.Tag);
        Assert.True(definition.IsEnabled);
        Assert.Equal(9, definition.Order);
        Assert.Null(definition.Properties);
    }

    /// <summary>
    /// 扩展属性以 JSON 往返，值读回为 JsonElement
    /// </summary>
    [Fact]
    public void 扩展属性以JSON往返且值读回为JsonElement()
    {
        var definition = new PermissionDefinition("A", "甲")
        {
            Properties = new Dictionary<string, object>
            {
                ["level"] = 3,
                ["label"] = "敏感"
            }
        };

        var entity = PermissionMapper.ToEntity(definition, 1L);

        Assert.False(string.IsNullOrWhiteSpace(entity.Properties));

        var restored = PermissionMapper.ToDefinition(entity);

        Assert.NotNull(restored.Properties);
        Assert.Equal(3, Assert.IsType<JsonElement>(restored.Properties["level"]).GetInt32());
        Assert.Equal("敏感", Assert.IsType<JsonElement>(restored.Properties["label"]).GetString());
    }
}
