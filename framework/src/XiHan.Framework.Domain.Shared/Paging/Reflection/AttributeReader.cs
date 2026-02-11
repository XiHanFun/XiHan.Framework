#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AttributeReader
// Guid:5e6f7a8b-9c0d-1e2f-3a4b-5c6d7e8f9a0b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/02 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Domain.Shared.Paging.Attributes;
using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Reflection;

/// <summary>
/// 特性读取器 - 读取实体类上的分页相关特性
/// </summary>
public static class AttributeReader
{
    /// <summary>
    /// 获取类型的查询字段信息
    /// </summary>
    public static Dictionary<string, QueryFieldInfo> GetQueryFields<T>()
    {
        return GetQueryFields(typeof(T));
    }

    /// <summary>
    /// 获取类型的查询字段信息
    /// </summary>
    public static Dictionary<string, QueryFieldInfo> GetQueryFields(Type type)
    {
        var result = new Dictionary<string, QueryFieldInfo>(StringComparer.OrdinalIgnoreCase);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var fieldInfo = GetQueryFieldInfo(property);
            if (fieldInfo != null)
            {
                result[property.Name] = fieldInfo;

                // 如果有别名，也添加别名映射
                if (!string.IsNullOrWhiteSpace(fieldInfo.Alias))
                {
                    result[fieldInfo.Alias] = fieldInfo;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 获取属性的查询字段信息
    /// </summary>
    private static QueryFieldInfo? GetQueryFieldInfo(PropertyInfo property)
    {
        var queryField = property.GetCustomAttribute<QueryFieldAttribute>();
        var keywordSearch = property.GetCustomAttribute<KeywordSearchAttribute>();
        var operatorSupport = property.GetCustomAttribute<QueryOperatorSupportAttribute>();

        // 如果没有任何特性，返回默认配置
        var fieldInfo = new QueryFieldInfo
        {
            PropertyName = property.Name,
            PropertyType = property.PropertyType,
            Alias = queryField?.Alias ?? string.Empty,
            AllowFilter = queryField?.AllowFilter ?? true,
            AllowSort = queryField?.AllowSort ?? true,
            Priority = queryField?.Priority ?? 0
        };

        // 关键字搜索配置
        if (keywordSearch != null)
        {
            fieldInfo.KeywordSearchEnabled = keywordSearch.Enabled;
            fieldInfo.KeywordMatchMode = keywordSearch.MatchMode;
            fieldInfo.KeywordPriority = keywordSearch.Priority;
            fieldInfo.IncludeInDefaultKeywordSearch = keywordSearch.IncludeInDefault;
            fieldInfo.KeywordAlias = keywordSearch.Alias;
        }

        // 支持的操作符
        if (operatorSupport != null)
        {
            fieldInfo.SupportedOperators = [.. operatorSupport.SupportedOperators];
        }
        else
        {
            // 根据属性类型推断默认支持的操作符
            fieldInfo.SupportedOperators = GetDefaultSupportedOperators(property.PropertyType);
        }

        return fieldInfo;
    }

    /// <summary>
    /// 根据类型推断默认支持的操作符
    /// </summary>
    private static List<QueryOperator> GetDefaultSupportedOperators(Type propertyType)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        // 字符串类型
        if (underlyingType == typeof(string))
        {
            return
            [
                QueryOperator.Equal,
                QueryOperator.NotEqual,
                QueryOperator.Contains,
                QueryOperator.StartsWith,
                QueryOperator.EndsWith,
                QueryOperator.In,
                QueryOperator.NotIn,
                QueryOperator.IsNull,
                QueryOperator.IsNotNull
            ];
        }

        // 数值类型
        if (underlyingType.IsNumericType())
        {
            return
            [
                QueryOperator.Equal,
                QueryOperator.NotEqual,
                QueryOperator.GreaterThan,
                QueryOperator.GreaterThanOrEqual,
                QueryOperator.LessThan,
                QueryOperator.LessThanOrEqual,
                QueryOperator.In,
                QueryOperator.NotIn,
                QueryOperator.Between
            ];
        }

        // 日期时间类型
        if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
        {
            return
            [
                QueryOperator.Equal,
                QueryOperator.NotEqual,
                QueryOperator.GreaterThan,
                QueryOperator.GreaterThanOrEqual,
                QueryOperator.LessThan,
                QueryOperator.LessThanOrEqual,
                QueryOperator.Between
            ];
        }

        // 布尔类型
        if (underlyingType == typeof(bool))
        {
            return
            [
                QueryOperator.Equal,
                QueryOperator.NotEqual
            ];
        }

        // 默认支持基础比较
        return
        [
            QueryOperator.Equal,
            QueryOperator.NotEqual,
            QueryOperator.IsNull,
            QueryOperator.IsNotNull
        ];
    }

    /// <summary>
    /// 获取默认关键字搜索字段
    /// </summary>
    public static List<string> GetDefaultKeywordFields<T>()
    {
        return GetDefaultKeywordFields(typeof(T));
    }

    /// <summary>
    /// 获取默认关键字搜索字段
    /// </summary>
    public static List<string> GetDefaultKeywordFields(Type type)
    {
        var fields = new List<string>();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var keywordSearch = property.GetCustomAttribute<KeywordSearchAttribute>();
            if (keywordSearch != null && keywordSearch.Enabled && keywordSearch.IncludeInDefault)
            {
                fields.Add(property.Name);
            }
        }

        return [.. fields.OrderBy(f => GetKeywordPriority(type.GetProperty(f)!))];
    }

    /// <summary>
    /// 获取关键字搜索优先级
    /// </summary>
    private static int GetKeywordPriority(PropertyInfo property)
    {
        var keywordSearch = property.GetCustomAttribute<KeywordSearchAttribute>();
        return keywordSearch?.Priority ?? int.MaxValue;
    }

    /// <summary>
    /// 验证字段是否允许过滤
    /// </summary>
    public static bool IsFilterAllowed<T>(string fieldName)
    {
        return IsFilterAllowed(typeof(T), fieldName);
    }

    /// <summary>
    /// 验证字段是否允许过滤
    /// </summary>
    public static bool IsFilterAllowed(Type type, string fieldName)
    {
        var property = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property == null)
        {
            return false;
        }

        var queryField = property.GetCustomAttribute<QueryFieldAttribute>();
        return queryField?.AllowFilter ?? true;
    }

    /// <summary>
    /// 验证字段是否允许排序
    /// </summary>
    public static bool IsSortAllowed<T>(string fieldName)
    {
        return IsSortAllowed(typeof(T), fieldName);
    }

    /// <summary>
    /// 验证字段是否允许排序
    /// </summary>
    public static bool IsSortAllowed(Type type, string fieldName)
    {
        var property = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property == null)
        {
            return false;
        }

        var queryField = property.GetCustomAttribute<QueryFieldAttribute>();
        return queryField?.AllowSort ?? true;
    }

    /// <summary>
    /// 验证操作符是否被支持
    /// </summary>
    public static bool IsOperatorSupported<T>(string fieldName, QueryOperator @operator)
    {
        return IsOperatorSupported(typeof(T), fieldName, @operator);
    }

    /// <summary>
    /// 验证操作符是否被支持
    /// </summary>
    public static bool IsOperatorSupported(Type type, string fieldName, QueryOperator @operator)
    {
        var property = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property == null)
        {
            return false;
        }

        var operatorSupport = property.GetCustomAttribute<QueryOperatorSupportAttribute>();
        if (operatorSupport != null)
        {
            return operatorSupport.IsSupported(@operator);
        }

        // 使用默认推断的操作符
        var defaultOperators = GetDefaultSupportedOperators(property.PropertyType);
        return defaultOperators.Contains(@operator);
    }
}

/// <summary>
/// 查询字段信息
/// </summary>
public class QueryFieldInfo
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// 属性类型
    /// </summary>
    public Type PropertyType { get; set; } = typeof(object);

    /// <summary>
    /// 别名
    /// </summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// 是否允许过滤
    /// </summary>
    public bool AllowFilter { get; set; } = true;

    /// <summary>
    /// 是否允许排序
    /// </summary>
    public bool AllowSort { get; set; } = true;

    /// <summary>
    /// 优先级
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 是否启用关键字搜索
    /// </summary>
    public bool KeywordSearchEnabled { get; set; }

    /// <summary>
    /// 关键字匹配模式
    /// </summary>
    public KeywordMatchMode KeywordMatchMode { get; set; } = KeywordMatchMode.Contains;

    /// <summary>
    /// 关键字搜索优先级
    /// </summary>
    public int KeywordPriority { get; set; }

    /// <summary>
    /// 是否参与默认关键字搜索
    /// </summary>
    public bool IncludeInDefaultKeywordSearch { get; set; }

    /// <summary>
    /// 关键字搜索别名
    /// </summary>
    public string KeywordAlias { get; set; } = string.Empty;

    /// <summary>
    /// 支持的操作符
    /// </summary>
    public List<QueryOperator> SupportedOperators { get; set; } = [];
}

/// <summary>
/// 类型扩展方法
/// </summary>
internal static class TypeExtensions
{
    /// <summary>
    /// 判断是否为数值类型
    /// </summary>
    public static bool IsNumericType(this Type type)
    {
        return type == typeof(byte) ||
               type == typeof(sbyte) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(int) ||
               type == typeof(uint) ||
               type == typeof(long) ||
               type == typeof(ulong) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(decimal);
    }
}
