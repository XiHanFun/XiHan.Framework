#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:KeywordSearchParser
// Guid:8f2c4a7e-9d1b-4e3a-b8c6-5e7f8a9b0c1d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/02 03:17:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;

namespace XiHan.Framework.Domain.Shared.Paging.Handlers;

/// <summary>
/// 关键字搜索解析器
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public static class KeywordSearchParser<T>
{
    /// <summary>
    /// 获取关键字搜索表达式
    /// </summary>
    /// <param name="keyword">搜索关键字</param>
    /// <returns>生成的 Lambda 表达式，如果关键字为空或没有字符串属性则返回 null</returns>
    public static Expression<Func<T, bool>>? GetKeywordSearchParser(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        // 获取所有字符串类型的属性
        var stringProperties = typeof(T)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.CanRead)
            .ToList();

        if (stringProperties.Count == 0)
        {
            return null;
        }

        // 构建关键字搜索表达式（对所有字符串属性进行 OR 查询）
        var param = Expression.Parameter(typeof(T), "x");
        Expression? combinedExpression = null;

        foreach (var property in stringProperties)
        {
            var propertyAccess = Expression.Property(param, property);
            // 检查属性是否为 null
            var notNull = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
            // 调用 Contains 方法
            var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)])!;
            var containsCall = Expression.Call(propertyAccess, containsMethod, Expression.Constant(keyword));
            // 组合：属性不为 null 且包含关键字
            var condition = Expression.AndAlso(notNull, containsCall);

            combinedExpression = combinedExpression == null
                ? condition
                : Expression.OrElse(combinedExpression, condition);
        }

        return combinedExpression != null
            ? Expression.Lambda<Func<T, bool>>(combinedExpression, param)
            : null;
    }
}
