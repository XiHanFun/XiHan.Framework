#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AutoQueryBuilder
// Guid:2c3d4e5f-6a7b-8c9d-0e1f-2a3b4c5d6e7f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/2 18:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Domain.Shared.Paging.Attributes;
using XiHan.Framework.Domain.Shared.Paging.Conventions;
using XiHan.Framework.Domain.Shared.Paging.Dtos;

namespace XiHan.Framework.Domain.Shared.Paging.Builders;

/// <summary>
/// 自动查询构建器 - 根据DTO属性自动构建查询条件
/// </summary>
public class AutoQueryBuilder
{
    private readonly QueryConvention _convention;
    private readonly object _dto;
    private readonly Type _dtoType;
    private readonly QueryBuilder _queryBuilder;

    /// <summary>
    /// 构造函数
    /// </summary>
    public AutoQueryBuilder(object dto, QueryConvention? convention = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _dto = dto;
        _dtoType = dto.GetType();
        _convention = convention ?? QueryConvention.Default;
        _queryBuilder = QueryBuilder.Create();
    }

    /// <summary>
    /// 从DTO创建自动查询构建器
    /// </summary>
    public static AutoQueryBuilder FromDto(object dto, QueryConvention? convention = null)
    {
        return new AutoQueryBuilder(dto, convention);
    }

    /// <summary>
    /// 从DTO自动构建查询请求
    /// </summary>
    public static PageRequestDtoBase BuildFrom(object dto, QueryConvention? convention = null)
    {
        return new AutoQueryBuilder(dto, convention).Build();
    }

    /// <summary>
    /// 自动构建查询请求
    /// </summary>
    public PageRequestDtoBase Build()
    {
        // 1. 处理分页参数
        ProcessPagingProperties();

        // 2. 收集关键字搜索字段
        var keywordFields = CollectKeywordFields();

        // 3. 处理查询条件
        ProcessQueryProperties(keywordFields);

        return _queryBuilder.Build();
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
    /// 处理分页属性
    /// </summary>
    private void ProcessPagingProperties()
    {
        // 尝试从 Page 嵌套对象获取
        var pageProp = _dtoType.GetProperty("Page", BindingFlags.Public | BindingFlags.Instance);
        if (pageProp != null && pageProp.PropertyType.Name == "PageRequestMetadata")
        {
            var pageObj = pageProp.GetValue(_dto);
            if (pageObj != null)
            {
                var pageType = pageObj.GetType();
                var pageIndexProp = pageType.GetProperty("PageIndex");
                var pageSizeProp = pageType.GetProperty("PageSize");

                if (pageIndexProp != null && pageIndexProp.PropertyType == typeof(int))
                {
                    var pageIndex = (int)pageIndexProp.GetValue(pageObj)!;
                    _queryBuilder.SetPageIndex(pageIndex);
                }

                if (pageSizeProp != null && pageSizeProp.PropertyType == typeof(int))
                {
                    var pageSize = (int)pageSizeProp.GetValue(pageObj)!;
                    _queryBuilder.SetPageSize(pageSize);
                }

                return;
            }
        }
    }

    /// <summary>
    /// 收集关键字搜索字段
    /// </summary>
    private List<string> CollectKeywordFields()
    {
        var keywordFields = new List<string>();
        var properties = _dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            // 检查是否有手动配置的 KeywordSearchAttribute
            var keywordAttr = property.GetCustomAttribute<KeywordSearchAttribute>();
            if (keywordAttr != null)
            {
                if (keywordAttr.Enabled && keywordAttr.IncludeInDefault)
                {
                    keywordFields.Add(property.Name);
                }

                continue;
            }

            // 根据约定自动判断
            if (_convention.ShouldIncludeInKeywordSearch(property.PropertyType, property.Name))
            {
                keywordFields.Add(property.Name);
            }
        }

        return keywordFields;
    }

    /// <summary>
    /// 处理查询属性
    /// </summary>
    private void ProcessQueryProperties(List<string> keywordFields)
    {
        var properties = _dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        string? keywordValue = null;
        var keywordProperty = properties.FirstOrDefault(p => _convention.IsKeywordProperty(p.Name));

        // 获取关键字值
        if (keywordProperty != null)
        {
            var value = keywordProperty.GetValue(_dto);
            if (value is string str && !string.IsNullOrWhiteSpace(str))
            {
                keywordValue = str;
            }
        }

        foreach (var property in properties)
        {
            // 跳过分页属性
            if (property.Name is "PageIndex" or "PageSize")
            {
                continue;
            }

            // 跳过 PageRequestDtoBase 的内置属性
            if (property.Name is "Conditions" or "Behavior" or "Page")
            {
                continue;
            }

            // 跳过关键字搜索输入字段
            if (_convention.IsKeywordProperty(property.Name))
            {
                continue;
            }

            // 获取属性值
            var value = property.GetValue(_dto);
            if (value == null)
            {
                continue;
            }

            // 检查是否有手动配置的 QueryFieldAttribute
            var queryFieldAttr = property.GetCustomAttribute<QueryFieldAttribute>();
            if (queryFieldAttr != null && !queryFieldAttr.AllowFilter)
            {
                continue;
            }

            // 处理该属性
            ProcessProperty(property, value);
        }

        // 设置关键字搜索
        if (!string.IsNullOrWhiteSpace(keywordValue) && keywordFields.Count > 0)
        {
            _queryBuilder.SetKeyword(keywordValue);
            _queryBuilder.AddKeywordField([.. keywordFields]);
        }
    }

    /// <summary>
    /// 处理单个属性
    /// </summary>
    private void ProcessProperty(PropertyInfo property, object value)
    {
        var propertyType = property.PropertyType;
        var propertyName = property.Name;

        // 获取实际字段名（去除Range、List等后缀）
        var actualFieldName = _convention.GetActualFieldName(propertyName);

        // 范围查询（数组类型且长度为2）
        if (_convention.IsRangeProperty(propertyName))
        {
            if (TryProcessRangeProperty(actualFieldName, value))
            {
                return;
            }
        }

        // 列表查询（List/数组类型）
        if (_convention.IsListProperty(propertyName) || IsCollectionType(propertyType))
        {
            if (TryProcessListProperty(actualFieldName, value, propertyType))
            {
                return;
            }
        }

        // 字符串类型
        if (propertyType == typeof(string))
        {
            var strValue = (string)value;
            if (!string.IsNullOrWhiteSpace(strValue))
            {
                // 默认使用 Contains 进行模糊搜索
                if (_convention.StringDefaultContains)
                {
                    _queryBuilder.WhereContains(actualFieldName, strValue);
                }
                else
                {
                    _queryBuilder.WhereEqual(actualFieldName, strValue);
                }
            }

            return;
        }

        // 可空类型
        var underlyingType = Nullable.GetUnderlyingType(propertyType);
        if (underlyingType != null)
        {
            // 对于可空类型，使用精确匹配
            _queryBuilder.WhereEqual(actualFieldName, value);
            return;
        }

        // 其他类型（枚举、数值、布尔等）
        _queryBuilder.WhereEqual(actualFieldName, value);
    }

    /// <summary>
    /// 处理范围查询
    /// </summary>
    private bool TryProcessRangeProperty(string fieldName, object value)
    {
        if (value is Array array && array.Length == 2)
        {
            var start = array.GetValue(0);
            var end = array.GetValue(1);

            if (start != null && end != null)
            {
                _queryBuilder.WhereBetween(fieldName, start, end);
                return true;
            }
        }

        if (value is IList<DateTime> dateList && dateList.Count == 2)
        {
            var startDate = dateList[0];
            var endDate = dateList[1];
            if (startDate != default && endDate != default)
            {
                _queryBuilder.WhereBetween(fieldName, startDate, endDate);
                return true;
            }
        }

        if (value is IList<DateTime?> nullableDateList && nullableDateList.Count == 2)
        {
            var startDate = nullableDateList[0];
            var endDate = nullableDateList[1];
            if (startDate.HasValue && endDate.HasValue)
            {
                _queryBuilder.WhereBetween(fieldName, startDate.Value, endDate.Value);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 处理列表查询
    /// </summary>
    private bool TryProcessListProperty(string fieldName, object value, Type propertyType)
    {
        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            var items = new List<object>();
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    items.Add(item);
                }
            }

            if (items.Count > 0)
            {
                _queryBuilder.WhereIn(fieldName, [.. items]);
                return true;
            }
        }

        return false;
    }
}
