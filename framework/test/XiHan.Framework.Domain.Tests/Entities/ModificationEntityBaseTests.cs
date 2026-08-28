// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities;

/// <summary>
/// 修改审计实体基类测试
/// </summary>
/// <remarks>
/// 修改时间必须默认为 null——「从未修改过」与「修改时间等于创建时间」是两种不同的业务口径。
/// </remarks>
public class ModificationEntityBaseTests
{
    /// <summary>
    /// 构造后修改时间为空
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_LeavesModifiedTimeNull()
    {
        var entity = new SampleModificationEntity();

        Assert.Null(entity.ModifiedTime);
    }

    /// <summary>
    /// 修改时间可写
    /// </summary>
    [Fact]
    public void ModifiedTime_WhenAssigned_KeepsAssignedValue()
    {
        var expected = new DateTimeOffset(2024, 7, 8, 9, 10, 11, TimeSpan.Zero);
        var entity = new SampleModificationEntity
        {
            ModifiedTime = expected
        };

        Assert.Equal(expected, entity.ModifiedTime);
    }

    /// <summary>
    /// 带主键版本无参构造为瞬态实体且修改信息为空
    /// </summary>
    [Fact]
    public void Constructor_WithKeyAndNoArguments_IsTransientAndUnmodified()
    {
        var entity = new SampleModificationEntityWithKey();

        Assert.True(entity.IsTransient());
        Assert.Null(entity.ModifiedTime);
        Assert.Equal(0L, entity.ModifiedId);
        Assert.Null(entity.ModifiedBy);
    }

    /// <summary>
    /// 单参构造只写主键
    /// </summary>
    [Fact]
    public void Constructor_WithBasicId_SetsIdOnly()
    {
        var entity = new SampleModificationEntityWithKey(51);

        Assert.Equal(51L, entity.BasicId);
        Assert.Null(entity.ModifiedTime);
    }

    /// <summary>
    /// 双参构造写入修改者主键但不写修改时间
    /// </summary>
    [Fact]
    public void Constructor_WithBasicIdAndModifiedId_SetsModifierButNotTime()
    {
        var entity = new SampleModificationEntityWithKey(51, 61);

        Assert.Equal(51L, entity.BasicId);
        Assert.Equal(61L, entity.ModifiedId);
        Assert.Null(entity.ModifiedTime);
    }

    /// <summary>
    /// 修改审计接口层级完整
    /// </summary>
    [Fact]
    public void ModificationEntityBase_ImplementsModificationContracts()
    {
        Assert.IsAssignableFrom<IModificationEntity>(new SampleModificationEntity());
        Assert.IsAssignableFrom<IModificationEntity>(new SampleModificationEntityWithKey());
        Assert.IsAssignableFrom<IModificationEntity<long>>(new SampleModificationEntityWithKey());
    }
}
