// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.SqlSugar.Entities;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 策略存储测试
/// </summary>
public class PolicyStoreTests
{
    /// <summary>
    /// 创建策略后按名称读回
    /// </summary>
    [Fact]
    public async Task 创建策略后按名称读回()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        await store.CreatePolicyAsync(new PolicyDefinition("AdminOnly", "仅管理员", "只允许管理员访问")
        {
            RequiredRoles = ["admin"],
            RequiredPermissions = ["User.Create"],
            RequiredClaims = new Dictionary<string, string> { ["dept"] = "it" },
            IsEnabled = false
        });

        var policy = await store.GetPolicyByNameAsync("AdminOnly");

        Assert.NotNull(policy);
        Assert.Equal("仅管理员", policy.DisplayName);
        Assert.Equal("只允许管理员访问", policy.Description);
        Assert.Equal(new[] { "admin" }, policy.RequiredRoles);
        Assert.Equal(new[] { "User.Create" }, policy.RequiredPermissions);
        Assert.Equal("it", policy.RequiredClaims["dept"]);
        Assert.False(policy.IsEnabled);
        Assert.Empty(policy.CustomRequirements);
    }

    /// <summary>
    /// 同名策略重复创建抛异常
    /// </summary>
    [Fact]
    public async Task 同名策略重复创建抛异常()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        await store.CreatePolicyAsync(new PolicyDefinition("AdminOnly", "仅管理员"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreatePolicyAsync(new PolicyDefinition("AdminOnly", "另一个")));
    }

    /// <summary>
    /// 空策略或空名称抛参数异常
    /// </summary>
    [Fact]
    public async Task 空策略或空名称抛参数异常()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreatePolicyAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreatePolicyAsync(new PolicyDefinition()));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdatePolicyAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdatePolicyAsync(new PolicyDefinition()));
    }

    /// <summary>
    /// 要求集合为空引用的策略拒绝写入，已有策略不被改写
    /// </summary>
    [Fact]
    public async Task 要求集合为空引用的策略拒绝写入()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreatePolicyAsync(new PolicyDefinition("P1", "甲") { RequiredRoles = null! }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreatePolicyAsync(new PolicyDefinition("P1", "甲") { RequiredPermissions = null! }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreatePolicyAsync(new PolicyDefinition("P1", "甲") { RequiredClaims = null! }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreatePolicyAsync(new PolicyDefinition("P1", "甲") { CustomRequirements = null! }));

        Assert.Equal(0, await context.Client.Queryable<SysAuthzPolicy>().CountAsync());

        await store.CreatePolicyAsync(new PolicyDefinition("P2", "乙") { RequiredPermissions = ["User.Create"] });

        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdatePolicyAsync(new PolicyDefinition("P2", "乙") { RequiredPermissions = null! }));

        var policy = await store.GetPolicyByNameAsync("P2");

        Assert.NotNull(policy);
        Assert.Equal(new[] { "User.Create" }, policy.RequiredPermissions);
    }

    /// <summary>
    /// 含自定义要求的策略拒绝创建且不写库
    /// </summary>
    [Fact]
    public async Task 含自定义要求的策略拒绝创建且不写库()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        await Assert.ThrowsAsync<NotSupportedException>(() => store.CreatePolicyAsync(new PolicyDefinition("Custom", "自定义")
        {
            CustomRequirements = [new AlwaysPassRequirement()]
        }));

        Assert.Null(await store.GetPolicyByNameAsync("Custom"));
        Assert.Equal(0, await context.Client.Queryable<SysAuthzPolicy>().CountAsync());
    }

    /// <summary>
    /// 含自定义要求的策略拒绝更新且原策略不变
    /// </summary>
    [Fact]
    public async Task 含自定义要求的策略拒绝更新且原策略不变()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        await store.CreatePolicyAsync(new PolicyDefinition("AdminOnly", "仅管理员") { RequiredRoles = ["admin"] });

        await Assert.ThrowsAsync<NotSupportedException>(() => store.UpdatePolicyAsync(new PolicyDefinition("AdminOnly", "被改")
        {
            RequiredRoles = ["other"],
            CustomRequirements = [new AlwaysPassRequirement()]
        }));

        var policy = await store.GetPolicyByNameAsync("AdminOnly");

        Assert.NotNull(policy);
        Assert.Equal("仅管理员", policy.DisplayName);
        Assert.Equal(new[] { "admin" }, policy.RequiredRoles);
    }

    /// <summary>
    /// 更新策略改写名称以外的全部字段
    /// </summary>
    [Fact]
    public async Task 更新策略改写名称以外的全部字段()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        await store.CreatePolicyAsync(new PolicyDefinition("AdminOnly", "仅管理员")
        {
            RequiredRoles = ["admin"],
            RequiredPermissions = ["User.Create"],
            RequiredClaims = new Dictionary<string, string> { ["dept"] = "it" }
        });

        await store.UpdatePolicyAsync(new PolicyDefinition("AdminOnly", "管理员或所有者", "新描述")
        {
            RequiredRoles = ["admin", "owner"],
            RequiredPermissions = [],
            RequiredClaims = new Dictionary<string, string> { ["region"] = "cn" },
            IsEnabled = false
        });

        var policy = await store.GetPolicyByNameAsync("AdminOnly");

        Assert.NotNull(policy);
        Assert.Equal("管理员或所有者", policy.DisplayName);
        Assert.Equal("新描述", policy.Description);
        Assert.Equal(new[] { "admin", "owner" }, policy.RequiredRoles);
        Assert.Empty(policy.RequiredPermissions);
        Assert.Equal("cn", Assert.Single(policy.RequiredClaims).Value);
        Assert.False(policy.IsEnabled);
    }

    /// <summary>
    /// 更新不存在的策略抛异常
    /// </summary>
    [Fact]
    public async Task 更新不存在的策略抛异常()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdatePolicyAsync(new PolicyDefinition("Nope", "无")));
    }

    /// <summary>
    /// 删除策略后读不到，删除空名称或不存在的策略不抛异常
    /// </summary>
    [Fact]
    public async Task 删除策略后读不到()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        await store.CreatePolicyAsync(new PolicyDefinition("AdminOnly", "仅管理员"));
        await store.DeletePolicyAsync("AdminOnly");
        await store.DeletePolicyAsync("AdminOnly");
        await store.DeletePolicyAsync("");

        Assert.Null(await store.GetPolicyByNameAsync("AdminOnly"));
    }

    /// <summary>
    /// 读取全部策略按名称排列且不过滤禁用
    /// </summary>
    [Fact]
    public async Task 读取全部策略按名称排列且不过滤禁用()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        await store.CreatePolicyAsync(new PolicyDefinition("B", "乙"));
        await store.CreatePolicyAsync(new PolicyDefinition("A", "甲") { IsEnabled = false });
        await store.CreatePolicyAsync(new PolicyDefinition("C", "丙"));

        var names = (await store.GetAllPoliciesAsync()).Select(policy => policy.Name).ToList();

        Assert.Equal(new[] { "A", "B", "C" }, names);
    }

    /// <summary>
    /// 空名称读取返回空
    /// </summary>
    [Fact]
    public async Task 空名称读取返回空()
    {
        using var context = new AuthorizationTestContext();
        var store = context.CreatePolicyStore();

        Assert.Null(await store.GetPolicyByNameAsync(""));
    }

    /// <summary>
    /// 恒通过的自定义要求
    /// </summary>
    private sealed class AlwaysPassRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// 要求名称
        /// </summary>
        public string Name => "always-pass";

        /// <summary>
        /// 评估授权要求
        /// </summary>
        /// <param name="context">授权上下文</param>
        /// <returns>恒为 true</returns>
        public Task<bool> EvaluateAsync(AuthorizationContext context)
        {
            return Task.FromResult(true);
        }
    }
}
