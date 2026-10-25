// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Queries;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="EntityQuery{T}"/> 的默认值、过滤组合、排序追加、分页与参数校验。
/// </summary>
public sealed class EntityQueryTests
{
    [Fact]
    public void All_HasNoFilter_DefaultPaging()
    {
        var query = EntityQuery.All<Widget>();

        Assert.Null(query.Filter);
        Assert.Empty(query.Orderings);
        Assert.Equal(0, query.Skip);
        Assert.Equal(EntityQuery.DefaultMaxTake, query.Take);
        Assert.False(query.IncludeDeleted);
    }

    [Fact]
    public void Where_ComposesFiltersWithAnd()
    {
        var query = EntityQuery.All<Widget>()
            .Where(new Specification<Widget>(static w => w.Rank > 1))
            .Where(new Specification<Widget>(static w => w.Name.StartsWith('a')));

        Assert.NotNull(query.Filter);
        Assert.True(query.Filter.IsSatisfiedBy(new Widget { Name = "abc", Rank = 2 }));
        Assert.False(query.Filter.IsSatisfiedBy(new Widget { Name = "abc", Rank = 1 }));
        Assert.False(query.Filter.IsSatisfiedBy(new Widget { Name = "xyz", Rank = 2 }));
    }

    [Fact]
    public void OrderBy_AppendsInOrder_AndPageSetsBounds()
    {
        var query = EntityQuery.All<Widget>()
            .OrderBy(static w => w.Rank, descending: true)
            .OrderBy(static w => w.Name)
            .Page(10, 20);

        Assert.Equal(2, query.Orderings.Count);
        Assert.True(query.Orderings[0].Descending);
        Assert.False(query.Orderings[1].Descending);
        Assert.Equal(10, query.Skip);
        Assert.Equal(20, query.Take);
        // 查询不可变：派生不影响共享的 All 实例
        Assert.Empty(EntityQuery.All<Widget>().Orderings);
    }

    [Fact]
    public void Page_RejectsNegativeSkipAndNonPositiveTake()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => EntityQuery.All<Widget>().Page(-1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(static () => EntityQuery.All<Widget>().Page(0, 0));
        Assert.Throws<ArgumentNullException>(static () => EntityQuery.All<Widget>().Where(null!));
        Assert.Throws<ArgumentNullException>(static () => EntityQuery.All<Widget>().OrderBy(null!));
    }
}
