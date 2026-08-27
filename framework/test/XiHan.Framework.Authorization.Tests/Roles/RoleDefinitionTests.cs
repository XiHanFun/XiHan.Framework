// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Authorization.Roles;

namespace XiHan.Framework.Authorization.Tests.Roles;

/// <summary>
/// 角色定义测试
/// </summary>
/// <remarks>
/// 权限检查器只认 <c>IsEnabled</c> 为真的角色，所以默认必须为真；
/// 创建时间由构造函数打戳、最后修改时间由存储在更新时补，二者的初始状态是可观察的契约。
/// </remarks>
public class RoleDefinitionTests
{
    /// <summary>
    /// 无参构造的默认值：启用、非默认、非静态、未修改过
    /// </summary>
    [Fact]
    public void New_ByDefault_UsesSafeDefaults()
    {
        var role = new RoleDefinition();

        Assert.Equal(string.Empty, role.Id);
        Assert.Equal(string.Empty, role.Name);
        Assert.Equal(string.Empty, role.DisplayName);
        Assert.Null(role.Description);
        Assert.True(role.IsEnabled);
        Assert.False(role.IsDefault);
        Assert.False(role.IsStatic);
        Assert.Equal(0, role.Order);
        Assert.Null(role.LastModifiedTime);
        Assert.Null(role.Properties);
    }

    /// <summary>
    /// 构造时打上 UTC 创建时间戳
    /// </summary>
    [Fact]
    public void New_StampsUtcCreatedTime()
    {
        var before = DateTime.UtcNow.AddSeconds(-5);
        var role = new RoleDefinition();
        var after = DateTime.UtcNow.AddSeconds(5);

        Assert.InRange(role.CreatedTime, before, after);
        Assert.Equal(DateTimeKind.Utc, role.CreatedTime.Kind);
    }

    /// <summary>
    /// 四参构造按位置写入标识、名称、显示名与描述
    /// </summary>
    [Fact]
    public void Ctor_WithArguments_AssignsFields()
    {
        var role = new RoleDefinition("r1", "admin", "管理员", "系统管理员");

        Assert.Equal("r1", role.Id);
        Assert.Equal("admin", role.Name);
        Assert.Equal("管理员", role.DisplayName);
        Assert.Equal("系统管理员", role.Description);
        Assert.True(role.IsEnabled);
        Assert.Null(role.LastModifiedTime);
    }

    /// <summary>
    /// 描述参数可省略
    /// </summary>
    [Fact]
    public void Ctor_WithoutDescription_LeavesItNull()
    {
        Assert.Null(new RoleDefinition("r1", "admin", "管理员").Description);
    }

    /// <summary>
    /// 角色定义是引用相等语义
    /// </summary>
    [Fact]
    public void Equality_IsReferenceBased()
    {
        var first = new RoleDefinition("r1", "admin", "管理员");
        var second = new RoleDefinition("r1", "admin", "管理员");

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// JSON 往返后字段名与取值保持不变
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesScalarFields()
    {
        var role = new RoleDefinition("r1", "admin", "administrator", "desc")
        {
            IsEnabled = false,
            IsDefault = true,
            IsStatic = true,
            Order = 3
        };

        var json = JsonSerializer.Serialize(role);
        var restored = JsonSerializer.Deserialize<RoleDefinition>(json);

        Assert.Contains("\"Id\":\"r1\"", json);
        Assert.Contains("\"Name\":\"admin\"", json);
        Assert.NotNull(restored);
        Assert.Equal("r1", restored!.Id);
        Assert.Equal("admin", restored.Name);
        Assert.Equal("administrator", restored.DisplayName);
        Assert.Equal("desc", restored.Description);
        Assert.False(restored.IsEnabled);
        Assert.True(restored.IsDefault);
        Assert.True(restored.IsStatic);
        Assert.Equal(3, restored.Order);
        Assert.Null(restored.LastModifiedTime);
    }
}
