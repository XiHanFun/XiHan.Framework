// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Roles;

namespace XiHan.Framework.Authorization.Tests.Permissions;

/// <summary>
/// 默认权限检查器测试
/// </summary>
/// <remarks>
/// 检查器的匹配规则是“权限名逐字相等 + 权限启用 + 角色启用”，没有通配也没有父子继承：
/// 这三条一起决定了越权边界，必须逐条钉死。禁用的权限或禁用的角色都要立即失效，
/// 空集合按 fail-closed 处理（任意/全部都判否）。
/// </remarks>
public class DefaultPermissionCheckerTests
{
    /// <summary>
    /// 用户直接授予的启用权限判为有权限
    /// </summary>
    [Fact]
    public async Task IsGrantedAsync_WithDirectGrant_ReturnsTrue()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, _) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await permissionStore.GrantPermissionToUserAsync("u1", "read", token);

        Assert.True(await checker.IsGrantedAsync("u1", "read", token));
    }

    /// <summary>
    /// 权限名逐字比较，大小写不同视为不同权限
    /// </summary>
    [Fact]
    public async Task IsGrantedAsync_IsCaseSensitive()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, _) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("Read", "读取"));
        await permissionStore.GrantPermissionToUserAsync("u1", "Read", token);

        Assert.True(await checker.IsGrantedAsync("u1", "Read", token));
        Assert.False(await checker.IsGrantedAsync("u1", "read", token));
    }

    /// <summary>
    /// 没有通配语义：授予 sys.* 不等于拥有 sys.user.create
    /// </summary>
    [Fact]
    public async Task IsGrantedAsync_DoesNotSupportWildcard()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, _) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("sys.*", "全部"));
        await permissionStore.GrantPermissionToUserAsync("u1", "sys.*", token);

        Assert.False(await checker.IsGrantedAsync("u1", "sys.user.create", token));
    }

    /// <summary>
    /// 没有父子继承：拥有父权限不等于拥有子权限
    /// </summary>
    [Fact]
    public async Task IsGrantedAsync_DoesNotInheritFromParentPermission()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, _) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("sys.user", "用户"));
        await permissionStore.AddOrUpdatePermissionAsync(
            new PermissionDefinition("sys.user.create", "创建用户") { ParentName = "sys.user" });
        await permissionStore.GrantPermissionToUserAsync("u1", "sys.user", token);

        Assert.False(await checker.IsGrantedAsync("u1", "sys.user.create", token));
    }

    /// <summary>
    /// 权限定义被禁用后立即失效
    /// </summary>
    [Fact]
    public async Task IsGrantedAsync_WhenPermissionDisabled_ReturnsFalse()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, _) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取") { IsEnabled = false });
        await permissionStore.GrantPermissionToUserAsync("u1", "read", token);

        Assert.False(await checker.IsGrantedAsync("u1", "read", token));
    }

    /// <summary>
    /// 只授予关系而没有权限定义时判为无权限
    /// </summary>
    [Fact]
    public async Task IsGrantedAsync_WithoutDefinition_ReturnsFalse()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, _) = CreateChecker();
        await permissionStore.GrantPermissionToUserAsync("u1", "read", token);

        Assert.False(await checker.IsGrantedAsync("u1", "read", token));
    }

    /// <summary>
    /// 通过启用的角色间接获得权限
    /// </summary>
    [Fact]
    public async Task IsGrantedAsync_ViaEnabledRole_ReturnsTrue()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, roleStore) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await roleStore.CreateRoleAsync(new RoleDefinition("r1", "reader", "读者"), token);
        await roleStore.AddUserToRoleAsync("u1", "reader", token);
        await permissionStore.GrantPermissionToRoleAsync("r1", "read", token);

        Assert.True(await checker.IsGrantedAsync("u1", "read", token));
    }

    /// <summary>
    /// 角色被禁用后，其携带的权限立即失效
    /// </summary>
    [Fact]
    public async Task IsGrantedAsync_ViaDisabledRole_ReturnsFalse()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, roleStore) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await roleStore.CreateRoleAsync(new RoleDefinition("r1", "reader", "读者") { IsEnabled = false }, token);
        await roleStore.AddUserToRoleAsync("u1", "reader", token);
        await permissionStore.GrantPermissionToRoleAsync("r1", "read", token);

        Assert.False(await checker.IsGrantedAsync("u1", "read", token));
    }

    /// <summary>
    /// 角色权限按角色标识而不是角色名称索引
    /// </summary>
    [Fact]
    public async Task IsGrantedAsync_RolePermissionsAreKeyedByRoleId()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, roleStore) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await roleStore.CreateRoleAsync(new RoleDefinition("r1", "reader", "读者"), token);
        await roleStore.AddUserToRoleAsync("u1", "reader", token);
        await permissionStore.GrantPermissionToRoleAsync("reader", "read", token);

        Assert.False(await checker.IsGrantedAsync("u1", "read", token));
    }

    /// <summary>
    /// 用户标识或权限名称为空时判为无权限
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="permissionName">权限名称</param>
    [Theory]
    [InlineData("", "read")]
    [InlineData("u1", "")]
    [InlineData(null, "read")]
    [InlineData("u1", null)]
    public async Task IsGrantedAsync_WithBlankArguments_ReturnsFalse(string? userId, string? permissionName)
    {
        var (checker, _, _) = CreateChecker();

        Assert.False(await checker.IsGrantedAsync(userId!, permissionName!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 任意权限：命中其一即为真，一个都没有为假
    /// </summary>
    [Fact]
    public async Task IsAnyGrantedAsync_ReturnsTrueWhenAnyMatches()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, _) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await permissionStore.GrantPermissionToUserAsync("u1", "read", token);

        Assert.True(await checker.IsAnyGrantedAsync("u1", ["write", "read"], token));
        Assert.False(await checker.IsAnyGrantedAsync("u1", ["write", "delete"], token));
    }

    /// <summary>
    /// 全部权限：缺一即为假
    /// </summary>
    [Fact]
    public async Task IsAllGrantedAsync_RequiresEveryPermission()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, _) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("write", "写入"));
        await permissionStore.GrantPermissionToUserAsync("u1", "read", token);
        await permissionStore.GrantPermissionToUserAsync("u1", "write", token);

        Assert.True(await checker.IsAllGrantedAsync("u1", ["read", "write"], token));
        Assert.False(await checker.IsAllGrantedAsync("u1", ["read", "delete"], token));
    }

    /// <summary>
    /// 空权限列表按 fail-closed 处理，任意与全部都判否
    /// </summary>
    [Fact]
    public async Task EmptyPermissionList_IsTreatedAsNotGranted()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, _, _) = CreateChecker();

        Assert.False(await checker.IsAnyGrantedAsync("u1", [], token));
        Assert.False(await checker.IsAllGrantedAsync("u1", [], token));
    }

    /// <summary>
    /// 已授予权限汇总同时包含直接授予与角色授予，并按名称去重
    /// </summary>
    [Fact]
    public async Task GetGrantedPermissionsAsync_MergesDirectAndRoleGrants()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, roleStore) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("write", "写入"));
        await permissionStore.GrantPermissionToUserAsync("u1", "read", token);
        await roleStore.CreateRoleAsync(new RoleDefinition("r1", "editor", "编辑"), token);
        await roleStore.AddUserToRoleAsync("u1", "editor", token);
        await permissionStore.GrantPermissionToRoleAsync("r1", "read", token);
        await permissionStore.GrantPermissionToRoleAsync("r1", "write", token);

        var permissions = await checker.GetGrantedPermissionsAsync("u1", token);

        Assert.Equal(2, permissions.Count);
        Assert.Contains("read", permissions);
        Assert.Contains("write", permissions);
    }

    /// <summary>
    /// 汇总时排除被禁用的权限
    /// </summary>
    [Fact]
    public async Task GetGrantedPermissionsAsync_ExcludesDisabledPermissions()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, _) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("write", "写入") { IsEnabled = false });
        await permissionStore.GrantPermissionToUserAsync("u1", "read", token);
        await permissionStore.GrantPermissionToUserAsync("u1", "write", token);

        Assert.Equal("read", Assert.Single(await checker.GetGrantedPermissionsAsync("u1", token)));
    }

    /// <summary>
    /// 权限存在性只看定义表，与是否授予、是否启用无关
    /// </summary>
    [Fact]
    public async Task PermissionExistsAsync_OnlyChecksDefinitionTable()
    {
        var token = TestContext.Current.CancellationToken;
        var (checker, permissionStore, _) = CreateChecker();
        await permissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取") { IsEnabled = false });

        Assert.True(await checker.PermissionExistsAsync("read", token));
        Assert.False(await checker.PermissionExistsAsync("write", token));
    }

    private static (DefaultPermissionChecker Checker, DefaultPermissionStore PermissionStore, DefaultRoleStore RoleStore) CreateChecker()
    {
        var permissionStore = new DefaultPermissionStore();
        var roleStore = new DefaultRoleStore();
        return (new DefaultPermissionChecker(permissionStore, roleStore), permissionStore, roleStore);
    }
}
