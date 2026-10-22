// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Conditions;
using static XiHan.Framework.Authorization.Tests.Infrastructure.ConditionTestKit;

namespace XiHan.Framework.Authorization.Tests.Conditions;

/// <summary>
/// 运算符与值种类的搭配规则：编辑界面据此列运算符，编译器据此拒绝不搭配的子句，两边必须一致
/// </summary>
public class ConditionOperatorRulesTests
{
    /// <summary>
    /// 各值种类能用的运算符
    /// </summary>
    [Theory]
    [InlineData("subject.userName", "Equals,NotEquals,Contains,NotContains,In,NotIn,StartsWith,EndsWith,IsNull,IsNotNull")]
    [InlineData("subject.roleCodes", "Equals,NotEquals,Contains,NotContains,In,NotIn,StartsWith,EndsWith,IsNull,IsNotNull")]
    [InlineData("resource.amount", "Equals,NotEquals,GreaterThan,GreaterThanOrEquals,LessThan,LessThanOrEquals,In,NotIn,Between,IsNull,IsNotNull")]
    [InlineData("environment.ip", "Equals,NotEquals,In,NotIn,IsNull,IsNotNull")]
    [InlineData("subject.isImpersonating", "Equals,NotEquals,In,NotIn,IsNull,IsNotNull")]
    [InlineData("environment.time", "Equals,NotEquals,GreaterThan,GreaterThanOrEquals,LessThan,LessThanOrEquals,In,NotIn,Between,IsNull,IsNotNull")]
    public void AllowedFor_ListsOperatorsByKind(string attribute, string expected)
    {
        var descriptor = Attributes.Single(item => item.Name == attribute);

        Assert.Equal(expected.Split(','), ConditionOperatorRules.AllowedFor(descriptor).Select(@operator => @operator.ToString()));
    }

    /// <summary>
    /// 编译器只在规则不允许时报运算符错误
    /// </summary>
    [Fact]
    public void Compiler_AgreesWithRules()
    {
        foreach (var descriptor in Attributes.Where(item => !item.IsFamily))
        {
            foreach (var @operator in Enum.GetValues<ConditionOperator>())
            {
                var compiled = Compiler.Compile(Single(new ConditionClause(descriptor.Name, @operator, "x")));
                var operatorRejected = compiled.Errors.Any(error => error.Message.StartsWith("运算符", StringComparison.Ordinal));

                Assert.Equal(!ConditionOperatorRules.IsAllowed(@operator, descriptor), operatorRejected);
            }
        }
    }
}
