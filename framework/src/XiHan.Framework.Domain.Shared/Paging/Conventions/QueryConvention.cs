#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryConvention
// Guid:1b2c3d4e-5f6a-7b8c-9d0e-1f2a3b4c5d6e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/02 18:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Conventions;

/// <summary>
/// 查询约定配置 - 定义自动推断规则
/// </summary>
public class QueryConvention
{
    /// <summary>
    /// 默认约定
    /// </summary>
    public static QueryConvention Default { get; } = new();

    /// <summary>
    /// 范围查询的后缀（例如：CreateTimeRange, AmountRange）
    /// </summary>
    public List<string> RangeSuffixes { get; set; } = ["Range"];

    /// <summary>
    /// 列表查询的后缀（例如：StatusList, UserIds）
    /// </summary>
    public List<string> ListSuffixes { get; set; } = ["List", "Ids", "s"];

    /// <summary>
    /// 关键字搜索的前缀或后缀
    /// </summary>
    public List<string> KeywordPatterns { get; set; } = ["Search", "Key", "Keyword"];

    /// <summary>
    /// 字符串类型默认支持模糊搜索
    /// </summary>
    public bool StringDefaultContains { get; set; } = true;

    /// <summary>
    /// 字符串类型默认参与关键字搜索
    /// </summary>
    public bool StringDefaultKeywordSearch { get; set; } = false;

    /// <summary>
    /// 数组长度为2时自动识别为范围查询
    /// </summary>
    public bool ArrayAsBetween { get; set; } = true;

    /// <summary>
    /// List类型自动识别为In查询
    /// </summary>
    public bool ListAsIn { get; set; } = true;

    /// <summary>
    /// 可空类型支持 IsNull/IsNotNull
    /// </summary>
    public bool NullableSupportsNullCheck { get; set; } = true;

    /// <summary>
    /// 判断属性名是否表示范围查询
    /// </summary>
    public bool IsRangeProperty(string propertyName)
    {
        return RangeSuffixes.Any(suffix =>
            propertyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判断属性名是否表示列表查询
    /// </summary>
    public bool IsListProperty(string propertyName)
    {
        return ListSuffixes.Any(suffix =>
            propertyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判断属性名是否表示关键字搜索
    /// </summary>
    public bool IsKeywordProperty(string propertyName)
    {
        return KeywordPatterns.Any(pattern =>
            propertyName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取实际字段名（去除后缀）
    /// </summary>
    public string GetActualFieldName(string propertyName)
    {
        // 去除 Range 后缀
        foreach (var suffix in RangeSuffixes)
        {
            if (propertyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return propertyName[..^suffix.Length];
            }
        }

        // 去除 List 后缀
        foreach (var suffix in ListSuffixes.Where(s => s.Length > 1))
        {
            if (propertyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return propertyName[..^suffix.Length];
            }
        }

        return propertyName;
    }

    /// <summary>
    /// 根据类型推断默认的查询操作符
    /// </summary>
    public List<QueryOperator> InferOperators(Type propertyType, string propertyName)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        // 范围查询
        if (IsRangeProperty(propertyName))
        {
            return [QueryOperator.Between];
        }

        // 列表查询
        if (IsListProperty(propertyName) || IsCollectionType(propertyType))
        {
            return [QueryOperator.In, QueryOperator.NotIn];
        }

        // 字符串类型
        if (underlyingType == typeof(string))
        {
            return StringDefaultContains
                ? [QueryOperator.Equal, QueryOperator.NotEqual, QueryOperator.Contains, QueryOperator.StartsWith,
                    QueryOperator.EndsWith, QueryOperator.In, QueryOperator.NotIn]
                : [QueryOperator.Equal, QueryOperator.NotEqual, QueryOperator.In, QueryOperator.NotIn];
        }

        // 数值类型
        if (IsNumericType(underlyingType))
        {
            return
            [
                QueryOperator.Equal, QueryOperator.NotEqual,
                QueryOperator.GreaterThan, QueryOperator.GreaterThanOrEqual,
                QueryOperator.LessThan, QueryOperator.LessThanOrEqual,
                QueryOperator.Between, QueryOperator.In, QueryOperator.NotIn
            ];
        }

        // 日期时间类型
        if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
        {
            return
            [
                QueryOperator.Equal, QueryOperator.NotEqual,
                QueryOperator.GreaterThan, QueryOperator.GreaterThanOrEqual,
                QueryOperator.LessThan, QueryOperator.LessThanOrEqual,
                QueryOperator.Between
            ];
        }

        // 布尔类型
        if (underlyingType == typeof(bool))
        {
            return [QueryOperator.Equal, QueryOperator.NotEqual];
        }

        // 枚举类型
        if (underlyingType.IsEnum)
        {
            return [QueryOperator.Equal, QueryOperator.NotEqual, QueryOperator.In, QueryOperator.NotIn];
        }

        // 默认
        return [QueryOperator.Equal, QueryOperator.NotEqual];
    }

    /// <summary>
    /// 判断是否应该参与关键字搜索
    /// </summary>
    public bool ShouldIncludeInKeywordSearch(Type propertyType, string propertyName)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        // 只有字符串类型参与关键字搜索
        if (underlyingType != typeof(string))
        {
            return false;
        }

        // 如果属性名包含关键字模式，不参与（这个是搜索输入字段）
        if (IsKeywordProperty(propertyName))
        {
            return false;
        }

        // 根据配置决定
        return StringDefaultKeywordSearch;
    }

    /// <summary>
    /// 判断是否为集合类型
    /// </summary>
    private static bool IsCollectionType(Type type)
    {
        if (type.IsArray)
        {
            return true;
        }

        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            return genericType == typeof(List<>) ||
                   genericType == typeof(IList<>) ||
                   genericType == typeof(ICollection<>) ||
                   genericType == typeof(IEnumerable<>);
        }

        return false;
    }

    /// <summary>
    /// 判断是否为数值类型
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }
}
