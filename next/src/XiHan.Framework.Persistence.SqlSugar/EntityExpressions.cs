// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 仓储用到的表达式。
/// </summary>
/// <remarks>
/// 按属性名访问实体类上的属性，表达式树里不出现接口转换，SqlSugar 的表达式解析器能按列名翻译。
/// </remarks>
internal static class EntityExpressions
{
    /// <summary>
    /// <c>entity => entity.Id == id</c>。
    /// </summary>
    public static Expression<Func<TEntity, bool>> IdEquals<TEntity, TKey>(TKey id)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        return Expression.Lambda<Func<TEntity, bool>>(Expression.Equal(Expression.Property(entity, nameof(IEntity<>.Id)), Expression.Constant(id, typeof(TKey))), entity);
    }

    /// <summary>
    /// <c>entity => entity.Id</c>。
    /// </summary>
    public static Expression<Func<TEntity, TKey>> Id<TEntity, TKey>()
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        return Expression.Lambda<Func<TEntity, TKey>>(Expression.Property(entity, nameof(IEntity<>.Id)), entity);
    }

    /// <summary>
    /// <c>entity => entity.IsDeleted == false</c>。
    /// </summary>
    /// <remarks>
    /// 用相等而不是取反，翻译成参数化的比较在各数据库上一致。
    /// </remarks>
    public static Expression<Func<TEntity, bool>> NotDeleted<TEntity>()
        where TEntity : class
    {
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        return Expression.Lambda<Func<TEntity, bool>>(Expression.Equal(Expression.Property(entity, nameof(ISoftDeletable.IsDeleted)), Expression.Constant(false)), entity);
    }

    /// <summary>
    /// <c>entity => entity.TenantId == tenantId</c>。
    /// </summary>
    public static Expression<Func<TEntity, bool>> TenantIs<TEntity>(string tenantId)
        where TEntity : class
    {
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        return Expression.Lambda<Func<TEntity, bool>>(Expression.Equal(Expression.Property(entity, nameof(IMultiTenant.TenantId)), Expression.Constant(tenantId, typeof(string))), entity);
    }
}
