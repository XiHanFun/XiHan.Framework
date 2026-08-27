// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Extensions;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Extensions;

/// <summary>
/// 审计扩展方法测试
/// </summary>
/// <remarks>
/// 这组扩展是审计拦截器唯一的写入口。所有方法都返回入参本身（不复制），
/// 时间参数为空时统一取 UTC 当下——两条都必须验证，否则批量写入会退化成逐条不同的时间戳。
/// </remarks>
public class AuditingExtensionsTests
{
    /// <summary>
    /// 设置创建审计不带时间时取 UTC 当下并返回原实体
    /// </summary>
    [Fact]
    public void SetCreationAuditInfo_WithoutTime_UsesUtcNowAndReturnsSameInstance()
    {
        ICreationEntity entity = new SampleCreationEntity
        {
            CreatedTime = DateTimeOffset.MinValue
        };
        var before = DateTimeOffset.UtcNow;

        var result = entity.SetCreationAuditInfo();

        Assert.Same(entity, result);
        Assert.InRange(entity.CreatedTime, before, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 设置创建审计可指定明确时间
    /// </summary>
    [Fact]
    public void SetCreationAuditInfo_WithExplicitTime_UsesGivenTime()
    {
        ICreationEntity entity = new SampleCreationEntity();
        var expected = new DateTimeOffset(2023, 3, 4, 5, 6, 7, TimeSpan.Zero);

        entity.SetCreationAuditInfo(expected);

        Assert.Equal(expected, entity.CreatedTime);
    }

    /// <summary>
    /// 带创建者的重载同时写入时间、创建者主键与创建人
    /// </summary>
    [Fact]
    public void SetCreationAuditInfo_WithCreator_WritesAllCreationFields()
    {
        ICreationEntity<long> entity = new SampleCreationEntityWithKey(1);
        var expected = new DateTimeOffset(2023, 3, 4, 5, 6, 7, TimeSpan.Zero);

        var result = entity.SetCreationAuditInfo(9L, "admin", expected);

        Assert.Same(entity, result);
        Assert.Equal(expected, entity.CreatedTime);
        Assert.Equal(9L, entity.CreatedId);
        Assert.Equal("admin", entity.CreatedBy);
    }

    /// <summary>
    /// 批量设置创建审计对每个实体生效且共用同一时间戳
    /// </summary>
    [Fact]
    public void SetCreationAuditInfos_AppliesSameTimestampToEveryEntity()
    {
        var expected = new DateTimeOffset(2023, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var entities = new List<ICreationEntity<long>>
        {
            new SampleCreationEntityWithKey(1),
            new SampleCreationEntityWithKey(2)
        };

        var result = entities.SetCreationAuditInfos(9L, "admin", expected);

        Assert.Same(entities, result);
        Assert.All(result, entity =>
        {
            Assert.Equal(expected, entity.CreatedTime);
            Assert.Equal(9L, entity.CreatedId);
            Assert.Equal("admin", entity.CreatedBy);
        });
    }

    /// <summary>
    /// 批量设置创建审计对空集合安全
    /// </summary>
    [Fact]
    public void SetCreationAuditInfos_WithEmptyCollection_ReturnsEmpty()
    {
        var entities = new List<ICreationEntity<long>>();

        var result = entities.SetCreationAuditInfos(9L);

        Assert.Empty(result);
    }

    /// <summary>
    /// 设置修改审计不带时间时取 UTC 当下
    /// </summary>
    [Fact]
    public void SetModificationAuditInfo_WithoutTime_UsesUtcNow()
    {
        IModificationEntity entity = new SampleModificationEntity();
        var before = DateTimeOffset.UtcNow;

        var result = entity.SetModificationAuditInfo();

        Assert.Same(entity, result);
        Assert.NotNull(entity.ModifiedTime);
        Assert.InRange(entity.ModifiedTime!.Value, before, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 带修改者的重载同时写入时间、修改者主键与修改人
    /// </summary>
    [Fact]
    public void SetModificationAuditInfo_WithModifier_WritesAllModificationFields()
    {
        IModificationEntity<long> entity = new SampleModificationEntityWithKey(1);
        var expected = new DateTimeOffset(2023, 8, 9, 10, 11, 12, TimeSpan.Zero);

        entity.SetModificationAuditInfo(8L, "editor", expected);

        Assert.Equal(expected, entity.ModifiedTime);
        Assert.Equal(8L, entity.ModifiedId);
        Assert.Equal("editor", entity.ModifiedBy);
    }

    /// <summary>
    /// 批量设置修改审计对每个实体生效
    /// </summary>
    [Fact]
    public void SetModificationAuditInfos_AppliesToEveryEntity()
    {
        var expected = new DateTimeOffset(2023, 8, 9, 10, 11, 12, TimeSpan.Zero);
        var entities = new List<IModificationEntity<long>>
        {
            new SampleModificationEntityWithKey(1),
            new SampleModificationEntityWithKey(2)
        };

        var result = entities.SetModificationAuditInfos(8L, "editor", expected);

        Assert.All(result, entity =>
        {
            Assert.Equal(expected, entity.ModifiedTime);
            Assert.Equal(8L, entity.ModifiedId);
            Assert.Equal("editor", entity.ModifiedBy);
        });
    }

    /// <summary>
    /// 设置删除审计同时打开软删除标记
    /// </summary>
    [Fact]
    public void SetDeletionAuditInfo_TurnsOnSoftDeleteFlag()
    {
        IDeletionEntity entity = new SampleDeletionEntity();
        var expected = new DateTimeOffset(2023, 9, 10, 11, 12, 13, TimeSpan.Zero);

        var result = entity.SetDeletionAuditInfo(expected);

        Assert.Same(entity, result);
        Assert.True(entity.IsDeleted);
        Assert.Equal(expected, entity.DeletedTime);
    }

    /// <summary>
    /// 带删除者的重载写入删除者主键与删除人
    /// </summary>
    [Fact]
    public void SetDeletionAuditInfo_WithDeleter_WritesAllDeletionFields()
    {
        IDeletionEntity<long> entity = new SampleDeletionEntityWithKey(1);
        var expected = new DateTimeOffset(2023, 9, 10, 11, 12, 13, TimeSpan.Zero);

        entity.SetDeletionAuditInfo(7L, "remover", expected);

        Assert.True(entity.IsDeleted);
        Assert.Equal(expected, entity.DeletedTime);
        Assert.Equal(7L, entity.DeletedId);
        Assert.Equal("remover", entity.DeletedBy);
    }

    /// <summary>
    /// 批量设置删除审计对每个实体生效
    /// </summary>
    [Fact]
    public void SetDeletionAuditInfos_AppliesToEveryEntity()
    {
        var entities = new List<IDeletionEntity<long>>
        {
            new SampleDeletionEntityWithKey(1),
            new SampleDeletionEntityWithKey(2)
        };

        var result = entities.SetDeletionAuditInfos(7L, "remover");

        Assert.All(result, entity =>
        {
            Assert.True(entity.IsDeleted);
            Assert.NotNull(entity.DeletedTime);
            Assert.Equal(7L, entity.DeletedId);
            Assert.Equal("remover", entity.DeletedBy);
        });
    }

    /// <summary>
    /// 清除删除审计把实体恢复为未删除
    /// </summary>
    [Fact]
    public void ClearDeletionAuditInfo_RestoresEntity()
    {
        IDeletionEntity entity = new SampleDeletionEntity
        {
            IsDeleted = true,
            DeletedTime = DateTimeOffset.UtcNow
        };

        var result = entity.ClearDeletionAuditInfo();

        Assert.Same(entity, result);
        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedTime);
    }

    /// <summary>
    /// 带删除者的清除重载连删除者信息一并抹掉
    /// </summary>
    [Fact]
    public void ClearDeletionAuditInfo_WithDeleter_AlsoClearsDeleterFields()
    {
        IDeletionEntity<long> entity = new SampleDeletionEntityWithKey(1);
        entity.SetDeletionAuditInfo(7L, "remover");

        entity.ClearDeletionAuditInfo();

        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedTime);
        Assert.Equal(0L, entity.DeletedId);
        Assert.Null(entity.DeletedBy);
    }

    /// <summary>
    /// 批量清除删除审计对每个实体生效
    /// </summary>
    [Fact]
    public void ClearDeletionAuditInfos_RestoresEveryEntity()
    {
        var entities = new List<IDeletionEntity<long>>
        {
            new SampleDeletionEntityWithKey(1),
            new SampleDeletionEntityWithKey(2)
        };
        entities.SetDeletionAuditInfos(7L, "remover");

        var result = entities.ClearDeletionAuditInfos();

        Assert.All(result, entity =>
        {
            Assert.False(entity.IsDeleted);
            Assert.Null(entity.DeletedTime);
            Assert.Equal(0L, entity.DeletedId);
            Assert.Null(entity.DeletedBy);
        });
    }

    /// <summary>
    /// 软删除判定与标记一致
    /// </summary>
    [Fact]
    public void IsSoftDeleted_ReflectsFlag()
    {
        ISoftDelete entity = new SampleSoftDeleteEntity();

        Assert.False(entity.IsSoftDeleted());

        entity.IsDeleted = true;

        Assert.True(entity.IsSoftDeleted());
    }

    /// <summary>
    /// 新建判定在给定时间窗内为真、窗外为假
    /// </summary>
    [Fact]
    public void IsNewlyCreated_ComparesAgainstGivenWindow()
    {
        ICreationEntity entity = new SampleCreationEntity();

        Assert.True(entity.IsNewlyCreated(TimeSpan.FromMinutes(5)));

        entity.CreatedTime = DateTimeOffset.UtcNow.AddHours(-2);

        Assert.False(entity.IsNewlyCreated(TimeSpan.FromMinutes(5)));
    }

    /// <summary>
    /// 实体年龄按创建时间到当下计算
    /// </summary>
    [Fact]
    public void GetAge_MeasuresFromCreatedTime()
    {
        ICreationEntity entity = new SampleCreationEntity
        {
            CreatedTime = DateTimeOffset.UtcNow.AddHours(-3)
        };

        var age = entity.GetAge();

        Assert.True(age >= TimeSpan.FromHours(3));
        Assert.True(age < TimeSpan.FromHours(4));
    }

    /// <summary>
    /// 从未修改的实体不属于最近修改
    /// </summary>
    [Fact]
    public void IsRecentlyModified_WhenNeverModified_ReturnsFalse()
    {
        IModificationEntity entity = new SampleModificationEntity();

        Assert.False(entity.IsRecentlyModified(TimeSpan.FromMinutes(5)));
        Assert.True(entity.IsNeverModified());
        Assert.Null(entity.GetTimeSinceLastModification());
    }

    /// <summary>
    /// 刚修改过的实体属于最近修改
    /// </summary>
    [Fact]
    public void IsRecentlyModified_WhenJustModified_ReturnsTrue()
    {
        IModificationEntity entity = new SampleModificationEntity();
        entity.SetModificationAuditInfo();

        Assert.True(entity.IsRecentlyModified(TimeSpan.FromMinutes(5)));
        Assert.False(entity.IsNeverModified());
        Assert.NotNull(entity.GetTimeSinceLastModification());
    }

    /// <summary>
    /// 久未修改的实体不属于最近修改
    /// </summary>
    [Fact]
    public void IsRecentlyModified_WhenModifiedLongAgo_ReturnsFalse()
    {
        IModificationEntity entity = new SampleModificationEntity
        {
            ModifiedTime = DateTimeOffset.UtcNow.AddDays(-1)
        };

        Assert.False(entity.IsRecentlyModified(TimeSpan.FromMinutes(5)));

        var elapsed = entity.GetTimeSinceLastModification();

        Assert.NotNull(elapsed);
        Assert.True(elapsed!.Value >= TimeSpan.FromHours(23));
    }

    /// <summary>
    /// 审计摘要只包含创建信息时不出现修改与删除段
    /// </summary>
    [Fact]
    public void GetAuditSummary_WhenOnlyCreated_ContainsCreationSegmentOnly()
    {
        IFullAuditedEntity<long> entity = new SampleFullAuditedEntityWithKey(1)
        {
            CreatedTime = new DateTimeOffset(2023, 1, 2, 3, 4, 5, TimeSpan.Zero),
            CreatedBy = "creator"
        };

        var summary = entity.GetAuditSummary();

        // 时间片段用同一套格式串回算，避免断言被运行环境的区域性时间分隔符影响
        Assert.StartsWith($"Created: {entity.CreatedTime:yyyy-MM-dd HH:mm:ss} by creator", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Modified:", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Deleted:", summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// 审计摘要在修改与删除后追加对应段落
    /// </summary>
    [Fact]
    public void GetAuditSummary_WhenModifiedAndDeleted_ContainsAllSegments()
    {
        IFullAuditedEntity<long> entity = new SampleFullAuditedEntityWithKey(1)
        {
            CreatedTime = new DateTimeOffset(2023, 1, 2, 3, 4, 5, TimeSpan.Zero),
            CreatedBy = "creator",
            ModifiedTime = new DateTimeOffset(2023, 2, 3, 4, 5, 6, TimeSpan.Zero),
            ModifiedBy = "editor",
            IsDeleted = true,
            DeletedTime = new DateTimeOffset(2023, 3, 4, 5, 6, 7, TimeSpan.Zero),
            DeletedBy = "remover"
        };

        var summary = entity.GetAuditSummary();

        Assert.Contains($"Modified: {entity.ModifiedTime!.Value:yyyy-MM-dd HH:mm:ss} by editor", summary, StringComparison.Ordinal);
        Assert.Contains($"Deleted: {entity.DeletedTime!.Value:yyyy-MM-dd HH:mm:ss} by remover", summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// 未打删除标记时即便有删除时间也不输出删除段
    /// </summary>
    [Fact]
    public void GetAuditSummary_WhenDeletedTimeSetButFlagOff_SkipsDeletionSegment()
    {
        IFullAuditedEntity<long> entity = new SampleFullAuditedEntityWithKey(1)
        {
            IsDeleted = false,
            DeletedTime = new DateTimeOffset(2023, 3, 4, 5, 6, 7, TimeSpan.Zero)
        };

        var summary = entity.GetAuditSummary();

        Assert.DoesNotContain("Deleted:", summary, StringComparison.Ordinal);
    }
}
