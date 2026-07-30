// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.VectorData;
using XiHan.Framework.AI.Rag;

namespace XiHan.Framework.AI.Tests;

/// <summary>
/// 知识切片记录的集合定义与维度校验测试。
/// </summary>
/// <remarks>
/// 字段模型不再由特性声明，集合定义是唯一事实源：定义漏字段不会编译失败，只会让该字段静默不落库。
/// </remarks>
public sealed class VectorStoreKnowledgeRecordTests
{
    /// <summary>
    /// 集合定义必须覆盖记录的每个公共可读写属性。
    /// </summary>
    /// <remarks>按反射枚举属性而不是硬编码名单，新增字段时漏登记会在此失败。</remarks>
    [Fact]
    public void CreateDefinition_ShouldCoverEveryRecordProperty()
    {
        var definition = VectorStoreKnowledgeRecord.CreateDefinition(1024);

        var declared = typeof(VectorStoreKnowledgeRecord)
            .GetProperties()
            .Where(property => property.CanRead && property.CanWrite)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        var mapped = definition.Properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(declared, mapped);
    }

    /// <summary>
    /// 向量维度必须取自入参而非固定值。
    /// </summary>
    [Theory]
    [InlineData(768)]
    [InlineData(1024)]
    [InlineData(1536)]
    public void CreateDefinition_ShouldUseGivenDimensions(int dimensions)
    {
        var definition = VectorStoreKnowledgeRecord.CreateDefinition(dimensions);

        var vector = Assert.Single(definition.Properties.OfType<VectorStoreVectorProperty>());
        Assert.Equal(dimensions, vector.Dimensions);
        Assert.Equal(nameof(VectorStoreKnowledgeRecord.Embedding), vector.Name);
    }

    /// <summary>
    /// 过滤维度必须建索引，否则 pre-filter 在部分连接器上直接失败。
    /// </summary>
    [Fact]
    public void CreateDefinition_ShouldIndexFilterProperties()
    {
        var definition = VectorStoreKnowledgeRecord.CreateDefinition(1536);

        var indexed = definition.Properties
            .OfType<VectorStoreDataProperty>()
            .Where(property => property.IsIndexed)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(nameof(VectorStoreKnowledgeRecord.DocumentId), indexed);
        Assert.Contains(nameof(VectorStoreKnowledgeRecord.TenantId), indexed);
    }

    /// <summary>
    /// 非法维度直接拒绝，不留到向量库建集合时才报错。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateDefinition_ShouldRejectNonPositiveDimensions(int dimensions)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VectorStoreKnowledgeRecord.CreateDefinition(dimensions));
    }

    /// <summary>
    /// 维度一致时放行。
    /// </summary>
    [Fact]
    public void EnsureDimensions_ShouldPassOnMatch()
    {
        VectorStoreKnowledgeRecord.EnsureDimensions(1536, 1536);
    }

    /// <summary>
    /// 维度不一致必须带上两侧实际数值，能直接据此改配置。
    /// </summary>
    [Fact]
    public void EnsureDimensions_ShouldReportBothSidesOnMismatch()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => VectorStoreKnowledgeRecord.EnsureDimensions(1024, 1536));

        Assert.Contains("1024", exception.Message, StringComparison.Ordinal);
        Assert.Contains("1536", exception.Message, StringComparison.Ordinal);
    }
}
