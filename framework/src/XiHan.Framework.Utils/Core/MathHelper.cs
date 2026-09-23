// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 数学运算辅助类
/// </summary>
public static class MathHelper
{
    /// <summary>
    /// <see cref="Factorial"/> 能在 <see cref="long"/> 范围内精确表示的最大入参，20! 是最后一个不溢出的值
    /// </summary>
    public const int MaxFactorialInput = 20;

    /// <summary>
    /// <see cref="Fibonacci"/> 能在 <see cref="int"/> 范围内精确表示的最大项数
    /// </summary>
    /// <remarks>数列以 0、1 开头，下标 46 的 1836311903 是最后一个不溢出的项，对应 47 项。</remarks>
    public const int MaxFibonacciCount = 47;

    #region 几何计算

    /// <summary>
    /// 计算圆的面积
    /// </summary>
    /// <param name="radius">圆的半径（必须为非负数）</param>
    /// <returns>圆的面积</returns>
    /// <exception cref="ArgumentException">当半径为负数时抛出</exception>
    public static double CircleArea(double radius)
    {
        return radius < 0 ? throw new ArgumentException("半径不能为负。") : Math.PI * radius * radius;
    }

    /// <summary>
    /// 计算圆的周长
    /// </summary>
    /// <param name="radius">圆的半径（必须为非负数）</param>
    /// <returns>圆的周长</returns>
    /// <exception cref="ArgumentException">当半径为负数时抛出</exception>
    public static double CircleCircumference(double radius)
    {
        return radius < 0 ? throw new ArgumentException("半径不能为负。") : 2 * Math.PI * radius;
    }

    /// <summary>
    /// 计算两点之间的距离
    /// </summary>
    /// <param name="x1">第一个点的X坐标</param>
    /// <param name="y1">第一个点的Y坐标</param>
    /// <param name="x2">第二个点的X坐标</param>
    /// <param name="y2">第二个点的Y坐标</param>
    /// <returns>两点之间的欧几里得距离</returns>
    public static double Distance(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
    }

    /// <summary>
    /// 计算三角形的面积(海伦公式)
    /// </summary>
    /// <param name="a">三角形第一条边长</param>
    /// <param name="b">三角形第二条边长</param>
    /// <param name="c">三角形第三条边长</param>
    /// <returns>三角形的面积</returns>
    /// <exception cref="ArgumentException">当边长无效或不能构成三角形时抛出</exception>
    public static double TriangleArea(double a, double b, double c)
    {
        if (a <= 0 || b <= 0 || c <= 0 || a + b <= c || a + c <= b || b + c <= a)
        {
            throw new ArgumentException("无效的三角形边长。");
        }

        var s = (a + b + c) / 2;
        return Math.Sqrt(s * (s - a) * (s - b) * (s - c));
    }

    #endregion 几何计算

    #region 统计运算

    /// <summary>
    /// 计算平均值
    /// </summary>
    /// <param name="numbers">数值集合</param>
    /// <returns>数值集合的平均值</returns>
    /// <exception cref="ArgumentException">当集合为空时抛出</exception>
    public static double Average(IEnumerable<double>? numbers)
    {
        return numbers is null || !numbers.Any() ? throw new ArgumentException("集合不能为空。") : numbers.Average();
    }

    /// <summary>
    /// 计算中位数
    /// </summary>
    /// <param name="numbers">数值集合</param>
    /// <returns>数值集合的中位数</returns>
    /// <exception cref="ArgumentException">当集合为空时抛出</exception>
    public static double Median(IEnumerable<double> numbers)
    {
        if (numbers is null || !numbers.Any())
        {
            throw new ArgumentException("集合不能为空。");
        }

        var sorted = numbers.OrderBy(n => n).ToArray();
        var count = sorted.Length;

        return count % 2 == 0 ? (sorted[(count / 2) - 1] + sorted[count / 2]) / 2 : sorted[count / 2];
    }

    /// <summary>
    /// 计算方差
    /// </summary>
    /// <param name="numbers">数值集合</param>
    /// <returns>数值集合的方差</returns>
    /// <exception cref="ArgumentException">当集合为空时抛出</exception>
    public static double Variance(IEnumerable<double> numbers)
    {
        if (numbers is null || !numbers.Any())
        {
            throw new ArgumentException("集合不能为空。");
        }

        var mean = Average(numbers);
        return numbers.Average(n => Math.Pow(n - mean, 2));
    }

    /// <summary>
    /// 计算标准差
    /// </summary>
    /// <param name="numbers">数值集合</param>
    /// <returns>数值集合的标准差</returns>
    public static double StandardDeviation(IEnumerable<double> numbers)
    {
        return Math.Sqrt(Variance(numbers));
    }

    #endregion 统计运算

    #region 数值处理

    /// <summary>
    /// 判断是否为素数
    /// </summary>
    /// <param name="number">要判断的整数</param>
    /// <returns>如果是素数返回true，否则返回false</returns>
    public static bool IsPrime(int number)
    {
        if (number < 2)
        {
            return false;
        }

        for (var i = 2; i <= Math.Sqrt(number); i++)
        {
            if (number % i == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 生成斐波那契数列
    /// </summary>
    /// <param name="count">要生成的斐波那契数列项数（必须为正整数，且不超过 <see cref="MaxFibonacciCount"/>）</param>
    /// <returns>斐波那契数列</returns>
    /// <exception cref="ArgumentOutOfRangeException">当项数不为正整数或超出 <see cref="int"/> 可表示范围时抛出</exception>
    /// <remarks>
    /// 原实现不限项数，第 48 项起超出 <see cref="int"/> 范围并静默回绕成负数
    /// （<c>Fibonacci(50)</c> 的第 50 项返回 -811192543）。
    /// 数列越界是调用方的输入问题，应当明确拒绝而不是返回一串错误数字。
    /// </remarks>
    public static List<int> Fibonacci(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxFibonacciCount);

        var sequence = new List<int>
        {
            0, 1
        };
        while (sequence.Count < count)
        {
            sequence.Add(sequence[^1] + sequence[^2]);
        }

        return [.. sequence.Take(count)];
    }

    /// <summary>
    /// 计算阶乘
    /// </summary>
    /// <param name="number">要计算阶乘的非负整数，不超过 <see cref="MaxFactorialInput"/></param>
    /// <returns>阶乘结果</returns>
    /// <exception cref="ArgumentOutOfRangeException">当数字为负数或超出 <see cref="long"/> 可表示范围时抛出</exception>
    /// <remarks>
    /// 原实现不限入参，21! 起超出 <see cref="long"/> 范围并静默回绕成负数
    /// （<c>Factorial(21)</c> 返回 -4249290049419214848，真实值是 51090942171709440000）。
    /// </remarks>
    public static long Factorial(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaxFactorialInput);

        long result = 1;
        for (var i = 2; i <= number; i++)
        {
            result *= i;
        }

        return result;
    }

    #endregion 数值处理
}
