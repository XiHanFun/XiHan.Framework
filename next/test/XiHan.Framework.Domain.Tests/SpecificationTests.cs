// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Domain.Tests;

public sealed class SpecificationTests
{
    private sealed record Product(string Name, decimal Price, bool Active);

    private static readonly Product[] Catalog =
    [
        new("cheap", 5m, true),
        new("mid", 50m, true),
        new("dear", 500m, false),
    ];

    private static readonly Specification<Product> Affordable = new(static p => p.Price < 100m);
    private static readonly Specification<Product> IsActive = new(static p => p.Active);

    [Fact]
    public void IsSatisfiedBy_EvaluatesCriteriaInMemory()
    {
        Assert.True(Affordable.IsSatisfiedBy(Catalog[0]));
        Assert.False(Affordable.IsSatisfiedBy(Catalog[2]));
        Assert.True(Specification.All<Product>().IsSatisfiedBy(Catalog[2]));
    }

    [Fact]
    public void And_Or_Not_ComposeBothInMemoryAndAsExpression()
    {
        var affordableAndActive = Affordable.And(IsActive);
        var dearOrInactive = Affordable.Not().Or(IsActive.Not());

        Assert.Equal(["cheap", "mid"], Catalog.Where(affordableAndActive.IsSatisfiedBy).Select(static p => p.Name));
        Assert.Equal(["dear"], Catalog.Where(dearOrInactive.IsSatisfiedBy).Select(static p => p.Name));

        // 同一规约交给查询提供器：表达式必须共享同一个参数，否则 LINQ 提供器无法翻译
        Assert.Equal(["cheap", "mid"], Catalog.AsQueryable().Where(affordableAndActive.Criteria).Select(static p => p.Name));
        Assert.Equal(["dear"], Catalog.AsQueryable().Where(dearOrInactive.Criteria).Select(static p => p.Name));
        Assert.Single(affordableAndActive.Criteria.Parameters);
    }

    [Fact]
    public void Constructor_And_Or_RejectNull()
    {
        Assert.Throws<ArgumentNullException>(static () => new Specification<Product>(null!));
        Assert.Throws<ArgumentNullException>(() => Affordable.And(null!));
        Assert.Throws<ArgumentNullException>(() => Affordable.Or(null!));
    }
}
