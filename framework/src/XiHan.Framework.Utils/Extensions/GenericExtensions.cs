#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GenericExtensions
// Guid:a5574e0a-f226-4b0a-89bc-4bd9e9618a3d
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2023-08-09 下午 05:23:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using System.Reflection;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 泛型扩展方法
/// </summary>
public static class GenericExtensions
{
    #region 属性信息

    /// <summary>
    /// 获取泛型类型名称
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string GetGenericTypeName(this Type type)
    {
        string typeName;

        if (type.IsGenericType)
        {
            var genericTypes = string.Join(",", type.GetGenericArguments().Select(t => t.Name).ToArray());
            typeName = $"{type.Name[..type.Name.IndexOf('`')]}<{genericTypes}>";
        }
        else
        {
            typeName = type.Name;
        }

        return typeName;
    }

    /// <summary>
    /// 获取泛型类型名称
    /// </summary>
    /// <param name="object"></param>
    /// <returns></returns>
    public static string GetGenericTypeName(this object @object)
    {
        return @object.GetType().GetGenericTypeName();
    }

    /// <summary>
    /// 递归获取最深层次的任何类型的属性
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="entity"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public static TEntity GetPropertyDeepestValue<TEntity>(this TEntity entity, string propertyName)
    {
        while (true)
        {
            var value = entity.GetPropertyValue<TEntity, TEntity>(propertyName);
            if (value is null)
            {
                return entity;
            }
        }
    }

    /// <summary>
    /// 获取对象属性值
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="entity"></param>
    /// <param name="propertyName">需要判断的属性</param>
    /// <returns></returns>
    public static TValue GetPropertyValue<TEntity, TValue>(this TEntity entity, string propertyName)
    {
        var objectType = typeof(TEntity);
        var propertyInfo = objectType.GetProperty(propertyName);
        if (propertyInfo is null || !propertyInfo.PropertyType.IsGenericType)
        {
            throw new ArgumentException($"属性 '{propertyName}' 不存在，或者不是类型 '{objectType.Name}' 中的泛型类型。");
        }

        var paramObj = Expression.Parameter(typeof(TEntity));

        // 转成真实类型，防止 Dynamic 类型转换成 object
        var bodyObj = Expression.Convert(paramObj, objectType);
        var body = Expression.Property(bodyObj, propertyInfo);
        var getValue = Expression.Lambda<Func<TEntity, TValue>>(body, paramObj).Compile();
        return getValue(entity);
    }

    /// <summary>
    /// 设置对象属性值
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="entity"></param>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetPropertyValue<TEntity, TValue>(this TEntity entity, string propertyName, TValue value)
    {
        var objectType = typeof(TEntity);
        var propertyInfo = objectType.GetProperty(propertyName);
        if (propertyInfo is null || !propertyInfo.PropertyType.IsGenericType)
        {
            throw new ArgumentException($"属性 '{propertyName}' 不存在，或者不是类型 '{objectType.Name}' 中的泛型类型。");
        }

        var paramObj = Expression.Parameter(objectType);
        var paramVal = Expression.Parameter(typeof(TValue));
        var bodyVal = Expression.Convert(paramVal, propertyInfo.PropertyType);

        // 获取设置属性的值的方法
        var setMethod = propertyInfo.GetSetMethod(true);

        // 如果只是只读,则 setMethod==null
        if (setMethod is null)
        {
            return false;
        }

        var body = Expression.Call(paramObj, setMethod, bodyVal);
        var setValue = Expression.Lambda<Action<TEntity, TValue>>(body, paramObj, paramVal).Compile();
        setValue(entity, value);

        return true;
    }

    /// <summary>
    /// 获取对象属性信息列表
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="entity"></param>
    /// <param name="isContainsInherited">是否含继承属性</param>
    /// <returns></returns>
    public static List<CustomPropertyInfo> GetProperties<TEntity>(this TEntity entity, bool isContainsInherited)
        where TEntity : class
    {
        var type = typeof(TEntity);
        var properties =
            isContainsInherited
                ? type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                : type.GetProperties();
        return [.. properties.Select(info => new CustomPropertyInfo
        {
            PropertyName = info.Name,
            PropertyType = info.PropertyType.Name,
            PropertyValue = info.GetValue(entity).ParseToString()
        })];
    }

    /// <summary>
    /// 对比两个相同类型的相同属性之间的差异信息
    /// </summary>
    /// <typeparam name="TEntity">对象类型</typeparam>
    /// <param name="oldEntity">对象实例1</param>
    /// <param name="newEntity">对象实例2</param>
    /// <param name="specialList">要排除某些特殊属性</param>
    /// <returns></returns>
    public static List<CustomPropertyVariance> GetPropertiesDetailedCompare<TEntity>(this TEntity oldEntity, TEntity newEntity, List<string>? specialList)
        where TEntity : class
    {
        var propertyInfo = typeof(TEntity).GetProperties();
        List<CustomPropertyVariance> result = [];

        foreach (var variance in propertyInfo)
        {
            var type = variance.PropertyType;
            var before = variance.GetValue(oldEntity, null).CastTo(type);
            var after = variance.GetValue(newEntity, null).CastTo(type);

            // 使用 Equals 进行值比较，处理值类型和引用类型
            if (before is not null && after is not null)
            {
                if (!before.Equals(after))
                {
                    result.Add(new CustomPropertyVariance
                    {
                        PropertyName = variance.Name,
                        Before = before.ToString() ?? string.Empty,
                        After = after.ToString() ?? string.Empty
                    });
                }
            }
            // 处理 null 的情况
            else if (before != after)
            {
                result.Add(new CustomPropertyVariance
                {
                    PropertyName = variance.Name,
                    Before = before?.ToString() ?? string.Empty,
                    After = after?.ToString() ?? string.Empty
                });
            }
        }

        // 要排除某些特殊属性
        if (result.Count != 0 && specialList is not null && specialList.Count != 0)
        {
            result = [.. result.Where(s => !specialList.Contains(s.PropertyName))];
        }

        return result;
    }

    /// <summary>
    /// 把两个对象的属性差异信息转换为 Json 格式
    /// </summary>
    /// <typeparam name="TEntity">对象类型</typeparam>
    /// <param name="oldEntity">对象实例1</param>
    /// <param name="newEntity">对象实例2</param>
    /// <param name="specialList">要排除某些特殊属性</param>
    /// <returns></returns>
    public static string GetPropertiesChangedNote<TEntity>(this TEntity oldEntity, TEntity newEntity, List<string>? specialList)
        where TEntity : class
    {
        var list = oldEntity.GetPropertiesDetailedCompare(newEntity, specialList);

        var enumerable = new
        {
            ChangedNote = list.Select(s => new
            {
                s.PropertyName,
                s.Before,
                s.After
            })
        };
        return enumerable.ToJson();
    }

    #endregion 属性信息

    #region 判断为空

    /// <summary>
    /// 判断对象是否为空，为空返回 true
    /// </summary>
    /// <typeparam name="T">要验证的对象的类型</typeparam>
    /// <param name="data">要验证的对象</param>
    public static bool IsNullOrEmpty<T>(this T? data)
    {
        // 如果为 null
        if (data is null)
        {
            return true;
        }

        // 如果为""
        if (data is not string)
        {
            return data is DBNull;
        }

        if (string.IsNullOrEmpty(data.ToString()?.Trim()))
        {
            return true;
        }

        // 如果为 DBNull
        return data is DBNull;
    }

    #endregion 判断为空

    #region 判断范围

    /// <summary>
    /// 判断当前值是否介于指定范围内
    /// </summary>
    /// <typeparam name="T">泛型</typeparam>
    /// <param name="value">泛型对象</param>
    /// <param name="start">范围起点</param>
    /// <param name="end">范围终点</param>
    /// <param name="leftEqual">是否可等于上限(默认等于)</param>
    /// <param name="rightEqual">是否可等于下限(默认等于)</param>
    /// <returns> 是否介于 </returns>
    public static bool IsBetween<T>(this IComparable<T> value, T start, T end, bool leftEqual = true, bool rightEqual = true)
        where T : IComparable
    {
        var flag = leftEqual ? value.CompareTo(start) >= 0 : value.CompareTo(start) > 0;
        return flag && (rightEqual ? value.CompareTo(end) <= 0 : value.CompareTo(end) < 0);
    }

    /// <summary>
    /// 判断当前值是否介于指定范围内
    /// </summary>
    /// <typeparam name="T">泛型</typeparam>
    /// <param name="value">泛型对象</param>
    /// <param name="min">范围小值</param>
    /// <param name="max">范围大值</param>
    /// <param name="minEqual">是否可等于小值(默认等于)</param>
    /// <param name="maxEqual">是否可等于大值(默认等于)</param>
    public static bool IsInRange<T>(this IComparable<T> value, T min, T max, bool minEqual = true, bool maxEqual = true)
        where T : IComparable
    {
        var flag = minEqual ? value.CompareTo(min) >= 0 : value.CompareTo(min) > 0;
        return flag && (maxEqual ? value.CompareTo(max) <= 0 : value.CompareTo(max) < 0);
    }

    #endregion 判断范围
}

/// <summary>
/// 属性信息
/// </summary>
public record CustomPropertyInfo
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public string? PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// 类型
    /// </summary>
    public string? PropertyType { get; set; } = string.Empty;

    /// <summary>
    /// 属性值
    /// </summary>
    public string? PropertyValue { get; init; } = string.Empty;
}

/// <summary>
/// 属性变化
/// </summary>
public record CustomPropertyVariance
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>
    /// 变化之前
    /// </summary>
    public string Before { get; init; } = string.Empty;

    /// <summary>
    /// 变化之后
    /// </summary>
    public string After { get; init; } = string.Empty;
}
