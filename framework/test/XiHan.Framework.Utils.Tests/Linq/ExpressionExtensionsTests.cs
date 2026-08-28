// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using XiHan.Framework.Utils.Linq.Expressions;

namespace XiHan.Framework.Utils.Tests.Linq;

/// <summary>
/// 表达式扩展方法测试
/// </summary>
/// <remarks>
/// 动态排序与动态投影都在内存 IQueryable 上验证结果顺序与内容，不断言生成的表达式节点结构。
/// </remarks>
public class ExpressionExtensionsTests
{
    /// <summary>
    /// 合并两个条件，第一个为 null 时直接返回第二个
    /// </summary>
    [Fact]
    public void Combine_WhenFirstIsNull_ReturnsSecond()
    {
        Expression<Func<Person, bool>> second = p => p.Age > 18;

        var combined = ExpressionExtensions.Combine(null, second, Expression.AndAlso);

        Assert.Same(second, combined);
    }

    /// <summary>
    /// 逻辑与合并要求两个条件同时成立
    /// </summary>
    [Fact]
    public void AndAlso_RequiresBothConditions()
    {
        Expression<Func<Person, bool>> adult = p => p.Age > 18;
        Expression<Func<Person, bool>> named = p => p.Name == "张三";

        var predicate = adult.AndAlso(named).Compile();

        Assert.True(predicate(new Person { Age = 19, Name = "张三" }));
        Assert.False(predicate(new Person { Age = 19, Name = "李四" }));
        Assert.False(predicate(new Person { Age = 17, Name = "张三" }));
    }

    /// <summary>
    /// 逻辑或合并只需一个条件成立
    /// </summary>
    [Fact]
    public void OrElse_RequiresEitherCondition()
    {
        Expression<Func<Person, bool>> senior = p => p.Age > 60;
        Expression<Func<Person, bool>> named = p => p.Name == "张三";

        var predicate = senior.OrElse(named).Compile();

        Assert.True(predicate(new Person { Age = 10, Name = "张三" }));
        Assert.True(predicate(new Person { Age = 61, Name = "李四" }));
        Assert.False(predicate(new Person { Age = 10, Name = "李四" }));
    }

    /// <summary>
    /// 按属性名与比较器动态创建过滤条件
    /// </summary>
    [Fact]
    public void CreateFilter_BuildsPredicateFromPropertyName()
    {
        var predicate = ExpressionExtensions.CreateFilter<Person>(nameof(Person.Age), 18, Expression.GreaterThanOrEqual).Compile();

        Assert.True(predicate(new Person { Age = 18 }));
        Assert.False(predicate(new Person { Age = 17 }));
    }

    /// <summary>
    /// 属性不存在时抛参数异常
    /// </summary>
    [Fact]
    public void CreateFilter_WhenPropertyMissing_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ExpressionExtensions.CreateFilter<Person>("NotExists", 1, Expression.Equal));
    }

    /// <summary>
    /// 等于、大于、小于三个快捷过滤器
    /// </summary>
    [Fact]
    public void EqualGreaterLessFilters_BuildExpectedPredicates()
    {
        var equalName = ExpressionExtensions.EqualFilter<Person>(nameof(Person.Name), "张三").Compile();
        var olderThan = ExpressionExtensions.GreaterThanFilter<Person>(nameof(Person.Age), 18).Compile();
        var youngerThan = ExpressionExtensions.LessThanFilter<Person>(nameof(Person.Age), 18).Compile();

        Assert.True(equalName(new Person { Name = "张三" }));
        Assert.False(equalName(new Person { Name = "李四" }));
        Assert.True(olderThan(new Person { Age = 19 }));
        Assert.False(olderThan(new Person { Age = 18 }));
        Assert.True(youngerThan(new Person { Age = 17 }));
        Assert.False(youngerThan(new Person { Age = 18 }));
    }

    /// <summary>
    /// 按属性名动态升序排序
    /// </summary>
    [Fact]
    public void OrderBy_ByPropertyName_SortsAscendingByDefault()
    {
        var source = CreatePeople();

        var sorted = source.OrderBy(nameof(Person.Name)).Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "a", "b", "c" }, sorted);
    }

    /// <summary>
    /// 按属性名动态降序排序
    /// </summary>
    [Fact]
    public void OrderBy_WhenDescending_ReversesOrder()
    {
        var source = CreatePeople();

        var sorted = source.OrderBy(nameof(Person.Name), false).Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "c", "b", "a" }, sorted);
    }

    /// <summary>
    /// 属性不存在时抛参数异常
    /// </summary>
    [Fact]
    public void OrderBy_WhenPropertyMissing_Throws()
    {
        var source = CreatePeople();

        Assert.Throws<ArgumentException>(() => source.OrderBy("NotExists"));
    }

    /// <summary>
    /// 二级排序在一级相同的分组内继续排
    /// </summary>
    [Fact]
    public void ThenBy_AppliesSecondarySort()
    {
        var source = new[]
        {
            new Person { Name = "b", Age = 1 },
            new Person { Name = "a", Age = 1 },
            new Person { Name = "c", Age = 0 }
        }.AsQueryable();

        var ascending = source.OrderBy(nameof(Person.Age)).ThenBy(nameof(Person.Name)).Select(p => p.Name).ToArray();
        var descending = source.OrderBy(nameof(Person.Age)).ThenBy(nameof(Person.Name), false).Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "c", "a", "b" }, ascending);
        Assert.Equal(new[] { "c", "b", "a" }, descending);
    }

    /// <summary>
    /// 动态投影只填充指定属性，其余保持默认值
    /// </summary>
    [Fact]
    public void SelectProperties_OnlyFillsRequestedProperties()
    {
        var source = new[]
        {
            new Person { Name = "a", Age = 11 },
            new Person { Name = "b", Age = 22 }
        }.AsQueryable();

        var projected = source.SelectProperties<Person, Person>(nameof(Person.Name)).ToArray();

        Assert.Equal(["a", "b"], projected.Select(p => p.Name));
        Assert.Equal([0, 0], projected.Select(p => p.Age));
    }

    /// <summary>
    /// 不指定属性时得到全新的默认对象
    /// </summary>
    [Fact]
    public void SelectProperties_WithoutPropertyNames_ReturnsDefaultObjects()
    {
        var source = CreatePeople();

        var projected = source.SelectProperties<Person, Person>().ToArray();

        Assert.Equal(3, projected.Length);
        Assert.All(projected, p => Assert.Equal(string.Empty, p.Name));
    }

    /// <summary>
    /// 构造固定的三条测试数据
    /// </summary>
    private static IQueryable<Person> CreatePeople()
    {
        return new[]
        {
            new Person { Name = "c", Age = 3 },
            new Person { Name = "a", Age = 1 },
            new Person { Name = "b", Age = 2 }
        }.AsQueryable();
    }

    /// <summary>
    /// 测试用实体
    /// </summary>
    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }
}
