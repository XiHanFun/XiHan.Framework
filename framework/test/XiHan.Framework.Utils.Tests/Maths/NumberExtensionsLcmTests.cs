// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Maths;

namespace XiHan.Framework.Utils.Tests.Maths;

/// <summary>
/// 最小公倍数测试
/// </summary>
/// <remarks>
/// 原实现写作 <c>(a * b).Abs() / a.Gcd(b)</c>，乘积先于除法求值，
/// 两个各自远在类型范围内的数也会在中间步骤溢出。
/// 改成先除后乘之后，中间值不超过最终结果，只有结果本身真正越界时才溢出。
/// </remarks>
public class NumberExtensionsLcmTests
{
    /// <summary>
    /// 结果在类型范围内时不再因中间乘积溢出而算错
    /// </summary>
    /// <remarks>
    /// 100000 与 150000 的最小公倍数是 300000，int 完全装得下，
    /// 但两数乘积 1.5e10 早已越界，原实现在这一步就丢了精度。
    /// </remarks>
    [Theory]
    [InlineData(100_000, 150_000, 300_000)]
    [InlineData(1_000_000, 2_000_000, 2_000_000)]
    [InlineData(46_341, 46_341, 46_341)]
    public void Lcm_WhenProductOverflowsButResultFits_IsStillCorrect(int a, int b, int expected)
    {
        Assert.Equal(expected, a.Lcm(b));
    }

    /// <summary>
    /// 常规取值的最小公倍数正确
    /// </summary>
    [Theory]
    [InlineData(4, 6, 12)]
    [InlineData(21, 6, 42)]
    [InlineData(7, 13, 91)]
    [InlineData(12, 12, 12)]
    [InlineData(1, 9, 9)]
    public void Lcm_ReturnsLeastCommonMultiple(int a, int b, int expected)
    {
        Assert.Equal(expected, a.Lcm(b));
        Assert.Equal(expected, b.Lcm(a));
    }

    /// <summary>
    /// 负数取绝对值后计算，结果为正
    /// </summary>
    [Theory]
    [InlineData(-4, 6, 12)]
    [InlineData(4, -6, 12)]
    [InlineData(-4, -6, 12)]
    public void Lcm_WithNegativeOperands_ReturnsPositiveResult(int a, int b, int expected)
    {
        Assert.Equal(expected, a.Lcm(b));
    }

    /// <summary>
    /// 任一操作数为零时结果为零
    /// </summary>
    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(0, 0)]
    public void Lcm_WithZeroOperand_ReturnsZero(int a, int b)
    {
        Assert.Equal(0, a.Lcm(b));
    }

    /// <summary>
    /// 更宽的类型上同样正确
    /// </summary>
    [Fact]
    public void Lcm_OnWiderType_HandlesLargeValues()
    {
        Assert.Equal(999_999_000_000L, 1_000_000L.Lcm(999_999L));
    }

    /// <summary>
    /// 结果与"乘积等于最大公约数乘最小公倍数"的恒等式一致
    /// </summary>
    /// <remarks>遍历小范围取值，直接锁住数学恒等式，不依赖挑选样例。</remarks>
    [Fact]
    public void Lcm_SatisfiesGcdLcmIdentity()
    {
        for (var a = 1; a <= 60; a++)
        {
            for (var b = 1; b <= 60; b++)
            {
                Assert.Equal(a * b, a.Gcd(b) * a.Lcm(b));
            }
        }
    }
}
