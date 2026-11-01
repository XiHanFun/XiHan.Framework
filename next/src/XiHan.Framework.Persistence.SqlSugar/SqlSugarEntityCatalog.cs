// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.Schema;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// SqlSugar 的实体目录。
/// </summary>
/// <remarks>
/// 表名与列取 <c>EntityMaintenance.GetEntityInfo</c>：施加了 <see cref="SqlSugarConventions"/> 与应用自己回调的映射，与建表、读写用的同一份。分表实体的表名是模板，逻辑名按 <see cref="SplitTableTemplate"/> 推出，模板另留给库结构读取折叠物理表。在独立作用域里取客户端，不连库；首次调用时算出并缓存。
/// </remarks>
/// <param name="services">根容器：在独立作用域里取作用域客户端。</param>
/// <param name="registrations">登记的实体。</param>
internal sealed class SqlSugarEntityCatalog(IServiceProvider services, RepositoryRegistrations registrations) : IEntityCatalog
{
    /// <summary>
    /// 只算一次：并发的首次调用排队等同一个结果。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 算好的目录。
    /// </summary>
    private Snapshot? _snapshot;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<EntityMapping>> ListAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(GetSnapshot(cancellationToken).Mappings);

    /// <summary>
    /// 目录与分表模板。
    /// </summary>
    /// <remarks>
    /// 取实体信息只读特性与回调、不连库，所以同步算。
    /// </remarks>
    internal Snapshot GetSnapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _snapshot) is { } ready)
        {
            return ready;
        }

        lock (_gate)
        {
            if (_snapshot is { } computed)
            {
                return computed;
            }

            // 客户端是作用域服务，在独立作用域里取；作用域里只解析客户端（可同步释放）
            using var scope = services.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            var mappings = new List<EntityMapping>(registrations.Entities.Count);
            var templates = new List<SplitTableTemplate>();
            foreach (var entity in registrations.Entities)
            {
                var info = client.EntityMaintenance.GetEntityInfo(entity.EntityType);
                var template = entity.EntityType.IsDefined(typeof(SplitTableAttribute), inherit: false) ? SplitTableTemplate.Create(info.DbTableName) : null;
                var columns = info.Columns
                    .Where(static c => !c.IsIgnore && c.PropertyInfo is not null)
                    .Select(static c => new EntityColumnMapping(c.PropertyName, c.DbColumnName, c.PropertyInfo.PropertyType, c.IsPrimarykey, c.IsNullable))
                    .ToList();
                mappings.Add(new EntityMapping(entity, template?.LogicalName ?? info.DbTableName, template is not null, columns));
                if (template is not null)
                {
                    templates.Add(template);
                }
            }

            var snapshot = new Snapshot(mappings, templates);
            Volatile.Write(ref _snapshot, snapshot);
            return snapshot;
        }
    }

    /// <summary>
    /// 算好的目录。
    /// </summary>
    /// <param name="Mappings">实体映射，按登记顺序。</param>
    /// <param name="SplitTemplates">分表实体的表名模板。</param>
    internal sealed record Snapshot(IReadOnlyList<EntityMapping> Mappings, IReadOnlyList<SplitTableTemplate> SplitTemplates);
}
