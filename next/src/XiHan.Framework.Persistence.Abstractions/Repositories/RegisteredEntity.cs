// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Repositories;

/// <summary>
/// 一个以 <see cref="RepositoryServiceCollectionExtensions.AddRepository{TEntity, TKey}"/> 登记的实体。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RegisteredEntity
{
    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="keyType">标识类型。</param>
    /// <param name="connection">实体落点的命名连接；<see langword="null"/> 是 Provider 的缺省落点。</param>
    /// <exception cref="ArgumentException"><paramref name="connection"/> 为空白。</exception>
    public RegisteredEntity([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type entityType, Type keyType, string? connection = null)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        KeyType = keyType ?? throw new ArgumentNullException(nameof(keyType));
        Connection = connection is null || !string.IsNullOrWhiteSpace(connection) ? connection : throw new ArgumentException("The connection name must not be blank.", nameof(connection));
    }

    /// <summary>
    /// 实体类型。
    /// </summary>
    /// <remarks>
    /// 保留公开属性的元数据：实体目录据此解析索引与列。
    /// </remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public Type EntityType { get; }

    /// <summary>
    /// 标识类型。
    /// </summary>
    public Type KeyType { get; }

    /// <summary>
    /// 实体落点的命名连接。
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 是 Provider 的缺省落点。非空时实体在这个命名连接上读写、建表（SqlSugar 是客户端里同名 <c>ConfigId</c> 的连接）；普通工作单元里它照常参与整体事务。不支持命名连接的 Provider（EF Core、内存实现）在装配时抛出携带 <see cref="PersistenceErrors.NotSupported"/> 的异常。
    /// </remarks>
    public string? Connection { get; }
}
