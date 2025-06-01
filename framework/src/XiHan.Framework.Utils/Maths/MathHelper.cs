#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MathHelper
// Guid:ab4d0b35-f577-4c7c-ba03-8b501fe44a1e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/9 6:04:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Maths;

/// <summary>
/// 数学运算辅助类
/// </summary>
public static class MathHelper
{
    #region 基本运算

    /// <summary>
    /// 返回两个数中的较大值
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static double Max(double a, double b)
    {
        return Math.Max(a, b);
    }

    /// <summary>
    /// 返回两个数中的较小值
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static double Min(double a, double b)
    {
        return Math.Min(a, b);
    }

    /// <summary>
    /// 计算平方根
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static double Sqrt(double number)
    {
        return number < 0 ? throw new ArgumentException("数字不能为负。") : Math.Sqrt(number);
    }

    /// <summary>
    /// 计算幂
    /// </summary>
    /// <param name="baseNumber"></param>
    /// <param name="exponent"></param>
    /// <returns></returns>
    public static double Pow(double baseNumber, double exponent)
    {
        return Math.Pow(baseNumber, exponent);
    }

    /// <summary>
    /// 计算绝对值
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
    public static double Abs(double number)
    {
        return Math.Abs(number);
    }

    /// <summary>
    /// 三角函数：正弦
    /// </summary>
    /// <param name="angleInRadians"></param>
    /// <returns></returns>
    public static double Sin(double angleInRadians)
    {
        return Math.Sin(angleInRadians);
    }

    /// <summary>
    /// 三角函数：余弦
    /// </summary>
    /// <param name="angleInRadians"></param>
    /// <returns></returns>
    public static double Cos(double angleInRadians)
    {
        return Math.Cos(angleInRadians);
    }

    /// <summary>
    /// 三角函数：正切
    /// </summary>
    /// <param name="angleInRadians"></param>
    /// <returns></returns>
    public static double Tan(double angleInRadians)
    {
        return Math.Tan(angleInRadians);
    }

    #endregion 基本运算

    #region 几何计算

    /// <summary>
    /// 计算圆的面积
    /// </summary>
    /// <param name="radius"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static double CircleArea(double radius)
    {
        return radius < 0 ? throw new ArgumentException("半径不能为负。") : Math.PI * radius * radius;
    }

    /// <summary>
    /// 计算圆的周长
    /// </summary>
    /// <param name="radius"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static double CircleCircumference(double radius)
    {
        return radius < 0 ? throw new ArgumentException("半径不能为负。") : 2 * Math.PI * radius;
    }

    /// <summary>
    /// 计算两点之间的距离
    /// </summary>
    /// <param name="x1"></param>
    /// <param name="y1"></param>
    /// <param name="x2"></param>
    /// <param name="y2"></param>
    /// <returns></returns>
    public static double Distance(double x1, double y1, double x2, double y2)
    {
        return Sqrt(Pow(x2 - x1, 2) + Pow(y2 - y1, 2));
    }

    /// <summary>
    /// 计算三角形的面积(海伦公式)
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="c"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static double TriangleArea(double a, double b, double c)
    {
        if (a <= 0 || b <= 0 || c <= 0 || a + b <= c || a + c <= b || b + c <= a)
        {
            throw new ArgumentException("无效的三角形边长。");
        }

        var s = (a + b + c) / 2;
        return Sqrt(s * (s - a) * (s - b) * (s - c));
    }

    #endregion 几何计算

    #region 统计运算

    /// <summary>
    /// 计算平均值
    /// </summary>
    /// <param name="numbers"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static double Average(IEnumerable<double>? numbers)
    {
        return numbers is null || !numbers.Any() ? throw new ArgumentException("集合不能为空。") : numbers.Average();
    }

    /// <summary>
    /// 计算中位数
    /// </summary>
    /// <param name="numbers"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
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
    /// <param name="numbers"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static double Variance(IEnumerable<double> numbers)
    {
        if (numbers is null || !numbers.Any())
        {
            throw new ArgumentException("集合不能为空。");
        }

        var mean = Average(numbers);
        return numbers.Average(n => Pow(n - mean, 2));
    }

    /// <summary>
    /// 计算标准差
    /// </summary>
    /// <param name="numbers"></param>
    /// <returns></returns>
    public static double StandardDeviation(IEnumerable<double> numbers)
    {
        return Sqrt(Variance(numbers));
    }

    #endregion 统计运算

    #region 数值处理

    /// <summary>
    /// 判断是否为素数
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
    public static bool IsPrime(int number)
    {
        if (number < 2)
        {
            return false;
        }

        for (var i = 2; i <= Sqrt(number); i++)
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
    /// <param name="count"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static List<int> Fibonacci(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("数量必须为正整数。");
        }

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
    /// <param name="number"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static long Factorial(int number)
    {
        if (number < 0)
        {
            throw new ArgumentException("数字不能为负。");
        }

        long result = 1;
        for (var i = 2; i <= number; i++)
        {
            result *= i;
        }

        return result;
    }

    #endregion 数值处理
}
