// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 按实体协议配置模型。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ModelBuilderExtensions
{
    /// <summary>
    /// 对已注册的每个实体。
    /// </summary>
    /// <remarks>
    /// <see cref="IHasConcurrencyStamp"/> 的并发戳设为并发令牌（长度 32）；<see cref="IMultiTenant"/> 的租户与 <see cref="ISoftDeletable"/> 的删除标记建索引；实体上的 <see cref="EntityIndexAttribute"/> 映射为 <c>HasIndex</c>（唯一性与索引名按 <see cref="EntityIndexes"/>，名字用此刻的表名推出，与 SqlSugar 建出的同名）。在注册全部实体、配置完表名之后调用。
    /// </remarks>
    /// <exception cref="InvalidOperationException">实体的索引声明不合法（见 <see cref="EntityIndexes"/>）。</exception>
    public static ModelBuilder ApplyPersistenceConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        // 先物化实体类型列表：配置属性与索引可能修改模型，不能一边枚举一边改
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;
            // 并发戳由 PersistenceConventions.NewStamp 生成，是 32 位十六进制，长度与之对齐
            if (typeof(IHasConcurrencyStamp).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp)).IsConcurrencyToken().HasMaxLength(32);
            }

            if (typeof(IMultiTenant).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasIndex(nameof(IMultiTenant.TenantId));
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasIndex(nameof(ISoftDeletable.IsDeleted));
            }

            // 从属类型与所有者共用一张表，不能单独作为实体配置；共享类型实体（属性包）没有可声明索引的 CLR 类型
            if (entityType.IsOwned() || entityType.HasSharedClrType)
            {
                continue;
            }

            // 索引名按表名推出：表名在这里取，ToTable 要在本方法之前配置
            foreach (var index in EntityIndexes.Resolve(clrType, entityType.GetTableName() ?? clrType.Name))
            {
                modelBuilder.Entity(clrType).HasIndex([.. index.Properties]).IsUnique(index.IsUnique).HasDatabaseName(index.Name);
            }
        }

        return modelBuilder;
    }
}
