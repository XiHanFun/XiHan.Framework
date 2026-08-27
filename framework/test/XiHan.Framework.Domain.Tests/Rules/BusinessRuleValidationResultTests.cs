// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Rules;

namespace XiHan.Framework.Domain.Tests.Rules;

/// <summary>
/// 业务规则验证结果测试
/// </summary>
/// <remarks>
/// 结果对象只能经两个静态工厂构造（构造函数是私有的），错误集合对外只读。
/// </remarks>
public class BusinessRuleValidationResultTests
{
    /// <summary>
    /// 成功结果没有错误信息
    /// </summary>
    [Fact]
    public void Success_HasNoErrors()
    {
        var result = BusinessRuleValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// 单条错误的失败结果只包含该条错误
    /// </summary>
    [Fact]
    public void Failure_WithSingleError_ContainsOnlyThatError()
    {
        var result = BusinessRuleValidationResult.Failure("坏了");

        Assert.False(result.IsValid);
        Assert.Equal("坏了", Assert.Single(result.Errors));
    }

    /// <summary>
    /// 多条错误的失败结果按原顺序保留
    /// </summary>
    [Fact]
    public void Failure_WithMultipleErrors_KeepsOrder()
    {
        var result = BusinessRuleValidationResult.Failure(new[] { "一", "二", "三" });

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
        Assert.Equal("一", result.Errors[0]);
        Assert.Equal("二", result.Errors[1]);
        Assert.Equal("三", result.Errors[2]);
    }

    /// <summary>
    /// 空错误集合仍然构成失败结果
    /// </summary>
    /// <remarks>
    /// 工厂并不校验集合非空，IsValid 完全由调用的工厂方法决定，这条语义要锁住。
    /// </remarks>
    [Fact]
    public void Failure_WithEmptyErrors_IsStillInvalid()
    {
        var result = BusinessRuleValidationResult.Failure(Array.Empty<string>());

        Assert.False(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// 错误集合是快照，构造后修改源集合不影响结果
    /// </summary>
    [Fact]
    public void Failure_TakesSnapshotOfSourceErrors()
    {
        var source = new List<string> { "一" };

        var result = BusinessRuleValidationResult.Failure(source);
        source.Add("二");

        Assert.Single(result.Errors);
    }

    /// <summary>
    /// 成功结果的字符串表示是固定文案
    /// </summary>
    [Fact]
    public void ToString_WhenValid_UsesSuccessText()
    {
        Assert.Equal("Validation successful", BusinessRuleValidationResult.Success().ToString());
    }

    /// <summary>
    /// 失败结果的字符串表示用分号拼接全部错误
    /// </summary>
    [Fact]
    public void ToString_WhenInvalid_JoinsErrorsWithSemicolon()
    {
        var result = BusinessRuleValidationResult.Failure(new[] { "一", "二" });

        Assert.Equal("Validation failed: 一; 二", result.ToString());
    }

    /// <summary>
    /// 结果对象没有公开构造函数
    /// </summary>
    [Fact]
    public void Type_HasNoPublicConstructor()
    {
        var constructors = typeof(BusinessRuleValidationResult).GetConstructors();

        Assert.Empty(constructors);
    }
}
