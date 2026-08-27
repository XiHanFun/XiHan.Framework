// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using XiHan.Framework.Utils.Linq.Expressions;

namespace XiHan.Framework.Utils.Tests.Linq;

/// <summary>
/// 可查询扩展方法测试
/// </summary>
public class QueryableExtensionsTests
{
    /// <summary>
    /// 条件为真时应用谓词过滤
    /// </summary>
    [Fact]
    public void WhereIf_WhenConditionTrue_AppliesPredicate()
    {
        var source = new[] { 1, 2, 3, 4 }.AsQueryable();

        var result = source.WhereIf(true, x => x > 2).ToArray();

        Assert.Equal(new[] { 3, 4 }, result);
    }

    /// <summary>
    /// 条件为假时原样返回同一个查询对象
    /// </summary>
    [Fact]
    public void WhereIf_WhenConditionFalse_ReturnsSameQueryable()
    {
        var source = new[] { 1, 2, 3, 4 }.AsQueryable();

        var result = source.WhereIf(false, x => x > 2);

        Assert.Same(source, result);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.ToArray());
    }

    /// <summary>
    /// 谓词以表达式树形式传入，可被查询提供程序继续翻译
    /// </summary>
    [Fact]
    public void WhereIf_KeepsPredicateAsExpression()
    {
        var source = new[] { 1, 2, 3 }.AsQueryable();
        Expression<Func<int, bool>> predicate = x => x % 2 == 1;

        var result = source.WhereIf(true, predicate).ToArray();

        Assert.Equal(new[] { 1, 3 }, result);
    }

    /// <summary>
    /// 空查询上过滤得到空结果
    /// </summary>
    [Fact]
    public void WhereIf_OnEmptySource_ReturnsEmpty()
    {
        var source = Array.Empty<int>().AsQueryable();

        Assert.Empty(source.WhereIf(true, x => x > 0).ToArray());
    }
}
