// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.SqlSugar.Entities;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 角色存储的用户角色关联测试
/// </summary>
public class UserRoleTests
{
    /// <summary>
    /// 加入不存在的角色抛异常
    /// </summary>
    [Fact]
    public async Task 加入不存在的角色抛异常()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() => roles.AddUserToRoleAsync("u1", "nope"));
    }

    /// <summary>
    /// 加入角色后可读回用户角色并判定在角色中
    /// </summary>
    [Fact]
    public async Task 加入角色后可读回用户角色并判定在角色中()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.AddUserToRoleAsync("u1", "editor");

        Assert.Equal("r1", Assert.Single(await roles.GetUserRolesAsync("u1")).Id);
        Assert.True(await roles.IsInRoleAsync("u1", "editor"));
        Assert.False(await roles.IsInRoleAsync("u2", "editor"));
    }

    /// <summary>
    /// 重复加入角色只保留一行
    /// </summary>
    [Fact]
    public async Task 重复加入角色只保留一行()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.AddUserToRoleAsync("u1", "editor");
        await roles.AddUserToRoleAsync("u1", "editor");

        Assert.Equal(1, await context.Client.Queryable<SysAuthzUserRole>().CountAsync());
    }

    /// <summary>
    /// 移出角色后不再在角色中，移出不存在的角色不抛异常
    /// </summary>
    [Fact]
    public async Task 移出角色后不再在角色中()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.AddUserToRoleAsync("u1", "editor");
        await roles.RemoveUserFromRoleAsync("u1", "editor");
        await roles.RemoveUserFromRoleAsync("u1", "nope");

        Assert.False(await roles.IsInRoleAsync("u1", "editor"));
        Assert.Empty(await roles.GetUserRolesAsync("u1"));
    }

    /// <summary>
    /// 读取角色中的用户
    /// </summary>
    [Fact]
    public async Task 读取角色中的用户()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.CreateRoleAsync(new RoleDefinition("r2", "viewer", "查看"));
        await roles.AddUserToRoleAsync("u2", "editor");
        await roles.AddUserToRoleAsync("u1", "editor");
        await roles.AddUserToRoleAsync("u3", "viewer");

        var userIds = (await roles.GetUsersInRoleAsync("editor")).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(new[] { "u1", "u2" }, userIds);
    }

    /// <summary>
    /// 角色改名后用户仍在角色中并保有角色权限
    /// </summary>
    [Fact]
    public async Task 角色改名后用户仍在角色中并保有角色权限()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();
        var permissions = context.CreatePermissionStore();

        await permissions.AddOrUpdatePermissionAsync(new PermissionDefinition("P1", "一"));
        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await permissions.GrantPermissionToRoleAsync("r1", "P1");
        await roles.AddUserToRoleAsync("u1", "editor");

        await roles.UpdateRoleAsync(new RoleDefinition("r1", "writer", "写作者"));

        Assert.True(await roles.IsInRoleAsync("u1", "writer"));
        Assert.False(await roles.IsInRoleAsync("u1", "editor"));

        var role = Assert.Single(await roles.GetUserRolesAsync("u1"));

        Assert.Equal("P1", Assert.Single(await permissions.GetRolePermissionsAsync(role.Id)).Name);
    }

    /// <summary>
    /// 判定在角色中与读取用户角色都不看角色是否启用
    /// </summary>
    [Fact]
    public async Task 判定在角色中与读取用户角色都不看角色是否启用()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "ghost", "停用") { IsEnabled = false });
        await roles.AddUserToRoleAsync("u1", "ghost");

        Assert.True(await roles.IsInRoleAsync("u1", "ghost"));
        Assert.False(Assert.Single(await roles.GetUserRolesAsync("u1")).IsEnabled);
    }

    /// <summary>
    /// 关联行与角色行租户不一致时不配对
    /// </summary>
    [Fact]
    public async Task 关联行与角色行租户不一致时不配对()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await context.Client.Insertable(new SysAuthzRole(context.IdGenerator.NextId())
        {
            TenantId = 2,
            RoleId = "r9",
            RoleName = "admin",
            DisplayName = "admin",
            IsEnabled = true,
            CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();

        await context.Client.Insertable(new SysAuthzUserRole(context.IdGenerator.NextId())
        {
            TenantId = 1,
            UserId = "u9",
            RoleId = "r9"
        }).ExecuteCommandAsync();

        context.CurrentTenant.Id = 1;

        Assert.Empty(await roles.GetUserRolesAsync("u9"));
        Assert.False(await roles.IsInRoleAsync("u9", "admin"));
        Assert.Empty(await roles.GetUsersInRoleAsync("admin"));
    }
}
