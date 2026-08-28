// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using XiHan.Framework.Domain.Specifications.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Specifications;

/// <summary>
/// 规约基类测试
/// </summary>
/// <remarks>
/// 组合规约的关键在于参数替换：两个规约各自的 lambda 参数名不同（person / candidate），
/// 组合时必须统一到同一个 ParameterExpression，否则 Lambda 构造会因「参数未绑定」抛异常，
/// 或者被 ORM 翻译成错误 SQL。这里的用例刻意让两侧参数名不同来覆盖这一点。
/// </remarks>
public class SpecificationTests
{
    /// <summary>
    /// 单个规约按表达式判定实体
    /// </summary>
    [Theory]
    [InlineData(17, false)]
    [InlineData(18, true)]
    [InlineData(30, true)]
    public void IsSatisfiedBy_OnSingleSpecification_MatchesExpression(int age, bool expected)
    {
        var specification = new SampleAdultSpecification();
        var person = new SamplePerson { Age = age };

        Assert.Equal(expected, specification.IsSatisfiedBy(person));
    }

    /// <summary>
    /// 与运算要求两侧同时成立
    /// </summary>
    [Theory]
    [InlineData(20, "Alice", true)]
    [InlineData(20, "Bob", false)]
    [InlineData(10, "Alice", false)]
    [InlineData(10, "Bob", false)]
    public void And_RequiresBothSides(int age, string name, bool expected)
    {
        var combined = new SampleAdultSpecification().And(new SampleNameStartsWithSpecification("A"));
        var person = new SamplePerson { Age = age, Name = name };

        Assert.Equal(expected, combined.IsSatisfiedBy(person));
    }

    /// <summary>
    /// 或运算只要一侧成立即可
    /// </summary>
    [Theory]
    [InlineData(20, "Bob", true)]
    [InlineData(10, "Alice", true)]
    [InlineData(10, "Bob", false)]
    public void Or_AcceptsEitherSide(int age, string name, bool expected)
    {
        var combined = new SampleAdultSpecification().Or(new SampleNameStartsWithSpecification("A"));
        var person = new SamplePerson { Age = age, Name = name };

        Assert.Equal(expected, combined.IsSatisfiedBy(person));
    }

    /// <summary>
    /// 非运算取反原规约
    /// </summary>
    [Theory]
    [InlineData(20, false)]
    [InlineData(10, true)]
    public void Not_InvertsSpecification(int age, bool expected)
    {
        var negated = new SampleAdultSpecification().Not();
        var person = new SamplePerson { Age = age };

        Assert.Equal(expected, negated.IsSatisfiedBy(person));
    }

    /// <summary>
    /// 组合后的表达式只保留一个参数，可以直接编译执行
    /// </summary>
    [Fact]
    public void And_ToExpression_UnifiesParametersIntoSingleLambda()
    {
        var combined = new SampleAdultSpecification().And(new SampleNameStartsWithSpecification("A"));

        var expression = combined.ToExpression();

        Assert.Single(expression.Parameters);

        var predicate = expression.Compile();

        Assert.True(predicate(new SamplePerson { Age = 20, Name = "Alice" }));
        Assert.False(predicate(new SamplePerson { Age = 20, Name = "Bob" }));
    }

    /// <summary>
    /// 或组合后的表达式同样只保留一个参数
    /// </summary>
    [Fact]
    public void Or_ToExpression_UnifiesParametersIntoSingleLambda()
    {
        var combined = new SampleAdultSpecification().Or(new SampleNameStartsWithSpecification("A"));

        var expression = combined.ToExpression();

        Assert.Single(expression.Parameters);

        var predicate = expression.Compile();

        Assert.True(predicate(new SamplePerson { Age = 10, Name = "Alice" }));
        Assert.False(predicate(new SamplePerson { Age = 10, Name = "Bob" }));
    }

    /// <summary>
    /// 取反表达式沿用原表达式的参数
    /// </summary>
    [Fact]
    public void Not_ToExpression_KeepsOriginalParameter()
    {
        var specification = new SampleAdultSpecification();
        var original = specification.ToExpression();

        var negated = specification.Not().ToExpression();

        Assert.Single(negated.Parameters);
        Assert.Equal(original.Parameters[0].Name, negated.Parameters[0].Name);
        Assert.False(negated.Compile()(new SamplePerson { Age = 20 }));
    }

    /// <summary>
    /// 三层组合仍然可编译执行
    /// </summary>
    [Fact]
    public void Combination_WhenNestedThreeLevels_StillCompiles()
    {
        var combined = new SampleAdultSpecification()
            .And(new SampleNameStartsWithSpecification("A"))
            .Or(new SampleNameStartsWithSpecification("Z"))
            .Not();

        var predicate = combined.ToExpression().Compile();

        Assert.False(predicate(new SamplePerson { Age = 20, Name = "Alice" }));
        Assert.False(predicate(new SamplePerson { Age = 10, Name = "Zoe" }));
        Assert.True(predicate(new SamplePerson { Age = 10, Name = "Bob" }));
    }

    /// <summary>
    /// 与运算传入空规约时抛出参数异常
    /// </summary>
    [Fact]
    public void And_WhenSpecificationIsNull_Throws()
    {
        var specification = new SampleAdultSpecification();

        Assert.Throws<ArgumentNullException>(() => { _ = specification.And(null!); });
    }

    /// <summary>
    /// 或运算传入空规约时抛出参数异常
    /// </summary>
    [Fact]
    public void Or_WhenSpecificationIsNull_Throws()
    {
        var specification = new SampleAdultSpecification();

        Assert.Throws<ArgumentNullException>(() => { _ = specification.Or(null!); });
    }

    /// <summary>
    /// 与运算符重载等价于 And 方法
    /// </summary>
    [Fact]
    public void BitwiseAndOperator_IsEquivalentToAndMethod()
    {
        var left = new SampleAdultSpecification();
        var right = new SampleNameStartsWithSpecification("A");

        var combined = left & right;

        Assert.True(combined.IsSatisfiedBy(new SamplePerson { Age = 20, Name = "Alice" }));
        Assert.False(combined.IsSatisfiedBy(new SamplePerson { Age = 20, Name = "Bob" }));
    }

    /// <summary>
    /// 或运算符重载等价于 Or 方法
    /// </summary>
    [Fact]
    public void BitwiseOrOperator_IsEquivalentToOrMethod()
    {
        var left = new SampleAdultSpecification();
        var right = new SampleNameStartsWithSpecification("A");

        var combined = left | right;

        Assert.True(combined.IsSatisfiedBy(new SamplePerson { Age = 10, Name = "Alice" }));
        Assert.False(combined.IsSatisfiedBy(new SamplePerson { Age = 10, Name = "Bob" }));
    }

    /// <summary>
    /// 非运算符重载等价于 Not 方法
    /// </summary>
    [Fact]
    public void NotOperator_IsEquivalentToNotMethod()
    {
        var specification = new SampleAdultSpecification();

        var negated = !specification;

        Assert.True(negated.IsSatisfiedBy(new SamplePerson { Age = 10 }));
        Assert.False(negated.IsSatisfiedBy(new SamplePerson { Age = 20 }));
    }

    /// <summary>
    /// 规约可隐式转换为查询表达式，便于直接喂给仓储
    /// </summary>
    [Fact]
    public void ImplicitConversion_ProducesQueryExpression()
    {
        Expression<Func<SamplePerson, bool>> expression = new SampleAdultSpecification();

        var predicate = expression.Compile();

        Assert.True(predicate(new SamplePerson { Age = 20 }));
        Assert.False(predicate(new SamplePerson { Age = 10 }));
    }

    /// <summary>
    /// 规约在内存集合上与表达式在查询上的筛选结果一致
    /// </summary>
    [Fact]
    public void ToExpression_AppliedToQueryable_MatchesInMemoryEvaluation()
    {
        var people = new List<SamplePerson>
        {
            new() { Age = 20, Name = "Alice" },
            new() { Age = 20, Name = "Bob" },
            new() { Age = 10, Name = "Amy" }
        };
        var specification = new SampleAdultSpecification().And(new SampleNameStartsWithSpecification("A"));

        var byExpression = people.AsQueryable().Where(specification.ToExpression()).ToList();
        var byPredicate = people.Where(specification.IsSatisfiedBy).ToList();

        Assert.Single(byExpression);
        Assert.Equal("Alice", byExpression[0].Name);
        Assert.Equal(byPredicate.Count, byExpression.Count);
    }

    /// <summary>
    /// 规约实现规约契约
    /// </summary>
    [Fact]
    public void Specification_ImplementsSpecificationContract()
    {
        Assert.IsAssignableFrom<ISpecification<SamplePerson>>(new SampleAdultSpecification());
    }
}
