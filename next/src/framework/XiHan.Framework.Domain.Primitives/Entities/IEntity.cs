namespace XiHan.Framework.Domain.Primitives.Entities;

/// <summary>
/// 表示具有主键标识的领域实体。
/// </summary>
/// <typeparam name="TKey">主键类型。</typeparam>
public interface IEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 获取实体主键。
    /// </summary>
    TKey Id { get; }
}
