// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Roles;

namespace XiHan.Framework.Authorization.Tests.Roles;

/// <summary>
/// 默认角色存储测试
/// </summary>
/// <remarks>
/// 角色存储同时维护“标识 → 定义”“名称 → 标识”“用户 → 名称集合”三张表，
/// 一致性靠写路径的显式校验保证：标识与名称各自唯一、给用户加不存在的角色要抛异常、
/// 删除角色要连带把用户身上的关联抹掉。这里逐条钉这些不变量。
/// </remarks>
public class DefaultRoleStoreTests
{
    /// <summary>
    /// 创建后能按名称与标识分别取回同一个实例
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_ThenLookupByNameAndId_ReturnsSameRole()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        var role = new RoleDefinition("r1", "admin", "管理员");

        await store.CreateRoleAsync(role, token);

        Assert.Same(role, await store.GetRoleByNameAsync("admin", token));
        Assert.Same(role, await store.GetRoleByIdAsync("r1", token));
    }

    /// <summary>
    /// 角色为 null 时抛空引用参数异常
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_WithNullRole_Throws()
    {
        var store = new DefaultRoleStore();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.CreateRoleAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 角色标识或名称为空时抛参数异常
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_WithBlankIdOrName_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.CreateRoleAsync(new RoleDefinition { Name = "admin" }, token));
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.CreateRoleAsync(new RoleDefinition { Id = "r1" }, token));
    }

    /// <summary>
    /// 角色标识重复时抛无效操作异常
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_WithDuplicateId_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.CreateRoleAsync(new RoleDefinition("r1", "ops", "运维"), token));
        Assert.Contains("角色ID", exception.Message);
    }

    /// <summary>
    /// 角色名称重复时抛无效操作异常
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_WithDuplicateName_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.CreateRoleAsync(new RoleDefinition("r2", "admin", "管理员"), token));
        Assert.Contains("角色名称", exception.Message);
    }

    /// <summary>
    /// 给用户添加不存在的角色时抛无效操作异常，避免出现悬空关联
    /// </summary>
    [Fact]
    public async Task AddUserToRoleAsync_WithUnknownRole_Throws()
    {
        var store = new DefaultRoleStore();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AddUserToRoleAsync("u1", "admin", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 添加后用户角色列表与在角色判断同时生效
    /// </summary>
    [Fact]
    public async Task AddUserToRoleAsync_ThenUserRolesReflectMembership()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);

        await store.AddUserToRoleAsync("u1", "admin", token);

        Assert.True(await store.IsInRoleAsync("u1", "admin", token));
        Assert.Equal("admin", Assert.Single(await store.GetUserRolesAsync("u1", token)).Name);
        Assert.Equal("admin", Assert.Single(await store.GetUserRoleNamesAsync("u1")));
        Assert.Equal("u1", Assert.Single(await store.GetUsersInRoleAsync("admin", token)));
    }

    /// <summary>
    /// 角色名称大小写敏感，不同大小写不算同一个角色
    /// </summary>
    [Fact]
    public async Task IsInRoleAsync_IsCaseSensitive()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "Admin", "管理员"), token);
        await store.AddUserToRoleAsync("u1", "Admin", token);

        Assert.True(await store.IsInRoleAsync("u1", "Admin", token));
        Assert.False(await store.IsInRoleAsync("u1", "admin", token));
    }

    /// <summary>
    /// 重复添加同一角色不会产生重复关联
    /// </summary>
    [Fact]
    public async Task AddUserToRoleAsync_CalledTwice_IsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);

        await store.AddUserToRoleAsync("u1", "admin", token);
        await store.AddUserToRoleAsync("u1", "admin", token);

        Assert.Single(await store.GetUserRoleNamesAsync("u1"));
    }

    /// <summary>
    /// 用户标识或角色名为空时静默返回，不抛异常也不写入
    /// </summary>
    [Fact]
    public async Task AddUserToRoleAsync_WithBlankArguments_DoesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();

        await store.AddUserToRoleAsync(string.Empty, "admin", token);
        await store.AddUserToRoleAsync("u1", string.Empty, token);

        Assert.Empty(await store.GetUserRoleNamesAsync("u1"));
    }

    /// <summary>
    /// 移除后关联消失，移除不存在的关联不抛异常
    /// </summary>
    [Fact]
    public async Task RemoveUserFromRoleAsync_RemovesMembership()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);
        await store.AddUserToRoleAsync("u1", "admin", token);

        await store.RemoveUserFromRoleAsync("u1", "admin", token);
        await store.RemoveUserFromRoleAsync("u2", "admin", token);

        Assert.False(await store.IsInRoleAsync("u1", "admin", token));
        Assert.Empty(await store.GetUsersInRoleAsync("admin", token));
    }

    /// <summary>
    /// 更新角色名称时同步刷新名称映射，旧名称立即失效
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_WithNewName_RemapsNameIndex()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);

        await store.UpdateRoleAsync(new RoleDefinition("r1", "administrator", "管理员"), token);

        Assert.Null(await store.GetRoleByNameAsync("admin", token));
        Assert.NotNull(await store.GetRoleByNameAsync("administrator", token));
        Assert.Equal("administrator", (await store.GetRoleByIdAsync("r1", token))!.Name);
    }

    /// <summary>
    /// 更新时会补上最后修改时间
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_StampsLastModifiedTime()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);
        var updated = new RoleDefinition("r1", "admin", "系统管理员");

        await store.UpdateRoleAsync(updated, token);

        Assert.NotNull(updated.LastModifiedTime);
        Assert.Equal(DateTimeKind.Utc, updated.LastModifiedTime!.Value.Kind);
    }

    /// <summary>
    /// 更新不存在的角色抛无效操作异常
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_WhenMissing_Throws()
    {
        var store = new DefaultRoleStore();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.UpdateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 更新时角色为 null 或标识为空抛异常
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_WithInvalidRole_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.UpdateRoleAsync(null!, token));
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.UpdateRoleAsync(new RoleDefinition { Name = "admin" }, token));
    }

    /// <summary>
    /// 改名撞上其它角色已用的名称时抛无效操作异常
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_WithNameTakenByAnotherRole_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);
        await store.CreateRoleAsync(new RoleDefinition("r2", "ops", "运维"), token);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.UpdateRoleAsync(new RoleDefinition("r2", "admin", "运维"), token));
        Assert.Contains("已被其他角色使用", exception.Message);
    }

    /// <summary>
    /// 删除角色会连带清掉用户身上的关联，不留悬空引用
    /// </summary>
    [Fact]
    public async Task DeleteRoleAsync_AlsoRemovesUserMemberships()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);
        await store.AddUserToRoleAsync("u1", "admin", token);

        await store.DeleteRoleAsync("r1", token);

        Assert.Null(await store.GetRoleByIdAsync("r1", token));
        Assert.Null(await store.GetRoleByNameAsync("admin", token));
        Assert.False(await store.IsInRoleAsync("u1", "admin", token));
        Assert.Empty(await store.GetUserRoleNamesAsync("u1"));
    }

    /// <summary>
    /// 删除不存在的角色或空标识都不抛异常
    /// </summary>
    [Fact]
    public async Task DeleteRoleAsync_WhenMissing_DoesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();

        await store.DeleteRoleAsync("r1", token);
        await store.DeleteRoleAsync(string.Empty, token);

        Assert.Empty(await store.GetAllRolesAsync(token));
    }

    /// <summary>
    /// 删除并释放名称后，同名角色可以重新创建
    /// </summary>
    [Fact]
    public async Task DeleteRoleAsync_ReleasesNameForReuse()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);
        await store.DeleteRoleAsync("r1", token);

        await store.CreateRoleAsync(new RoleDefinition("r2", "admin", "管理员"), token);

        Assert.Equal("r2", (await store.GetRoleByNameAsync("admin", token))!.Id);
    }

    /// <summary>
    /// 未知用户或空标识查询返回空集合
    /// </summary>
    [Fact]
    public async Task GetUserRolesAsync_WhenUnknown_ReturnsEmpty()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();

        Assert.Empty(await store.GetUserRolesAsync("u1", token));
        Assert.Empty(await store.GetUserRolesAsync(string.Empty, token));
        Assert.Empty(await store.GetUserRoleNamesAsync(string.Empty));
        Assert.Empty(await store.GetUsersInRoleAsync(string.Empty, token));
    }

    /// <summary>
    /// 未知角色查询返回 null
    /// </summary>
    [Fact]
    public async Task GetRoleAsync_WhenUnknown_ReturnsNull()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();

        Assert.Null(await store.GetRoleByNameAsync("admin", token));
        Assert.Null(await store.GetRoleByIdAsync("r1", token));
        Assert.Null(await store.GetRoleByNameAsync(string.Empty, token));
        Assert.Null(await store.GetRoleByIdAsync(string.Empty, token));
    }

    /// <summary>
    /// 批量添加跳过标识或名称冲突项与无效项，且不抛异常
    /// </summary>
    [Fact]
    public async Task AddRolesAsync_SkipsConflictingAndInvalidEntries()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);

        await store.AddRolesAsync([
            new RoleDefinition("r1", "duplicated-id", "重复标识"),
            new RoleDefinition("r9", "admin", "重复名称"),
            new RoleDefinition("r2", "ops", "运维"),
            new RoleDefinition(),
            null!
        ]);

        var roles = await store.GetAllRolesAsync(token);
        Assert.Equal(2, roles.Count);
        Assert.Null(await store.GetRoleByIdAsync("r9", token));
        Assert.NotNull(await store.GetRoleByIdAsync("r2", token));
    }

    /// <summary>
    /// 批量添加传 null 集合时静默返回
    /// </summary>
    [Fact]
    public async Task AddRolesAsync_WithNullList_DoesNothing()
    {
        var store = new DefaultRoleStore();

        await store.AddRolesAsync(null!);

        Assert.Empty(await store.GetAllRolesAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 清空会同时抹掉角色定义与用户关联
    /// </summary>
    [Fact]
    public async Task ClearAsync_RemovesRolesAndMemberships()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();
        await store.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);
        await store.AddUserToRoleAsync("u1", "admin", token);

        await store.ClearAsync();

        Assert.Empty(await store.GetAllRolesAsync(token));
        Assert.Empty(await store.GetUserRoleNamesAsync("u1"));
    }

    /// <summary>
    /// 并发创建互不冲突的角色时不丢数据（写路径宣称线程安全）
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_UnderConcurrency_KeepsAllRoles()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultRoleStore();

        await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(index => Task.Run(
                () => store.CreateRoleAsync(new RoleDefinition($"r{index}", $"role{index}", $"角色{index}"), token),
                token)));

        Assert.Equal(100, (await store.GetAllRolesAsync(token)).Count);
    }
}
