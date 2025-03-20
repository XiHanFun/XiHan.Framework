#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PropertyInfoExtensions
// Guid:3db8aa54-c08d-43bf-a55d-0da2bc860ef5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 1:09:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using System.Reflection;
using XiHan.Framework.Utils.Text;

namespace XiHan.Framework.Utils.Reflections;

/// <summary>
/// 属性信息扩展方法
/// </summary>
public static class PropertyInfoExtensions
{
    /// <summary>
    /// 返回当前属性信息是否为 virtual
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    public static bool IsVirtual(this PropertyInfo property)
    {
        var accessor = property.GetAccessors().FirstOrDefault();
        return accessor is { IsVirtual: true, IsFinal: false };
    }

    /// <summary>
    /// 从泛型委托获取属性名
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="keySelector"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static string GetPropertyName<T>(this Expression<Func<T, object>> keySelector)
    {
        if (keySelector.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        if (keySelector.Body is UnaryExpression unaryExpression)
        {
            if (unaryExpression.Operand is MemberExpression operand)
            {
                return operand.Member.Name;
            }
        }

        throw new InvalidOperationException("无法从键选择器中获取属性名称。");
    }

    /// <summary>
    /// 获取属性
    /// </summary>
    /// <param name="type"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static PropertyInfo GetPropertyInfo(this Type type, string propertyName)
    {
        var strategies = new Func<string, string>[]
        {
            // 原始名称
            name => name,
            // 驼峰命名
            name => name.ToPascalCase(),
            // 蛇形命名
            name => name.ToSnakeCase(),
            // 短横线命名
            name => name.ToKebabCase()
        };

        foreach (var strategy in strategies)
        {
            var transformedName = strategy(propertyName);
            var property = type.GetProperty(transformedName);
            if (property is not null)
            {
                return property;
            }
        }

        throw new ArgumentException($"在类型 {type.Name} 中没有发现属性 {propertyName}。");
    }
}
