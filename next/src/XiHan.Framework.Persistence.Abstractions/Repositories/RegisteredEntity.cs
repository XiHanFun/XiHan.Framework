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
    public RegisteredEntity([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type entityType, Type keyType)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        KeyType = keyType ?? throw new ArgumentNullException(nameof(keyType));
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
}
