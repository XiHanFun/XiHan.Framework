// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using XiHan.Framework.Utils.Linq.Expressions;

namespace XiHan.Framework.Utils.Tests.Linq;

/// <summary>
/// 断言组合器测试
/// </summary>
/// <remarks>
/// 组合器的价值在于"参数重写正确、编译后语义正确"，所以每个用例都把结果编译成委托跑真实对象。
/// </remarks>
public class PredicateComposerTests
{
    /// <summary>
    /// 恒真与恒假断言
    /// </summary>
    [Fact]
    public void TrueAndFalse_ProduceConstantPredicates()
    {
        var always = PredicateComposer.True<Person>().Compile();
        var never = PredicateComposer.False<Person>().Compile();

        Assert.True(always(new Person()));
        Assert.False(never(new Person()));
    }

    /// <summary>
    /// 逻辑与要求两个条件同时成立
    /// </summary>
    [Fact]
    public void And_RequiresBothConditions()
    {
        Expression<Func<Person, bool>> adult = p => p.Age > 18;
        Expression<Func<Person, bool>> named = p => p.Name == "张三";

        var predicate = adult.And(named).Compile();

        Assert.True(predicate(new Person { Age = 19, Name = "张三" }));
        Assert.False(predicate(new Person { Age = 19, Name = "李四" }));
        Assert.False(predicate(new Person { Age = 17, Name = "张三" }));
    }

    /// <summary>
    /// 逻辑或只需一个条件成立
    /// </summary>
    [Fact]
    public void Or_RequiresEitherCondition()
    {
        Expression<Func<Person, bool>> senior = p => p.Age > 60;
        Expression<Func<Person, bool>> named = p => p.Name == "张三";

        var predicate = senior.Or(named).Compile();

        Assert.True(predicate(new Person { Age = 10, Name = "张三" }));
        Assert.True(predicate(new Person { Age = 61, Name = "李四" }));
        Assert.False(predicate(new Person { Age = 10, Name = "李四" }));
    }

    /// <summary>
    /// 取反翻转判定结果
    /// </summary>
    [Fact]
    public void Not_InvertsPredicate()
    {
        Expression<Func<Person, bool>> adult = p => p.Age > 18;

        var predicate = adult.Not().Compile();

        Assert.False(predicate(new Person { Age = 19 }));
        Assert.True(predicate(new Person { Age = 17 }));
    }

    /// <summary>
    /// 条件为假时退化为恒真断言
    /// </summary>
    [Fact]
    public void If_WhenConditionFalse_FallsBackToTrue()
    {
        Expression<Func<Person, bool>> adult = p => p.Age > 18;

        var applied = PredicateComposer.If(true, adult).Compile();
        var skipped = PredicateComposer.If(false, adult).Compile();

        Assert.False(applied(new Person { Age = 10 }));
        Assert.True(skipped(new Person { Age = 10 }));
    }

    /// <summary>
    /// 条件为假时保持原表达式不变
    /// </summary>
    [Fact]
    public void AndIfAndOrIf_OnlyComposeWhenConditionHolds()
    {
        Expression<Func<Person, bool>> adult = p => p.Age > 18;
        Expression<Func<Person, bool>> named = p => p.Name == "张三";

        Assert.Same(adult, adult.AndIf(false, named));
        Assert.Same(adult, adult.OrIf(false, named));

        var andComposed = adult.AndIf(true, named).Compile();
        var orComposed = adult.OrIf(true, named).Compile();

        Assert.False(andComposed(new Person { Age = 19, Name = "李四" }));
        Assert.True(orComposed(new Person { Age = 10, Name = "张三" }));
    }

    /// <summary>
    /// 批量与组合要求全部成立，空集合退化为恒真
    /// </summary>
    [Fact]
    public void AndAll_RequiresEveryPredicate()
    {
        Expression<Func<Person, bool>> adult = p => p.Age > 18;
        Expression<Func<Person, bool>> named = p => p.Name == "张三";

        var combined = PredicateComposer.AndAll(adult, named).Compile();
        var empty = PredicateComposer.AndAll<Person>().Compile();

        Assert.True(combined(new Person { Age = 19, Name = "张三" }));
        Assert.False(combined(new Person { Age = 19, Name = "李四" }));
        Assert.True(empty(new Person()));
    }

    /// <summary>
    /// 批量或组合只需一个成立，空集合退化为恒假
    /// </summary>
    [Fact]
    public void OrAll_RequiresAnyPredicate()
    {
        Expression<Func<Person, bool>> senior = p => p.Age > 60;
        Expression<Func<Person, bool>> named = p => p.Name == "张三";

        var combined = PredicateComposer.OrAll(senior, named).Compile();
        var empty = PredicateComposer.OrAll<Person>().Compile();

        Assert.True(combined(new Person { Age = 10, Name = "张三" }));
        Assert.False(combined(new Person { Age = 10, Name = "李四" }));
        Assert.False(empty(new Person()));
    }

    /// <summary>
    /// 集合重载与参数数组重载语义一致
    /// </summary>
    [Fact]
    public void AndAllAndOrAll_WithEnumerable_BehaveSameAsParamsForm()
    {
        var predicates = new List<Expression<Func<Person, bool>>>
        {
            p => p.Age > 18,
            p => p.Name == "张三"
        };

        var and = PredicateComposer.AndAll(predicates).Compile();
        var or = PredicateComposer.OrAll(predicates).Compile();

        Assert.True(and(new Person { Age = 19, Name = "张三" }));
        Assert.False(and(new Person { Age = 19, Name = "李四" }));
        Assert.True(or(new Person { Age = 19, Name = "李四" }));
        Assert.False(or(new Person { Age = 10, Name = "李四" }));
    }

    /// <summary>
    /// 单个断言的批量组合直接返回该断言的语义
    /// </summary>
    [Fact]
    public void AndAll_WithSinglePredicate_KeepsItsSemantics()
    {
        Expression<Func<Person, bool>> adult = p => p.Age > 18;

        var combined = PredicateComposer.AndAll(adult).Compile();

        Assert.True(combined(new Person { Age = 19 }));
        Assert.False(combined(new Person { Age = 18 }));
    }

    /// <summary>
    /// 按属性名动态构建等于与不等于断言
    /// </summary>
    [Fact]
    public void EqualAndNotEqual_BuildFromPropertyName()
    {
        var equal = PredicateComposer.Equal<Person>(nameof(Person.Name), "张三").Compile();
        var notEqual = PredicateComposer.NotEqual<Person>(nameof(Person.Name), "张三").Compile();

        Assert.True(equal(new Person { Name = "张三" }));
        Assert.False(equal(new Person { Name = "李四" }));
        Assert.True(notEqual(new Person { Name = "李四" }));
    }

    /// <summary>
    /// 传 null 时按属性类型构造常量，可用于判空
    /// </summary>
    [Fact]
    public void Equal_WithNullValue_MatchesNullProperty()
    {
        var isNull = PredicateComposer.Equal<Person>(nameof(Person.Nickname), null).Compile();

        Assert.True(isNull(new Person { Nickname = null }));
        Assert.False(isNull(new Person { Nickname = "昵称" }));
    }

    /// <summary>
    /// 按属性名动态构建大于与小于断言
    /// </summary>
    [Fact]
    public void GreaterThanAndLessThan_BuildFromPropertyName()
    {
        var greater = PredicateComposer.GreaterThan<Person>(nameof(Person.Age), 18).Compile();
        var less = PredicateComposer.LessThan<Person>(nameof(Person.Age), 18).Compile();

        Assert.True(greater(new Person { Age = 19 }));
        Assert.False(greater(new Person { Age = 18 }));
        Assert.True(less(new Person { Age = 17 }));
        Assert.False(less(new Person { Age = 18 }));
    }

    /// <summary>
    /// 属性不存在时抛参数异常
    /// </summary>
    [Fact]
    public void DynamicBuilders_WhenPropertyMissing_Throw()
    {
        Assert.Throws<ArgumentException>(() => PredicateComposer.Equal<Person>("NotExists", 1));
        Assert.Throws<ArgumentException>(() => PredicateComposer.GreaterThan<Person>("NotExists", 1));
    }

    /// <summary>
    /// 字符串包含断言，支持忽略大小写
    /// </summary>
    [Fact]
    public void Contains_MatchesSubstring()
    {
        var sensitive = PredicateComposer.Contains<Person>(nameof(Person.Name), "han").Compile();
        var insensitive = PredicateComposer.Contains<Person>(nameof(Person.Name), "HAN", true).Compile();

        var person = new Person { Name = "XihanFun" };

        Assert.True(sensitive(person));
        Assert.False(sensitive(new Person { Name = "other" }));
        Assert.True(insensitive(person));
    }

    /// <summary>
    /// 集合包含断言
    /// </summary>
    [Fact]
    public void In_ChecksMembership()
    {
        var predicate = PredicateComposer.In<Person, int>(nameof(Person.Age), [18, 20]).Compile();

        Assert.True(predicate(new Person { Age = 18 }));
        Assert.True(predicate(new Person { Age = 20 }));
        Assert.False(predicate(new Person { Age = 19 }));
    }

    /// <summary>
    /// 空集合的包含断言恒为假
    /// </summary>
    [Fact]
    public void In_WithEmptyValues_IsAlwaysFalse()
    {
        var predicate = PredicateComposer.In<Person, int>(nameof(Person.Age), []).Compile();

        Assert.False(predicate(new Person { Age = 18 }));
    }

    /// <summary>
    /// 通过转换器把断言迁移到另一种承载类型上
    /// </summary>
    [Fact]
    public void Convert_MovesPredicateToAnotherType()
    {
        Expression<Func<Person, bool>> adult = p => p.Age > 18;

        var predicate = PredicateComposer.Convert<Person, Wrapper>(adult, w => w.Owner).Compile();

        Assert.True(predicate(new Wrapper { Owner = new Person { Age = 19 } }));
        Assert.False(predicate(new Wrapper { Owner = new Person { Age = 17 } }));
    }

    /// <summary>
    /// 测试用实体
    /// </summary>
    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }

        public string? Nickname { get; set; }
    }

    /// <summary>
    /// 测试用外层承载类型
    /// </summary>
    private sealed class Wrapper
    {
        public Person Owner { get; set; } = new();
    }
}
