// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Tests.Samples;

/// <summary>
/// 无主键实体基类的最小具体子类
/// </summary>
public sealed class SampleRowVersionEntity : EntityBase
{
}

/// <summary>
/// long 主键实体基类的最小具体子类
/// </summary>
public class SampleEntity : EntityBase<long>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SampleEntity()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SampleEntity(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 暴露受保护的主键写入口，供持久化语义测试使用
    /// </summary>
    /// <param name="basicId">主键</param>
    public void AssignBasicId(long basicId)
    {
        BasicId = basicId;
    }
}

/// <summary>
/// 与 <see cref="SampleEntity"/> 主键类型相同但 CLR 类型不同的实体，用于验证跨类型相等性
/// </summary>
public sealed class AnotherSampleEntity : EntityBase<long>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public AnotherSampleEntity(long basicId) : base(basicId)
    {
    }
}

/// <summary>
/// 由 <see cref="SampleEntity"/> 派生的实体，用于验证父子类型之间不相等
/// </summary>
public sealed class DerivedSampleEntity : SampleEntity
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public DerivedSampleEntity(long basicId) : base(basicId)
    {
    }
}

/// <summary>
/// Guid 主键实体，用于验证引用型默认值的瞬态判定
/// </summary>
public sealed class SampleGuidEntity : EntityBase<Guid>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SampleGuidEntity()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SampleGuidEntity(Guid basicId) : base(basicId)
    {
    }
}

/// <summary>
/// 创建审计实体基类的最小具体子类
/// </summary>
public sealed class SampleCreationEntity : CreationEntityBase
{
}

/// <summary>
/// 带主键的创建审计实体基类的最小具体子类
/// </summary>
public sealed class SampleCreationEntityWithKey : CreationEntityBase<long>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SampleCreationEntityWithKey()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SampleCreationEntityWithKey(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="createdId">创建者主键</param>
    public SampleCreationEntityWithKey(long basicId, long createdId) : base(basicId, createdId)
    {
    }
}

/// <summary>
/// 软删除实体基类的最小具体子类
/// </summary>
public sealed class SampleSoftDeleteEntity : SoftDeleteEntityBase
{
}

/// <summary>
/// 删除审计实体基类的最小具体子类
/// </summary>
public sealed class SampleDeletionEntity : DeletionEntityBase
{
}

/// <summary>
/// 带主键的删除审计实体基类的最小具体子类
/// </summary>
public sealed class SampleDeletionEntityWithKey : DeletionEntityBase<long>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SampleDeletionEntityWithKey()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SampleDeletionEntityWithKey(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="deletedId">删除者主键</param>
    public SampleDeletionEntityWithKey(long basicId, long deletedId) : base(basicId, deletedId)
    {
    }
}

/// <summary>
/// 修改审计实体基类的最小具体子类
/// </summary>
public sealed class SampleModificationEntity : ModificationEntityBase
{
}

/// <summary>
/// 带主键的修改审计实体基类的最小具体子类
/// </summary>
public sealed class SampleModificationEntityWithKey : ModificationEntityBase<long>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SampleModificationEntityWithKey()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SampleModificationEntityWithKey(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="modifiedId">修改者主键</param>
    public SampleModificationEntityWithKey(long basicId, long modifiedId) : base(basicId, modifiedId)
    {
    }
}

/// <summary>
/// 完整审计实体基类的最小具体子类
/// </summary>
public sealed class SampleFullAuditedEntity : FullAuditedEntityBase
{
}

/// <summary>
/// 带主键的完整审计实体基类的最小具体子类
/// </summary>
public sealed class SampleFullAuditedEntityWithKey : FullAuditedEntityBase<long>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SampleFullAuditedEntityWithKey()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SampleFullAuditedEntityWithKey(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="createdId">创建者主键</param>
    public SampleFullAuditedEntityWithKey(long basicId, long createdId) : base(basicId, createdId)
    {
    }
}

/// <summary>
/// 多租户实体基类的最小具体子类
/// </summary>
public sealed class SampleMultiTenantEntity : MultiTenantEntityBase<long>
{
    /// <summary>
    /// 暴露受保护的主键写入口
    /// </summary>
    /// <param name="basicId">主键</param>
    public void AssignBasicId(long basicId)
    {
        BasicId = basicId;
    }
}

/// <summary>
/// 多租户创建审计实体基类的最小具体子类
/// </summary>
public sealed class SampleMultiTenantCreationEntity : MultiTenantCreationEntityBase<long>
{
}

/// <summary>
/// 多租户修改审计实体基类的最小具体子类
/// </summary>
public sealed class SampleMultiTenantModificationEntity : MultiTenantModificationEntityBase<long>
{
}

/// <summary>
/// 多租户删除审计实体基类的最小具体子类
/// </summary>
public sealed class SampleMultiTenantDeletionEntity : MultiTenantDeletionEntityBase<long>
{
}

/// <summary>
/// 多租户完整审计实体基类的最小具体子类
/// </summary>
public sealed class SampleMultiTenantFullAuditedEntity : MultiTenantFullAuditedEntityBase<long>
{
}

/// <summary>
/// 链路追踪实体的最小实现
/// </summary>
public sealed class SampleTraceableEntity : ITraceableEntity
{
    /// <summary>
    /// 链路追踪标识
    /// </summary>
    public string? TraceId { get; set; }
}

/// <summary>
/// 链路追踪标识提供者的最小实现
/// </summary>
public sealed class SampleTraceIdProvider : ITraceIdProvider
{
    private readonly string? _traceId;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="traceId">要返回的链路追踪标识</param>
    public SampleTraceIdProvider(string? traceId)
    {
        _traceId = traceId;
    }

    /// <summary>
    /// 获取当前请求的链路追踪标识
    /// </summary>
    /// <returns>链路追踪标识</returns>
    public string? GetCurrentTraceId()
    {
        return _traceId;
    }
}
