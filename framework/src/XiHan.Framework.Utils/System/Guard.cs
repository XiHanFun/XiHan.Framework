#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Guard
// Guid:464fa6b8-81f7-4751-8eae-b4d9a9ffe0c9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 6:52:48
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.System;

/// <summary>
/// 数据检测警卫
/// </summary>
[DebuggerStepThrough]
public static class Guard
{
    /// <summary>
    /// 数据不为空判断
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static T NotNull<T>([NotNull] T? value, string parameterName)
    {
        return value is null ? throw new ArgumentNullException(parameterName) : value;
    }

    /// <summary>
    /// 数据不为空判断
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="parameterName"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static T NotNull<T>([NotNull] T? value, string parameterName, string message)
    {
        return value is null ? throw new ArgumentNullException(parameterName, message) : value;
    }

    /// <summary>
    /// 数据不为空判断
    /// </summary>
    /// <param name="value"></param>
    /// <param name="parameterName"></param>
    /// <param name="maxLength"></param>
    /// <param name="minLength"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string NotNull([NotNull] string? value, string parameterName, int maxLength = int.MaxValue, int minLength = 0)
    {
        return value is null
            ? throw new ArgumentException($"{parameterName} 不能为空", parameterName)
            : value.Length > maxLength
                ? throw new ArgumentException($"{parameterName} 长度必须等于或小于 {maxLength}", parameterName)
                : minLength > 0 && value.Length < minLength
                    ? throw new ArgumentException($"{parameterName} 长度必须等于或大于 {minLength}", parameterName)
                    : value;
    }

    /// <summary>
    /// 数据不为无效或空白判断
    /// </summary>
    /// <param name="value"></param>
    /// <param name="parameterName"></param>
    /// <param name="maxLength"></param>
    /// <param name="minLength"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string NotNullOrWhiteSpace([NotNull] string? value, string parameterName, int maxLength = int.MaxValue, int minLength = 0)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{parameterName} 不能为无效、空值或空白", parameterName)
            : value.Length > maxLength
                ? throw new ArgumentException($"{parameterName} 长度必须等于或小于 {maxLength}", parameterName)
                : minLength > 0 && value.Length < minLength
                    ? throw new ArgumentException($"{parameterName} 长度必须等于或大于 {minLength}", parameterName)
                    : value;
    }

    /// <summary>
    /// 数据不为无效或空值判断
    /// </summary>
    /// <param name="value"></param>
    /// <param name="parameterName"></param>
    /// <param name="maxLength"></param>
    /// <param name="minLength"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string NotNullOrEmpty([NotNull] string? value, string parameterName, int maxLength = int.MaxValue, int minLength = 0)
    {
        return string.IsNullOrEmpty(value)
            ? throw new ArgumentException($"{parameterName} 不能为无效、空值", parameterName)
            : value.Length > maxLength
                ? throw new ArgumentException($"{parameterName} 长度必须等于或小于 {maxLength}", parameterName)
                : minLength > 0 && value.Length < minLength
                    ? throw new ArgumentException($"{parameterName} 长度必须等于或大于 {minLength}", parameterName)
                    : value;
    }

    /// <summary>
    /// 验证集合是否不为无效或空值
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static ICollection<T> NotNullOrEmpty<T>([NotNull] ICollection<T>? value, string parameterName)
    {
        return value == null || value.Count <= 0
            ? throw new ArgumentException($"{parameterName} 不能为无效、空值", parameterName)
            : value;
    }

    /// <summary>
    /// 验证值是否不为默认值或空值
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static T NotDefaultOrNull<T>([NotNull] T? value, string parameterName)
       where T : struct
    {
        return value == null
            ? throw new ArgumentException($"{parameterName} 为空", parameterName)
            : value.Value.Equals(default(T))
                ? throw new ArgumentException($"{parameterName} 是默认值", parameterName)
                : value.Value;
    }

    /// <summary>
    /// 验证类型是否可分配给指定的基类型
    /// </summary>
    /// <typeparam name="TBaseType"></typeparam>
    /// <param name="type"></param>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static Type AssignableTo<TBaseType>(Type type, string parameterName)
    {
        NotNull(type, parameterName);

        return type.IsAssignableTo<TBaseType>()
            ? throw new ArgumentException($"{parameterName} (类型 {type.AssemblyQualifiedName}) 应该可以分配给 {typeof(TBaseType).GetFullNameWithAssemblyName()}")
            : type;
    }

    /// <summary>
    /// 验证字符串的长度是否满足指定的最大长度和最小长度要求
    /// </summary>
    /// <param name="value"></param>
    /// <param name="parameterName"></param>
    /// <param name="maxLength"></param>
    /// <param name="minLength"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string? Length(string? value, string parameterName, int maxLength, int minLength = 0)
    {
        return minLength <= 0
            ? value is not null && value.Length > maxLength
                ? throw new ArgumentException($"{parameterName} 长度必须等于或小于 {maxLength}", parameterName)
                : value
            : string.IsNullOrEmpty(value)
                ? throw new ArgumentException($"{parameterName} 不能为无效、空值", parameterName)
                : value.Length < minLength
                    ? throw new ArgumentException($"{parameterName} 长度必须等于或大于 {minLength}", parameterName)
                    : value.Length > maxLength
                        ? throw new ArgumentException($"{parameterName} 长度必须等于或小于 {maxLength}", parameterName)
                        : value;
    }

    /// <summary>
    /// 验证 Int16 参数是否在指定范围内
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="parameterName">参数名(用于异常信息)</param>
    /// <param name="minimumValue">最小值(含)</param>
    /// <param name="maximumValue">最大值(含，默认为 <see cref="short.MaxValue"/>)</param>
    /// <returns>如果验证通过，返回原始值</returns>
    /// <exception cref="ArgumentException">当值超出指定范围时抛出</exception>
    public static short Range(short value, string parameterName, short minimumValue, short maximumValue = short.MaxValue)
    {
        return value < minimumValue || value > maximumValue
            ? throw new ArgumentException($"{parameterName} 超出范围：最小值 = {minimumValue}，最大值 = {maximumValue}")
            : value;
    }

    /// <summary>
    /// 验证 Int32 参数是否在指定范围内
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="parameterName">参数名(用于异常信息)</param>
    /// <param name="minimumValue">最小值(含)</param>
    /// <param name="maximumValue">最大值(含，默认为 <see cref="int.MaxValue"/>)</param>
    /// <returns>如果验证通过，返回原始值</returns>
    /// <exception cref="ArgumentException">当值超出指定范围时抛出</exception>
    public static int Range(int value, string parameterName, int minimumValue, int maximumValue = int.MaxValue)
    {
        return value < minimumValue || value > maximumValue
            ? throw new ArgumentException($"{parameterName} 超出范围：最小值 = {minimumValue}，最大值 = {maximumValue}")
            : value;
    }

    /// <summary>
    /// 验证 Int64 参数是否在指定范围内
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="parameterName">参数名(用于异常信息)</param>
    /// <param name="minimumValue">最小值(含)</param>
    /// <param name="maximumValue">最大值(含，默认为 <see cref="long.MaxValue"/>)</param>
    /// <returns>如果验证通过，返回原始值</returns>
    /// <exception cref="ArgumentException">当值超出指定范围时抛出</exception>
    public static long Range(long value, string parameterName, long minimumValue, long maximumValue = long.MaxValue)
    {
        return value < minimumValue || value > maximumValue
            ? throw new ArgumentException($"{parameterName} 超出范围：最小值 = {minimumValue}，最大值 = {maximumValue}")
            : value;
    }

    /// <summary>
    /// 验证 float 参数是否在指定范围内
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="parameterName">参数名(用于异常信息)</param>
    /// <param name="minimumValue">最小值(含)</param>
    /// <param name="maximumValue">最大值(含，默认为 <see cref="float.MaxValue"/>)</param>
    /// <returns>如果验证通过，返回原始值</returns>
    /// <exception cref="ArgumentException">当值超出指定范围时抛出</exception>
    public static float Range(float value, string parameterName, float minimumValue, float maximumValue = float.MaxValue)
    {
        return value < minimumValue || value > maximumValue
            ? throw new ArgumentException($"{parameterName} 超出范围：最小值 = {minimumValue}，最大值 = {maximumValue}")
            : value;
    }

    /// <summary>
    /// 验证 double 参数是否在指定范围内
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="parameterName">参数名(用于异常信息)</param>
    /// <param name="minimumValue">最小值(含)</param>
    /// <param name="maximumValue">最大值(含，默认为 <see cref="double.MaxValue"/>)</param>
    /// <returns>如果验证通过，返回原始值</returns>
    /// <exception cref="ArgumentException">当值超出指定范围时抛出</exception>
    public static double Range(double value, string parameterName, double minimumValue, double maximumValue = double.MaxValue)
    {
        return value < minimumValue || value > maximumValue
            ? throw new ArgumentException($"{parameterName} 超出范围：最小值 = {minimumValue}，最大值 = {maximumValue}")
            : value;
    }

    /// <summary>
    /// 验证 decimal 参数是否在指定范围内
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="parameterName">参数名(用于异常信息)</param>
    /// <param name="minimumValue">最小值(含)</param>
    /// <param name="maximumValue">最大值(含，默认为 <see cref="decimal.MaxValue"/>)</param>
    /// <returns>如果验证通过，返回原始值</returns>
    /// <exception cref="ArgumentException">当值超出指定范围时抛出</exception>
    public static decimal Range(decimal value, string parameterName, decimal minimumValue, decimal maximumValue = decimal.MaxValue)
    {
        return value < minimumValue || value > maximumValue
            ? throw new ArgumentException($"{parameterName} 超出范围：最小值 = {minimumValue}，最大值 = {maximumValue}")
            : value;
    }

    /// <summary>
    /// 验证一个值是否在指定范围之内(包含最小值和最大值)
    /// </summary>
    /// <typeparam name="T">要比较的类型，必须实现 IComparable&lt;T&gt;</typeparam>
    /// <param name="value">要验证的值</param>
    /// <param name="parameterName">参数名(用于异常信息)</param>
    /// <param name="minimumValue">允许的最小值(含)</param>
    /// <param name="maximumValue">允许的最大值(含)</param>
    /// <returns>验证通过时返回原始值</returns>
    /// <exception cref="ArgumentException">当值超出范围时抛出</exception>
    public static T Range<T>(T value, string parameterName, T minimumValue, T maximumValue)
        where T : IComparable<T>
    {
        return value.CompareTo(minimumValue) < 0 || value.CompareTo(maximumValue) > 0
            ? throw new ArgumentException($"{parameterName} 超出范围：最小值 = {minimumValue}，最大值 = {maximumValue}")
            : value;
    }
}
