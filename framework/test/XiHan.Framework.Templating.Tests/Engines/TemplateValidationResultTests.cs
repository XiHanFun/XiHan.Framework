// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Tests.Engines;

/// <summary>
/// <see cref="TemplateValidationResult"/> 工厂方法与值语义的测试
/// </summary>
/// <remarks>
/// 该结果类型是所有引擎校验的公共出口，成功态必须不带错误信息，
/// 失败态的行列号是可选的（正则类校验给不出位置），这两点是调用方判空的依据。
/// </remarks>
public class TemplateValidationResultTests
{
    /// <summary>
    /// 成功结果不带任何错误信息
    /// </summary>
    [Fact]
    public void Success_IsValid_WithoutErrorDetails()
    {
        var result = TemplateValidationResult.Success;

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ErrorLine);
        Assert.Null(result.ErrorColumn);
    }

    /// <summary>
    /// 成功结果每次访问都是新实例
    /// </summary>
    [Fact]
    public void Success_ReturnsNewInstanceEachTime()
    {
        // 它是表达式体属性而不是静态只读字段，调用方不能依赖引用相等来判定成功
        Assert.NotSame(TemplateValidationResult.Success, TemplateValidationResult.Success);
    }

    /// <summary>
    /// 只给错误消息时行列号为空
    /// </summary>
    [Fact]
    public void Failure_WithMessageOnly_HasNullPosition()
    {
        var result = TemplateValidationResult.Failure("语法错误");

        Assert.False(result.IsValid);
        Assert.Equal("语法错误", result.ErrorMessage);
        Assert.Null(result.ErrorLine);
        Assert.Null(result.ErrorColumn);
    }

    /// <summary>
    /// 给出行列号时原样保留
    /// </summary>
    [Fact]
    public void Failure_WithPosition_KeepsLineAndColumn()
    {
        var result = TemplateValidationResult.Failure("语法错误", 3, 7);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.ErrorLine);
        Assert.Equal(7, result.ErrorColumn);
    }

    /// <summary>
    /// 内容相同的两个失败结果按值相等
    /// </summary>
    [Fact]
    public void Failure_TwoResultsWithSameValues_AreEqual()
    {
        var first = TemplateValidationResult.Failure("语法错误", 1, 2);
        var second = TemplateValidationResult.Failure("语法错误", 1, 2);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    /// 内容不同的失败结果不相等
    /// </summary>
    [Fact]
    public void Failure_WithDifferentMessage_AreNotEqual()
    {
        Assert.NotEqual(
            TemplateValidationResult.Failure("甲"),
            TemplateValidationResult.Failure("乙"));
    }

    /// <summary>
    /// with 表达式只改动指定成员
    /// </summary>
    [Fact]
    public void With_ChangesOnlyTargetedMember()
    {
        var original = TemplateValidationResult.Failure("语法错误", 1, 2);

        var changed = original with { ErrorLine = 9 };

        Assert.Equal(9, changed.ErrorLine);
        Assert.Equal("语法错误", changed.ErrorMessage);
        Assert.Equal(2, changed.ErrorColumn);
        Assert.Equal(1, original.ErrorLine);
    }
}
