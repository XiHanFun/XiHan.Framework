#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NumberExtensions
// Guid:4d393136-f9ab-4c92-9464-22cd85f09c75
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/10 14:55:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Numerics;

namespace XiHan.Framework.Utils.Maths;

/// <summary>
/// INumber 扩展方法
/// </summary>
public static class NumberExtensions
{
    #region 基本数学运算

    /// <summary>
    /// 确保数值为非负数，如果为负数则返回零
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <returns>非负数值</returns>
    /// <example>
    /// <code>
    /// int negative = -5;
    /// int result = negative.EnsureNonNegative(); // 返回 0
    ///
    /// int positive = 10;
    /// int result2 = positive.EnsureNonNegative(); // 返回 10
    /// </code>
    /// </example>
    public static T EnsureNonNegative<T>(this T value) where T : INumber<T>
    {
        return value < T.Zero ? T.Zero : value;
    }

    /// <summary>
    /// 确保数值为正数，如果为零或负数则返回一
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <returns>正数值</returns>
    public static T EnsurePositive<T>(this T value) where T : INumber<T>
    {
        return value <= T.Zero ? T.One : value;
    }

    /// <summary>
    /// 计算 value 对 modulus 的非负余数（数学意义上的模运算）
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">被除数</param>
    /// <param name="modulus">除数</param>
    /// <returns>非负余数</returns>
    /// <exception cref="ArgumentException">当除数为零时抛出异常</exception>
    /// <example>
    /// <code>
    /// int result1 = (-7).Mod(3); // 返回 2 (而不是 -1)
    /// int result2 = 7.Mod(3);    // 返回 1
    /// </code>
    /// </example>
    public static T Mod<T>(this T value, T modulus) where T : INumber<T>
    {
        if (modulus == T.Zero)
        {
            throw new ArgumentException("除数不能为零", nameof(modulus));
        }

        var remainder = value % modulus;
        return remainder < T.Zero ? remainder + modulus : remainder;
    }

    /// <summary>
    /// 执行整数除法并向下取整（Floor Division）
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">被除数</param>
    /// <param name="divisor">除数</param>
    /// <returns>向下取整的商</returns>
    /// <exception cref="ArgumentException">当除数为零时抛出异常</exception>
    public static T FloorDiv<T>(this T value, T divisor) where T : INumber<T>
    {
        if (divisor == T.Zero)
        {
            throw new ArgumentException("除数不能为零", nameof(divisor));
        }

        var result = value / divisor;
        if (value < T.Zero != divisor < T.Zero && result * divisor != value)
        {
            result -= T.One;
        }
        return result;
    }

    /// <summary>
    /// 计算 value 和 multiplier 的乘积
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">被乘数</param>
    /// <param name="multiplier">乘数</param>
    /// <returns>乘积</returns>
    public static T Multiply<T>(this T value, T multiplier) where T : INumber<T>
    {
        return value * multiplier;
    }

    /// <summary>
    /// 计算 value 和 addend 的和
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">被加数</param>
    /// <param name="addend">加数</param>
    /// <returns>和</returns>
    public static T Add<T>(this T value, T addend) where T : INumber<T>
    {
        return value + addend;
    }

    /// <summary>
    /// 计算 value 和 subtrahend 的差
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">被减数</param>
    /// <param name="subtrahend">减数</param>
    /// <returns>差</returns>
    public static T Subtract<T>(this T value, T subtrahend) where T : INumber<T>
    {
        return value - subtrahend;
    }

    /// <summary>
    /// 计算数值的绝对值
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">输入数值</param>
    /// <returns>绝对值</returns>
    public static T Abs<T>(this T value) where T : INumber<T>
    {
        return value < T.Zero ? -value : value;
    }

    /// <summary>
    /// 计算数值的相反数
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">输入数值</param>
    /// <returns>相反数</returns>
    public static T Negate<T>(this T value) where T : INumber<T>
    {
        return -value;
    }

    #endregion

    #region 数值范围和边界检查

    /// <summary>
    /// 将数值限制在指定范围内
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要限制的数值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>限制在范围内的数值</returns>
    /// <exception cref="ArgumentException">当最小值大于最大值时抛出异常</exception>
    public static T Clamp<T>(this T value, T min, T max) where T : INumber<T>
    {
        return min > max
            ? throw new ArgumentException("最小值不能大于最大值", nameof(min))
            : value < min
            ? min
            : value > max ? max : value;
    }

    /// <summary>
    /// 检查数值是否在指定范围内（包含边界）
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>如果在范围内返回true，否则返回false</returns>
    public static bool IsInRange<T>(this T value, T min, T max) where T : INumber<T>
    {
        return value >= min && value <= max;
    }

    /// <summary>
    /// 检查数值是否在指定范围内（不包含边界）
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>如果在范围内返回true，否则返回false</returns>
    public static bool IsBetween<T>(this T value, T min, T max) where T : INumber<T>
    {
        return value > min && value < max;
    }

    /// <summary>
    /// 检查数值是否为零
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <returns>如果为零返回true，否则返回false</returns>
    public static bool IsZero<T>(this T value) where T : INumber<T>
    {
        return value == T.Zero;
    }

    /// <summary>
    /// 检查数值是否为正数
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <returns>如果为正数返回true，否则返回false</returns>
    public static bool IsPositive<T>(this T value) where T : INumber<T>
    {
        return value > T.Zero;
    }

    /// <summary>
    /// 检查数值是否为负数
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <returns>如果为负数返回true，否则返回false</returns>
    public static bool IsNegative<T>(this T value) where T : INumber<T>
    {
        return value < T.Zero;
    }

    /// <summary>
    /// 检查数值是否为非负数（大于等于零）
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <returns>如果为非负数返回true，否则返回false</returns>
    public static bool IsNonNegative<T>(this T value) where T : INumber<T>
    {
        return value >= T.Zero;
    }

    /// <summary>
    /// 检查数值是否为非正数（小于等于零）
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <returns>如果为非正数返回true，否则返回false</returns>
    public static bool IsNonPositive<T>(this T value) where T : INumber<T>
    {
        return value <= T.Zero;
    }

    #endregion

    #region 数值比较和判断

    /// <summary>
    /// 返回两个数中的较大值
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">第一个数值</param>
    /// <param name="other">第二个数值</param>
    /// <returns>较大的数值</returns>
    public static T Max<T>(this T value, T other) where T : INumber<T>
    {
        return value > other ? value : other;
    }

    /// <summary>
    /// 返回两个数中的较小值
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">第一个数值</param>
    /// <param name="other">第二个数值</param>
    /// <returns>较小的数值</returns>
    public static T Min<T>(this T value, T other) where T : INumber<T>
    {
        return value < other ? value : other;
    }

    /// <summary>
    /// 比较两个数值的大小关系
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">第一个数值</param>
    /// <param name="other">第二个数值</param>
    /// <returns>-1表示小于，0表示等于，1表示大于</returns>
    public static int CompareTo<T>(this T value, T other) where T : INumber<T>
    {
        return value < other ? -1 : value > other ? 1 : 0;
    }

    #endregion

    #region 高级数学运算

    /// <summary>
    /// 计算数值的平方
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">输入数值</param>
    /// <returns>平方值</returns>
    public static T Square<T>(this T value) where T : INumber<T>
    {
        return value * value;
    }

    /// <summary>
    /// 计算数值的立方
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">输入数值</param>
    /// <returns>立方值</returns>
    public static T Cube<T>(this T value) where T : INumber<T>
    {
        return value * value * value;
    }

    /// <summary>
    /// 计算整数的阶乘
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">输入数值</param>
    /// <returns>阶乘值</returns>
    /// <exception cref="ArgumentException">当输入为负数时抛出异常</exception>
    public static T Factorial<T>(this T value) where T : INumber<T>
    {
        if (value < T.Zero)
        {
            throw new ArgumentException("阶乘的输入必须为非负数", nameof(value));
        }

        if (value == T.Zero || value == T.One)
        {
            return T.One;
        }

        var result = T.One;
        var current = value;
        while (current > T.One)
        {
            result *= current;
            current -= T.One;
        }
        return result;
    }

    /// <summary>
    /// 计算两个数的最大公约数（GCD）
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="a">第一个数</param>
    /// <param name="b">第二个数</param>
    /// <returns>最大公约数</returns>
    public static T Gcd<T>(this T a, T b) where T : INumber<T>
    {
        a = a.Abs();
        b = b.Abs();

        while (b != T.Zero)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    /// <summary>
    /// 计算两个数的最小公倍数（LCM）
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="a">第一个数</param>
    /// <param name="b">第二个数</param>
    /// <returns>最小公倍数</returns>
    public static T Lcm<T>(this T a, T b) where T : INumber<T>
    {
        return a == T.Zero || b == T.Zero ? T.Zero : (a * b).Abs() / a.Gcd(b);
    }

    /// <summary>
    /// 检查数字是否为偶数
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <returns>如果为偶数返回true，否则返回false</returns>
    public static bool IsEven<T>(this T value) where T : INumber<T>
    {
        return value % (T.One + T.One) == T.Zero;
    }

    /// <summary>
    /// 检查数字是否为奇数
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="value">要检查的数值</param>
    /// <returns>如果为奇数返回true，否则返回false</returns>
    public static bool IsOdd<T>(this T value) where T : INumber<T>
    {
        return value % (T.One + T.One) != T.Zero;
    }

    #endregion

    #region 数值转换和格式化

    /// <summary>
    /// 将数值转换为指定类型
    /// </summary>
    /// <typeparam name="TSource">源数字类型</typeparam>
    /// <typeparam name="TTarget">目标数字类型</typeparam>
    /// <param name="value">要转换的数值</param>
    /// <returns>转换后的数值</returns>
    public static TTarget ConvertTo<TSource, TTarget>(this TSource value)
        where TSource : INumber<TSource>
        where TTarget : INumber<TTarget>
    {
        return TTarget.CreateChecked(value);
    }

    /// <summary>
    /// 尝试将数值转换为指定类型
    /// </summary>
    /// <typeparam name="TSource">源数字类型</typeparam>
    /// <typeparam name="TTarget">目标数字类型</typeparam>
    /// <param name="value">要转换的数值</param>
    /// <param name="result">转换结果</param>
    /// <returns>如果转换成功返回true，否则返回false</returns>
    public static bool TryConvertTo<TSource, TTarget>(this TSource value, out TTarget result)
        where TSource : INumber<TSource>
        where TTarget : INumber<TTarget>
    {
        try
        {
            result = TTarget.CreateChecked(value);
            return true;
        }
        catch
        {
            result = TTarget.Zero;
            return false;
        }
    }

    #endregion

    #region 数值安全操作

    /// <summary>
    /// 安全加法，检查溢出
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>安全加法结果</returns>
    /// <exception cref="OverflowException">当发生溢出时抛出异常</exception>
    public static T SafeAdd<T>(this T left, T right) where T : INumber<T>
    {
        return checked(left + right);
    }

    /// <summary>
    /// 安全减法，检查溢出
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>安全减法结果</returns>
    /// <exception cref="OverflowException">当发生溢出时抛出异常</exception>
    public static T SafeSubtract<T>(this T left, T right) where T : INumber<T>
    {
        return checked(left - right);
    }

    /// <summary>
    /// 安全乘法，检查溢出
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>安全乘法结果</returns>
    /// <exception cref="OverflowException">当发生溢出时抛出异常</exception>
    public static T SafeMultiply<T>(this T left, T right) where T : INumber<T>
    {
        return checked(left * right);
    }

    /// <summary>
    /// 尝试安全加法
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <param name="result">运算结果</param>
    /// <returns>如果运算成功返回true，否则返回false</returns>
    public static bool TrySafeAdd<T>(this T left, T right, out T result) where T : INumber<T>
    {
        try
        {
            result = checked(left + right);
            return true;
        }
        catch (OverflowException)
        {
            result = T.Zero;
            return false;
        }
    }

    /// <summary>
    /// 尝试安全减法
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <param name="result">运算结果</param>
    /// <returns>如果运算成功返回true，否则返回false</returns>
    public static bool TrySafeSubtract<T>(this T left, T right, out T result) where T : INumber<T>
    {
        try
        {
            result = checked(left - right);
            return true;
        }
        catch (OverflowException)
        {
            result = T.Zero;
            return false;
        }
    }

    /// <summary>
    /// 尝试安全乘法
    /// </summary>
    /// <typeparam name="T">数字类型，必须实现INumber接口</typeparam>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <param name="result">运算结果</param>
    /// <returns>如果运算成功返回true，否则返回false</returns>
    public static bool TrySafeMultiply<T>(this T left, T right, out T result) where T : INumber<T>
    {
        try
        {
            result = checked(left * right);
            return true;
        }
        catch (OverflowException)
        {
            result = T.Zero;
            return false;
        }
    }

    #endregion
}
