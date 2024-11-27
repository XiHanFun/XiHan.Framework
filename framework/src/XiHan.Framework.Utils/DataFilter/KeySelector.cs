#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:KeySelector
// Guid:03f8c708-54fe-4b89-9368-97ddbbda5b7a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/28 4:40:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using System.Reflection;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 属性选择器
/// </summary>
public static class KeySelector<T>
{
    /// <summary>
    /// 获取属性选择器
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public static Func<T, object> GetKeySelector(string propertyName)
    {
        var propertyInfo = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) ??
            throw new ArgumentException($"Property '{propertyName}' does not exist on type '{typeof(T).Name}'.");

        return item =>
        {
            var value = propertyInfo.GetValue(item);
            return value ?? throw new InvalidOperationException($"Property '{propertyName}' on type '{typeof(T).Name}' returned null.");
        };
    }

    /// <summary>
    /// 获取属性选择器表达式
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public static Expression<Func<T, object>> GetKeySelectorExpression(string propertyName)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyName);
        return Expression.Lambda<Func<T, object>>(Expression.Convert(property, typeof(object)), parameter);
    }
}
