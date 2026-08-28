// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;

namespace XiHan.Framework.Authorization.Tests.Policies;

/// <summary>
/// 默认策略存储测试
/// </summary>
/// <remarks>
/// 策略存储的写路径是“显式冲突”语义：重复创建、更新不存在的策略都要抛异常，
/// 而删除不存在的策略是幂等的静默返回。批量添加则退化成“已存在就跳过”，与单条创建不同，这条差异要钉住。
/// </remarks>
public class DefaultPolicyStoreTests
{
    /// <summary>
    /// 创建后能按名称取回
    /// </summary>
    [Fact]
    public async Task CreatePolicyAsync_ThenGetByName_ReturnsPolicy()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();

        await store.CreatePolicyAsync(new PolicyDefinition("p1", "策略一"), token);

        var policy = await store.GetPolicyByNameAsync("p1", token);
        Assert.NotNull(policy);
        Assert.Equal("策略一", policy!.DisplayName);
    }

    /// <summary>
    /// 策略为 null 或名称为空时抛参数异常
    /// </summary>
    [Fact]
    public async Task CreatePolicyAsync_WithInvalidPolicy_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreatePolicyAsync(null!, token));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreatePolicyAsync(new PolicyDefinition(), token));
    }

    /// <summary>
    /// 重复创建同名策略抛无效操作异常
    /// </summary>
    [Fact]
    public async Task CreatePolicyAsync_WithDuplicateName_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();
        await store.CreatePolicyAsync(new PolicyDefinition("p1", "策略一"), token);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.CreatePolicyAsync(new PolicyDefinition("p1", "策略二"), token));
        Assert.Contains("已存在", exception.Message);
    }

    /// <summary>
    /// 更新已存在的策略会整体替换
    /// </summary>
    [Fact]
    public async Task UpdatePolicyAsync_ReplacesExistingPolicy()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();
        await store.CreatePolicyAsync(new PolicyDefinition("p1", "旧名"), token);

        await store.UpdatePolicyAsync(new PolicyDefinition("p1", "新名"), token);

        var policy = await store.GetPolicyByNameAsync("p1", token);
        Assert.Equal("新名", policy!.DisplayName);
        Assert.Single(await store.GetAllPoliciesAsync(token));
    }

    /// <summary>
    /// 更新不存在的策略抛无效操作异常
    /// </summary>
    [Fact]
    public async Task UpdatePolicyAsync_WhenMissing_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.UpdatePolicyAsync(new PolicyDefinition("p1", "策略一"), token));
        Assert.Contains("不存在", exception.Message);
    }

    /// <summary>
    /// 更新时策略为 null 或名称为空抛参数异常
    /// </summary>
    [Fact]
    public async Task UpdatePolicyAsync_WithInvalidPolicy_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdatePolicyAsync(null!, token));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdatePolicyAsync(new PolicyDefinition(), token));
    }

    /// <summary>
    /// 删除已存在的策略后查不到
    /// </summary>
    [Fact]
    public async Task DeletePolicyAsync_RemovesPolicy()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();
        await store.CreatePolicyAsync(new PolicyDefinition("p1", "策略一"), token);

        await store.DeletePolicyAsync("p1", token);

        Assert.Null(await store.GetPolicyByNameAsync("p1", token));
        Assert.False(await store.PolicyExistsAsync("p1"));
    }

    /// <summary>
    /// 删除不存在的策略或空名称都不抛异常
    /// </summary>
    [Fact]
    public async Task DeletePolicyAsync_WhenMissing_DoesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();

        await store.DeletePolicyAsync("p1", token);
        await store.DeletePolicyAsync(string.Empty, token);

        Assert.Empty(await store.GetAllPoliciesAsync(token));
    }

    /// <summary>
    /// 名称为空时查询返回 null 而不是抛异常
    /// </summary>
    [Fact]
    public async Task GetPolicyByNameAsync_WithBlankName_ReturnsNull()
    {
        var store = new DefaultPolicyStore();

        Assert.Null(await store.GetPolicyByNameAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 批量添加对已存在的同名策略跳过而不是抛异常，也不会覆盖
    /// </summary>
    [Fact]
    public async Task AddPoliciesAsync_SkipsExistingAndInvalidEntries()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();
        await store.CreatePolicyAsync(new PolicyDefinition("p1", "原始"), token);

        await store.AddPoliciesAsync([
            new PolicyDefinition("p1", "覆盖尝试"),
            new PolicyDefinition("p2", "策略二"),
            new PolicyDefinition(),
            null!
        ]);

        Assert.Equal("原始", (await store.GetPolicyByNameAsync("p1", token))!.DisplayName);
        Assert.NotNull(await store.GetPolicyByNameAsync("p2", token));
        Assert.Equal(2, (await store.GetAllPoliciesAsync(token)).Count);
    }

    /// <summary>
    /// 批量添加传 null 集合时静默返回
    /// </summary>
    [Fact]
    public async Task AddPoliciesAsync_WithNullList_DoesNothing()
    {
        var store = new DefaultPolicyStore();

        await store.AddPoliciesAsync(null!);

        Assert.Empty(await store.GetAllPoliciesAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 存在性判断对空名称返回假
    /// </summary>
    [Fact]
    public async Task PolicyExistsAsync_WithBlankName_ReturnsFalse()
    {
        var store = new DefaultPolicyStore();

        Assert.False(await store.PolicyExistsAsync(string.Empty));
    }

    /// <summary>
    /// 清空后所有策略消失
    /// </summary>
    [Fact]
    public async Task ClearAsync_RemovesEverything()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();
        await store.CreatePolicyAsync(new PolicyDefinition("p1", "策略一"), token);

        await store.ClearAsync();

        Assert.Empty(await store.GetAllPoliciesAsync(token));
    }

    /// <summary>
    /// 策略名称大小写敏感
    /// </summary>
    [Fact]
    public async Task GetPolicyByNameAsync_IsCaseSensitive()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new DefaultPolicyStore();
        await store.CreatePolicyAsync(new PolicyDefinition("Tenant-Admin", "租户管理员"), token);

        Assert.NotNull(await store.GetPolicyByNameAsync("Tenant-Admin", token));
        Assert.Null(await store.GetPolicyByNameAsync("tenant-admin", token));
    }
}
