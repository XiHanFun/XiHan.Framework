#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageValidator
// Guid:de8c54f0-4a83-4d27-9af1-037ec12ed429
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/02 15:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

        if (meta.PageIndex < PageRequestMetadata.DefaultPageIndex)
        {
            errors.Add($"页码不能小于 {PageRequestMetadata.DefaultPageIndex}");
        }

        if (meta.PageSize < PageRequestMetadata.MinPageSize)
        {
            errors.Add($"每页大小不能小于 {PageRequestMetadata.MinPageSize}");
        }

        if (meta.PageSize > PageRequestMetadata.MaxPageSize)
        {
            errors.Add($"每页大小不能大于 {PageRequestMetadata.MaxPageSize}");
        }

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

        var errors = new List<string>();

        if (metadata.PageIndex < PageRequestMetadata.DefaultPageIndex)
        {
            errors.Add($"页码不能小于 {PageRequestMetadata.DefaultPageIndex}");
        }

        if (metadata.PageSize < PageRequestMetadata.MinPageSize)
        {
            errors.Add($"每页大小不能小于 {PageRequestMetadata.MinPageSize}");
        }

        if (metadata.PageSize > PageRequestMetadata.MaxPageSize)
        {
            errors.Add($"每页大小不能大于 {PageRequestMetadata.MaxPageSize}");
        }

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
