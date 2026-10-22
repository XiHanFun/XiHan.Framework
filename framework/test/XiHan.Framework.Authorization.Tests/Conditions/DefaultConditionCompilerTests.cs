// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Conditions;
using XiHan.Framework.Authorization.Tests.Infrastructure;
using static XiHan.Framework.Authorization.Tests.Infrastructure.ConditionTestKit;

namespace XiHan.Framework.Authorization.Tests.Conditions;

/// <summary>
/// 条件编译器测试
/// </summary>
/// <remarks>
/// 编译是保存前的唯一关卡：属性要登记、运算符要配得上值种类、条件值要解析得了；
/// 出错的子句保留在结果里（求值时无法判定），这样存量脏数据不会让整个条件集失效。
/// </remarks>
public class DefaultConditionCompilerTests
{
    /// <summary>
    /// 未登记的属性记一条错误，子句保留
    /// </summary>
    [Fact]
    public void Compile_UnknownAttribute_RecordsErrorAndKeepsClause()
    {
        var compiled = Compiler.Compile(Single(new ConditionClause("subject.unknown", ConditionOperator.Equals, "x", Key: "c1")));

        var error = Assert.Single(compiled.Errors);
        Assert.Equal("c1", error.ClauseKey);
        Assert.Contains("未登记", error.Message, StringComparison.Ordinal);
        Assert.NotNull(Assert.Single(Assert.Single(compiled.Groups).Clauses).Error);
    }

    /// <summary>
    /// 运算符配不上值种类时报错：字符串不能比大小，数字不能用开头 / 结尾，单值数字不能用包含
    /// </summary>
    [Theory]
    [InlineData("subject.userName", ConditionOperator.GreaterThan, "a")]
    [InlineData("subject.userName", ConditionOperator.Between, "a,b")]
    [InlineData("resource.amount", ConditionOperator.StartsWith, "1")]
    [InlineData("resource.amount", ConditionOperator.Contains, "1")]
    [InlineData("environment.ip", ConditionOperator.LessThan, "10.0.0.1")]
    [InlineData("subject.isImpersonating", ConditionOperator.Contains, "true")]
    public void Compile_OperatorNotAllowedForKind_RecordsError(string attribute, ConditionOperator @operator, string value)
    {
        var compiled = Compiler.Compile(Single(new ConditionClause(attribute, @operator, value)));

        Assert.Contains("不能用于", Assert.Single(compiled.Errors).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 多值属性可以用包含（列表含该值），不分值种类
    /// </summary>
    [Fact]
    public void Compile_ContainsOnMultiValuedNumber_IsAllowed()
    {
        var compiled = Compiler.Compile(Single(new ConditionClause("subject.levels", ConditionOperator.Contains, "3")));

        Assert.Empty(compiled.Errors);
    }

    /// <summary>
    /// 集合三种写法都认：JSON 数组、英文逗号、中文逗号与换行
    /// </summary>
    [Theory]
    [InlineData("[\"admin\",\"auditor\"]")]
    [InlineData("admin, auditor")]
    [InlineData("admin，auditor")]
    [InlineData("admin\nauditor")]
    public void Compile_ListFormats_AreAccepted(string value)
    {
        var compiled = Compiler.Compile(Single(new ConditionClause("subject.userName", ConditionOperator.In, value)));

        Assert.Empty(compiled.Errors);
    }

    /// <summary>
    /// 空集合、JSON 不是数组、元素解析不了都报错
    /// </summary>
    [Theory]
    [InlineData("subject.userName", "")]
    [InlineData("subject.userName", "[]")]
    [InlineData("subject.userName", "[{\"a\":1}]")]
    [InlineData("subject.userName", "[\"a\"")]
    [InlineData("resource.amount", "1,abc")]
    public void Compile_InvalidList_RecordsError(string attribute, string value)
    {
        var compiled = Compiler.Compile(Single(new ConditionClause(attribute, ConditionOperator.In, value)));

        Assert.Single(compiled.Errors);
    }

    /// <summary>
    /// 区间三种写法都认：逗号、JSON 数组、min/max 对象（文档里的旧例 9,18 也在内）
    /// </summary>
    [Theory]
    [InlineData("9,18")]
    [InlineData("[9,18]")]
    [InlineData("{\"min\":9,\"max\":18}")]
    [InlineData("{\"MIN\":\"9\",\"Max\":\"18\"}")]
    public void Compile_RangeFormats_AreAccepted(string value)
    {
        var compiled = Compiler.Compile(Single(new ConditionClause("resource.amount", ConditionOperator.Between, value)));

        Assert.Empty(compiled.Errors);
    }

    /// <summary>
    /// 区间不是两个值、下限大于上限都报错；时刻允许下限大于上限（跨零点）
    /// </summary>
    [Fact]
    public void Compile_RangeBounds_AreChecked()
    {
        Assert.Single(Compiler.Compile(Single(new ConditionClause("resource.amount", ConditionOperator.Between, "1,2,3"))).Errors);
        Assert.Single(Compiler.Compile(Single(new ConditionClause("resource.amount", ConditionOperator.Between, "18,9"))).Errors);
        Assert.Single(Compiler.Compile(Single(new ConditionClause("resource.amount", ConditionOperator.Between, "{\"min\":9}"))).Errors);
        Assert.Empty(Compiler.Compile(Single(new ConditionClause("environment.time", ConditionOperator.Between, "22:00,06:00"))).Errors);
    }

    /// <summary>
    /// IP：集合可写 CIDR，主机位不为零的网段、简写的 IPv4、等于里写网段都报错
    /// </summary>
    [Theory]
    [InlineData(ConditionOperator.In, "203.0.113.0/24,2001:db8::/32", true)]
    [InlineData(ConditionOperator.In, "203.0.113.5/24", false)]
    [InlineData(ConditionOperator.In, "10.1", false)]
    [InlineData(ConditionOperator.In, "203.0.113.0/33", false)]
    [InlineData(ConditionOperator.Equals, "203.0.113.7", true)]
    [InlineData(ConditionOperator.Equals, "203.0.113.0/24", false)]
    public void Compile_IpValues_AreStrict(ConditionOperator @operator, string value, bool valid)
    {
        var compiled = Compiler.Compile(Single(new ConditionClause("environment.ip", @operator, value)));

        Assert.Equal(valid, compiled.Errors.Count == 0);
    }

    /// <summary>
    /// 标量写成 JSON 字符串时去掉引号；空值只有为空 / 不为空可以
    /// </summary>
    [Fact]
    public void Compile_ScalarValues()
    {
        Assert.Empty(Compiler.Compile(Single(new ConditionClause("subject.userName", ConditionOperator.Equals, "\"draft\""))).Errors);
        Assert.Single(Compiler.Compile(Single(new ConditionClause("subject.userName", ConditionOperator.Equals, " "))).Errors);
        Assert.Single(Compiler.Compile(Single(new ConditionClause("resource.amount", ConditionOperator.LessThan, "abc"))).Errors);
        Assert.Empty(Compiler.Compile(Single(new ConditionClause("subject.userName", ConditionOperator.IsNull, null))).Errors);
    }

    /// <summary>
    /// 未定义的运算符整数值报错
    /// </summary>
    [Fact]
    public void Compile_UndefinedOperator_RecordsError()
    {
        var compiled = Compiler.Compile(Single(new ConditionClause("subject.userName", (ConditionOperator)99, "x")));

        Assert.Contains("不支持的运算符", Assert.Single(compiled.Errors).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 空组跳过；全是空组的条件集是无条件
    /// </summary>
    [Fact]
    public void Compile_EmptyGroups_AreSkipped()
    {
        var compiled = Compiler.Compile(new ConditionSet([new ConditionGroup([]), new ConditionGroup([])]));

        Assert.True(compiled.IsEmpty);
        Assert.True(Compiler.Compile(ConditionSet.Empty).IsEmpty);
    }

    /// <summary>
    /// 引用到的属性按规范名收集，别名与族都折算；用了别名会标出
    /// </summary>
    [Fact]
    public void Compile_ReferencedAttributes_UseCanonicalNames()
    {
        var compiled = Compiler.Compile(new ConditionSet(
        [
            new ConditionGroup([new ConditionClause("subject.role", ConditionOperator.Contains, "admin")]),
            new ConditionGroup([new ConditionClause("resource.query.id", ConditionOperator.Equals, "7")])
        ]));

        Assert.Empty(compiled.Errors);
        Assert.Contains("subject.roleCodes", compiled.ReferencedAttributes);
        Assert.Contains("resource.query.id", compiled.ReferencedAttributes);
        Assert.True(compiled.UsesAlias);
    }

    /// <summary>
    /// 额外登记项只对这一次编译生效，并优先于登记表
    /// </summary>
    [Fact]
    public void Compile_AdditionalAttributes_TakePrecedence()
    {
        var clause = new ConditionClause("resource.status", ConditionOperator.LessThan, "3");
        Assert.Single(Compiler.Compile(Single(clause)).Errors);

        var compiled = Compiler.Compile(Single(clause), new ConditionCompileOptions
        {
            AdditionalAttributes = [new ConditionAttributeDescriptor("resource.status", ConditionValueKind.Number)]
        });

        Assert.Empty(compiled.Errors);
        Assert.Single(Compiler.Compile(Single(clause)).Errors);
    }
}
