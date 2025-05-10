#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NumberImplementations
// Author:ZhaiFanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/08 17:45:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Numerics;

namespace XiHan.Framework.Utils.Maths;

/// <summary>
/// Int32类型的INumber实现
/// </summary>
public readonly struct Int32Number : INumber<Int32Number>
{
    /// <summary>
    /// 内部存储的整数值
    /// </summary>
    private readonly int _value;

    /// <summary>
    /// 初始化Int32Number
    /// </summary>
    /// <param name="value">整数值</param>
    public Int32Number(int value) => _value = value;

    // 静态属性
    /// <summary>
    /// 零值
    /// </summary>
    public static Int32Number Zero => new(0);

    /// <summary>
    /// 一值
    /// </summary>
    public static Int32Number One => new(1);

    // 类型转换
    /// <summary>
    /// 隐式转换为int
    /// </summary>
    public static implicit operator int(Int32Number number) => number._value;

    /// <summary>
    /// 隐式从int转换
    /// </summary>
    public static implicit operator Int32Number(int value) => new(value);

    /// <summary>
    /// 从其他类型创建Int32Number
    /// </summary>
    public static Int32Number CreateChecked<TOther>(TOther value) => new(Convert.ToInt32(value));

    /// <summary>
    /// 取余运算
    /// </summary>
    public static Int32Number Remainder(Int32Number left, Int32Number right) => left._value % right._value;

    /// <summary>
    /// 取绝对值
    /// </summary>
    public static Int32Number Abs(Int32Number value) => Math.Abs(value._value);

    // 运算符重载
    /// <summary>
    /// 加法运算
    /// </summary>
    public static Int32Number operator +(Int32Number left, Int32Number right) => left._value + right._value;

    /// <summary>
    /// 减法运算
    /// </summary>
    public static Int32Number operator -(Int32Number left, Int32Number right) => left._value - right._value;

    /// <summary>
    /// 乘法运算
    /// </summary>
    public static Int32Number operator *(Int32Number left, Int32Number right) => left._value * right._value;

    /// <summary>
    /// 除法运算
    /// </summary>
    public static Int32Number operator /(Int32Number left, Int32Number right) => left._value / right._value;

    /// <summary>
    /// 大于比较
    /// </summary>
    public static bool operator >(Int32Number left, Int32Number right) => left._value > right._value;

    /// <summary>
    /// 小于比较
    /// </summary>
    public static bool operator <(Int32Number left, Int32Number right) => left._value < right._value;

    // 实现INumber接口
    /// <summary>
    /// 比较大小
    /// </summary>
    public readonly int CompareTo(Int32Number other) => _value.CompareTo(other._value);

    /// <summary>
    /// 判断相等
    /// </summary>
    public readonly bool Equals(Int32Number other) => _value.Equals(other._value);
}

/// <summary>
/// Int64类型的INumber实现
/// </summary>
public readonly struct Int64Number : INumber<Int64Number>
{
    /// <summary>
    /// 内部存储的长整数值
    /// </summary>
    private readonly long _value;

    /// <summary>
    /// 初始化Int64Number
    /// </summary>
    /// <param name="value">长整数值</param>
    public Int64Number(long value) => _value = value;

    // 静态属性
    /// <summary>
    /// 零值
    /// </summary>
    public static Int64Number Zero => new(0);

    /// <summary>
    /// 一值
    /// </summary>
    public static Int64Number One => new(1);

    // 类型转换
    /// <summary>
    /// 隐式转换为long
    /// </summary>
    public static implicit operator long(Int64Number number) => number._value;

    /// <summary>
    /// 隐式从long转换
    /// </summary>
    public static implicit operator Int64Number(long value) => new(value);

    /// <summary>
    /// 从其他类型创建Int64Number
    /// </summary>
    public static Int64Number CreateChecked<TOther>(TOther value) => new(Convert.ToInt64(value));

    /// <summary>
    /// 取余运算
    /// </summary>
    public static Int64Number Remainder(Int64Number left, Int64Number right) => left._value % right._value;

    /// <summary>
    /// 取绝对值
    /// </summary>
    public static Int64Number Abs(Int64Number value) => Math.Abs(value._value);

    // 运算符重载
    /// <summary>
    /// 加法运算
    /// </summary>
    public static Int64Number operator +(Int64Number left, Int64Number right) => left._value + right._value;

    /// <summary>
    /// 减法运算
    /// </summary>
    public static Int64Number operator -(Int64Number left, Int64Number right) => left._value - right._value;

    /// <summary>
    /// 乘法运算
    /// </summary>
    public static Int64Number operator *(Int64Number left, Int64Number right) => left._value * right._value;

    /// <summary>
    /// 除法运算
    /// </summary>
    public static Int64Number operator /(Int64Number left, Int64Number right) => left._value / right._value;

    /// <summary>
    /// 大于比较
    /// </summary>
    public static bool operator >(Int64Number left, Int64Number right) => left._value > right._value;

    /// <summary>
    /// 小于比较
    /// </summary>
    public static bool operator <(Int64Number left, Int64Number right) => left._value < right._value;

    // 实现INumber接口
    /// <summary>
    /// 比较大小
    /// </summary>
    public readonly int CompareTo(Int64Number other) => _value.CompareTo(other._value);

    /// <summary>
    /// 判断相等
    /// </summary>
    public readonly bool Equals(Int64Number other) => _value.Equals(other._value);
}

/// <summary>
/// UInt32类型的INumber实现
/// </summary>
public readonly struct UInt32Number : INumber<UInt32Number>
{
    /// <summary>
    /// 内部存储的无符号整数值
    /// </summary>
    private readonly uint _value;

    /// <summary>
    /// 初始化UInt32Number
    /// </summary>
    /// <param name="value">无符号整数值</param>
    public UInt32Number(uint value) => _value = value;

    // 静态属性
    /// <summary>
    /// 零值
    /// </summary>
    public static UInt32Number Zero => new(0);

    /// <summary>
    /// 一值
    /// </summary>
    public static UInt32Number One => new(1);

    // 类型转换
    /// <summary>
    /// 隐式转换为uint
    /// </summary>
    public static implicit operator uint(UInt32Number number) => number._value;

    /// <summary>
    /// 隐式从uint转换
    /// </summary>
    public static implicit operator UInt32Number(uint value) => new(value);

    /// <summary>
    /// 从其他类型创建UInt32Number
    /// </summary>
    public static UInt32Number CreateChecked<TOther>(TOther value) => new(Convert.ToUInt32(value));

    /// <summary>
    /// 取余运算
    /// </summary>
    public static UInt32Number Remainder(UInt32Number left, UInt32Number right) => left._value % right._value;

    /// <summary>
    /// 取绝对值（无符号类型直接返回原值）
    /// </summary>
    public static UInt32Number Abs(UInt32Number value) => value;

    // 运算符重载
    /// <summary>
    /// 加法运算
    /// </summary>
    public static UInt32Number operator +(UInt32Number left, UInt32Number right) => left._value + right._value;

    /// <summary>
    /// 减法运算
    /// </summary>
    public static UInt32Number operator -(UInt32Number left, UInt32Number right) => left._value - right._value;

    /// <summary>
    /// 乘法运算
    /// </summary>
    public static UInt32Number operator *(UInt32Number left, UInt32Number right) => left._value * right._value;

    /// <summary>
    /// 除法运算
    /// </summary>
    public static UInt32Number operator /(UInt32Number left, UInt32Number right) => left._value / right._value;

    /// <summary>
    /// 大于比较
    /// </summary>
    public static bool operator >(UInt32Number left, UInt32Number right) => left._value > right._value;

    /// <summary>
    /// 小于比较
    /// </summary>
    public static bool operator <(UInt32Number left, UInt32Number right) => left._value < right._value;

    // 实现INumber接口
    /// <summary>
    /// 比较大小
    /// </summary>
    public readonly int CompareTo(UInt32Number other) => _value.CompareTo(other._value);

    /// <summary>
    /// 判断相等
    /// </summary>
    public readonly bool Equals(UInt32Number other) => _value.Equals(other._value);
}

/// <summary>
/// UInt64类型的INumber实现
/// </summary>
public readonly struct UInt64Number : INumber<UInt64Number>
{
    /// <summary>
    /// 内部存储的无符号长整数值
    /// </summary>
    private readonly ulong _value;

    /// <summary>
    /// 初始化UInt64Number
    /// </summary>
    /// <param name="value">无符号长整数值</param>
    public UInt64Number(ulong value) => _value = value;

    // 静态属性
    /// <summary>
    /// 零值
    /// </summary>
    public static UInt64Number Zero => new(0);

    /// <summary>
    /// 一值
    /// </summary>
    public static UInt64Number One => new(1);

    // 类型转换
    /// <summary>
    /// 隐式转换为ulong
    /// </summary>
    public static implicit operator ulong(UInt64Number number) => number._value;

    /// <summary>
    /// 隐式从ulong转换
    /// </summary>
    public static implicit operator UInt64Number(ulong value) => new(value);

    /// <summary>
    /// 从其他类型创建UInt64Number
    /// </summary>
    public static UInt64Number CreateChecked<TOther>(TOther value) => new(Convert.ToUInt64(value));

    /// <summary>
    /// 取余运算
    /// </summary>
    public static UInt64Number Remainder(UInt64Number left, UInt64Number right) => left._value % right._value;

    /// <summary>
    /// 取绝对值（无符号类型直接返回原值）
    /// </summary>
    public static UInt64Number Abs(UInt64Number value) => value;

    // 运算符重载
    /// <summary>
    /// 加法运算
    /// </summary>
    public static UInt64Number operator +(UInt64Number left, UInt64Number right) => left._value + right._value;

    /// <summary>
    /// 减法运算
    /// </summary>
    public static UInt64Number operator -(UInt64Number left, UInt64Number right) => left._value - right._value;

    /// <summary>
    /// 乘法运算
    /// </summary>
    public static UInt64Number operator *(UInt64Number left, UInt64Number right) => left._value * right._value;

    /// <summary>
    /// 除法运算
    /// </summary>
    public static UInt64Number operator /(UInt64Number left, UInt64Number right) => left._value / right._value;

    /// <summary>
    /// 大于比较
    /// </summary>
    public static bool operator >(UInt64Number left, UInt64Number right) => left._value > right._value;

    /// <summary>
    /// 小于比较
    /// </summary>
    public static bool operator <(UInt64Number left, UInt64Number right) => left._value < right._value;

    // 实现INumber接口
    /// <summary>
    /// 比较大小
    /// </summary>
    public readonly int CompareTo(UInt64Number other) => _value.CompareTo(other._value);

    /// <summary>
    /// 判断相等
    /// </summary>
    public readonly bool Equals(UInt64Number other) => _value.Equals(other._value);
}

/// <summary>
/// BigInteger类型的INumber实现
/// </summary>
public readonly struct BigIntegerNumber : INumber<BigIntegerNumber>
{
    /// <summary>
    /// 内部存储的大整数值
    /// </summary>
    private readonly BigInteger _value;

    /// <summary>
    /// 初始化BigIntegerNumber
    /// </summary>
    /// <param name="value">大整数值</param>
    public BigIntegerNumber(BigInteger value) => _value = value;

    // 静态属性
    /// <summary>
    /// 零值
    /// </summary>
    public static BigIntegerNumber Zero => new(BigInteger.Zero);

    /// <summary>
    /// 一值
    /// </summary>
    public static BigIntegerNumber One => new(BigInteger.One);

    // 类型转换
    /// <summary>
    /// 隐式转换为BigInteger
    /// </summary>
    public static implicit operator BigInteger(BigIntegerNumber number) => number._value;

    /// <summary>
    /// 隐式从BigInteger转换
    /// </summary>
    public static implicit operator BigIntegerNumber(BigInteger value) => new(value);

    /// <summary>
    /// 从其他类型创建BigIntegerNumber
    /// </summary>
    public static BigIntegerNumber CreateChecked<TOther>(TOther value)
    {
        return value is BigInteger bi ? new(bi) : (BigIntegerNumber)new(new BigInteger(Convert.ToInt64(value)));
    }

    /// <summary>
    /// 取余运算
    /// </summary>
    public static BigIntegerNumber Remainder(BigIntegerNumber left, BigIntegerNumber right) => left._value % right._value;

    /// <summary>
    /// 取绝对值
    /// </summary>
    public static BigIntegerNumber Abs(BigIntegerNumber value) => BigInteger.Abs(value._value);

    // 运算符重载
    /// <summary>
    /// 加法运算
    /// </summary>
    public static BigIntegerNumber operator +(BigIntegerNumber left, BigIntegerNumber right) => left._value + right._value;

    /// <summary>
    /// 减法运算
    /// </summary>
    public static BigIntegerNumber operator -(BigIntegerNumber left, BigIntegerNumber right) => left._value - right._value;

    /// <summary>
    /// 乘法运算
    /// </summary>
    public static BigIntegerNumber operator *(BigIntegerNumber left, BigIntegerNumber right) => left._value * right._value;

    /// <summary>
    /// 除法运算
    /// </summary>
    public static BigIntegerNumber operator /(BigIntegerNumber left, BigIntegerNumber right) => left._value / right._value;

    /// <summary>
    /// 大于比较
    /// </summary>
    public static bool operator >(BigIntegerNumber left, BigIntegerNumber right) => left._value > right._value;

    /// <summary>
    /// 小于比较
    /// </summary>
    public static bool operator <(BigIntegerNumber left, BigIntegerNumber right) => left._value < right._value;

    // 实现INumber接口
    /// <summary>
    /// 比较大小
    /// </summary>
    public readonly int CompareTo(BigIntegerNumber other) => _value.CompareTo(other._value);

    /// <summary>
    /// 判断相等
    /// </summary>
    public readonly bool Equals(BigIntegerNumber other) => _value.Equals(other._value);
}
