#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AttributeBasedValidator
// Guid:6f7a8b9c-0d1e-2f3a-4b5c-6d7e8f9a0b1c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/2 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Models;
using XiHan.Framework.Domain.Shared.Paging.Reflection;

namespace XiHan.Framework.Domain.Shared.Paging.Validators;

/// <summary>
/// 基于特性的验证器 - 根据实体类上的 Attribute 验证查询请求
/// </summary>
public static class AttributeBasedValidator
{
    /// <summary>
    /// 验证分页请求（基于实体类型的 Attribute）
    /// </summary>
    public static ValidationResult ValidatePageRequest<T>(PageRequestDtoBase request)
    {
        return ValidatePageRequest(typeof(T), request);
    }

    /// <summary>
    /// 验证分页请求（基于实体类型的 Attribute）
    /// </summary>
    public static ValidationResult ValidatePageRequest(Type entityType, PageRequestDtoBase request)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();

        // 1. 基础验证
        var baseValidation = PageValidator.ValidatePageRequest(request);
        if (!baseValidation.IsValid)
        {
            errors.AddRange(baseValidation.Errors);
        }

        // 2. 获取实体类型的查询字段信息
        var queryFields = AttributeReader.GetQueryFields(entityType);

        var q = request.QueryMetadata;
        foreach (var filter in q?.Filters ?? [])
        {
            var fieldValidation = ValidateFilter(entityType, queryFields, filter);
            if (!fieldValidation.IsValid)
            {
                errors.AddRange(fieldValidation.Errors);
            }
        }

        foreach (var sort in q?.Sorts ?? [])
        {
            var sortValidation = ValidateSort(entityType, queryFields, sort);
            if (!sortValidation.IsValid)
            {
                errors.AddRange(sortValidation.Errors);
            }
        }

        if (!string.IsNullOrWhiteSpace(q?.Keyword))
        {
            var keywordValidation = ValidateKeywordSearch(entityType, queryFields, q.KeywordFields ?? []);
            if (!keywordValidation.IsValid)
            {
                errors.AddRange(keywordValidation.Errors);
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// 验证过滤条件（基于 Attribute）
    /// </summary>
    private static ValidationResult ValidateFilter(Type entityType, Dictionary<string, QueryFieldInfo> queryFields,
        QueryFilter filter)
    {
        var errors = new List<string>();

        // 基础验证
        if (!filter.IsValid())
        {
            errors.Add($"过滤条件无效: 字段={filter.Field}, 操作符={filter.Operator}");
            return new ValidationResult(false, errors);
        }

        // 解析字段名（可能是别名）
        var actualFieldName = ResolveFieldName(queryFields, filter.Field);
        if (string.IsNullOrEmpty(actualFieldName))
        {
            errors.Add($"字段 '{filter.Field}' 不存在或不可用");
            return new ValidationResult(false, errors);
        }

        // 检查是否允许过滤
        if (!AttributeReader.IsFilterAllowed(entityType, actualFieldName))
        {
            errors.Add($"字段 '{filter.Field}' 不允许过滤");
        }

        // 检查操作符是否被支持
        if (!AttributeReader.IsOperatorSupported(entityType, actualFieldName, filter.Operator))
        {
            var fieldInfo = queryFields.GetValueOrDefault(actualFieldName);
            var supportedOps = fieldInfo != null
                ? string.Join(", ", fieldInfo.SupportedOperators)
                : "未知";
            errors.Add($"字段 '{filter.Field}' 不支持操作符 '{filter.Operator}'，支持的操作符: {supportedOps}");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// 验证排序条件（基于 Attribute）
    /// </summary>
    private static ValidationResult ValidateSort(Type entityType, Dictionary<string, QueryFieldInfo> queryFields,
        QuerySort sort)
    {
        var errors = new List<string>();

        // 基础验证
        if (!sort.IsValid())
        {
            errors.Add($"排序条件无效: 字段={sort.Field}");
            return new ValidationResult(false, errors);
        }

        // 解析字段名
        var actualFieldName = ResolveFieldName(queryFields, sort.Field);
        if (string.IsNullOrEmpty(actualFieldName))
        {
            errors.Add($"字段 '{sort.Field}' 不存在或不可用");
            return new ValidationResult(false, errors);
        }

        // 检查是否允许排序
        if (!AttributeReader.IsSortAllowed(entityType, actualFieldName))
        {
            errors.Add($"字段 '{sort.Field}' 不允许排序");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// 验证关键字搜索（基于 Attribute）
    /// </summary>
    private static ValidationResult ValidateKeywordSearch(Type entityType, Dictionary<string, QueryFieldInfo> queryFields,
        List<string> keywordFields)
    {
        var errors = new List<string>();

        if (keywordFields.Count == 0)
        {
            // 使用默认关键字字段
            var defaultFields = AttributeReader.GetDefaultKeywordFields(entityType);
            if (defaultFields.Count == 0)
            {
                errors.Add("未指定关键字搜索字段，且实体类型没有配置默认关键字搜索字段");
            }
        }
        else
        {
            // 验证指定的关键字字段
            foreach (var field in keywordFields)
            {
                var actualFieldName = ResolveFieldName(queryFields, field);
                if (string.IsNullOrEmpty(actualFieldName))
                {
                    errors.Add($"关键字搜索字段 '{field}' 不存在");
                    continue;
                }

                var fieldInfo = queryFields.GetValueOrDefault(actualFieldName);
                if (fieldInfo?.PropertyType != typeof(string))
                {
                    errors.Add($"关键字搜索字段 '{field}' 必须是字符串类型");
                }
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// 解析字段名（处理别名）
    /// </summary>
    private static string? ResolveFieldName(Dictionary<string, QueryFieldInfo> queryFields, string fieldName)
    {
        if (queryFields.TryGetValue(fieldName, out var fieldInfo))
        {
            return fieldInfo.PropertyName;
        }

        return null;
    }

    /// <summary>
    /// 获取字段的验证信息
    /// </summary>
    public static string GetFieldValidationInfo<T>(string fieldName)
    {
        return GetFieldValidationInfo(typeof(T), fieldName);
    }

    /// <summary>
    /// 获取字段的验证信息
    /// </summary>
    public static string GetFieldValidationInfo(Type entityType, string fieldName)
    {
        var queryFields = AttributeReader.GetQueryFields(entityType);
        if (!queryFields.TryGetValue(fieldName, out var fieldInfo))
        {
            return $"字段 '{fieldName}' 不存在";
        }

        var info = new List<string>
        {
            $"字段名: {fieldInfo.PropertyName}",
            $"类型: {fieldInfo.PropertyType.Name}",
            $"允许过滤: {fieldInfo.AllowFilter}",
            $"允许排序: {fieldInfo.AllowSort}",
            $"支持的操作符: {string.Join(", ", fieldInfo.SupportedOperators)}"
        };

        if (fieldInfo.KeywordSearchEnabled)
        {
            info.Add($"关键字搜索: 启用（匹配模式: {fieldInfo.KeywordMatchMode}）");
        }

        return string.Join("\n", info);
    }
}
