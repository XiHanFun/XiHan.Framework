// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Linq.Expressions;

namespace XiHan.Framework.Utils.Tests.Linq;

/// <summary>
/// 泛型表达式构建器测试
/// </summary>
/// <remarks>
/// 断言方式：把构建出来的表达式编译成委托后拿真实对象跑一遍，
/// 这样验的是表达式树语义而不是它的节点长相，后者属实现细节。
/// 不覆盖 FromExpression 与 And/Or 的"构建器重载"，原因见交付报告的疑似缺陷段落：
/// 它们直接搬用了外部 Lambda 的 Body，却没有把参数重写到本构建器自己的参数上。
/// </remarks>
public class ExpressionBuilderTests
{
    /// <summary>
    /// 没有任何条件时构建出恒真表达式
    /// </summary>
    [Fact]
    public void Build_WithoutAnyCondition_IsAlwaysTrue()
    {
        var predicate = ExpressionBuilder<Person>.Create().Compile();

        Assert.True(predicate(new Person { Name = "甲", Age = 1 }));
        Assert.True(predicate(new Person { Name = "乙", Age = 99 }));
    }

    /// <summary>
    /// 构建出的 Lambda 使用指定的参数名
    /// </summary>
    [Fact]
    public void Create_WithParameterName_UsesGivenName()
    {
        var expression = ExpressionBuilder<Person>.Create("p").Property(nameof(Person.Age)).GreaterThan(1).Build();

        Assert.Equal("p", expression.Parameters[0].Name);
    }

    /// <summary>
    /// 等于与不等于比较
    /// </summary>
    [Fact]
    public void EqualAndNotEqual_CompareByValue()
    {
        var isTarget = ExpressionBuilder<Person>.Create().Property(nameof(Person.Name)).Equal("张三").Compile();
        var isNotTarget = ExpressionBuilder<Person>.Create().Property(nameof(Person.Name)).NotEqual("张三").Compile();

        Assert.True(isTarget(new Person { Name = "张三" }));
        Assert.False(isTarget(new Person { Name = "李四" }));
        Assert.False(isNotTarget(new Person { Name = "张三" }));
        Assert.True(isNotTarget(new Person { Name = "李四" }));
    }

    /// <summary>
    /// 大小比较系列覆盖开闭区间
    /// </summary>
    [Fact]
    public void ComparisonOperators_BuildExpectedPredicates()
    {
        var greater = ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).GreaterThan(18).Compile();
        var greaterOrEqual = ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).GreaterThanOrEqual(18).Compile();
        var less = ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).LessThan(18).Compile();
        var lessOrEqual = ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).LessThanOrEqual(18).Compile();

        var exact = new Person { Age = 18 };

        Assert.False(greater(exact));
        Assert.True(greaterOrEqual(exact));
        Assert.False(less(exact));
        Assert.True(lessOrEqual(exact));
        Assert.True(greater(new Person { Age = 19 }));
        Assert.True(less(new Person { Age = 17 }));
    }

    /// <summary>
    /// 嵌套属性访问按路径逐级取值
    /// </summary>
    [Fact]
    public void NestedProperty_WalksPropertyPath()
    {
        var predicate = ExpressionBuilder<Person>.Create()
            .NestedProperty($"{nameof(Person.Company)}.{nameof(Company.Title)}")
            .Equal("曦寒")
            .Compile();

        Assert.True(predicate(new Person { Company = new Company { Title = "曦寒" } }));
        Assert.False(predicate(new Person { Company = new Company { Title = "其他" } }));
    }

    /// <summary>
    /// 字符串包含、前缀、后缀判断
    /// </summary>
    [Fact]
    public void StringOperations_MatchSubstrings()
    {
        var contains = ExpressionBuilder<Person>.Create().Property(nameof(Person.Name)).Contains("han").Compile();
        var startsWith = ExpressionBuilder<Person>.Create().Property(nameof(Person.Name)).StartsWith("Xi").Compile();
        var endsWith = ExpressionBuilder<Person>.Create().Property(nameof(Person.Name)).EndsWith("Fun").Compile();

        var person = new Person { Name = "XihanFun" };

        Assert.True(contains(person));
        Assert.True(startsWith(person));
        Assert.True(endsWith(person));
        Assert.False(contains(new Person { Name = "other" }));
    }

    /// <summary>
    /// 字符串操作支持忽略大小写
    /// </summary>
    [Fact]
    public void StringOperations_WithIgnoreCase_MatchRegardlessOfCase()
    {
        var contains = ExpressionBuilder<Person>.Create().Property(nameof(Person.Name)).Contains("HAN", true).Compile();
        var startsWith = ExpressionBuilder<Person>.Create().Property(nameof(Person.Name)).StartsWith("xi", true).Compile();
        var endsWith = ExpressionBuilder<Person>.Create().Property(nameof(Person.Name)).EndsWith("FUN", true).Compile();

        var person = new Person { Name = "XihanFun" };

        Assert.True(contains(person));
        Assert.True(startsWith(person));
        Assert.True(endsWith(person));
    }

    /// <summary>
    /// 对非字符串属性使用字符串操作时抛无效操作异常
    /// </summary>
    [Fact]
    public void StringOperations_OnNonStringProperty_Throw()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).Contains("x"));
        Assert.Throws<InvalidOperationException>(() =>
            ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).StartsWith("x"));
        Assert.Throws<InvalidOperationException>(() =>
            ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).EndsWith("x"));
        Assert.Throws<InvalidOperationException>(() =>
            ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).IsNullOrEmpty());
        Assert.Throws<InvalidOperationException>(() =>
            ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).IsNotNullOrEmpty());
    }

    /// <summary>
    /// 集合包含与不包含
    /// </summary>
    [Fact]
    public void InAndNotIn_CheckMembership()
    {
        var inSet = ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).In([18, 20]).Compile();
        var notInSet = ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).NotIn([18, 20]).Compile();

        Assert.True(inSet(new Person { Age = 18 }));
        Assert.False(inSet(new Person { Age = 19 }));
        Assert.False(notInSet(new Person { Age = 20 }));
        Assert.True(notInSet(new Person { Age = 19 }));
    }

    /// <summary>
    /// 空值与非空值判断
    /// </summary>
    [Fact]
    public void IsNullAndIsNotNull_DetectNullProperty()
    {
        var isNull = ExpressionBuilder<Person>.Create().Property(nameof(Person.Nickname)).IsNull().Compile();
        var isNotNull = ExpressionBuilder<Person>.Create().Property(nameof(Person.Nickname)).IsNotNull().Compile();

        Assert.True(isNull(new Person { Nickname = null }));
        Assert.False(isNull(new Person { Nickname = "昵称" }));
        Assert.False(isNotNull(new Person { Nickname = null }));
        Assert.True(isNotNull(new Person { Nickname = "昵称" }));
    }

    /// <summary>
    /// 字符串空判断把 null 与空串都算作空
    /// </summary>
    [Fact]
    public void IsNullOrEmptyAndIsNotNullOrEmpty_TreatNullAndEmptyAlike()
    {
        var isEmpty = ExpressionBuilder<Person>.Create().Property(nameof(Person.Nickname)).IsNullOrEmpty().Compile();
        var isNotEmpty = ExpressionBuilder<Person>.Create().Property(nameof(Person.Nickname)).IsNotNullOrEmpty().Compile();

        Assert.True(isEmpty(new Person { Nickname = null }));
        Assert.True(isEmpty(new Person { Nickname = string.Empty }));
        Assert.False(isEmpty(new Person { Nickname = "x" }));
        Assert.True(isNotEmpty(new Person { Nickname = "x" }));
        Assert.False(isNotEmpty(new Person { Nickname = string.Empty }));
    }

    /// <summary>
    /// 取反当前条件
    /// </summary>
    [Fact]
    public void Not_InvertsCurrentCondition()
    {
        var predicate = ExpressionBuilder<Person>.Create().Property(nameof(Person.Age)).GreaterThan(18).Not().Compile();

        Assert.False(predicate(new Person { Age = 19 }));
        Assert.True(predicate(new Person { Age = 17 }));
    }

    /// <summary>
    /// 没有条件时取反得到恒假表达式
    /// </summary>
    [Fact]
    public void Not_WithoutCondition_IsAlwaysFalse()
    {
        var predicate = ExpressionBuilder<Person>.Create().Not().Compile();

        Assert.False(predicate(new Person()));
    }

    /// <summary>
    /// 与 Lambda 做逻辑与，参数会被重写到构建器自身的参数上
    /// </summary>
    [Fact]
    public void And_WithLambda_CombinesConditions()
    {
        var predicate = ExpressionBuilder<Person>.Create()
            .Property(nameof(Person.Age))
            .GreaterThan(18)
            .And(p => p.Name == "张三")
            .Compile();

        Assert.True(predicate(new Person { Age = 19, Name = "张三" }));
        Assert.False(predicate(new Person { Age = 19, Name = "李四" }));
        Assert.False(predicate(new Person { Age = 17, Name = "张三" }));
    }

    /// <summary>
    /// 与 Lambda 做逻辑或
    /// </summary>
    [Fact]
    public void Or_WithLambda_CombinesConditions()
    {
        var predicate = ExpressionBuilder<Person>.Create()
            .Property(nameof(Person.Age))
            .GreaterThan(60)
            .Or(p => p.Name == "张三")
            .Compile();

        Assert.True(predicate(new Person { Age = 10, Name = "张三" }));
        Assert.True(predicate(new Person { Age = 61, Name = "李四" }));
        Assert.False(predicate(new Person { Age = 10, Name = "李四" }));
    }

    /// <summary>
    /// 尚无条件时与 Lambda 组合直接采用该 Lambda
    /// </summary>
    [Fact]
    public void AndOrWithLambda_WhenNoExistingCondition_UsesLambdaAlone()
    {
        var andOnly = ExpressionBuilder<Person>.Create().And(p => p.Age > 18).Compile();
        var orOnly = ExpressionBuilder<Person>.Create().Or(p => p.Age > 18).Compile();

        Assert.True(andOnly(new Person { Age = 19 }));
        Assert.False(andOnly(new Person { Age = 17 }));
        Assert.True(orOnly(new Person { Age = 19 }));
        Assert.False(orOnly(new Person { Age = 17 }));
    }

    /// <summary>
    /// 属性不存在时抛参数异常
    /// </summary>
    [Fact]
    public void Property_WhenNameNotFound_Throws()
    {
        Assert.Throws<ArgumentException>(() => ExpressionBuilder<Person>.Create().Property("NotExists"));
    }

    /// <summary>
    /// 测试用实体
    /// </summary>
    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }

        public string? Nickname { get; set; }

        public Company Company { get; set; } = new();
    }

    /// <summary>
    /// 测试用嵌套实体
    /// </summary>
    private sealed class Company
    {
        public string Title { get; set; } = string.Empty;
    }
}
