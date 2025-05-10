#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:INumber
// Author:ZhaiFanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/08 17:44:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Maths;

/// <summary>
/// 数字接口，兼容低版本.NET的数字运算
/// </summary>
/// <typeparam name="TSelf">数字类型自身</typeparam>
public interface INumber<TSelf> where TSelf : INumber<TSelf>
{
    /// <summary>
    /// 零值
    /// </summary>
    static abstract TSelf Zero { get; }

    /// <summary>
    /// 一值
    /// </summary>
    static abstract TSelf One { get; }

    /// <summary>
    /// 从一个对象创建数字类型实例
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <returns>转换结果</returns>
    static abstract TSelf CreateChecked<TOther>(TOther value);

    /// <summary>
    /// 取余数
    /// </summary>
    /// <param name="left">被除数</param>
    /// <param name="right">除数</param>
    /// <returns>余数</returns>
    static abstract TSelf Remainder(TSelf left, TSelf right);

    /// <summary>
    /// 取绝对值
    /// </summary>
    /// <param name="value">输入值</param>
    /// <returns>绝对值</returns>
    static abstract TSelf Abs(TSelf value);

    /// <summary>
    /// 加法
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>加法结果</returns>
    static abstract TSelf operator +(TSelf left, TSelf right);

    /// <summary>
    /// 减法
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>减法结果</returns>
    static abstract TSelf operator -(TSelf left, TSelf right);

    /// <summary>
    /// 乘法
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>乘法结果</returns>
    static abstract TSelf operator *(TSelf left, TSelf right);

    /// <summary>
    /// 除法
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>除法结果</returns>
    static abstract TSelf operator /(TSelf left, TSelf right);

    /// <summary>
    /// 大于
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>大于结果</returns>
    static abstract bool operator >(TSelf left, TSelf right);

    /// <summary>
    /// 小于
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>小于结果</returns>
    static abstract bool operator <(TSelf left, TSelf right);

    /// <summary>
    /// 与另一个数值进行比较
    /// </summary>
    /// <param name="other">要比较的数值</param>
    /// <returns>比较结果</returns>
    int CompareTo(TSelf other);

    /// <summary>
    /// 判断是否相等
    /// </summary>
    /// <param name="other">要比较的数值</param>
    /// <returns>是否相等</returns>
    bool Equals(TSelf other);
}
