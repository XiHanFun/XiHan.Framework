// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.SqlSugar.Entities;
using XiHan.Framework.Authorization.SqlSugar.Permissions;
using XiHan.Framework.Authorization.SqlSugar.Roles;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 权限检查器测试
/// </summary>
/// <remarks>
/// 造数：u1 在启用角色 editor（授予 P1、P5）与禁用角色 ghost（授予 P2）中，直接授予 P3、P4；
/// P4、P5 的定义已禁用；P6 有定义但未授予；PX 无定义。
/// </remarks>
public class PermissionCheckerTests
{
    /// <summary>
    /// 判定单个权限
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <param name="expected">期望结果</param>
    [Theory]
    [InlineData("P1", true)]
    [InlineData("P2", false)]
    [InlineData("P3", true)]
    [InlineData("P4", false)]
    [InlineData("P5", false)]
    [InlineData("P6", false)]
    [InlineData("PX", false)]
    public async Task 判定单个权限(string permissionName, bool expected)
    {
        using var context = new AuthorizationTestContext();
        await SeedAsync(context);

        Assert.Equal(expected, await context.CreatePermissionChecker().IsGrantedAsync("u1", permissionName));
    }

    /// <summary>
    /// 与默认检查器在同一份数据上的判定一致
    /// </summary>
    [Fact]
    public async Task 与默认检查器判定一致()
    {
        using var context = new AuthorizationTestContext();
        await SeedAsync(context);

        var expected = new DefaultPermissionChecker(context.CreatePermissionStore(), context.CreateRoleStore());
        var actual = context.CreatePermissionChecker();

        foreach (var name in new[] { "P1", "P2", "P3", "P4", "P5", "P6", "PX" })
        {
            Assert.Equal(await expected.IsGrantedAsync("u1", name), await actual.IsGrantedAsync("u1", name));
        }

        Assert.Equal(
            (await expected.GetGrantedPermissionsAsync("u1")).Order(StringComparer.Ordinal),
            (await actual.GetGrantedPermissionsAsync("u1")).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// 任意一个与全部权限的判定
    /// </summary>
    [Fact]
    public async Task 任意一个与全部权限的判定()
    {
        using var context = new AuthorizationTestContext();
        await SeedAsync(context);
        var checker = context.CreatePermissionChecker();

        Assert.True(await checker.IsAnyGrantedAsync("u1", ["P2", "P1"]));
        Assert.False(await checker.IsAnyGrantedAsync("u1", ["P2", "P6"]));
        Assert.False(await checker.IsAnyGrantedAsync("u1", []));

        Assert.True(await checker.IsAllGrantedAsync("u1", ["P1", "P3"]));
        Assert.False(await checker.IsAllGrantedAsync("u1", ["P1", "P2"]));
        Assert.False(await checker.IsAllGrantedAsync("u1", ["P1", ""]));
        Assert.False(await checker.IsAllGrantedAsync("u1", []));
    }

    /// <summary>
    /// 获取已授予权限合并直接与角色授予且去重
    /// </summary>
    [Fact]
    public async Task 获取已授予权限合并直接与角色授予且去重()
    {
        using var context = new AuthorizationTestContext();
        var (permissions, _) = await SeedAsync(context);
        await permissions.GrantPermissionToUserAsync("u1", "P1");

        var granted = (await context.CreatePermissionChecker().GetGrantedPermissionsAsync("u1"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "P1", "P3" }, granted);
    }

    /// <summary>
    /// 空用户或空权限一律判定为无
    /// </summary>
    [Fact]
    public async Task 空用户或空权限一律判定为无()
    {
        using var context = new AuthorizationTestContext();
        await SeedAsync(context);
        var checker = context.CreatePermissionChecker();

        Assert.False(await checker.IsGrantedAsync("", "P1"));
        Assert.False(await checker.IsGrantedAsync("u1", ""));
        Assert.False(await checker.IsAnyGrantedAsync("", ["P1"]));
        Assert.False(await checker.IsAllGrantedAsync("", ["P1"]));
        Assert.Empty(await checker.GetGrantedPermissionsAsync(""));
    }

    /// <summary>
    /// 权限是否存在不看启用状态
    /// </summary>
    [Fact]
    public async Task 权限是否存在不看启用状态()
    {
        using var context = new AuthorizationTestContext();
        await SeedAsync(context);
        var checker = context.CreatePermissionChecker();

        Assert.True(await checker.PermissionExistsAsync("P4"));
        Assert.False(await checker.PermissionExistsAsync("PX"));
        Assert.False(await checker.PermissionExistsAsync(""));
    }

    /// <summary>
    /// 直接授予命中时只执行一条 SQL
    /// </summary>
    [Fact]
    public async Task 直接授予命中时只执行一条SQL()
    {
        using var context = new AuthorizationTestContext();
        await SeedAsync(context);
        var checker = context.CreatePermissionChecker();

        var sqlCount = 0;
        context.Client.Aop.OnLogExecuting = (_, _) => sqlCount++;

        Assert.True(await checker.IsGrantedAsync("u1", "P3"));
        Assert.Equal(1, sqlCount);
    }

    /// <summary>
    /// 判定与角色数、权限数无关，至多执行两条 SQL
    /// </summary>
    [Fact]
    public async Task 判定与角色数权限数无关至多执行两条SQL()
    {
        using var context = new AuthorizationTestContext();
        var (permissions, roles) = await SeedAsync(context);

        for (var index = 0; index < 5; index++)
        {
            await roles.CreateRoleAsync(new RoleDefinition($"extra{index}", $"extra{index}", "附加"));
            await roles.AddUserToRoleAsync("u1", $"extra{index}");
            await permissions.GrantPermissionToRoleAsync($"extra{index}", "P6");
        }

        var checker = context.CreatePermissionChecker();
        var sqlCount = 0;
        context.Client.Aop.OnLogExecuting = (_, _) => sqlCount++;

        Assert.True(await checker.IsGrantedAsync("u1", "P1"));
        Assert.Equal(2, sqlCount);

        sqlCount = 0;
        Assert.True(await checker.IsAllGrantedAsync("u1", ["P1", "P3", "P6"]));
        Assert.Equal(2, sqlCount);

        sqlCount = 0;
        await checker.GetGrantedPermissionsAsync("u1");
        Assert.Equal(2, sqlCount);
    }

    /// <summary>
    /// 关联行、角色行、角色权限行租户不一致时不授予
    /// </summary>
    [Fact]
    public async Task 关联行与角色行租户不一致时不授予()
    {
        using var context = new AuthorizationTestContext();
        var checker = context.CreatePermissionChecker();

        await InsertPermissionAsync(context, "T1");
        await InsertPermissionAsync(context, "T2");

        await InsertRoleAsync(context, 2, "r9");
        await InsertUserRoleAsync(context, 1, "u9", "r9");
        await InsertRolePermissionAsync(context, 2, "r9", "T1");

        await InsertRoleAsync(context, 1, "r8");
        await InsertUserRoleAsync(context, 1, "u8", "r8");
        await InsertRolePermissionAsync(context, 2, "r8", "T1");
        await InsertRolePermissionAsync(context, 1, "r8", "T2");

        context.CurrentTenant.Id = 1;

        Assert.False(await checker.IsGrantedAsync("u9", "T1"));
        Assert.False(await checker.IsGrantedAsync("u8", "T1"));
        Assert.True(await checker.IsGrantedAsync("u8", "T2"));
    }

    /// <summary>
    /// 造数
    /// </summary>
    private static async Task<(SqlSugarPermissionStore Permissions, SqlSugarRoleStore Roles)> SeedAsync(AuthorizationTestContext context)
    {
        var permissions = context.CreatePermissionStore();
        var roles = context.CreateRoleStore();

        await permissions.AddPermissionsAsync(
        [
            new PermissionDefinition("P1", "经启用角色"),
            new PermissionDefinition("P2", "经禁用角色"),
            new PermissionDefinition("P3", "直接授予"),
            new PermissionDefinition("P4", "直接授予但定义禁用") { IsEnabled = false },
            new PermissionDefinition("P5", "经启用角色但定义禁用") { IsEnabled = false },
            new PermissionDefinition("P6", "未授予")
        ]);

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.CreateRoleAsync(new RoleDefinition("r2", "ghost", "停用") { IsEnabled = false });
        await roles.AddUserToRoleAsync("u1", "editor");
        await roles.AddUserToRoleAsync("u1", "ghost");

        await permissions.GrantPermissionToRoleAsync("r1", "P1");
        await permissions.GrantPermissionToRoleAsync("r1", "P5");
        await permissions.GrantPermissionToRoleAsync("r2", "P2");
        await permissions.GrantPermissionToUserAsync("u1", "P3");
        await permissions.GrantPermissionToUserAsync("u1", "P4");

        return (permissions, roles);
    }

    private static async Task InsertPermissionAsync(AuthorizationTestContext context, string name)
    {
        await context.Client.Insertable(new SysAuthzPermission(context.IdGenerator.NextId())
        {
            PermissionName = name,
            DisplayName = name,
            IsEnabled = true
        }).ExecuteCommandAsync();
    }

    private static async Task InsertRoleAsync(AuthorizationTestContext context, long tenantId, string roleId)
    {
        await context.Client.Insertable(new SysAuthzRole(context.IdGenerator.NextId())
        {
            TenantId = tenantId,
            RoleId = roleId,
            RoleName = roleId,
            DisplayName = roleId,
            IsEnabled = true,
            CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
    }

    private static async Task InsertUserRoleAsync(AuthorizationTestContext context, long tenantId, string userId, string roleId)
    {
        await context.Client.Insertable(new SysAuthzUserRole(context.IdGenerator.NextId())
        {
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId
        }).ExecuteCommandAsync();
    }

    private static async Task InsertRolePermissionAsync(AuthorizationTestContext context, long tenantId, string roleId, string permissionName)
    {
        await context.Client.Insertable(new SysAuthzRolePermission(context.IdGenerator.NextId())
        {
            TenantId = tenantId,
            RoleId = roleId,
            PermissionName = permissionName
        }).ExecuteCommandAsync();
    }
}
