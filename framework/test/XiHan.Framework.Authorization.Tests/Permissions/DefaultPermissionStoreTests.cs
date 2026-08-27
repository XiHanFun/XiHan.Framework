// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Permissions;

namespace XiHan.Framework.Authorization.Tests.Permissions;

/// <summary>
/// 默认权限存储测试
/// </summary>
/// <remarks>
/// 内存实现有一条容易踩的语义：授予关系与权限定义是两张表，只授予不定义时
/// <c>GetUserPermissionsAsync</c> 取不到任何定义，但 <c>GetUserPermissionNamesAsync</c> 仍能取到名称。
/// 这条差异必须锁死，否则权限检查器会静默失效。空标识一律走静默返回而不是抛异常，也是既定契约。
/// </remarks>
public class DefaultPermissionStoreTests
{
    /// <summary>
    /// 未授予任何权限的用户得到空列表
    /// </summary>
    [Fact]
    public async Task GetUserPermissionsAsync_WhenUserUnknown_ReturnsEmpty()
    {
        var store = new DefaultPermissionStore();

        Assert.Empty(await store.GetUserPermissionsAsync("u1", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 用户标识为空时静默返回空列表
    /// </summary>
    /// <param name="userId">用户标识</param>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetUserPermissionsAsync_WhenUserIdBlank_ReturnsEmpty(string? userId)
    {
        var store = new DefaultPermissionStore();

        Assert.Empty(await store.GetUserPermissionsAsync(userId!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 已定义且已授予的权限能被完整取回
    /// </summary>
    [Fact]
    public async Task GetUserPermissionsAsync_WithDefinedPermission_ReturnsDefinition()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();
        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await store.GrantPermissionToUserAsync("u1", "read", token);

        var permissions = await store.GetUserPermissionsAsync("u1", token);

        Assert.Equal("read", Assert.Single(permissions).Name);
    }

    /// <summary>
    /// 只授予未定义的权限时定义列表为空，但名称列表仍能取到
    /// </summary>
    [Fact]
    public async Task GrantPermissionToUserAsync_WithoutDefinition_KeepsNameButNotDefinition()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();

        await store.GrantPermissionToUserAsync("u1", "read", token);

        Assert.Empty(await store.GetUserPermissionsAsync("u1", token));
        Assert.Equal("read", Assert.Single(await store.GetUserPermissionNamesAsync("u1")));
    }

    /// <summary>
    /// 重复授予同一权限不会产生重复项
    /// </summary>
    [Fact]
    public async Task GrantPermissionToUserAsync_CalledTwice_IsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();

        await store.GrantPermissionToUserAsync("u1", "read", token);
        await store.GrantPermissionToUserAsync("u1", "read", token);

        Assert.Single(await store.GetUserPermissionNamesAsync("u1"));
    }

    /// <summary>
    /// 撤销后名称列表里不再有该权限
    /// </summary>
    [Fact]
    public async Task RevokePermissionFromUserAsync_RemovesGrant()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();
        await store.GrantPermissionToUserAsync("u1", "read", token);

        await store.RevokePermissionFromUserAsync("u1", "read", token);

        Assert.Empty(await store.GetUserPermissionNamesAsync("u1"));
    }

    /// <summary>
    /// 撤销不存在的授予关系不抛异常
    /// </summary>
    [Fact]
    public async Task RevokePermissionFromUserAsync_WhenNotGranted_DoesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();

        await store.RevokePermissionFromUserAsync("u1", "read", token);

        Assert.Empty(await store.GetUserPermissionNamesAsync("u1"));
    }

    /// <summary>
    /// 角色权限与用户权限互不影响
    /// </summary>
    [Fact]
    public async Task RolePermissions_AreIsolatedFromUserPermissions()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();
        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));

        await store.GrantPermissionToRoleAsync("r1", "read", token);

        Assert.Equal("read", Assert.Single(await store.GetRolePermissionsAsync("r1", token)).Name);
        Assert.Empty(await store.GetUserPermissionsAsync("r1", token));
    }

    /// <summary>
    /// 撤销角色权限后不再返回
    /// </summary>
    [Fact]
    public async Task RevokePermissionFromRoleAsync_RemovesGrant()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();
        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await store.GrantPermissionToRoleAsync("r1", "read", token);

        await store.RevokePermissionFromRoleAsync("r1", "read", token);

        Assert.Empty(await store.GetRolePermissionsAsync("r1", token));
        Assert.Empty(await store.GetRolePermissionNamesAsync("r1"));
    }

    /// <summary>
    /// 角色标识为空时静默返回空列表
    /// </summary>
    [Fact]
    public async Task GetRolePermissionsAsync_WhenRoleIdBlank_ReturnsEmpty()
    {
        var store = new DefaultPermissionStore();

        Assert.Empty(await store.GetRolePermissionsAsync(string.Empty, TestContext.Current.CancellationToken));
        Assert.Empty(await store.GetRolePermissionNamesAsync(string.Empty));
    }

    /// <summary>
    /// 同名权限定义按后写覆盖
    /// </summary>
    [Fact]
    public async Task AddOrUpdatePermissionAsync_WithSameName_Overwrites()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();

        Assert.True(await store.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "旧名")));
        Assert.True(await store.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "新名")));

        var permission = await store.GetPermissionByNameAsync("read", token);
        Assert.NotNull(permission);
        Assert.Equal("新名", permission!.DisplayName);
        Assert.Single(await store.GetAllPermissionsAsync(token));
    }

    /// <summary>
    /// 定义为 null 或名称为空时拒绝写入
    /// </summary>
    [Fact]
    public async Task AddOrUpdatePermissionAsync_WithInvalidDefinition_ReturnsFalse()
    {
        var store = new DefaultPermissionStore();

        Assert.False(await store.AddOrUpdatePermissionAsync(null!));
        Assert.False(await store.AddOrUpdatePermissionAsync(new PermissionDefinition()));
        Assert.Empty(await store.GetAllPermissionsAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 删除存在的定义返回真，删除不存在的返回假
    /// </summary>
    [Fact]
    public async Task RemovePermissionAsync_ReflectsWhetherDefinitionExisted()
    {
        var store = new DefaultPermissionStore();
        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));

        Assert.True(await store.RemovePermissionAsync("read"));
        Assert.False(await store.RemovePermissionAsync("read"));
        Assert.False(await store.RemovePermissionAsync(string.Empty));
    }

    /// <summary>
    /// 按名称查询不存在的权限返回 null，名称为空同样返回 null
    /// </summary>
    [Fact]
    public async Task GetPermissionByNameAsync_WhenMissing_ReturnsNull()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();

        Assert.Null(await store.GetPermissionByNameAsync("read", token));
        Assert.Null(await store.GetPermissionByNameAsync(string.Empty, token));
    }

    /// <summary>
    /// 权限名称大小写敏感，不同大小写视为不同权限
    /// </summary>
    [Fact]
    public async Task GetPermissionByNameAsync_IsCaseSensitive()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();
        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("Read", "读取"));

        Assert.NotNull(await store.GetPermissionByNameAsync("Read", token));
        Assert.Null(await store.GetPermissionByNameAsync("read", token));
    }

    /// <summary>
    /// 批量添加跳过 null 与无名称项
    /// </summary>
    [Fact]
    public async Task AddPermissionsAsync_SkipsInvalidEntries()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();

        await store.AddPermissionsAsync([new PermissionDefinition("read", "读取"), new PermissionDefinition(), null!]);

        Assert.Equal("read", Assert.Single(await store.GetAllPermissionsAsync(token)).Name);
    }

    /// <summary>
    /// 批量添加传 null 集合时静默返回
    /// </summary>
    [Fact]
    public async Task AddPermissionsAsync_WithNullList_DoesNothing()
    {
        var store = new DefaultPermissionStore();

        await store.AddPermissionsAsync(null!);

        Assert.Empty(await store.GetAllPermissionsAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 清空同时抹掉定义、用户授予与角色授予三张表
    /// </summary>
    [Fact]
    public async Task ClearAsync_ClearsDefinitionsAndGrants()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();
        await store.AddOrUpdatePermissionAsync(new PermissionDefinition("read", "读取"));
        await store.GrantPermissionToUserAsync("u1", "read", token);
        await store.GrantPermissionToRoleAsync("r1", "read", token);

        await store.ClearAsync();

        Assert.Empty(await store.GetAllPermissionsAsync(token));
        Assert.Empty(await store.GetUserPermissionNamesAsync("u1"));
        Assert.Empty(await store.GetRolePermissionNamesAsync("r1"));
    }

    /// <summary>
    /// 并发授予不同权限时不丢数据（写路径宣称线程安全）
    /// </summary>
    [Fact]
    public async Task GrantPermissionToUserAsync_UnderConcurrency_KeepsAllGrants()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPermissionStore();

        await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(index => Task.Run(() => store.GrantPermissionToUserAsync("u1", $"p{index}", token), token)));

        Assert.Equal(200, (await store.GetUserPermissionNamesAsync("u1")).Count);
    }
}
