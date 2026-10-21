// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Domain.Tests;

public sealed class SpecificationTests
{
    private sealed record Product(string Name, decimal Price, bool Active);

    private static readonly Product[] _catalog =
    [
        new("cheap", 5m, true),
        new("mid", 50m, true),
        new("dear", 500m, false),
    ];

    private static readonly Specification<Product> _affordable = new(static p => p.Price < 100m);
    private static readonly Specification<Product> _isActive = new(static p => p.Active);

    [Fact]
    public void IsSatisfiedBy_EvaluatesCriteriaInMemory()
    {
        Assert.True(_affordable.IsSatisfiedBy(_catalog[0]));
        Assert.False(_affordable.IsSatisfiedBy(_catalog[2]));
        Assert.True(Specification.All<Product>().IsSatisfiedBy(_catalog[2]));
    }

    [Fact]
    public void And_Or_Not_ComposeBothInMemoryAndAsExpression()
    {
        var affordableAndActive = _affordable.And(_isActive);
        var dearOrInactive = _affordable.Not().Or(_isActive.Not());

        Assert.Equal(["cheap", "mid"], _catalog.Where(affordableAndActive.IsSatisfiedBy).Select(static p => p.Name));
        Assert.Equal(["dear"], _catalog.Where(dearOrInactive.IsSatisfiedBy).Select(static p => p.Name));

        // 同一规约交给查询提供器：表达式必须共享同一个参数，否则 LINQ 提供器无法翻译
        Assert.Equal(["cheap", "mid"], _catalog.AsQueryable().Where(affordableAndActive.Criteria).Select(static p => p.Name));
        Assert.Equal(["dear"], _catalog.AsQueryable().Where(dearOrInactive.Criteria).Select(static p => p.Name));
        Assert.Single(affordableAndActive.Criteria.Parameters);
    }

    [Fact]
    public void Constructor_And_Or_RejectNull()
    {
        Assert.Throws<ArgumentNullException>(static () => new Specification<Product>(null!));
        Assert.Throws<ArgumentNullException>(() => _affordable.And(null!));
        Assert.Throws<ArgumentNullException>(() => _affordable.Or(null!));
    }
}
