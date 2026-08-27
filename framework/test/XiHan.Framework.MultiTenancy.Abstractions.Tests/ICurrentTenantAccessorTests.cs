// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 当前租户访问器契约的测试
/// </summary>
/// <remarks>
/// 访问器是整个多租户上下文的唯一可变存储点，<see cref="ICurrentTenant.Change"/> 的还原能力完全建立在
/// <see cref="ICurrentTenantAccessor.Current"/> 可读可写、且写入什么就读出什么之上，所以这里把它单独锁一遍。
/// </remarks>
public class ICurrentTenantAccessorTests
{
    /// <summary>
    /// 未设置任何租户时当前租户为 null
    /// </summary>
    [Fact]
    public void Current_WithoutAssignment_IsNull()
    {
        ICurrentTenantAccessor accessor = new FakeCurrentTenantAccessor();

        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 写入的租户信息必须原样读回同一个实例，不允许被复制或重建
    /// </summary>
    [Fact]
    public void Current_AfterAssignment_ReturnsSameInstance()
    {
        ICurrentTenantAccessor accessor = new FakeCurrentTenantAccessor();
        var info = new BasicTenantInfo(7L, "曦寒租户");

        accessor.Current = info;

        Assert.Same(info, accessor.Current);
        Assert.Equal<long?>(7L, accessor.Current!.TenantId);
        Assert.Equal("曦寒租户", accessor.Current.Name);
    }

    /// <summary>
    /// 写入 null 表示回到宿主上下文
    /// </summary>
    [Fact]
    public void Current_AssignedNull_ClearsTenant()
    {
        ICurrentTenantAccessor accessor = new FakeCurrentTenantAccessor
        {
            Current = new BasicTenantInfo(7L)
        };

        accessor.Current = null;

        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 契约要求 Current 是可读可写的引用槽位
    /// </summary>
    /// <remarks>
    /// 一旦被改成只读属性，租户切换作用域就无法还原，这是必须锁死的形状约束。
    /// </remarks>
    [Fact]
    public void Contract_Current_IsReadWriteAndNullable()
    {
        var property = typeof(ICurrentTenantAccessor).GetProperty(nameof(ICurrentTenantAccessor.Current));

        Assert.NotNull(property);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);
        Assert.Equal(typeof(BasicTenantInfo), property.PropertyType);
    }
}
