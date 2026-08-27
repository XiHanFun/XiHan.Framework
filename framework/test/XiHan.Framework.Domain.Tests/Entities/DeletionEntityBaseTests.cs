// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities;

/// <summary>
/// 软删除与删除审计实体基类测试
/// </summary>
/// <remarks>
/// 软删除标记必须默认 false：一旦默认成 true，全局查询过滤器会把新建实体直接过滤掉。
/// </remarks>
public class DeletionEntityBaseTests
{
    /// <summary>
    /// 软删除实体构造后未被删除
    /// </summary>
    [Fact]
    public void Constructor_OnSoftDeleteEntity_SetsIsDeletedFalse()
    {
        var entity = new SampleSoftDeleteEntity();

        Assert.False(entity.IsDeleted);
    }

    /// <summary>
    /// 删除审计实体构造后未被删除且无删除时间
    /// </summary>
    [Fact]
    public void Constructor_OnDeletionEntity_LeavesDeletionFieldsEmpty()
    {
        var entity = new SampleDeletionEntity();

        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedTime);
    }

    /// <summary>
    /// 删除标记与删除时间均可写
    /// </summary>
    [Fact]
    public void DeletionFields_WhenAssigned_KeepAssignedValues()
    {
        var deletedTime = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);
        var entity = new SampleDeletionEntity
        {
            IsDeleted = true,
            DeletedTime = deletedTime
        };

        Assert.True(entity.IsDeleted);
        Assert.Equal(deletedTime, entity.DeletedTime);
    }

    /// <summary>
    /// 带主键版本无参构造为瞬态实体且未删除
    /// </summary>
    [Fact]
    public void Constructor_WithKeyAndNoArguments_IsTransientAndNotDeleted()
    {
        var entity = new SampleDeletionEntityWithKey();

        Assert.True(entity.IsTransient());
        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedTime);
        Assert.Equal(0L, entity.DeletedId);
        Assert.Null(entity.DeletedBy);
    }

    /// <summary>
    /// 单参构造只写主键
    /// </summary>
    [Fact]
    public void Constructor_WithBasicId_SetsIdAndKeepsNotDeleted()
    {
        var entity = new SampleDeletionEntityWithKey(31);

        Assert.Equal(31L, entity.BasicId);
        Assert.False(entity.IsDeleted);
    }

    /// <summary>
    /// 双参构造写入删除者主键但仍不标记为已删除
    /// </summary>
    /// <remarks>
    /// 这是源码的既有语义：构造函数只登记删除者，删除动作由审计扩展方法完成。
    /// </remarks>
    [Fact]
    public void Constructor_WithBasicIdAndDeletedId_SetsDeleterButNotFlag()
    {
        var entity = new SampleDeletionEntityWithKey(31, 41);

        Assert.Equal(31L, entity.BasicId);
        Assert.Equal(41L, entity.DeletedId);
        Assert.False(entity.IsDeleted);
    }

    /// <summary>
    /// 删除审计接口层级完整
    /// </summary>
    [Fact]
    public void DeletionEntityBase_ImplementsDeletionContracts()
    {
        Assert.IsAssignableFrom<ISoftDelete>(new SampleSoftDeleteEntity());
        Assert.IsAssignableFrom<IDeletionEntity>(new SampleDeletionEntity());
        Assert.IsAssignableFrom<ISoftDelete>(new SampleDeletionEntityWithKey());
        Assert.IsAssignableFrom<IDeletionEntity<long>>(new SampleDeletionEntityWithKey());
    }
}
