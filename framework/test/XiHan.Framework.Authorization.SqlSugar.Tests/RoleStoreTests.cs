// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Roles;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 角色存储的角色读写测试
/// </summary>
public class RoleStoreTests
{
    /// <summary>
    /// 创建角色后可按标识与名称读回
    /// </summary>
    [Fact]
    public async Task 创建角色后可按标识与名称读回()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();
        var before = DateTime.UtcNow.AddSeconds(-1);

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑", "可编辑内容")
        {
            IsDefault = true,
            IsStatic = true,
            Order = 5
        });

        var byId = await roles.GetRoleByIdAsync("r1");

        Assert.NotNull(byId);
        Assert.Equal("editor", byId.Name);
        Assert.Equal("编辑", byId.DisplayName);
        Assert.Equal("可编辑内容", byId.Description);
        Assert.True(byId.IsEnabled);
        Assert.True(byId.IsDefault);
        Assert.True(byId.IsStatic);
        Assert.Equal(5, byId.Order);
        Assert.Equal(DateTimeKind.Utc, byId.CreatedTime.Kind);
        Assert.True(byId.CreatedTime >= before);
        Assert.Null(byId.LastModifiedTime);

        var byName = await roles.GetRoleByNameAsync("editor");

        Assert.NotNull(byName);
        Assert.Equal("r1", byName.Id);
    }

    /// <summary>
    /// 创建角色时标识重复抛异常
    /// </summary>
    [Fact]
    public async Task 创建角色时标识重复抛异常()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            roles.CreateRoleAsync(new RoleDefinition("r1", "viewer", "查看")));
    }

    /// <summary>
    /// 创建角色时名称重复抛异常
    /// </summary>
    [Fact]
    public async Task 创建角色时名称重复抛异常()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            roles.CreateRoleAsync(new RoleDefinition("r2", "editor", "编辑")));
    }

    /// <summary>
    /// 创建角色的参数校验
    /// </summary>
    [Fact]
    public async Task 创建角色的参数校验()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() => roles.CreateRoleAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => roles.CreateRoleAsync(new RoleDefinition("", "editor", "编辑")));
        await Assert.ThrowsAsync<ArgumentException>(() => roles.CreateRoleAsync(new RoleDefinition("r1", "", "编辑")));
    }

    /// <summary>
    /// 更新角色改写字段并记录修改时间，不改创建时间
    /// </summary>
    [Fact]
    public async Task 更新角色改写字段并记录修改时间且不改创建时间()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        var createdTime = (await roles.GetRoleByIdAsync("r1"))!.CreatedTime;

        var update = new RoleDefinition("r1", "writer", "写作者", "新描述")
        {
            IsEnabled = false,
            IsDefault = true,
            Order = 9
        };

        await roles.UpdateRoleAsync(update);

        Assert.NotNull(update.LastModifiedTime);

        var stored = await roles.GetRoleByIdAsync("r1");

        Assert.NotNull(stored);
        Assert.Equal("writer", stored.Name);
        Assert.Equal("写作者", stored.DisplayName);
        Assert.Equal("新描述", stored.Description);
        Assert.False(stored.IsEnabled);
        Assert.True(stored.IsDefault);
        Assert.Equal(9, stored.Order);
        Assert.NotNull(stored.LastModifiedTime);
        Assert.Equal(DateTimeKind.Utc, stored.LastModifiedTime.Value.Kind);
        Assert.Equal(createdTime, stored.CreatedTime);
        Assert.Null(await roles.GetRoleByNameAsync("editor"));
    }

    /// <summary>
    /// 更新不存在的角色抛异常
    /// </summary>
    [Fact]
    public async Task 更新不存在的角色抛异常()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            roles.UpdateRoleAsync(new RoleDefinition("nope", "nope", "无")));
    }

    /// <summary>
    /// 改名与其他角色冲突时抛异常
    /// </summary>
    [Fact]
    public async Task 改名与其他角色冲突时抛异常()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.CreateRoleAsync(new RoleDefinition("r2", "viewer", "查看"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            roles.UpdateRoleAsync(new RoleDefinition("r2", "editor", "查看")));
    }

    /// <summary>
    /// 更新角色的参数校验
    /// </summary>
    [Fact]
    public async Task 更新角色的参数校验()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() => roles.UpdateRoleAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => roles.UpdateRoleAsync(new RoleDefinition("", "editor", "编辑")));
    }

    /// <summary>
    /// 读取全部角色按排序与名称排列
    /// </summary>
    [Fact]
    public async Task 读取全部角色按排序与名称排列()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        await roles.CreateRoleAsync(new RoleDefinition("r1", "b", "乙") { Order = 1 });
        await roles.CreateRoleAsync(new RoleDefinition("r2", "a", "甲") { Order = 1 });
        await roles.CreateRoleAsync(new RoleDefinition("r3", "c", "丙") { Order = 0 });

        var names = (await roles.GetAllRolesAsync()).Select(role => role.Name).ToList();

        Assert.Equal(new[] { "c", "a", "b" }, names);
    }

    /// <summary>
    /// 同一角色标识与名称可在不同租户各建一个且互不可见
    /// </summary>
    [Fact]
    public async Task 同一角色标识与名称可在不同租户各建一个()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        context.CurrentTenant.Id = 1;
        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));

        context.CurrentTenant.Id = 2;
        Assert.Null(await roles.GetRoleByIdAsync("r1"));
        Assert.Null(await roles.GetRoleByNameAsync("editor"));

        await roles.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"));
        await roles.AddUserToRoleAsync("u1", "editor");

        Assert.Single(await roles.GetAllRolesAsync());

        context.CurrentTenant.Id = null;
        Assert.Empty(await roles.GetAllRolesAsync());
        Assert.False(await roles.IsInRoleAsync("u1", "editor"));

        context.CurrentTenant.Id = 1;
        Assert.False(await roles.IsInRoleAsync("u1", "editor"));
    }

    /// <summary>
    /// 空参数读取返回空值
    /// </summary>
    [Fact]
    public async Task 空参数读取返回空值()
    {
        using var context = new AuthorizationTestContext();
        var roles = context.CreateRoleStore();

        Assert.Null(await roles.GetRoleByIdAsync(""));
        Assert.Null(await roles.GetRoleByNameAsync(""));
        Assert.Empty(await roles.GetUserRolesAsync(""));
        Assert.False(await roles.IsInRoleAsync("", "editor"));
        Assert.False(await roles.IsInRoleAsync("u1", ""));
        Assert.Empty(await roles.GetUsersInRoleAsync(""));
    }
}
