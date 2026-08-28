// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 当前租户契约的测试
/// </summary>
/// <remarks>
/// 抽象包本身不含实现，这里用 <see cref="FakeCurrentTenant"/> 按接口 XML 文档描述的语义落地一份最小实现，
/// 断言的是「文档承诺的行为」而不是某个具体实现的内部细节：
/// 唯一标识为 null 即不可用、Change 返回的释放器负责还原上一层上下文、嵌套作用域按后进先出还原。
/// </remarks>
public class ICurrentTenantTests
{
    /// <summary>
    /// 没有任何切换作用域时处于宿主上下文
    /// </summary>
    [Fact]
    public void Id_WithoutAnyScope_IsNullAndUnavailable()
    {
        var currentTenant = CreateCurrentTenant();

        Assert.Null(currentTenant.Id);
        Assert.Null(currentTenant.Name);
        Assert.False(currentTenant.IsAvailable);
    }

    /// <summary>
    /// 切换到指定租户后唯一标识与名称同时生效
    /// </summary>
    [Fact]
    public void Change_WithIdAndName_ExposesBothValues()
    {
        var currentTenant = CreateCurrentTenant();

        using (currentTenant.Change(9L, "曦寒租户"))
        {
            Assert.True(currentTenant.IsAvailable);
            Assert.Equal<long?>(9L, currentTenant.Id);
            Assert.Equal("曦寒租户", currentTenant.Name);
        }
    }

    /// <summary>
    /// 任意非 null 唯一标识都算作租户可用，包括 0 与负数
    /// </summary>
    /// <remarks>
    /// 可用性只看 null 与否，不看数值大小；把 0 当作「无租户」是常见误实现，这里显式钉死。
    /// </remarks>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    public void Change_WithAnyNonNullId_MakesTenantAvailable(long tenantId)
    {
        var currentTenant = CreateCurrentTenant();

        using (currentTenant.Change(tenantId))
        {
            Assert.True(currentTenant.IsAvailable);
            Assert.Equal<long?>(tenantId, currentTenant.Id);
        }
    }

    /// <summary>
    /// 名称参数可省略，省略时当前租户名称为 null
    /// </summary>
    [Fact]
    public void Change_WithoutName_LeavesNameNull()
    {
        var currentTenant = CreateCurrentTenant();

        using (currentTenant.Change(9L))
        {
            Assert.Equal<long?>(9L, currentTenant.Id);
            Assert.Null(currentTenant.Name);
        }
    }

    /// <summary>
    /// 释放作用域后回到进入前的宿主上下文
    /// </summary>
    [Fact]
    public void Change_Disposed_RestoresHostContext()
    {
        var currentTenant = CreateCurrentTenant();

        var scope = currentTenant.Change(9L, "曦寒租户");
        Assert.True(currentTenant.IsAvailable);

        scope.Dispose();

        Assert.Null(currentTenant.Id);
        Assert.Null(currentTenant.Name);
        Assert.False(currentTenant.IsAvailable);
    }

    /// <summary>
    /// 嵌套切换按后进先出逐层还原
    /// </summary>
    [Fact]
    public void Change_Nested_RestoresInLastInFirstOutOrder()
    {
        var currentTenant = CreateCurrentTenant();

        using (currentTenant.Change(1L, "租户一"))
        {
            Assert.Equal<long?>(1L, currentTenant.Id);

            using (currentTenant.Change(2L, "租户二"))
            {
                Assert.Equal<long?>(2L, currentTenant.Id);
                Assert.Equal("租户二", currentTenant.Name);

                using (currentTenant.Change(3L, "租户三"))
                {
                    Assert.Equal<long?>(3L, currentTenant.Id);
                }

                Assert.Equal<long?>(2L, currentTenant.Id);
                Assert.Equal("租户二", currentTenant.Name);
            }

            Assert.Equal<long?>(1L, currentTenant.Id);
            Assert.Equal("租户一", currentTenant.Name);
        }

        Assert.Null(currentTenant.Id);
    }

    /// <summary>
    /// 在租户作用域内切换到 null 表示临时回到宿主，释放后仍要还原为原租户
    /// </summary>
    /// <remarks>
    /// 这是跨租户的平台级操作（例如宿主查询全局数据）最关键的一条路径，还原失败会直接造成数据串租户。
    /// </remarks>
    [Fact]
    public void Change_ToNullInsideTenantScope_TemporarilySwitchesToHost()
    {
        var currentTenant = CreateCurrentTenant();

        using (currentTenant.Change(1L, "租户一"))
        {
            using (currentTenant.Change(null))
            {
                Assert.False(currentTenant.IsAvailable);
                Assert.Null(currentTenant.Id);
                Assert.Null(currentTenant.Name);
            }

            Assert.True(currentTenant.IsAvailable);
            Assert.Equal<long?>(1L, currentTenant.Id);
            Assert.Equal("租户一", currentTenant.Name);
        }
    }

    /// <summary>
    /// 重复释放同一个作用域不会再次还原，不能污染后来的作用域
    /// </summary>
    /// <remarks>
    /// 用例刻意在第一次释放之后又开了一层新作用域，用来把「幂等」和「碰巧还原成同一个值」区分开：
    /// 若释放器不幂等，第二次释放会把上下文错误地拉回到 1，从而覆盖掉当前正生效的 3。
    /// </remarks>
    [Fact]
    public void Change_DisposedTwice_IsIdempotent()
    {
        var currentTenant = CreateCurrentTenant();

        using (currentTenant.Change(1L, "租户一"))
        {
            var inner = currentTenant.Change(2L, "租户二");
            inner.Dispose();
            Assert.Equal<long?>(1L, currentTenant.Id);

            using (currentTenant.Change(3L, "租户三"))
            {
                inner.Dispose();

                Assert.Equal<long?>(3L, currentTenant.Id);
                Assert.Equal("租户三", currentTenant.Name);
            }

            Assert.Equal<long?>(1L, currentTenant.Id);
        }
    }

    /// <summary>
    /// 切换动作直接落在访问器上，两者共享同一份上下文
    /// </summary>
    [Fact]
    public void Change_WritesThroughToAccessor()
    {
        var accessor = new FakeCurrentTenantAccessor();
        ICurrentTenant currentTenant = new FakeCurrentTenant(accessor);

        using (currentTenant.Change(5L, "曦寒租户"))
        {
            var scoped = accessor.Current;

            Assert.NotNull(scoped);
            Assert.Equal<long?>(5L, scoped.TenantId);
            Assert.Equal("曦寒租户", scoped.Name);
        }

        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 契约要求 Change 返回释放器，且名称参数可选
    /// </summary>
    /// <remarks>
    /// 返回类型一旦不是 <see cref="IDisposable"/>，所有 using 形态的调用点都会失效，属于必须锁死的契约形状。
    /// </remarks>
    [Fact]
    public void Contract_Change_ReturnsDisposableWithOptionalName()
    {
        var method = typeof(ICurrentTenant).GetMethod(nameof(ICurrentTenant.Change));

        Assert.NotNull(method);
        Assert.Equal(typeof(IDisposable), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(long?), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.True(parameters[1].IsOptional);
    }

    /// <summary>
    /// 契约要求三个状态属性均为只读
    /// </summary>
    [Fact]
    public void Contract_StateProperties_AreReadOnly()
    {
        var isAvailable = typeof(ICurrentTenant).GetProperty(nameof(ICurrentTenant.IsAvailable));
        var id = typeof(ICurrentTenant).GetProperty(nameof(ICurrentTenant.Id));
        var name = typeof(ICurrentTenant).GetProperty(nameof(ICurrentTenant.Name));

        Assert.NotNull(isAvailable);
        Assert.NotNull(id);
        Assert.NotNull(name);
        Assert.Equal(typeof(bool), isAvailable.PropertyType);
        Assert.Equal(typeof(long?), id.PropertyType);
        Assert.Equal(typeof(string), name.PropertyType);
        Assert.False(isAvailable.CanWrite);
        Assert.False(id.CanWrite);
        Assert.False(name.CanWrite);
    }

    /// <summary>
    /// 创建基于手写访问器的当前租户实例
    /// </summary>
    /// <returns>当前租户</returns>
    private static ICurrentTenant CreateCurrentTenant()
    {
        return new FakeCurrentTenant(new FakeCurrentTenantAccessor());
    }
}
