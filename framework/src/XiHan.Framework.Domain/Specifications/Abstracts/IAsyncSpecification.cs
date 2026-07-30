// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Specifications.Abstracts;

/// <summary>
/// 异步规约接口
/// 支持异步验证逻辑的规约模式
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public interface IAsyncSpecification<T> : ISpecification<T>
{
    /// <summary>
    /// 异步检查实体是否满足规约
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果满足返回 true，否则返回 false</returns>
    Task<bool> IsSatisfiedByAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步与运算
    /// </summary>
    /// <param name="specification">另一个规约</param>
    /// <returns>组合后的异步规约</returns>
    IAsyncSpecification<T> AndAsync(IAsyncSpecification<T> specification);

    /// <summary>
    /// 异步或运算
    /// </summary>
    /// <param name="specification">另一个规约</param>
    /// <returns>组合后的异步规约</returns>
    IAsyncSpecification<T> OrAsync(IAsyncSpecification<T> specification);

    /// <summary>
    /// 异步非运算
    /// </summary>
    /// <returns>取反后的异步规约</returns>
    IAsyncSpecification<T> NotAsync();
}
