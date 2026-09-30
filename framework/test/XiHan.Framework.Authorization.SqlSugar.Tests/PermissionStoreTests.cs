// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.SqlSugar.Entities;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 权限存储测试
/// </summary>
public class PermissionStoreTests
{
    /// <summary>
    /// 新增权限定义后可按名称读回
    /// </summary>
    [Fact]
    public async Task 新增权限定义后可按名称读回()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        var added = await store.AddOrUpdatePermissionAsync(new PermissionDefinition("User.Create", "创建用户", "允许创建用户")
        {
            ParentName = "User",
            Tag = "用户",
            Order = 3
        });

        Assert.True(added);

        var permission = await store.GetPermissionByNameAsync("User.Create");

        Assert.NotNull(permission);
        Assert.Equal("创建用户", permission.DisplayName);
        Assert.Equal("允许创建用户", permission.Description);
        Assert.Equal("User", permission.ParentName);
        Assert.Equal("用户", permission.Tag);
        Assert.Equal(3, permission.Order);
        Assert.True(permission.IsEnabled);
    }

    /// <summary>
    /// 同名权限定义再次写入时更新而不新增
    /// </summary>
    [Fact]
    public async Task 同名权限定义再次写入时更新而不新增()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("User.Create", "创建用户"));
        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("User.Create", "新建用户") { IsEnabled = false });

        var permission = Assert.Single(await store.GetAllPermissionsAsync());

        Assert.Equal("新建用户", permission.DisplayName);
        Assert.False(permission.IsEnabled);
    }

    /// <summary>
    /// 空名称的权限定义不写入
    /// </summary>
    [Fact]
    public async Task 空名称的权限定义不写入()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        Assert.False(await store.AddOrUpdatePermissionAsync(new PermissionDefinition()));
        Assert.Empty(await store.GetAllPermissionsAsync());
    }

    /// <summary>
    /// 批量写入权限定义后按排序与名称读回
    /// </summary>
    [Fact]
    public async Task 批量写入权限定义后按排序与名称读回()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.AddPermissionsAsync(
        [
            new PermissionDefinition("B", "乙") { Order = 1 },
            new PermissionDefinition("A", "甲") { Order = 1 },
            new PermissionDefinition("C", "丙") { Order = 0 },
            new PermissionDefinition()
        ]);

        var names = (await store.GetAllPermissionsAsync()).Select(permission => permission.Name).ToList();

        Assert.Equal(new[] { "C", "A", "B" }, names);
    }

    /// <summary>
    /// 删除权限定义返回是否删到并且之后读不到
    /// </summary>
    [Fact]
    public async Task 删除权限定义返回是否删到并且之后读不到()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("A", "甲"));

        Assert.True(await store.RemovePermissionAsync("A"));
        Assert.False(await store.RemovePermissionAsync("A"));
        Assert.Null(await store.GetPermissionByNameAsync("A"));
    }

    /// <summary>
    /// 授予用户权限后按用户读回
    /// </summary>
    [Fact]
    public async Task 授予用户权限后按用户读回()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("User.Create", "创建用户"));
        await store.GrantPermissionToUserAsync("u1", "User.Create");

        Assert.Equal("User.Create", Assert.Single(await store.GetUserPermissionsAsync("u1")).Name);
        Assert.Empty(await store.GetUserPermissionsAsync("u2"));
    }

    /// <summary>
    /// 重复授予用户权限只保留一行
    /// </summary>
    [Fact]
    public async Task 重复授予用户权限只保留一行()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.GrantPermissionToUserAsync("u1", "A");
        await store.GrantPermissionToUserAsync("u1", "A");

        Assert.Equal(1, await context.Client.Queryable<SysAuthzUserPermission>().CountAsync());
    }

    /// <summary>
    /// 撤销用户权限后不再返回
    /// </summary>
    [Fact]
    public async Task 撤销用户权限后不再返回()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("A", "甲"));
        await store.GrantPermissionToUserAsync("u1", "A");
        await store.RevokePermissionFromUserAsync("u1", "A");

        Assert.Empty(await store.GetUserPermissionsAsync("u1"));
        Assert.Equal(0, await context.Client.Queryable<SysAuthzUserPermission>().CountAsync());
    }

    /// <summary>
    /// 未定义的权限即使已授予也不返回，补回定义后立即生效
    /// </summary>
    [Fact]
    public async Task 未定义的权限即使已授予也不返回()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.GrantPermissionToUserAsync("u1", "Ghost");

        Assert.Empty(await store.GetUserPermissionsAsync("u1"));

        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("Ghost", "幽灵"));

        Assert.Equal("Ghost", Assert.Single(await store.GetUserPermissionsAsync("u1")).Name);
    }

    /// <summary>
    /// 删除权限定义不删除授予行
    /// </summary>
    [Fact]
    public async Task 删除权限定义不删除授予行()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("A", "甲"));
        await store.GrantPermissionToUserAsync("u1", "A");
        await store.GrantPermissionToRoleAsync("r1", "A");
        await store.RemovePermissionAsync("A");

        Assert.Equal(1, await context.Client.Queryable<SysAuthzUserPermission>().CountAsync());
        Assert.Equal(1, await context.Client.Queryable<SysAuthzRolePermission>().CountAsync());
    }

    /// <summary>
    /// 读取用户权限不过滤已禁用的定义
    /// </summary>
    [Fact]
    public async Task 读取用户权限不过滤已禁用的定义()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("A", "甲") { IsEnabled = false });
        await store.GrantPermissionToUserAsync("u1", "A");

        Assert.False(Assert.Single(await store.GetUserPermissionsAsync("u1")).IsEnabled);
    }

    /// <summary>
    /// 角色权限按角色标识读取且互不影响
    /// </summary>
    [Fact]
    public async Task 角色权限按角色标识读取且互不影响()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.AddPermissionsAsync([new PermissionDefinition("P1", "一"), new PermissionDefinition("P2", "二")]);
        await store.GrantPermissionToRoleAsync("r1", "P1");
        await store.GrantPermissionToRoleAsync("r2", "P2");

        Assert.Equal("P1", Assert.Single(await store.GetRolePermissionsAsync("r1")).Name);
        Assert.Equal("P2", Assert.Single(await store.GetRolePermissionsAsync("r2")).Name);
        Assert.Empty(await store.GetUserPermissionsAsync("r1"));
    }

    /// <summary>
    /// 重复授予角色权限只保留一行，撤销后不再返回
    /// </summary>
    [Fact]
    public async Task 重复授予角色权限只保留一行且撤销后不再返回()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("P1", "一"));
        await store.GrantPermissionToRoleAsync("r1", "P1");
        await store.GrantPermissionToRoleAsync("r1", "P1");

        Assert.Equal(1, await context.Client.Queryable<SysAuthzRolePermission>().CountAsync());

        await store.RevokePermissionFromRoleAsync("r1", "P1");

        Assert.Empty(await store.GetRolePermissionsAsync("r1"));
    }

    /// <summary>
    /// 授予与撤销只作用于当前租户，平台态对应租户 0
    /// </summary>
    [Fact]
    public async Task 授予与撤销只作用于当前租户()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("A", "甲"));

        context.CurrentTenant.Id = 1;
        await store.GrantPermissionToUserAsync("u1", "A");
        await store.GrantPermissionToRoleAsync("r1", "A");

        context.CurrentTenant.Id = 2;
        Assert.Empty(await store.GetUserPermissionsAsync("u1"));
        Assert.Empty(await store.GetRolePermissionsAsync("r1"));
        await store.RevokePermissionFromUserAsync("u1", "A");
        await store.RevokePermissionFromRoleAsync("r1", "A");

        context.CurrentTenant.Id = null;
        Assert.Empty(await store.GetUserPermissionsAsync("u1"));

        context.CurrentTenant.Id = 1;
        Assert.Single(await store.GetUserPermissionsAsync("u1"));
        Assert.Single(await store.GetRolePermissionsAsync("r1"));

        var rows = await context.Client.Queryable<SysAuthzUserPermission>().ToListAsync();
        Assert.Equal(1L, Assert.Single(rows).TenantId);
    }

    /// <summary>
    /// 空参数时不写库也不抛异常
    /// </summary>
    [Fact]
    public async Task 空参数时不写库也不抛异常()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePermissionStore();

        await store.GrantPermissionToUserAsync("", "A");
        await store.GrantPermissionToUserAsync("u1", "");
        await store.GrantPermissionToRoleAsync("", "A");
        await store.GrantPermissionToRoleAsync("r1", "");
        await store.RevokePermissionFromUserAsync("", "A");
        await store.RevokePermissionFromRoleAsync("r1", "");

        Assert.Empty(await store.GetUserPermissionsAsync(""));
        Assert.Empty(await store.GetRolePermissionsAsync(""));
        Assert.Null(await store.GetPermissionByNameAsync(""));
        Assert.False(await store.RemovePermissionAsync(""));
        Assert.Equal(0, await context.Client.Queryable<SysAuthzUserPermission>().CountAsync());
        Assert.Equal(0, await context.Client.Queryable<SysAuthzRolePermission>().CountAsync());
    }
}
