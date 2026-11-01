// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="EntityIndexes.Resolve"/>：命名规则、软删除实体的唯一索引、声明错误。
/// </summary>
public sealed class EntityIndexesTests
{
    [Fact]
    public void Resolve_NamesIndexesFromTableAndColumnAbbreviations_SortedByName()
    {
        var indexes = EntityIndexes.Resolve(typeof(Order), "Order");

        Assert.Equal(
            [
                ("IX_Order_ETa_Leco", new[] { nameof(Order.ETag), nameof(Order.Legacy_code) }, false),
                ("IX_Order_HTSeNa", new[] { nameof(Order.HTTPServerName) }, false),
                ("UX_Order_TeId_NeFiAt", new[] { nameof(Order.TenantId), nameof(Order.NextFireAt) }, true),
            ],
            indexes.Select(static i => (i.Name, i.Properties.ToArray(), i.IsUnique)));
    }

    [Fact]
    public void Resolve_ReplacesCharactersOutsideIdentifiersInTheTableName()
    {
        var index = EntityIndexes.Resolve(typeof(Order), "dbo.order-items").Single(static i => i.IsUnique);

        Assert.Equal("UX_dbo_order_items_TeId_NeFiAt", index.Name);
    }

    [Fact]
    public void Resolve_MakesUniqueIndexesOfSoftDeletableEntities_PartialOverUndeletedRows()
    {
        var indexes = EntityIndexes.Resolve(typeof(Article), "Article").ToDictionary(static i => i.Name);

        // 唯一索引只约束未删除的行：列就是声明的列，删除标记在条件里
        Assert.Equal([nameof(Article.TenantId), nameof(Article.Slug)], indexes["UX_Article_TeId_Sl"].Properties);
        Assert.True(indexes["UX_Article_TeId_Sl"].ExcludesDeleted);
        // 普通索引不加条件
        Assert.Equal([nameof(Article.Slug)], indexes["IX_Article_Sl"].Properties);
        Assert.False(indexes["IX_Article_Sl"].ExcludesDeleted);
        // 声明里显式写了删除标记的，是应用选择的普通唯一索引
        Assert.Equal([nameof(Article.Code), nameof(ISoftDeletable.IsDeleted)], indexes["UX_Article_Co_IsDe"].Properties);
        Assert.False(indexes["UX_Article_Co_IsDe"].ExcludesDeleted);
    }

    [Fact]
    public void Resolve_KeepsUniqueIndexesOfEntitiesWithoutSoftDelete_Unconditional()
    {
        var index = EntityIndexes.Resolve(typeof(Order), "Order").Single(static i => i.IsUnique);

        Assert.False(index.ExcludesDeleted);
    }

    [Fact]
    public void Resolve_ReturnsEmpty_WhenNothingIsDeclared()
    {
        Assert.Empty(EntityIndexes.Resolve(typeof(Widget), "Widget"));
    }

    [Theory]
    [InlineData(typeof(EmptyDeclaration), "without properties")]
    [InlineData(typeof(BlankProperty), "blank property name")]
    [InlineData(typeof(RepeatedProperty), "repeats property 'Code'")]
    [InlineData(typeof(UnknownProperty), "on 'Missing', which is not a public instance property")]
    [InlineData(typeof(CollidingNames), "more than one index named 'IX_CollidingNames_TeId'")]
    [InlineData(typeof(SameColumns), "more than one index on ('Code')")]
    public void Resolve_RejectsInvalidDeclarations(Type entityType, string reason)
    {
        var error = Assert.Throws<InvalidOperationException>(() => EntityIndexes.Resolve(entityType, entityType.Name));

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_RejectsNamesLongerThanTheLimit_WithoutTruncating()
    {
        // 最长的是 UX_<表名>_TeId_NeFiAt：表名再长一个字符就超过上限
        var fits = new string('t', EntityIndexes.MaxNameLength - "UX__TeId_NeFiAt".Length);

        Assert.Equal(EntityIndexes.MaxNameLength, EntityIndexes.Resolve(typeof(Order), fits).Max(static i => i.Name.Length));
        var error = Assert.Throws<InvalidOperationException>(() => EntityIndexes.Resolve(typeof(Order), fits + "t"));
        Assert.Contains($"longer than {EntityIndexes.MaxNameLength} characters", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_RejectsBlankTable()
    {
        Assert.Throws<ArgumentException>(() => EntityIndexes.Resolve(typeof(Order), " "));
    }

    /// <summary>
    /// 普通实体：唯一索引、首字母缩写词、下划线分词。
    /// </summary>
    [EntityIndex(nameof(TenantId), nameof(NextFireAt), IsUnique = true)]
    [EntityIndex(nameof(HTTPServerName))]
    [EntityIndex(nameof(ETag), nameof(Legacy_code))]
    private sealed class Order : IEntity<Guid>
    {
        /// <inheritdoc />
        public Guid Id { get; init; }

        /// <summary>
        /// 租户。
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// 下一次时刻。
        /// </summary>
        public DateTimeOffset? NextFireAt { get; set; }

        /// <summary>
        /// 连续大写开头的名字：<c>HTTP</c> 与 <c>Server</c> 分成两个词。
        /// </summary>
        public string HTTPServerName { get; set; } = string.Empty;

        /// <summary>
        /// 单字母词：<c>E</c> 与 <c>Tag</c>。
        /// </summary>
        public string ETag { get; set; } = string.Empty;

        /// <summary>
        /// 下划线分词。
        /// </summary>
        public string Legacy_code { get; set; } = string.Empty;
    }

    /// <summary>
    /// 软删除实体。
    /// </summary>
    [EntityIndex(nameof(TenantId), nameof(Slug), IsUnique = true)]
    [EntityIndex(nameof(Slug))]
    [EntityIndex(nameof(Code), nameof(IsDeleted), IsUnique = true)]
    private sealed class Article : IEntity<Guid>, ISoftDeletable
    {
        /// <inheritdoc />
        public Guid Id { get; init; }

        /// <summary>
        /// 租户。
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// 路径名。
        /// </summary>
        public string Slug { get; set; } = string.Empty;

        /// <summary>
        /// 编码。
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <inheritdoc />
        public bool IsDeleted { get; set; }

        /// <inheritdoc />
        public DateTimeOffset? DeletedAt { get; set; }
    }

    /// <summary>
    /// 没有列的声明。
    /// </summary>
    [EntityIndex]
    private sealed class EmptyDeclaration : IEntity<Guid>
    {
        /// <inheritdoc />
        public Guid Id { get; init; }
    }

    /// <summary>
    /// 列名空白的声明。
    /// </summary>
    [EntityIndex(" ")]
    private sealed class BlankProperty : IEntity<Guid>
    {
        /// <inheritdoc />
        public Guid Id { get; init; }
    }

    /// <summary>
    /// 同一列写了两次的声明。
    /// </summary>
    [EntityIndex(nameof(Code), nameof(Code))]
    private sealed class RepeatedProperty : IEntity<Guid>
    {
        /// <inheritdoc />
        public Guid Id { get; init; }

        /// <summary>
        /// 编码。
        /// </summary>
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// 引用了不存在的属性的声明。
    /// </summary>
    [EntityIndex("Missing")]
    private sealed class UnknownProperty : IEntity<Guid>
    {
        /// <inheritdoc />
        public Guid Id { get; init; }
    }

    /// <summary>
    /// 两列缩写相同（<c>TenantId</c> 与 <c>TemplateId</c> 都是 <c>TeId</c>），索引同名。
    /// </summary>
    [EntityIndex(nameof(TenantId))]
    [EntityIndex(nameof(TemplateId))]
    private sealed class CollidingNames : IEntity<Guid>
    {
        /// <inheritdoc />
        public Guid Id { get; init; }

        /// <summary>
        /// 租户。
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// 模板。
        /// </summary>
        public string TemplateId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 同一列既声明唯一又声明普通索引。
    /// </summary>
    [EntityIndex(nameof(Code))]
    [EntityIndex(nameof(Code), IsUnique = true)]
    private sealed class SameColumns : IEntity<Guid>
    {
        /// <inheritdoc />
        public Guid Id { get; init; }

        /// <summary>
        /// 编码。
        /// </summary>
        public string Code { get; set; } = string.Empty;
    }
}
