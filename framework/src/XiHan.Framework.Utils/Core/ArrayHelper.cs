#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ArrayHelper
// Guid:6g5f4e3d-2c1b-0a9f-8e7d-6c5b4a3f2e1d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/20 0:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 数组操作帮助类
/// </summary>
public static class ArrayHelper
{
    #region 基础判断

    /// <summary>
    /// 判断数组是否为空
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <returns>如果数组长度为0则返回 true，否则返回 false</returns>
    public static bool IsEmpty<T>(T[]? array)
    {
        return array == null || array.Length == 0;
    }

    /// <summary>
    /// 判断数组是否为 null 或空
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <returns>如果数组为 null 或长度为0则返回 true，否则返回 false</returns>
    public static bool IsNullOrEmpty<T>(T[]? array)
    {
        return array == null || array.Length == 0;
    }

    /// <summary>
    /// 判断数组是否有元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <returns>如果数组不为 null 且长度大于0则返回 true，否则返回 false</returns>
    public static bool HasElements<T>(T[]? array)
    {
        return array != null && array.Length > 0;
    }

    /// <summary>
    /// 判断数组是否包含指定元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="item">要查找的元素</param>
    /// <param name="comparer">比较器</param>
    /// <returns>如果包含则返回 true，否则返回 false</returns>
    public static bool Contains<T>(T[]? array, T item, IEqualityComparer<T>? comparer = null)
    {
        if (IsNullOrEmpty(array))
        {
            return false;
        }

        comparer ??= EqualityComparer<T>.Default;
        return array!.Contains(item, comparer);
    }

    /// <summary>
    /// 比较两个数组是否相等
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array1">第一个数组</param>
    /// <param name="array2">第二个数组</param>
    /// <param name="comparer">比较器</param>
    /// <returns>如果相等则返回 true，否则返回 false</returns>
    public static bool SequenceEqual<T>(T[]? array1, T[]? array2, IEqualityComparer<T>? comparer = null)
    {
        if (array1 == null && array2 == null)
        {
            return true;
        }
        if (array1 == null || array2 == null)
        {
            return false;
        }

        comparer ??= EqualityComparer<T>.Default;
        return array1!.SequenceEqual(array2!, comparer);
    }

    #endregion

    #region 索引操作

    /// <summary>
    /// 查找元素的索引
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="item">要查找的元素</param>
    /// <param name="startIndex">开始索引</param>
    /// <param name="comparer">比较器</param>
    /// <returns>元素索引，如果未找到则返回 -1</returns>
    public static int IndexOf<T>(T[]? array, T item, int startIndex = 0, IEqualityComparer<T>? comparer = null)
    {
        if (IsNullOrEmpty(array) || startIndex < 0 || startIndex >= array!.Length)
        {
            return -1;
        }

        comparer ??= EqualityComparer<T>.Default;

        for (var i = startIndex; i < array.Length; i++)
        {
            if (comparer.Equals(array[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 从后往前查找元素的索引
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="item">要查找的元素</param>
    /// <param name="startIndex">开始索引（从后往前）</param>
    /// <param name="comparer">比较器</param>
    /// <returns>元素索引，如果未找到则返回 -1</returns>
    public static int LastIndexOf<T>(T[]? array, T item, int? startIndex = null, IEqualityComparer<T>? comparer = null)
    {
        if (IsNullOrEmpty(array))
        {
            return -1;
        }

        var start = startIndex ?? array!.Length - 1;
        if (start < 0 || start >= array!.Length)
        {
            return -1;
        }

        comparer ??= EqualityComparer<T>.Default;

        for (var i = start; i >= 0; i--)
        {
            if (comparer.Equals(array[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 查找满足条件的第一个元素的索引
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="predicate">条件谓词</param>
    /// <param name="startIndex">开始索引</param>
    /// <returns>元素索引，如果未找到则返回 -1</returns>
    public static int FindIndex<T>(T[]? array, Predicate<T> predicate, int startIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (IsNullOrEmpty(array) || startIndex < 0 || startIndex >= array!.Length)
        {
            return -1;
        }

        for (var i = startIndex; i < array.Length; i++)
        {
            if (predicate(array[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 从后往前查找满足条件的第一个元素的索引
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="predicate">条件谓词</param>
    /// <param name="startIndex">开始索引（从后往前）</param>
    /// <returns>元素索引，如果未找到则返回 -1</returns>
    public static int FindLastIndex<T>(T[]? array, Predicate<T> predicate, int? startIndex = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (IsNullOrEmpty(array))
        {
            return -1;
        }

        var start = startIndex ?? array!.Length - 1;
        if (start < 0 || start >= array!.Length)
        {
            return -1;
        }

        for (var i = start; i >= 0; i--)
        {
            if (predicate(array[i]))
            {
                return i;
            }
        }

        return -1;
    }

    #endregion

    #region 数组查找

    /// <summary>
    /// 查找满足条件的第一个元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="predicate">条件谓词</param>
    /// <returns>找到的元素，如果未找到则返回默认值</returns>
    public static T? Find<T>(T[]? array, Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (IsNullOrEmpty(array))
        {
            return default;
        }

        foreach (var item in array!)
        {
            if (predicate(item))
            {
                return item;
            }
        }

        return default;
    }

    /// <summary>
    /// 从后往前查找满足条件的第一个元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="predicate">条件谓词</param>
    /// <returns>找到的元素，如果未找到则返回默认值</returns>
    public static T? FindLast<T>(T[]? array, Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (IsNullOrEmpty(array))
        {
            return default;
        }

        for (var i = array!.Length - 1; i >= 0; i--)
        {
            if (predicate(array[i]))
            {
                return array[i];
            }
        }

        return default;
    }

    /// <summary>
    /// 查找所有满足条件的元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="predicate">条件谓词</param>
    /// <returns>满足条件的元素数组</returns>
    public static T[] FindAll<T>(T[]? array, Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (IsNullOrEmpty(array))
        {
            return [];
        }

        var result = new List<T>();
        foreach (var item in array!)
        {
            if (predicate(item))
            {
                result.Add(item);
            }
        }

        return [.. result];
    }

    /// <summary>
    /// 判断是否存在满足条件的元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="predicate">条件谓词</param>
    /// <returns>如果存在则返回 true，否则返回 false</returns>
    public static bool Exists<T>(T[]? array, Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (IsNullOrEmpty(array))
        {
            return false;
        }

        foreach (var item in array!)
        {
            if (predicate(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断是否所有元素都满足条件
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="predicate">条件谓词</param>
    /// <returns>如果所有元素都满足条件则返回 true，否则返回 false</returns>
    public static bool All<T>(T[]? array, Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (IsNullOrEmpty(array))
        {
            return true; // 空数组默认所有元素都满足条件
        }

        foreach (var item in array!)
        {
            if (!predicate(item))
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region 数组操作

    /// <summary>
    /// 调整数组大小
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="newSize">新大小</param>
    /// <param name="defaultValue">新增元素的默认值</param>
    /// <returns>调整大小后的数组</returns>
    public static T[] Resize<T>(T[]? array, int newSize, T defaultValue = default!)
    {
        if (newSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newSize), "数组大小不能为负数");
        }

        var result = new T[newSize];

        if (array != null && array.Length > 0)
        {
            var copyLength = Math.Min(array.Length, newSize);
            Array.Copy(array, result, copyLength);
        }

        // 如果新数组更大，填充默认值
        if (array != null && newSize > array.Length && !EqualityComparer<T>.Default.Equals(defaultValue, default!))
        {
            for (var i = array.Length; i < newSize; i++)
            {
                result[i] = defaultValue;
            }
        }

        return result;
    }

    /// <summary>
    /// 在数组中插入元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">原数组</param>
    /// <param name="index">插入位置</param>
    /// <param name="item">要插入的元素</param>
    /// <returns>插入元素后的新数组</returns>
    public static T[] Insert<T>(T[]? array, int index, T item)
    {
        array ??= [];

        if (index < 0 || index > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "插入位置超出数组范围");
        }

        var result = new T[array.Length + 1];

        // 复制插入位置之前的元素
        if (index > 0)
        {
            Array.Copy(array, 0, result, 0, index);
        }

        // 插入新元素
        result[index] = item;

        // 复制插入位置之后的元素
        if (index < array.Length)
        {
            Array.Copy(array, index, result, index + 1, array.Length - index);
        }

        return result;
    }

    /// <summary>
    /// 在数组中插入多个元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">原数组</param>
    /// <param name="index">插入位置</param>
    /// <param name="items">要插入的元素</param>
    /// <returns>插入元素后的新数组</returns>
    public static T[] InsertRange<T>(T[]? array, int index, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        array ??= [];
        var itemsArray = items.ToArray();

        if (index < 0 || index > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "插入位置超出数组范围");
        }

        if (itemsArray.Length == 0)
        {
            return [.. array!]; // 返回原数组的副本
        }

        var result = new T[array.Length + itemsArray.Length];

        // 复制插入位置之前的元素
        if (index > 0)
        {
            Array.Copy(array, 0, result, 0, index);
        }

        // 插入新元素
        Array.Copy(itemsArray, 0, result, index, itemsArray.Length);

        // 复制插入位置之后的元素
        if (index < array.Length)
        {
            Array.Copy(array, index, result, index + itemsArray.Length, array.Length - index);
        }

        return result;
    }

    /// <summary>
    /// 从数组中删除第一个匹配的元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">原数组</param>
    /// <param name="item">要删除的元素</param>
    /// <param name="comparer">比较器</param>
    /// <returns>删除元素后的新数组</returns>
    public static T[] Remove<T>(T[]? array, T item, IEqualityComparer<T>? comparer = null)
    {
        if (IsNullOrEmpty(array))
        {
            return [];
        }

        var index = IndexOf(array, item, 0, comparer);
        if (index == -1)
        {
            return [.. array!]; // 返回原数组的副本
        }

        return RemoveAt(array, index);
    }

    /// <summary>
    /// 从数组中删除指定位置的元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">原数组</param>
    /// <param name="index">要删除的位置</param>
    /// <returns>删除元素后的新数组</returns>
    public static T[] RemoveAt<T>(T[]? array, int index)
    {
        if (IsNullOrEmpty(array))
        {
            throw new ArgumentException("数组为空");
        }

        if (index < 0 || index >= array!.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "删除位置超出数组范围");
        }

        var result = new T[array.Length - 1];

        // 复制删除位置之前的元素
        if (index > 0)
        {
            Array.Copy(array, 0, result, 0, index);
        }

        // 复制删除位置之后的元素
        if (index < array.Length - 1)
        {
            Array.Copy(array, index + 1, result, index, array.Length - index - 1);
        }

        return result;
    }

    /// <summary>
    /// 从数组中删除指定范围的元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">原数组</param>
    /// <param name="index">开始删除的位置</param>
    /// <param name="count">删除的元素数量</param>
    /// <returns>删除元素后的新数组</returns>
    public static T[] RemoveRange<T>(T[]? array, int index, int count)
    {
        if (IsNullOrEmpty(array))
        {
            if (index == 0 && count == 0)
            {
                return [];
            }
            throw new ArgumentException("数组为空");
        }

        if (index < 0 || count < 0 || index + count > array!.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "删除范围超出数组范围");
        }

        if (count == 0)
        {
            return [.. array!]; // 返回原数组的副本
        }

        var result = new T[array.Length - count];

        // 复制删除范围之前的元素
        if (index > 0)
        {
            Array.Copy(array, 0, result, 0, index);
        }

        // 复制删除范围之后的元素
        if (index + count < array.Length)
        {
            Array.Copy(array, index + count, result, index, array.Length - index - count);
        }

        return result;
    }

    /// <summary>
    /// 反转数组
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">原数组</param>
    /// <returns>反转后的新数组</returns>
    public static T[] Reverse<T>(T[]? array)
    {
        if (IsNullOrEmpty(array))
        {
            return [];
        }

        var result = new T[array!.Length];
        for (var i = 0; i < array.Length; i++)
        {
            result[i] = array[array.Length - 1 - i];
        }

        return result;
    }

    /// <summary>
    /// 排序数组
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">原数组</param>
    /// <param name="comparer">比较器</param>
    /// <returns>排序后的新数组</returns>
    public static T[] Sort<T>(T[]? array, IComparer<T>? comparer = null)
    {
        if (IsNullOrEmpty(array))
        {
            return [];
        }

        var result = array!.ToArray();
        Array.Sort(result, comparer);
        return result;
    }

    #endregion

    #region 数组合并

    /// <summary>
    /// 合并多个数组
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="arrays">要合并的数组</param>
    /// <returns>合并后的数组</returns>
    public static T[] Combine<T>(params T[]?[] arrays)
    {
        if (arrays == null || arrays.Length == 0)
        {
            return [];
        }

        var totalLength = arrays.Where(arr => arr != null).Sum(arr => arr!.Length);
        var result = new T[totalLength];
        var currentIndex = 0;

        foreach (var array in arrays)
        {
            if (array != null && array.Length > 0)
            {
                Array.Copy(array, 0, result, currentIndex, array.Length);
                currentIndex += array.Length;
            }
        }

        return result;
    }

    /// <summary>
    /// 连接两个数组
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="first">第一个数组</param>
    /// <param name="second">第二个数组</param>
    /// <returns>连接后的数组</returns>
    public static T[] Concat<T>(T[]? first, T[]? second)
    {
        return Combine(first, second);
    }

    /// <summary>
    /// 获取多个数组的并集
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="arrays">数组集合</param>
    /// <param name="comparer">比较器</param>
    /// <returns>并集数组</returns>
    public static T[] Union<T>(IEnumerable<T[]> arrays, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(arrays);

        var result = new HashSet<T>(comparer);
        foreach (var array in arrays)
        {
            if (array != null)
            {
                foreach (var item in array!)
                {
                    result.Add(item);
                }
            }
        }

        return [.. result];
    }

    /// <summary>
    /// 获取多个数组的交集
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="arrays">数组集合</param>
    /// <param name="comparer">比较器</param>
    /// <returns>交集数组</returns>
    public static T[] Intersect<T>(IEnumerable<T[]> arrays, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(arrays);

        var arrayList = arrays.Where(arr => arr != null && arr.Length > 0).ToList();
        if (arrayList.Count == 0)
        {
            return [];
        }

        var result = new HashSet<T>(arrayList[0], comparer);
        for (var i = 1; i < arrayList.Count; i++)
        {
            result.IntersectWith(arrayList[i]);
            if (result.Count == 0)
            {
                break;
            }
        }

        return [.. result];
    }

    /// <summary>
    /// 获取第一个数组中不在其他数组中的元素
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="first">第一个数组</param>
    /// <param name="others">其他数组</param>
    /// <param name="comparer">比较器</param>
    /// <returns>差集数组</returns>
    public static T[] Except<T>(T[]? first, IEnumerable<T[]> others, IEqualityComparer<T>? comparer = null)
    {
        if (IsNullOrEmpty(first))
        {
            return [];
        }

        ArgumentNullException.ThrowIfNull(others);

        var result = new HashSet<T>(first!, comparer);
        foreach (var other in others)
        {
            if (other != null)
            {
                result.ExceptWith(other);
            }
        }

        return [.. result];
    }

    #endregion

    #region 数组转换

    /// <summary>
    /// 转换数组元素类型
    /// </summary>
    /// <typeparam name="TInput">输入类型</typeparam>
    /// <typeparam name="TOutput">输出类型</typeparam>
    /// <param name="array">原数组</param>
    /// <param name="converter">转换器</param>
    /// <returns>转换后的数组</returns>
    public static TOutput[] ConvertAll<TInput, TOutput>(TInput[]? array, Func<TInput, TOutput> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);

        if (IsNullOrEmpty(array))
        {
            return [];
        }

        var result = new TOutput[array!.Length];
        for (var i = 0; i < array.Length; i++)
        {
            result[i] = converter(array[i]);
        }

        return result;
    }

    /// <summary>
    /// 转换数组为列表
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <returns>列表</returns>
    public static List<T> ToList<T>(T[]? array)
    {
        return IsNullOrEmpty(array) ? [] : [.. array!];
    }

    /// <summary>
    /// 转换数组为哈希集合
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="comparer">比较器</param>
    /// <returns>哈希集合</returns>
    public static HashSet<T> ToHashSet<T>(T[]? array, IEqualityComparer<T>? comparer = null)
    {
        return IsNullOrEmpty(array) ? new HashSet<T>(comparer) : new HashSet<T>(array!, comparer);
    }

    /// <summary>
    /// 转换数组为字典
    /// </summary>
    /// <typeparam name="T">数组元素类型</typeparam>
    /// <typeparam name="TKey">字典键类型</typeparam>
    /// <typeparam name="TValue">字典值类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="keySelector">键选择器</param>
    /// <param name="valueSelector">值选择器</param>
    /// <param name="comparer">键比较器</param>
    /// <returns>字典</returns>
    public static Dictionary<TKey, TValue> ToDictionary<T, TKey, TValue>(T[]? array,
        Func<T, TKey> keySelector, Func<T, TValue> valueSelector, IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(valueSelector);

        if (IsNullOrEmpty(array))
        {
            return new Dictionary<TKey, TValue>(comparer);
        }

        return array!.ToDictionary(keySelector, valueSelector, comparer);
    }

    #endregion

    #region 其他工具方法

    /// <summary>
    /// 创建指定长度的数组，并用指定值填充
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="length">数组长度</param>
    /// <param name="value">填充值</param>
    /// <returns>填充后的数组</returns>
    public static T[] Fill<T>(int length, T value)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "数组长度不能为负数");
        }

        var array = new T[length];
        if (!EqualityComparer<T>.Default.Equals(value, default!))
        {
            for (var i = 0; i < length; i++)
            {
                array[i] = value;
            }
        }

        return array;
    }

    /// <summary>
    /// 创建数组的子数组
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">原数组</param>
    /// <param name="startIndex">开始索引</param>
    /// <param name="length">子数组长度</param>
    /// <returns>子数组</returns>
    public static T[] SubArray<T>(T[]? array, int startIndex, int length)
    {
        if (IsNullOrEmpty(array))
        {
            if (startIndex == 0 && length == 0)
            {
                return [];
            }
            throw new ArgumentException("数组为空");
        }

        if (startIndex < 0 || length < 0 || startIndex + length > array!.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex), "子数组范围超出数组范围");
        }

        if (length == 0)
        {
            return [];
        }

        var result = new T[length];
        Array.Copy(array, startIndex, result, 0, length);
        return result;
    }

    /// <summary>
    /// 打乱数组顺序
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="array">原数组</param>
    /// <param name="random">随机数生成器</param>
    /// <returns>打乱后的新数组</returns>
    public static T[] Shuffle<T>(T[]? array, Random? random = null)
    {
        if (IsNullOrEmpty(array))
        {
            return [];
        }

        random ??= new Random();
        var result = array!.ToArray();

        // Fisher-Yates shuffle 算法
        for (var i = result.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }

    #endregion
}