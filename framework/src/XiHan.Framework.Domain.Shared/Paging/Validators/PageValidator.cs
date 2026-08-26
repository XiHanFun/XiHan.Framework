// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Paging.Validators;

/// <summary>
/// 分页验证器
/// </summary>
public static class PageValidator
{
    /// <summary>
    /// 验证分页请求
    /// </summary>
    public static ValidationResult ValidatePageRequest(PageRequestDtoBase request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var meta = request.Page;
        var cond = request.Conditions;

        // 页码/页大小越界由 PageRequestMetadata 的 setter 静默夹取（1..500），
        // 这里无需也无法再触发边界错误，验证器只负责过滤/排序/关键字语义。
        for (var i = 0; i < cond.Filters.Count; i++)
        {
            var filter = cond.Filters[i];
            if (!filter.IsValid())
            {
                errors.Add($"过滤条件 [{i}] 无效: 字段名={filter.Field}, 操作符={filter.Operator}");
            }
        }

        for (var i = 0; i < cond.Sorts.Count; i++)
        {
            var sort = cond.Sorts[i];
            if (!sort.IsValid())
            {
                errors.Add($"排序条件 [{i}] 无效: 字段名={sort.Field}");
            }
        }

        if (!string.IsNullOrWhiteSpace(cond.Keyword?.Value) && (cond.Keyword?.Fields?.Count ?? 0) == 0)
        {
            errors.Add("指定了关键字但未指定搜索字段");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// 验证分页元数据
    /// </summary>
    public static ValidationResult ValidatePageMetadata(PageRequestMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        // 边界归一化在 PageRequestMetadata setter 完成，此处只返回语义有效的空结果。
        var errors = new List<string>();

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// 验证查询过滤条件
    /// </summary>
    public static ValidationResult ValidateFilter(QueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(filter.Field))
        {
            errors.Add("字段名不能为空");
        }

        if (!filter.IsValid())
        {
            errors.Add($"过滤条件无效: 字段={filter.Field}, 操作符={filter.Operator}, 值={filter.Value}");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// 验证查询排序条件
    /// </summary>
    public static ValidationResult ValidateSort(QuerySort sort)
    {
        ArgumentNullException.ThrowIfNull(sort);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(sort.Field))
        {
            errors.Add("字段名不能为空");
        }

        if (!sort.IsValid())
        {
            errors.Add($"排序条件无效: 字段={sort.Field}");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}
