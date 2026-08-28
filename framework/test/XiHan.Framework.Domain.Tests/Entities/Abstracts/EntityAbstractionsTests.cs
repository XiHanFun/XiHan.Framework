// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities.Abstracts;

/// <summary>
/// 实体抽象契约测试
/// </summary>
/// <remarks>
/// 这些接口的继承关系被仓储的全局查询过滤器、审计拦截器和分表策略直接依赖
/// （例如「实体是不是 ISplitTableEntity」决定要不要走分表），所以层级本身就是契约，必须锁死。
/// </remarks>
public class EntityAbstractionsTests
{
    /// <summary>
    /// 删除审计接口继承软删除标记接口
    /// </summary>
    [Fact]
    public void IDeletionEntity_Inherits_ISoftDelete()
    {
        Assert.True(typeof(ISoftDelete).IsAssignableFrom(typeof(IDeletionEntity)));
        Assert.True(typeof(IDeletionEntity).IsAssignableFrom(typeof(IDeletionEntity<long>)));
    }

    /// <summary>
    /// 带用户的审计接口继承对应的无用户接口
    /// </summary>
    [Fact]
    public void GenericAuditContracts_Inherit_NonGenericCounterparts()
    {
        Assert.True(typeof(ICreationEntity).IsAssignableFrom(typeof(ICreationEntity<long>)));
        Assert.True(typeof(IModificationEntity).IsAssignableFrom(typeof(IModificationEntity<long>)));
        Assert.True(typeof(IEntityBase).IsAssignableFrom(typeof(IEntityBase<long>)));
    }

    /// <summary>
    /// 完整审计接口聚合创建、修改、删除三组契约
    /// </summary>
    [Fact]
    public void IFullAuditedEntity_Aggregates_AllAuditContracts()
    {
        Assert.True(typeof(IEntityBase).IsAssignableFrom(typeof(IFullAuditedEntity)));
        Assert.True(typeof(ICreationEntity).IsAssignableFrom(typeof(IFullAuditedEntity)));
        Assert.True(typeof(IModificationEntity).IsAssignableFrom(typeof(IFullAuditedEntity)));
        Assert.True(typeof(IDeletionEntity).IsAssignableFrom(typeof(IFullAuditedEntity)));
    }

    /// <summary>
    /// 带用户的完整审计接口聚合对应的带用户契约
    /// </summary>
    [Fact]
    public void GenericFullAuditedEntity_Aggregates_GenericAuditContracts()
    {
        Assert.True(typeof(IEntityBase<long>).IsAssignableFrom(typeof(IFullAuditedEntity<long>)));
        Assert.True(typeof(ICreationEntity<long>).IsAssignableFrom(typeof(IFullAuditedEntity<long>)));
        Assert.True(typeof(IModificationEntity<long>).IsAssignableFrom(typeof(IFullAuditedEntity<long>)));
        Assert.True(typeof(IDeletionEntity<long>).IsAssignableFrom(typeof(IFullAuditedEntity<long>)));
    }

    /// <summary>
    /// 分表实体接口强制携带创建时间
    /// </summary>
    /// <remarks>
    /// 分表按创建时间划分范围，缺了 CreatedTime 分表路由就无从计算。
    /// </remarks>
    [Fact]
    public void ISplitTableEntity_Requires_CreationTime()
    {
        Assert.True(typeof(ICreationEntity).IsAssignableFrom(typeof(ISplitTableEntity)));
    }

    /// <summary>
    /// 严格隔离多租户接口是多租户接口的收紧标记
    /// </summary>
    [Fact]
    public void IStrictMultiTenantEntity_Narrows_IMultiTenantEntity()
    {
        Assert.True(typeof(IMultiTenantEntity).IsAssignableFrom(typeof(IStrictMultiTenantEntity)));
        Assert.False(typeof(IStrictMultiTenantEntity).IsAssignableFrom(typeof(IMultiTenantEntity)));
    }

    /// <summary>
    /// 链路追踪实体的追踪标识可读写且默认为空
    /// </summary>
    [Fact]
    public void ITraceableEntity_TraceId_IsNullableAndWritable()
    {
        ITraceableEntity entity = new SampleTraceableEntity();

        Assert.Null(entity.TraceId);

        entity.TraceId = "trace-1";

        Assert.Equal("trace-1", entity.TraceId);
    }

    /// <summary>
    /// 链路追踪提供者在无上下文时返回空
    /// </summary>
    [Fact]
    public void ITraceIdProvider_WhenNoContext_ReturnsNull()
    {
        ITraceIdProvider empty = new SampleTraceIdProvider(null);
        ITraceIdProvider filled = new SampleTraceIdProvider("trace-2");

        Assert.Null(empty.GetCurrentTraceId());
        Assert.Equal("trace-2", filled.GetCurrentTraceId());
    }
}
