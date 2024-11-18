#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RandomHelper
// Guid:b18b6086-49ff-45f2-abb9-86a7a8505db4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-09-18 上午 02:20:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.System;

/// <summary>
/// 随机帮助类
/// </summary>
public static class RandomHelper
{
    // 默认随机数生成器
    private static readonly Random Rnd = new();

    private static readonly Lock Lock = new();

    #region 常用类型

    // 默认数字字符源
    private const string DefaultNumberSource = "0123456789";

    // 默认大写字母字符源
    private const string DefaultUpperLetterSource = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // 默认小写字母字符源
    private const string DefaultLowerLetterSource = "abcdefghijklmnopqrstuvwxyz";

    // 默认字母或数字字符源
    private const string DefaultNumberOrLetterSource = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// 随机数字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义数字字符源</param>
    /// <returns></returns>
    public static string GetNumber(int? length, string? source)
    {
        return GetRandom(length ?? 6, source ?? DefaultNumberSource);
    }

    /// <summary>
    /// 随机大写字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义大写字母字符源</param>
    /// <returns></returns>
    public static string GetUpperLetter(int? length, string? source)
    {
        return GetRandom(length ?? 6, source?.ToUpperInvariant() ?? DefaultUpperLetterSource);
    }

    /// <summary>
    /// 随机小写字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义小写字母字符源</param>
    /// <returns></returns>
    public static string GetLowerLetter(int? length, string? source)
    {
        return GetRandom(length ?? 6, source?.ToLowerInvariant() ?? DefaultLowerLetterSource);
    }

    /// <summary>
    /// 随机字母或数字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义字母或数字字符源</param>
    /// <returns></returns>
    public static string GetNumberOrLetter(int? length, string? source)
    {
        return GetRandom(length ?? 6, source ?? DefaultNumberOrLetterSource);
    }

    #endregion

    #region 其他类型

    /// <summary>
    /// 根据字符源生成随机字符
    /// </summary>
    /// <param name="length">生成长度</param>
    /// <param name="source">自定义字符源</param>
    /// <returns></returns>
    public static string GetRandom(int length, string source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        _ = CheckHelper.NotNullOrEmpty(source, nameof(source));

        StringBuilder result = new();

        lock (Lock)
        {
            for (var i = 0; i < length; i++)
            {
                _ = result.Append(source[Rnd.Next(0, source.Length)]);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 返回一个指定范围的非负随机数
    /// </summary>
    /// <param name="minValue">返回的随机数的下限</param>
    /// <param name="maxValue">返回的随机数的上限，maxValue 必须大于或等于 minValue</param>
    /// <returns></returns>
    public static int GetRandom(int minValue, int maxValue)
    {
        lock (Lock)
        {
            return Rnd.Next(minValue, maxValue);
        }
    }

    /// <summary>
    /// 返回一个小于指定最大值的非负随机数
    /// </summary>
    /// <param name="maxValue">maxValue 必须大于或等于零</param>
    /// <returns>
    /// 一个大于或等于零且小于 maxValue 的 32 位有符号整数；也就是说，返回值的范围通常包括零但不包括 maxValue
    /// 然而，如果 maxValue 等于零，则返回 maxValue
    /// </returns>
    public static int GetRandom(int maxValue)
    {
        lock (Lock)
        {
            return Rnd.Next(maxValue);
        }
    }

    /// <summary>
    /// 返回一个非负随机数
    /// </summary>
    /// <returns>一个 32 位有符号整数，大于或等于零且小于 <see cref="int.MaxValue"/></returns>
    public static int GetRandom()
    {
        lock (Lock)
        {
            return Rnd.Next();
        }
    }

    /// <summary>
    /// 从给定的对象生成一个随机化项
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="objs"></param>
    /// <returns></returns>
    public static T GetRandomOf<T>(params T[] objs)
    {
        _ = CheckHelper.NotNullOrEmpty(objs, nameof(objs));

        return objs[GetRandom(0, objs.Length)];
    }

    /// <summary>
    /// 从给定的列表生成一个随机化项
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <returns></returns>
    public static T GetRandomOfList<T>(IList<T> list)
    {
        _ = CheckHelper.NotNullOrEmpty(list, nameof(list));

        return list[GetRandom(0, list.Count)];
    }

    /// <summary>
    /// 从给定的可枚举对象生成一个随机化列表
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items"></param>
    /// <returns></returns>
    public static List<T> GenerateRandomizedList<T>(IEnumerable<T> items)
    {
        _ = CheckHelper.NotNull(items, nameof(items));

        List<T>? currentList = new(items);
        List<T>? randomList = [];

        while (currentList.Count != 0)
        {
            var randomIndex = GetRandom(0, currentList.Count);
            randomList.Add(currentList[randomIndex]);
            currentList.RemoveAt(randomIndex);
        }

        return randomList;
    }

    #endregion
}
