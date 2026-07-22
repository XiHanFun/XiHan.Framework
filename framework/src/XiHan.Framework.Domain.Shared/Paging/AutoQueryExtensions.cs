// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Builders;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Executors;

namespace XiHan.Framework.Domain.Shared.Paging;

/// <summary>
/// 自动查询扩展方法
/// </summary>
public static class AutoQueryExtensions
{
    /// <summary>
    /// 自动构建查询请求（根据DTO属性类型和命名约定）
    /// </summary>
    /// <param name="dto">查询DTO</param>
    public static PageRequestDtoBase AutoBuild(this object dto)
    {
        return AutoQueryBuilder.BuildFrom(dto);
    }

    /// <summary>
    /// 自动查询并返回分页结果
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="query">查询源</param>
    /// <param name="dto">查询DTO</param>
    /// <param name="validate">是否验证（默认启用）</param>
    public static PageResultDtoBase<T> ToPageResultAuto<T>(
        this IQueryable<T> query,
        object dto,
        bool validate = true) where T : class
    {
        var request = dto.AutoBuild();
        return validate
            ? query.ToPageResultWithValidation(request)
            : query.ToPageResult(request);
    }

    /// <summary>
    /// 自动查询并返回分页结果（异步）
    /// </summary>
    public static async Task<PageResultDtoBase<T>> ToPageResultAutoAsync<T>(
        this IQueryable<T> query,
        object dto,
        bool validate = true,
        CancellationToken cancellationToken = default) where T : class
    {
        var request = dto.AutoBuild();

        if (validate)
        {
            var executor = new PageQueryExecutor<T>();
            return await executor.ExecuteAsync(query, request, validate, cancellationToken);
        }

        return await query.ToPageResultAsync(request, cancellationToken);
    }
}
