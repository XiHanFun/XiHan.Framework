#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AttributePageExtensions
// Guid:8b9c0d1e-2f3a-4b5c-6d7e-8f9a0b1c2d3e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/2 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Executors;
using XiHan.Framework.Domain.Shared.Paging.Validators;

namespace XiHan.Framework.Domain.Shared.Paging;

/// <summary>
/// 基于 Attribute 的分页扩展方法
/// </summary>
public static class AttributePageExtensions
{
    /// <summary>
    /// 应用分页查询（自动根据实体类型的 Attribute 进行验证和处理）
    /// </summary>
    /// <param name="query">查询源</param>
    /// <param name="request">分页请求</param>
    /// <param name="validate">是否验证请求（默认启用）</param>
    public static PageResultDtoBase<T> ToPageResultWithValidation<T>(
        this IQueryable<T> query,
        PageRequestDtoBase request,
        bool validate = true) where T : class
    {
        var executor = new PageQueryExecutor<T>();
        return executor.Execute(query, request, validate);
    }

    /// <summary>
    /// 应用分页查询（异步，自动根据实体类型的 Attribute 进行验证和处理）
    /// </summary>
    public static async Task<PageResultDtoBase<T>> ToPageResultWithValidationAsync<T>(
        this IQueryable<T> query,
        PageRequestDtoBase request,
        bool validate = true,
        CancellationToken cancellationToken = default) where T : class
    {
        var executor = new PageQueryExecutor<T>();
        return await executor.ExecuteAsync(query, request, validate, cancellationToken);
    }

    /// <summary>
    /// 验证分页请求（基于实体类型的 Attribute）
    /// </summary>
    public static ValidationResult ValidateRequest<T>(this PageRequestDtoBase request)
    {
        return AttributeBasedValidator.ValidatePageRequest<T>(request);
    }

    /// <summary>
    /// 验证分页请求并抛出异常（如果验证失败）
    /// </summary>
    public static PageRequestDtoBase EnsureValid<T>(this PageRequestDtoBase request)
    {
        var validationResult = AttributeBasedValidator.ValidatePageRequest<T>(request);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"分页请求验证失败: {validationResult.GetErrorMessage()}");
        }

        return request;
    }

    /// <summary>
    /// 尝试验证分页请求
    /// </summary>
    public static bool TryValidate<T>(this PageRequestDtoBase request, out ValidationResult? validationResult)
    {
        validationResult = AttributeBasedValidator.ValidatePageRequest<T>(request);
        return validationResult.IsValid;
    }
}
