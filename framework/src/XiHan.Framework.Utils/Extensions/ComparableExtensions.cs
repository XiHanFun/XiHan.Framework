#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ComparableExtensions
// Guid:8f7e6d5c-4b3a-2f1e-9d8c-7a6b5f4e3d2c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 23:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// IComparable 扩展方法
/// </summary>
public static class ComparableExtensions
{
    #region 范围检查

    /// <summary>
    /// 检查值是否在指定范围内（包含边界）
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>如果值在范围内返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentException">当最小值大于最大值时抛出异常</exception>
    public static bool IsInRange<T>(this T value, T min, T max) where T : IComparable<T>
    {
        if (min.CompareTo(max) > 0)
        {
            throw new ArgumentException("最小值不能大于最大值", nameof(min));
        }

        return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
    }

    /// <summary>
    /// 检查值是否在指定范围内（不包含边界）
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>如果值在范围内返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentException">当最小值大于等于最大值时抛出异常</exception>
    public static bool IsBetween<T>(this T value, T min, T max) where T : IComparable<T>
    {
        if (min.CompareTo(max) >= 0)
        {
            throw new ArgumentException("最小值必须小于最大值", nameof(min));
        }

        return value.CompareTo(min) > 0 && value.CompareTo(max) < 0;
    }

    /// <summary>
    /// 检查值是否在指定范围内（自定义边界包含性）
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="includeMin">是否包含最小值边界</param>
    /// <param name="includeMax">是否包含最大值边界</param>
    /// <returns>如果值在范围内返回 true，否则返回 false</returns>
    public static bool IsInRange<T>(this T value, T min, T max, bool includeMin, bool includeMax) where T : IComparable<T>
    {
        var minCheck = includeMin ? value.CompareTo(min) >= 0 : value.CompareTo(min) > 0;
        var maxCheck = includeMax ? value.CompareTo(max) <= 0 : value.CompareTo(max) < 0;
        return minCheck && maxCheck;
    }

    #endregion

    #region 比较操作

    /// <summary>
    /// 检查值是否大于指定值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="other">比较的值</param>
    /// <returns>如果 value 大于 other 返回 true，否则返回 false</returns>
    public static bool IsGreaterThan<T>(this T value, T other) where T : IComparable<T>
    {
        return value.CompareTo(other) > 0;
    }

    /// <summary>
    /// 检查值是否大于等于指定值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="other">比较的值</param>
    /// <returns>如果 value 大于等于 other 返回 true，否则返回 false</returns>
    public static bool IsGreaterThanOrEqual<T>(this T value, T other) where T : IComparable<T>
    {
        return value.CompareTo(other) >= 0;
    }

    /// <summary>
    /// 检查值是否小于指定值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="other">比较的值</param>
    /// <returns>如果 value 小于 other 返回 true，否则返回 false</returns>
    public static bool IsLessThan<T>(this T value, T other) where T : IComparable<T>
    {
        return value.CompareTo(other) < 0;
    }

    /// <summary>
    /// 检查值是否小于等于指定值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="other">比较的值</param>
    /// <returns>如果 value 小于等于 other 返回 true，否则返回 false</returns>
    public static bool IsLessThanOrEqual<T>(this T value, T other) where T : IComparable<T>
    {
        return value.CompareTo(other) <= 0;
    }

    /// <summary>
    /// 检查值是否等于指定值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="other">比较的值</param>
    /// <returns>如果 value 等于 other 返回 true，否则返回 false</returns>
    public static bool IsEqualTo<T>(this T value, T other) where T : IComparable<T>
    {
        return value.CompareTo(other) == 0;
    }

    /// <summary>
    /// 检查值是否不等于指定值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="other">比较的值</param>
    /// <returns>如果 value 不等于 other 返回 true，否则返回 false</returns>
    public static bool IsNotEqualTo<T>(this T value, T other) where T : IComparable<T>
    {
        return value.CompareTo(other) != 0;
    }

    #endregion

    #region 限制和边界

    /// <summary>
    /// 将值限制在指定范围内
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要限制的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>限制在范围内的值</returns>
    /// <exception cref="ArgumentException">当最小值大于最大值时抛出异常</exception>
    public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T>
    {
        if (min.CompareTo(max) > 0)
        {
            throw new ArgumentException("最小值不能大于最大值", nameof(min));
        }

        return value.CompareTo(min) < 0 ? min : value.CompareTo(max) > 0 ? max : value;
    }

    /// <summary>
    /// 确保值不小于指定的最小值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="min">最小值</param>
    /// <returns>如果值小于最小值则返回最小值，否则返回原值</returns>
    public static T AtLeast<T>(this T value, T min) where T : IComparable<T>
    {
        return value.CompareTo(min) < 0 ? min : value;
    }

    /// <summary>
    /// 确保值不大于指定的最大值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="max">最大值</param>
    /// <returns>如果值大于最大值则返回最大值，否则返回原值</returns>
    public static T AtMost<T>(this T value, T max) where T : IComparable<T>
    {
        return value.CompareTo(max) > 0 ? max : value;
    }

    #endregion

    #region 最大最小值

    /// <summary>
    /// 返回两个值中的较大值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">第一个值</param>
    /// <param name="other">第二个值</param>
    /// <returns>较大的值</returns>
    public static T Max<T>(this T value, T other) where T : IComparable<T>
    {
        return value.CompareTo(other) >= 0 ? value : other;
    }

    /// <summary>
    /// 返回两个值中的较小值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">第一个值</param>
    /// <param name="other">第二个值</param>
    /// <returns>较小的值</returns>
    public static T Min<T>(this T value, T other) where T : IComparable<T>
    {
        return value.CompareTo(other) <= 0 ? value : other;
    }

    /// <summary>
    /// 返回多个值中的最大值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">第一个值</param>
    /// <param name="others">其他值</param>
    /// <returns>最大值</returns>
    /// <exception cref="ArgumentNullException">当 others 为 null 时抛出异常</exception>
    public static T Max<T>(this T value, params T[] others) where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(others);

        var max = value;
        foreach (var other in others)
        {
            if (other.CompareTo(max) > 0)
            {
                max = other;
            }
        }
        return max;
    }

    /// <summary>
    /// 返回多个值中的最小值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">第一个值</param>
    /// <param name="others">其他值</param>
    /// <returns>最小值</returns>
    /// <exception cref="ArgumentNullException">当 others 为 null 时抛出异常</exception>
    public static T Min<T>(this T value, params T[] others) where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(others);

        var min = value;
        foreach (var other in others)
        {
            if (other.CompareTo(min) < 0)
            {
                min = other;
            }
        }
        return min;
    }

    #endregion

    #region 空值安全比较

    /// <summary>
    /// 空值安全的比较（null 被视为最小值）
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要比较的值</param>
    /// <param name="other">另一个值</param>
    /// <returns>比较结果</returns>
    public static int CompareToNullSafe<T>(this T? value, T? other) where T : class, IComparable<T>
    {
        if (value == null && other == null)
        {
            return 0;
        }

        if (value == null)
        {
            return -1;
        }

        if (other == null)
        {
            return 1;
        }

        return value.CompareTo(other);
    }

    /// <summary>
    /// 空值安全的比较（null 被视为最小值）- 适用于可空值类型
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的值类型</typeparam>
    /// <param name="value">要比较的值</param>
    /// <param name="other">另一个值</param>
    /// <returns>比较结果</returns>
    public static int CompareToNullSafe<T>(this T? value, T? other) where T : struct, IComparable<T>
    {
        if (!value.HasValue && !other.HasValue)
        {
            return 0;
        }

        if (!value.HasValue)
        {
            return -1;
        }

        if (!other.HasValue)
        {
            return 1;
        }

        return value.Value.CompareTo(other.Value);
    }

    /// <summary>
    /// 空值安全的最大值比较
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">第一个值</param>
    /// <param name="other">第二个值</param>
    /// <returns>较大的值，如果都为 null 则返回 null</returns>
    public static T? MaxNullSafe<T>(this T? value, T? other) where T : class, IComparable<T>
    {
        if (value == null)
        {
            return other;
        }

        if (other == null)
        {
            return value;
        }

        return value.CompareTo(other) >= 0 ? value : other;
    }

    /// <summary>
    /// 空值安全的最小值比较
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">第一个值</param>
    /// <param name="other">第二个值</param>
    /// <returns>较小的值，如果都为 null 则返回 null</returns>
    public static T? MinNullSafe<T>(this T? value, T? other) where T : class, IComparable<T>
    {
        if (value == null)
        {
            return other;
        }

        if (other == null)
        {
            return value;
        }

        return value.CompareTo(other) <= 0 ? value : other;
    }

    #endregion

    #region 集合比较

    /// <summary>
    /// 检查值是否在指定的集合中
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="collection">值的集合</param>
    /// <returns>如果值在集合中返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当 collection 为 null 时抛出异常</exception>
    public static bool IsIn<T>(this T value, IEnumerable<T> collection) where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(collection);

        return collection.Any(item => value.CompareTo(item) == 0);
    }

    /// <summary>
    /// 检查值是否在指定的参数列表中
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="values">值的参数列表</param>
    /// <returns>如果值在参数列表中返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当 values 为 null 时抛出异常</exception>
    public static bool IsIn<T>(this T value, params T[] values) where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(values);

        return values.Any(item => value.CompareTo(item) == 0);
    }

    /// <summary>
    /// 检查值是否不在指定的集合中
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="collection">值的集合</param>
    /// <returns>如果值不在集合中返回 true，否则返回 false</returns>
    public static bool IsNotIn<T>(this T value, IEnumerable<T> collection) where T : IComparable<T>
    {
        return !value.IsIn(collection);
    }

    /// <summary>
    /// 检查值是否不在指定的参数列表中
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <param name="values">值的参数列表</param>
    /// <returns>如果值不在参数列表中返回 true，否则返回 false</returns>
    public static bool IsNotIn<T>(this T value, params T[] values) where T : IComparable<T>
    {
        return !value.IsIn(values);
    }

    #endregion

    #region 实用工具

    /// <summary>
    /// 获取值的绝对比较结果（总是非负数）
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要比较的值</param>
    /// <param name="other">另一个值</param>
    /// <returns>绝对比较结果</returns>
    public static int AbsCompareTo<T>(this T value, T other) where T : IComparable<T>
    {
        return Math.Abs(value.CompareTo(other));
    }

    /// <summary>
    /// 检查值是否等于默认值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <returns>如果值等于默认值返回 true，否则返回 false</returns>
    public static bool IsDefault<T>(this T value) where T : IComparable<T>
    {
        return value.CompareTo(default(T)!) == 0;
    }

    /// <summary>
    /// 检查值是否不等于默认值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <returns>如果值不等于默认值返回 true，否则返回 false</returns>
    public static bool IsNotDefault<T>(this T value) where T : IComparable<T>
    {
        return value.CompareTo(default(T)!) != 0;
    }

    /// <summary>
    /// 如果当前值满足条件则返回该值，否则返回替代值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">当前值</param>
    /// <param name="predicate">条件谓词</param>
    /// <param name="alternative">替代值</param>
    /// <returns>满足条件时返回当前值，否则返回替代值</returns>
    /// <exception cref="ArgumentNullException">当 predicate 为 null 时抛出异常</exception>
    public static T IfThen<T>(this T value, Func<T, bool> predicate, T alternative) where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return predicate(value) ? value : alternative;
    }

    /// <summary>
    /// 如果当前值满足条件则返回该值，否则返回由工厂函数生成的值
    /// </summary>
    /// <typeparam name="T">实现 IComparable&lt;T&gt; 的类型</typeparam>
    /// <param name="value">当前值</param>
    /// <param name="predicate">条件谓词</param>
    /// <param name="alternativeFactory">替代值工厂函数</param>
    /// <returns>满足条件时返回当前值，否则返回工厂函数生成的值</returns>
    /// <exception cref="ArgumentNullException">当 predicate 或 alternativeFactory 为 null 时抛出异常</exception>
    public static T IfThen<T>(this T value, Func<T, bool> predicate, Func<T> alternativeFactory) where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(alternativeFactory);

        return predicate(value) ? value : alternativeFactory();
    }

    #endregion
}
