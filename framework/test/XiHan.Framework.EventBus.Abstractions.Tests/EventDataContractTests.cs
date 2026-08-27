// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 事件数据可选接口契约测试
/// </summary>
/// <remarks>
/// 覆盖两个「事件数据自我描述」的接口：
/// <see cref="IEventDataMayHaveTenantId"/> 决定事件被投递时切到哪个租户上下文，
/// <see cref="IEventDataWithInheritableGenericArgument"/> 决定泛型事件能否沿实体继承链向父类型广播。
/// 后者的语义只有配合 <c>MakeGenericType</c> 重建实例才成立，因此这里按事件总线的真实用法验证。
/// </remarks>
public class EventDataContractTests
{
    /// <summary>
    /// 声明与租户相关时回填租户唯一标识
    /// </summary>
    [Fact]
    public void IsMultiTenant_WhenEventCarriesTenant_ReturnsTrueAndSetsTenantId()
    {
        IEventDataMayHaveTenantId eventData = new TenantAwareSampleEvent(1024L);

        var isMultiTenant = eventData.IsMultiTenant(out var tenantId);

        Assert.True(isMultiTenant);
        Assert.Equal(1024L, tenantId);
    }

    /// <summary>
    /// 声明与租户无关时出参无意义，按契约固定为 null
    /// </summary>
    /// <remarks>
    /// 返回 false 时出参不代表「租户唯一标识为 null 的租户」，调用方必须先看返回值再看出参。
    /// </remarks>
    [Fact]
    public void IsMultiTenant_WhenEventHasNoTenant_ReturnsFalse()
    {
        IEventDataMayHaveTenantId eventData = new TenantAgnosticSampleEvent();

        var isMultiTenant = eventData.IsMultiTenant(out var tenantId);

        Assert.False(isMultiTenant);
        Assert.Null(tenantId);
    }

    /// <summary>
    /// 租户唯一标识以可空 long 承载，与多租户模块口径一致
    /// </summary>
    [Fact]
    public void IsMultiTenant_TenantIdParameter_IsNullableLongOutParameter()
    {
        var method = typeof(IEventDataMayHaveTenantId)
            .GetMethod(nameof(IEventDataMayHaveTenantId.IsMultiTenant));

        Assert.NotNull(method);
        Assert.Equal(typeof(bool), method.ReturnType);

        var parameter = Assert.Single(method.GetParameters());
        Assert.True(parameter.IsOut);
        Assert.Equal(typeof(long?).MakeByRefType(), parameter.ParameterType);
    }

    /// <summary>
    /// 构造参数原样返回，可用于以父类泛型参数重建事件实例
    /// </summary>
    [Fact]
    public void GetConstructorArgs_ReturnsArgumentsForRebuild()
    {
        var student = new SampleStudent { Name = "张三", StudentNo = "S-001" };
        IEventDataWithInheritableGenericArgument eventData = new InheritableSampleEventData<SampleStudent>(student);

        var args = eventData.GetConstructorArgs();

        Assert.Same(student, Assert.Single(args));
    }

    /// <summary>
    /// 用返回的构造参数可以成功构造父类型的同名泛型事件
    /// </summary>
    /// <remarks>
    /// 这是该接口存在的唯一理由：触发 <c>Event{Student}</c> 时事件总线要能再触发一次 <c>Event{Person}</c>。
    /// </remarks>
    [Fact]
    public void GetConstructorArgs_CanRebuildEventWithBaseTypeArgument()
    {
        var student = new SampleStudent { Name = "张三", StudentNo = "S-001" };
        IEventDataWithInheritableGenericArgument eventData = new InheritableSampleEventData<SampleStudent>(student);

        var baseEventType = typeof(InheritableSampleEventData<>).MakeGenericType(typeof(SamplePerson));
        var rebuilt = Activator.CreateInstance(baseEventType, eventData.GetConstructorArgs());

        Assert.NotNull(rebuilt);
        Assert.IsType<InheritableSampleEventData<SamplePerson>>(rebuilt);
        Assert.Same(student, ((InheritableSampleEventData<SamplePerson>)rebuilt).Entity);
    }

    /// <summary>
    /// 两个可选接口都不强制事件数据继承任何基类
    /// </summary>
    [Theory]
    [InlineData(typeof(IEventDataMayHaveTenantId))]
    [InlineData(typeof(IEventDataWithInheritableGenericArgument))]
    public void EventDataInterfaces_AreStandaloneInterfaces(Type contract)
    {
        Assert.True(contract.IsInterface);
        Assert.Empty(contract.GetInterfaces());
    }
}
