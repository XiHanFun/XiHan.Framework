// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 基本租户信息的测试
/// </summary>
/// <remarks>
/// <see cref="BasicTenantInfo"/> 是租户上下文里被反复快照/还原的那份不可变数据，
/// 因此这里锁死三件事：构造参数的原样透传、两个属性的只读性、以及「它是引用相等而不是值相等」。
/// 最后一条尤其重要——它是 class 不是 record，任何依赖值相等去判断「租户没变」的调用方都是错的。
/// </remarks>
public class BasicTenantInfoTests
{
    /// <summary>
    /// 同时给出唯一标识与名称时两者原样暴露
    /// </summary>
    [Fact]
    public void Constructor_WithIdAndName_ExposesBothValues()
    {
        var info = new BasicTenantInfo(42L, "曦寒租户");

        Assert.Equal<long?>(42L, info.TenantId);
        Assert.Equal("曦寒租户", info.Name);
    }

    /// <summary>
    /// 名称参数可省略，省略时为 null
    /// </summary>
    [Fact]
    public void Constructor_WithoutName_LeavesNameNull()
    {
        var info = new BasicTenantInfo(42L);

        Assert.Equal<long?>(42L, info.TenantId);
        Assert.Null(info.Name);
    }

    /// <summary>
    /// 唯一标识为 null 表示宿主（无租户）上下文
    /// </summary>
    [Fact]
    public void Constructor_WithNullId_RepresentsHostContext()
    {
        var info = new BasicTenantInfo(null, "宿主");

        Assert.Null(info.TenantId);
        Assert.Equal("宿主", info.Name);
    }

    /// <summary>
    /// 唯一标识不做任何归一化，边界值原样保留
    /// </summary>
    /// <remarks>
    /// 0 必须被当作合法租户唯一标识保留，不能被实现当成「空值」吞掉——只有 null 才代表无租户。
    /// </remarks>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void Constructor_WithBoundaryId_KeepsValueUnchanged(long tenantId)
    {
        var info = new BasicTenantInfo(tenantId);

        Assert.Equal<long?>(tenantId, info.TenantId);
    }

    /// <summary>
    /// 名称允许为空字符串，不会被归一化成 null
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_KeepsValueUnchanged(string name)
    {
        var info = new BasicTenantInfo(1L, name);

        Assert.Equal(name, info.Name);
    }

    /// <summary>
    /// 值相同的两个实例仍然互不相等，走引用相等语义
    /// </summary>
    [Fact]
    public void Equality_WithSameValues_UsesReferenceSemantics()
    {
        var left = new BasicTenantInfo(1L, "曦寒租户");
        var right = new BasicTenantInfo(1L, "曦寒租户");

        Assert.NotSame(left, right);
        Assert.False(left.Equals(right));
        Assert.NotEqual(left, right);
        Assert.True(left.Equals(left));
    }

    /// <summary>
    /// 两个属性均为只读，实例创建后不可篡改
    /// </summary>
    [Fact]
    public void Properties_AreReadOnlyAfterConstruction()
    {
        var tenantId = typeof(BasicTenantInfo).GetProperty(nameof(BasicTenantInfo.TenantId));
        var name = typeof(BasicTenantInfo).GetProperty(nameof(BasicTenantInfo.Name));

        Assert.NotNull(tenantId);
        Assert.NotNull(name);
        Assert.True(tenantId.CanRead);
        Assert.True(name.CanRead);
        Assert.False(tenantId.CanWrite);
        Assert.False(name.CanWrite);
    }

    /// <summary>
    /// 契约类型只提供一个构造函数，且名称参数是可选的
    /// </summary>
    /// <remarks>
    /// 名称的可选性被 <see cref="ICurrentTenant.Change"/> 直接依赖，一旦变成必填会连锁破坏所有只传唯一标识的调用点。
    /// </remarks>
    [Fact]
    public void Constructor_HasSingleOverload_WithOptionalName()
    {
        var constructors = typeof(BasicTenantInfo).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        var constructor = Assert.Single(constructors);
        var parameters = constructor.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(long?), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.True(parameters[1].IsOptional);
    }
}
