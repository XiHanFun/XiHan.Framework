// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.SqlSugar.Entities;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 删除角色的级联与事务测试
/// </summary>
public class RoleDeletionTests
{
    /// <summary>
    /// 删除角色时一并删除其用户关联与角色权限，不动其他角色
    /// </summary>
    [Fact]
    public async Task 删除角色时一并删除用户关联与角色权限()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();
        var permissions = context.CreatePermissionStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.CreateRoleAsync(new RoleDefinition("r2", "viewer", "查看"));
        await roles.AddUserToRoleAsync("u1", "editor");
        await roles.AddUserToRoleAsync("u2", "editor");
        await roles.AddUserToRoleAsync("u1", "viewer");
        await permissions.GrantPermissionToRoleAsync("r1", "P1");
        await permissions.GrantPermissionToRoleAsync("r2", "P2");

        await roles.DeleteRoleAsync("r1");

        Assert.Null(await roles.GetRoleByIdAsync("r1"));
        Assert.NotNull(await roles.GetRoleByIdAsync("r2"));

        var remainingMember = Assert.Single(await context.Client.Queryable<SysAuthzUserRole>().ToListAsync());
        Assert.Equal("r2", remainingMember.RoleId);

        var remainingGrant = Assert.Single(await context.Client.Queryable<SysAuthzRolePermission>().ToListAsync());
        Assert.Equal("r2", remainingGrant.RoleId);
    }

    /// <summary>
    /// 删除后以同一标识重建角色不继承旧授权
    /// </summary>
    [Fact]
    public async Task 删除后以同一标识重建角色不继承旧授权()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();
        var permissions = context.CreatePermissionStore();

        await permissions.AddOrUpdatePermissionAsync(new PermissionDefinition("P1", "一"));
        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.AddUserToRoleAsync("u1", "editor");
        await permissions.GrantPermissionToRoleAsync("r1", "P1");

        await roles.DeleteRoleAsync("r1");
        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));

        Assert.Empty(await permissions.GetRolePermissionsAsync("r1"));
        Assert.False(await roles.IsInRoleAsync("u1", "editor"));
    }

    /// <summary>
    /// 删除不存在或空标识的角色不抛异常
    /// </summary>
    [Fact]
    public async Task 删除不存在或空标识的角色不抛异常()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.DeleteRoleAsync("nope");
        await roles.DeleteRoleAsync("");

        Assert.Empty(await roles.GetAllRolesAsync());
    }

    /// <summary>
    /// 静态角色同样可以删除
    /// </summary>
    [Fact]
    public async Task 静态角色同样可以删除()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员") { IsStatic = true });
        await roles.DeleteRoleAsync("r1");

        Assert.Null(await roles.GetRoleByIdAsync("r1"));
    }

    /// <summary>
    /// 级联删除中途失败时整体回滚并抛出
    /// </summary>
    [Fact]
    public async Task 级联删除中途失败时整体回滚并抛出()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.AddUserToRoleAsync("u1", "editor");

        context.Client.DbMaintenance.DropTable("sys_authz_role_permission");

        await Assert.ThrowsAnyAsync<Exception>(() => roles.DeleteRoleAsync("r1"));

        Assert.NotNull(await roles.GetRoleByIdAsync("r1"));
        Assert.Equal(1, await context.Client.Queryable<SysAuthzUserRole>().CountAsync());
    }

    /// <summary>
    /// 外层事务回滚时删除一并回滚
    /// </summary>
    [Fact]
    public async Task 外层事务回滚时删除一并回滚()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.AddUserToRoleAsync("u1", "editor");

        context.Client.Ado.BeginTran();
        await roles.DeleteRoleAsync("r1");
        context.Client.Ado.RollbackTran();

        Assert.NotNull(await roles.GetRoleByIdAsync("r1"));
        Assert.Equal(1, await context.Client.Queryable<SysAuthzUserRole>().CountAsync());
    }
}
