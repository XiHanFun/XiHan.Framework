// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.SqlSugar.Entities;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 租户隔离测试
/// </summary>
/// <remarks>
/// 造数：租户 5 与租户 6 各有标识 r1、名称 admin 的角色与用户 u1 的关联；
/// 租户 5 的角色授予 P1、直接授予 P3，租户 6 的角色授予 P2、直接授予 P4；平台租户 0 给 u1 直接授予 P5。
/// </remarks>
public class TenantFilterTests
{
    /// <summary>
    /// 只见当前租户的角色与授权，与是否注册租户过滤器无关
    /// </summary>
    /// <param name="registerStrictFilter">是否在客户端上注册严格租户过滤器</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 只见当前租户的角色与授权(bool registerStrictFilter)
    {
        using var context = new AuthorizationTestContext();
        await SeedAsync(context);

        if (registerStrictFilter)
        {
            context.Client.QueryFilter.AddTableFilter<IStrictMultiTenantEntity>(entity => entity.TenantId == 5);
        }

        context.CurrentTenant.Id = 5;

        var roles = context.CreateRoleStore();
        var permissions = context.CreatePermissionStore();
        var checker = context.CreatePermissionChecker();

        Assert.Single(await roles.GetAllRolesAsync());
        Assert.NotNull(await roles.GetRoleByNameAsync("admin"));
        Assert.Single(await roles.GetUserRolesAsync("u1"));
        Assert.True(await roles.IsInRoleAsync("u1", "admin"));
        Assert.Equal(new[] { "u1" }, await roles.GetUsersInRoleAsync("admin"));
        Assert.Equal("P3", Assert.Single(await permissions.GetUserPermissionsAsync("u1")).Name);
        Assert.Equal("P1", Assert.Single(await permissions.GetRolePermissionsAsync("r1")).Name);

        Assert.Equal(
            new[] { "P1", "P3" },
            (await checker.GetGrantedPermissionsAsync("u1")).Order(StringComparer.Ordinal));
        Assert.False(await checker.IsGrantedAsync("u1", "P2"));
        Assert.False(await checker.IsGrantedAsync("u1", "P4"));
        Assert.False(await checker.IsGrantedAsync("u1", "P5"));
    }

    /// <summary>
    /// 平台态只见租户 0 的数据
    /// </summary>
    [Fact]
    public async Task 平台态只见租户0的数据()
    {
        using var context = new AuthorizationTestContext();
        await SeedAsync(context);

        context.CurrentTenant.Id = null;

        var roles = context.CreateRoleStore();
        var checker = context.CreatePermissionChecker();

        Assert.Empty(await roles.GetAllRolesAsync());
        Assert.False(await roles.IsInRoleAsync("u1", "admin"));
        Assert.Equal(new[] { "P5" }, await checker.GetGrantedPermissionsAsync("u1"));
    }

    /// <summary>
    /// 造数
    /// </summary>
    /// <param name="context">测试夹具</param>
    private static async Task SeedAsync(AuthorizationTestContext context)
    {
        foreach (var name in new[] { "P1", "P2", "P3", "P4", "P5" })
        {
            await context.Client.Insertable(new SysAuthzPermission(context.IdGenerator.NextId())
            {
                PermissionName = name,
                DisplayName = name,
                IsEnabled = true
            }).ExecuteCommandAsync();
        }

        foreach (var (tenantId, rolePermission, userPermission) in new[] { (5L, "P1", "P3"), (6L, "P2", "P4") })
        {
            await context.Client.Insertable(new SysAuthzRole(context.IdGenerator.NextId())
            {
                TenantId = tenantId,
                RoleId = "r1",
                RoleName = "admin",
                DisplayName = "管理员",
                IsEnabled = true,
                CreatedTime = DateTime.UtcNow
            }).ExecuteCommandAsync();

            await context.Client.Insertable(new SysAuthzUserRole(context.IdGenerator.NextId())
            {
                TenantId = tenantId,
                UserId = "u1",
                RoleId = "r1"
            }).ExecuteCommandAsync();

            await context.Client.Insertable(new SysAuthzRolePermission(context.IdGenerator.NextId())
            {
                TenantId = tenantId,
                RoleId = "r1",
                PermissionName = rolePermission
            }).ExecuteCommandAsync();

            await context.Client.Insertable(new SysAuthzUserPermission(context.IdGenerator.NextId())
            {
                TenantId = tenantId,
                UserId = "u1",
                PermissionName = userPermission
            }).ExecuteCommandAsync();
        }

        await context.Client.Insertable(new SysAuthzUserPermission(context.IdGenerator.NextId())
        {
            TenantId = 0,
            UserId = "u1",
            PermissionName = "P5"
        }).ExecuteCommandAsync();
    }
}
