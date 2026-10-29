// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Security.Abstractions.Conditions;
using static XiHan.Framework.Security.Abstractions.Tests.Fixtures.ConditionTestKit;

namespace XiHan.Framework.Security.Abstractions.Tests.Conditions;

/// <summary>
/// 条件编译器。
/// </summary>
/// <remarks>
/// 编译是保存前的唯一关卡——属性要登记、运算符要配得上值种类、条件值要解析得了；出错的子句保留在结果里（求值时无法判定），错误码一律 <see cref="SecurityErrors.ConditionInvalid"/>，消息键说明原因。
/// </remarks>
public sealed class ConditionCompilerTests
{
    [Fact]
    public void UnknownAttribute_RecordsErrorAndKeepsClause()
    {
        var compiled = Compiler.Compile(Single(new ConditionClause("subject.unknown", ConditionOperator.Equals, "x", Key: "c1")));

        var error = Assert.Single(compiled.Errors);
        Assert.Equal(("c1", "subject.unknown"), (error.ClauseKey, error.AttributeName));
        Assert.Equal((SecurityErrors.ConditionInvalid, SecurityMessages.ConditionAttributeUnknown), (error.Error.Code, error.Error.MessageKey));
        Assert.Equal("subject.unknown", error.Error.Details!["attribute"]);
        Assert.NotNull(Assert.Single(Assert.Single(compiled.Groups).Clauses).Error);
    }

    [Theory]
    [InlineData("subject.userName", ConditionOperator.GreaterThan, "a", false)]
    [InlineData("subject.userName", ConditionOperator.Between, "a,b", false)]
    [InlineData("resource.amount", ConditionOperator.StartsWith, "1", false)]
    [InlineData("resource.amount", ConditionOperator.Contains, "1", false)]
    [InlineData("environment.ip", ConditionOperator.LessThan, "10.0.0.1", false)]
    [InlineData("subject.isImpersonating", ConditionOperator.Contains, "true", false)]
    [InlineData("subject.levels", ConditionOperator.StartsWith, "1", true)]
    public void OperatorNotAllowedForKind_RecordsMismatch(string attribute, ConditionOperator @operator, string value, bool multiValued)
    {
        var error = Assert.Single(Compiler.Compile(Single(new ConditionClause(attribute, @operator, value))).Errors).Error;

        Assert.Equal(multiValued ? SecurityMessages.ConditionOperatorMismatchMultiValued : SecurityMessages.ConditionOperatorMismatch, error.MessageKey);
        Assert.Equal(@operator, error.Details!["operator"]);
    }

    [Fact]
    public void ContainsOnMultiValuedNumber_IsAllowed()
        => Assert.Empty(Compiler.Compile(Single(new ConditionClause("subject.levels", ConditionOperator.Contains, "3"))).Errors);

    [Theory]
    [InlineData("[\"admin\",\"auditor\"]")]
    [InlineData("admin, auditor")]
    [InlineData("admin，auditor")]
    [InlineData("admin\nauditor")]
    [InlineData("admin\r\nauditor")]
    public void ListFormats_AreAccepted(string value)
        => Assert.Empty(Compiler.Compile(Single(new ConditionClause("subject.userName", ConditionOperator.In, value))).Errors);

    [Theory]
    [InlineData("subject.userName", "", "security.condition.list_empty")]
    [InlineData("subject.userName", "[]", "security.condition.list_empty")]
    [InlineData("subject.userName", "[{\"a\":1}]", "security.condition.list_element_unsupported")]
    [InlineData("subject.userName", "[\"a\"", "security.condition.list_json_invalid")]
    [InlineData("resource.amount", "1,abc", "security.condition.list_element_invalid")]
    public void InvalidList_RecordsError(string attribute, string value, string messageKey)
        => Assert.Equal(messageKey, Assert.Single(Compiler.Compile(Single(new ConditionClause(attribute, ConditionOperator.In, value))).Errors).Error.MessageKey);

    [Theory]
    [InlineData("9,18")]
    [InlineData("9，18")]
    [InlineData("[9,18]")]
    [InlineData("{\"min\":9,\"max\":18}")]
    [InlineData("{\"MIN\":\"9\",\"Max\":\"18\"}")]
    public void RangeFormats_AreAccepted(string value)
        => Assert.Empty(Compiler.Compile(Single(new ConditionClause("resource.amount", ConditionOperator.Between, value))).Errors);

    [Theory]
    [InlineData("resource.amount", "1,2,3", "security.condition.range_arity")]
    [InlineData("resource.amount", "18,9", "security.condition.range_reversed")]
    [InlineData("resource.amount", "{\"min\":9}", "security.condition.range_object_invalid")]
    [InlineData("resource.amount", "a,b", "security.condition.range_value_invalid")]
    public void RangeBounds_AreChecked(string attribute, string value, string messageKey)
        => Assert.Equal(messageKey, Assert.Single(Compiler.Compile(Single(new ConditionClause(attribute, ConditionOperator.Between, value))).Errors).Error.MessageKey);

    [Fact]
    public void TimeOfDayRange_MayCrossMidnight()
        => Assert.Empty(Compiler.Compile(Single(new ConditionClause("environment.time", ConditionOperator.Between, "22:00,06:00"))).Errors);

    [Theory]
    [InlineData(ConditionOperator.In, "203.0.113.0/24,2001:db8::/32", true)]
    [InlineData(ConditionOperator.In, "203.0.113.5/24", false)]
    [InlineData(ConditionOperator.In, "10.1", false)]
    [InlineData(ConditionOperator.In, "203.0.113.0/33", false)]
    [InlineData(ConditionOperator.In, "fe80::1%3", false)]
    [InlineData(ConditionOperator.Equals, "203.0.113.7", true)]
    [InlineData(ConditionOperator.Equals, "203.0.113.0/24", false)]
    public void IpValues_AreStrict(ConditionOperator @operator, string value, bool valid)
        => Assert.Equal(valid, Compiler.Compile(Single(new ConditionClause("environment.ip", @operator, value))).Errors.Count == 0);

    [Fact]
    public void ScalarValues()
    {
        Assert.Empty(Compiler.Compile(Single(new ConditionClause("subject.userName", ConditionOperator.Equals, "\"draft\""))).Errors);
        Assert.Equal(SecurityMessages.ConditionValueRequired, Assert.Single(Compiler.Compile(Single(new ConditionClause("subject.userName", ConditionOperator.Equals, " "))).Errors).Error.MessageKey);
        var invalid = Assert.Single(Compiler.Compile(Single(new ConditionClause("resource.amount", ConditionOperator.LessThan, "abc"))).Errors).Error;
        Assert.Equal((SecurityMessages.ConditionValueInvalid, (object?)ConditionValueKind.Number, (object?)"abc"), (invalid.MessageKey, invalid.Details!["kind"], invalid.Details["value"]));
        Assert.Empty(Compiler.Compile(Single(new ConditionClause("subject.userName", ConditionOperator.IsNull, null))).Errors);
    }

    [Fact]
    public void UndefinedOperatorOrEmptyAttribute_RecordsError()
    {
        Assert.Equal(SecurityMessages.ConditionOperatorUnsupported, Assert.Single(Compiler.Compile(Single(new ConditionClause("subject.userName", (ConditionOperator)99, "x"))).Errors).Error.MessageKey);
        Assert.Equal(SecurityMessages.ConditionAttributeRequired, Assert.Single(Compiler.Compile(Single(new ConditionClause(" ", ConditionOperator.Equals, "x"))).Errors).Error.MessageKey);
    }

    [Fact]
    public void EmptyGroups_AreSkipped()
    {
        Assert.True(Compiler.Compile(new ConditionSet([new ConditionGroup([]), new ConditionGroup([])])).IsEmpty);
        Assert.True(Compiler.Compile(ConditionSet.Empty).IsEmpty);
    }

    [Fact]
    public void ReferencedAttributes_UseCanonicalNames()
    {
        var compiled = Compiler.Compile(new ConditionSet(
        [
            new ConditionGroup([new ConditionClause("subject.role", ConditionOperator.Contains, "admin")]),
            new ConditionGroup([new ConditionClause("resource.query.id", ConditionOperator.Equals, "7")]),
        ]));

        Assert.Empty(compiled.Errors);
        Assert.Contains("subject.roleCodes", compiled.ReferencedAttributes);
        Assert.Contains("RESOURCE.QUERY.ID", compiled.ReferencedAttributes);
        Assert.True(compiled.UsesAlias);
    }

    [Fact]
    public void AdditionalAttributes_TakePrecedence_ForThatCompileOnly()
    {
        var clause = new ConditionClause("resource.status", ConditionOperator.LessThan, "3");
        Assert.Single(Compiler.Compile(Single(clause)).Errors);

        var compiled = Compiler.Compile(Single(clause), new ConditionCompileOptions { AdditionalAttributes = [new ConditionAttributeDescriptor("resource.status", ConditionValueKind.Number)] });

        Assert.Empty(compiled.Errors);
        Assert.Single(Compiler.Compile(Single(clause)).Errors);
    }
}
