// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Security.Abstractions.Conditions;
using static XiHan.Framework.Security.Abstractions.Tests.Fixtures.ConditionTestKit;

namespace XiHan.Framework.Security.Abstractions.Tests.Conditions;

/// <summary>
/// 运算符与值种类的搭配规则：编辑界面据此列运算符，编译器据此拒绝不搭配的子句，两边必须一致。
/// </summary>
public sealed class ConditionOperatorRulesTests
{
    [Theory]
    [InlineData("subject.userName", "Equals,NotEquals,Contains,NotContains,In,NotIn,StartsWith,EndsWith,IsNull,IsNotNull")]
    [InlineData("subject.roleCodes", "Equals,NotEquals,Contains,NotContains,In,NotIn,StartsWith,EndsWith,IsNull,IsNotNull")]
    [InlineData("subject.levels", "Equals,NotEquals,GreaterThan,GreaterThanOrEquals,LessThan,LessThanOrEquals,Contains,NotContains,In,NotIn,Between,IsNull,IsNotNull")]
    [InlineData("resource.amount", "Equals,NotEquals,GreaterThan,GreaterThanOrEquals,LessThan,LessThanOrEquals,In,NotIn,Between,IsNull,IsNotNull")]
    [InlineData("environment.ip", "Equals,NotEquals,In,NotIn,IsNull,IsNotNull")]
    [InlineData("subject.isImpersonating", "Equals,NotEquals,In,NotIn,IsNull,IsNotNull")]
    [InlineData("environment.time", "Equals,NotEquals,GreaterThan,GreaterThanOrEquals,LessThan,LessThanOrEquals,In,NotIn,Between,IsNull,IsNotNull")]
    public void AllowedFor_ListsOperatorsByKind(string attribute, string expected)
        => Assert.Equal(expected.Split(','), ConditionOperatorRules.AllowedFor(Attributes.Single(item => item.Name == attribute)).Select(static @operator => @operator.ToString()));

    [Fact]
    public void Compiler_RejectsTheOperatorExactlyWhenTheRulesDo()
    {
        foreach (var descriptor in Attributes.Where(static item => !item.IsFamily))
        {
            foreach (var @operator in Enum.GetValues<ConditionOperator>())
            {
                var compiled = Compiler.Compile(Single(new ConditionClause(descriptor.Name, @operator, "x")));
                var operatorRejected = compiled.Errors.Any(static error => error.Error.MessageKey is SecurityMessages.ConditionOperatorMismatch or SecurityMessages.ConditionOperatorMismatchMultiValued);

                Assert.Equal(!ConditionOperatorRules.IsAllowed(@operator, descriptor), operatorRejected);
            }
        }
    }
}
