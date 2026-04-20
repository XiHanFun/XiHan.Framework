using XiHan.Framework.Domain.Primitives.Entities;

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 表示领域层最基础的仓储契约。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
/// <typeparam name="TKey">主键类型。</typeparam>
public interface IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 根据主键获取实体。
    /// </summary>
    /// <param name="id">实体主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实体实例，不存在时返回 <see langword="null"/>。</returns>
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
}
