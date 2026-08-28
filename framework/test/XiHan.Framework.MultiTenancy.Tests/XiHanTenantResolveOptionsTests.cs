// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Tests.Fakes;

namespace XiHan.Framework.MultiTenancy.Tests;

/// <summary>
/// 曦寒多租户解析选项的测试
/// </summary>
/// <remarks>
/// 这个选项类没有校验方法，价值全在默认值语义上：
/// 两个键集合的元素顺序就是解析优先级，两个开关的默认值决定「不配置时能不能从请求里解析出租户」，
/// 而解析器集合是只读属性 + 可变列表，注册期靠往里插元素来编排链条。
/// 这些都是会被外部依赖的形状，逐条钉死。
/// </remarks>
public class XiHanTenantResolveOptionsTests
{
    /// <summary>
    /// 配置节名称不漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:MultiTenancy:Resolve", XiHanTenantResolveOptions.SectionName);
    }

    /// <summary>
    /// 默认启用 Header 与 QueryString 两种解析方式
    /// </summary>
    [Fact]
    public void Constructor_EnablesHeaderAndQueryStringResolveByDefault()
    {
        var options = new XiHanTenantResolveOptions();

        Assert.True(options.EnableHeaderResolve);
        Assert.True(options.EnableQueryStringResolve);
    }

    /// <summary>
    /// 默认没有回退租户
    /// </summary>
    /// <remarks>
    /// 回退租户一旦有默认值，解析不到租户的请求会被静默归属到某个租户，属于数据安全事故，默认必须为空。
    /// </remarks>
    [Fact]
    public void Constructor_LeavesFallbackTenantNull()
    {
        var options = new XiHanTenantResolveOptions();

        Assert.Null(options.FallbackTenant);
    }

    /// <summary>
    /// 默认解析器集合为空，由注册期填充
    /// </summary>
    [Fact]
    public void Constructor_StartsWithEmptyResolverList()
    {
        var options = new XiHanTenantResolveOptions();

        Assert.NotNull(options.TenantResolvers);
        Assert.Empty(options.TenantResolvers);
    }

    /// <summary>
    /// 默认 Header 键集合的内容与优先级顺序
    /// </summary>
    [Fact]
    public void Constructor_SetsDefaultHeaderKeysInPriorityOrder()
    {
        var options = new XiHanTenantResolveOptions();

        Assert.Equal(3, options.HeaderKeys.Length);
        Assert.Equal("X-Tenant-Id", options.HeaderKeys[0]);
        Assert.Equal("x-tenant-id", options.HeaderKeys[1]);
        Assert.Equal("TenantId", options.HeaderKeys[2]);
    }

    /// <summary>
    /// 默认 QueryString 键集合的内容与优先级顺序
    /// </summary>
    [Fact]
    public void Constructor_SetsDefaultQueryStringKeysInPriorityOrder()
    {
        var options = new XiHanTenantResolveOptions();

        Assert.Equal(2, options.QueryStringKeys.Length);
        Assert.Equal("tenantId", options.QueryStringKeys[0]);
        Assert.Equal("tenant", options.QueryStringKeys[1]);
    }

    /// <summary>
    /// 解析器集合是只读属性但列表本身可变
    /// </summary>
    /// <remarks>
    /// 注册期通过 Insert/Add 编排链条，属性若可写会让「后配置的模块整体替换掉前面的解析器」成为可能，
    /// 这条形状必须钉死。
    /// </remarks>
    [Fact]
    public void TenantResolvers_IsReadOnlyPropertyWithMutableList()
    {
        var property = typeof(XiHanTenantResolveOptions).GetProperty(nameof(XiHanTenantResolveOptions.TenantResolvers));
        var options = new XiHanTenantResolveOptions();

        Assert.NotNull(property);
        Assert.False(property.CanWrite);

        options.TenantResolvers.Add(new RecordingTenantResolveContributor("Header"));
        options.TenantResolvers.Insert(0, new RecordingTenantResolveContributor("CurrentUser"));

        Assert.Equal(2, options.TenantResolvers.Count);
        Assert.Equal("CurrentUser", options.TenantResolvers[0].Name);
        Assert.Equal("Header", options.TenantResolvers[1].Name);
    }

    /// <summary>
    /// 两个键集合与开关均可被配置覆盖
    /// </summary>
    [Fact]
    public void Options_AreOverridable()
    {
        var options = new XiHanTenantResolveOptions
        {
            EnableHeaderResolve = false,
            EnableQueryStringResolve = false,
            HeaderKeys = ["X-Custom-Tenant"],
            QueryStringKeys = ["t"],
            FallbackTenant = "default-tenant"
        };

        Assert.False(options.EnableHeaderResolve);
        Assert.False(options.EnableQueryStringResolve);
        Assert.Equal("X-Custom-Tenant", Assert.Single(options.HeaderKeys));
        Assert.Equal("t", Assert.Single(options.QueryStringKeys));
        Assert.Equal("default-tenant", options.FallbackTenant);
    }

    /// <summary>
    /// 不同实例之间不共享任何集合
    /// </summary>
    /// <remarks>
    /// 默认值若写成静态字段，两个租户解析选项实例会互相污染；
    /// 用例往其中一个实例里插元素、改数组，再检查另一个实例是否保持默认。
    /// </remarks>
    [Fact]
    public void Instances_DoNotShareCollections()
    {
        var first = new XiHanTenantResolveOptions();
        var second = new XiHanTenantResolveOptions();

        first.TenantResolvers.Add(new RecordingTenantResolveContributor("Header"));
        first.HeaderKeys = ["X-Custom-Tenant"];

        Assert.NotSame(first.TenantResolvers, second.TenantResolvers);
        Assert.Empty(second.TenantResolvers);
        Assert.Equal(3, second.HeaderKeys.Length);
        Assert.NotSame(first.QueryStringKeys, second.QueryStringKeys);
    }
}
