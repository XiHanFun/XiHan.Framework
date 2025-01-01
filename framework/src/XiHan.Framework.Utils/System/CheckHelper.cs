#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CheckHelper
// Guid:464fa6b8-81f7-4751-8eae-b4d9a9ffe0c9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 6:52:48
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;

namespace XiHan.Framework.Utils.System;

/// <summary>
/// 数据检测帮助类
/// </summary>
[DebuggerStepThrough]
public static class CheckHelper
{
    /// <summary>
    /// 数据不为空判断
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static T NotNull<T>(T? value, string parameterName)
    {
        return value == null ? throw new ArgumentNullException(parameterName) : value;
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
    public static T NotNull<T>(T? value, string parameterName, string message)
    {
        return value == null ? throw new ArgumentNullException(parameterName, message) : value;
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
    public static string NotNull(string? value, string parameterName, int maxLength = int.MaxValue, int minLength = 0)
    {
        return value == null
            ? throw new ArgumentException($"{parameterName}不能为空!", parameterName)
            : value.Length > maxLength
                ? throw new ArgumentException($"{parameterName}长度必须等于或小于{maxLength}!", parameterName)
                : minLength > 0 && value.Length < minLength
                    ? throw new ArgumentException($"{parameterName}长度必须等于或大于{minLength}!", parameterName)
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
    public static string NotNullOrWhiteSpace(string? value, string parameterName, int maxLength = int.MaxValue, int minLength = 0)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{parameterName}不能为无效、空值或空白!", parameterName)
            : value.Length > maxLength
                ? throw new ArgumentException($"{parameterName}长度必须等于或小于{maxLength}!", parameterName)
                : minLength > 0 && value.Length < minLength
                    ? throw new ArgumentException($"{parameterName}长度必须等于或大于{minLength}!", parameterName)
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
    public static string NotNullOrEmpty(string? value, string parameterName, int maxLength = int.MaxValue, int minLength = 0)
    {
        return string.IsNullOrEmpty(value)
            ? throw new ArgumentException($"{parameterName}不能为无效、空值!", parameterName)
            : value.Length > maxLength
                ? throw new ArgumentException($"{parameterName}长度必须等于或小于{maxLength}!", parameterName)
                : minLength > 0 && value.Length < minLength
                    ? throw new ArgumentException($"{parameterName}长度必须等于或大于{minLength}!", parameterName)
                    : value;
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
            ? value != null && value.Length > maxLength
                ? throw new ArgumentException($"{parameterName}长度必须等于或小于{maxLength}!", parameterName)
                : value
            : string.IsNullOrEmpty(value)
                ? throw new ArgumentException($"{parameterName}不能为无效、空值!", parameterName)
                : value.Length < minLength
                    ? throw new ArgumentException($"{parameterName}长度必须等于或大于{minLength}!", parameterName)
                    : value.Length > maxLength
                        ? throw new ArgumentException($"{parameterName}长度必须等于或小于{maxLength}!", parameterName)
                        : value;
    }

    /// <summary>
    /// 数据不为无效或空值判断
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static ICollection<T> NotNullOrEmpty<T>(ICollection<T>? value, string parameterName)
    {
        return value is not { Count: > 0 } ? throw new ArgumentException($"{parameterName}不能为无效、空值!", parameterName) : value;
    }
}
