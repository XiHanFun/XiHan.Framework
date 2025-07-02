#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NumberExtensions
// Guid:4d393136-f9ab-4c92-9464-22cd85f09c75
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/10 14:55:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Numerics;

namespace XiHan.Framework.Utils.Maths;

/// <summary>
/// 扩展INumber接口实现基本数学运算
/// </summary>
public static class NumberExtensions
{
    /// <summary>
    /// 计算 value 对 modulus 的非负余数
    /// </summary>
    /// <typeparam name="T">数字类型</typeparam>
    /// <param name="value">被除数</param>
    /// <param name="modulus">除数</param>
    /// <returns>非负余数</returns>
    public static T Mod<T>(this T value, T modulus) where T : INumber<T>
    {
        var remainder = value % modulus;
        return remainder < T.Zero ? remainder + modulus : remainder;
    }

    /// <summary>
    /// 执行整数除法并向下取整
    /// </summary>
    /// <typeparam name="T">数字类型</typeparam>
    /// <param name="value">被除数</param>
    /// <param name="divisor">除数</param>
    /// <returns>向下取整的商</returns>
    public static T Div<T>(this T value, T divisor) where T : INumber<T>
    {
        var result = value / divisor;
        if (result * divisor > value)
        {
            result -= T.One;
        }
        return result;
    }

    /// <summary>
    /// 计算 value 和 multiplier 的乘积
    /// </summary>
    /// <typeparam name="T">数字类型</typeparam>
    /// <param name="value">被乘数</param>
    /// <param name="multiplier">乘数</param>
    /// <returns>乘积</returns>
    public static T Mul<T>(this T value, T multiplier) where T : INumber<T>
    {
        return value * multiplier;
    }

    /// <summary>
    /// 计算 value 和 addend 的和
    /// </summary>
    /// <typeparam name="T">数字类型</typeparam>
    /// <param name="value">被加数</param>
    /// <param name="addend">加数</param>
    /// <returns>和</returns>
    public static T Add<T>(this T value, T addend) where T : INumber<T>
    {
        return value + addend;
    }
}
