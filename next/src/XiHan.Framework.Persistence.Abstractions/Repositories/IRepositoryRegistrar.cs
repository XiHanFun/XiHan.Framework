// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Repositories;

/// <summary>
/// Provider 注册闭合泛型仓储的入口。
/// </summary>
/// <remarks>
/// 应用通过 <see cref="RepositoryServiceCollectionExtensions.AddRepository{TEntity, TKey}"/> 登记实体，Provider 的特性在配置阶段逐一调用本接口，因此换 Provider 不改业务代码，也不需要开放泛型或反射。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IRepositoryRegistrar
{
    /// <summary>
    /// 为实体注册 <see cref="IRepository{TEntity, TKey}"/>。
    /// </summary>
    void Register<TEntity, TKey>(IServiceCollection services)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>;
}
