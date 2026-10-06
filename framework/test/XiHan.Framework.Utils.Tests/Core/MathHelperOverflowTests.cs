// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Core;

namespace XiHan.Framework.Utils.Tests.Core;

/// <summary>
/// 数学运算辅助类的溢出边界测试
/// </summary>
/// <remarks>
/// 阶乘与斐波那契原先都不限入参，越界后静默回绕成负数
/// （<c>Factorial(21)</c> 返回 -4249290049419214848，<c>Fibonacci(50)</c> 的第 50 项返回 -811192543）。
/// 返回一串错误数字比抛异常危险得多，调用方无从察觉。
/// </remarks>
public class MathHelperOverflowTests
{
    /// <summary>
    /// 边界内的阶乘结果精确
    /// </summary>
    [Theory]
    [InlineData(0, 1L)]
    [InlineData(1, 1L)]
    [InlineData(5, 120L)]
    [InlineData(20, 2432902008176640000L)]
    public void Factorial_WithinRange_ReturnsExactValue(int number, long expected)
    {
        Assert.Equal(expected, MathHelper.Factorial(number));
    }

    /// <summary>
    /// 超出 long 范围的阶乘被拒绝，而不是回绕成负数
    /// </summary>
    [Theory]
    [InlineData(21)]
    [InlineData(25)]
    [InlineData(int.MaxValue)]
    public void Factorial_BeyondRange_Throws(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathHelper.Factorial(number));
    }

    /// <summary>
    /// 负数入参被拒绝
    /// </summary>
    [Fact]
    public void Factorial_WhenNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathHelper.Factorial(-1));
    }

    /// <summary>
    /// 允许的最大入参恰好不溢出，再大一个就溢出
    /// </summary>
    /// <remarks>把常量与真实溢出点钉在一起，常量改错会立刻暴露。</remarks>
    [Fact]
    public void MaxFactorialInput_IsTheLastValueThatFitsInLong()
    {
        var largest = MathHelper.Factorial(MathHelper.MaxFactorialInput);

        Assert.True(largest > 0);
        Assert.True(largest > long.MaxValue / (MathHelper.MaxFactorialInput + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathHelper.Factorial(MathHelper.MaxFactorialInput + 1));
    }

    /// <summary>
    /// 斐波那契数列内容正确
    /// </summary>
    [Fact]
    public void Fibonacci_ReturnsExpectedSequence()
    {
        Assert.Equal(new[] { 0 }, MathHelper.Fibonacci(1));
        Assert.Equal(new[] { 0, 1 }, MathHelper.Fibonacci(2));
        Assert.Equal(new[] { 0, 1, 1, 2, 3, 5, 8, 13 }, MathHelper.Fibonacci(8));
    }

    /// <summary>
    /// 允许的最大项数全部为正且严格递增
    /// </summary>
    [Fact]
    public void Fibonacci_AtMaxCount_StaysWithinIntRange()
    {
        var sequence = MathHelper.Fibonacci(MathHelper.MaxFibonacciCount);

        Assert.Equal(MathHelper.MaxFibonacciCount, sequence.Count);
        Assert.Equal(1836311903, sequence[^1]);
        Assert.All(sequence, value => Assert.True(value >= 0));

        // 除开头的 0、1、1 外严格递增，回绕必然打破这一点
        for (var i = 3; i < sequence.Count; i++)
        {
            Assert.True(sequence[i] > sequence[i - 1]);
        }
    }

    /// <summary>
    /// 超出 int 范围的项数被拒绝，而不是回绕成负数
    /// </summary>
    [Theory]
    [InlineData(48)]
    [InlineData(50)]
    [InlineData(100)]
    public void Fibonacci_BeyondRange_Throws(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathHelper.Fibonacci(count));
    }

    /// <summary>
    /// 非正项数被拒绝
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Fibonacci_WhenCountNotPositive_Throws(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathHelper.Fibonacci(count));
    }
}
