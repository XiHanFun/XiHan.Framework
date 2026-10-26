// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 存储共用的仓储读写：按标识批量查找与查询都以 <see cref="EntityQuery.DefaultMaxTake"/> 为一批，超过的分批或分页循环；批量写入把逐项结果汇总成一个结果。
/// </summary>
internal static class RecordQueries
{
    /// <summary>
    /// 按标识批量查找：去重后每 <see cref="EntityQuery.DefaultMaxTake"/> 个一批，不存在的不在结果里。
    /// </summary>
    public static async Task<List<TEntity>> FindManyAsync<TEntity>(IRepository<TEntity, string> repository, IEnumerable<string> ids, CancellationToken cancellationToken)
        where TEntity : class, IEntity<string>
    {
        var distinct = ids.Distinct(StringComparer.Ordinal).ToList();
        var found = new List<TEntity>(distinct.Count);
        for (var offset = 0; offset < distinct.Count; offset += EntityQuery.DefaultMaxTake)
        {
            var batch = distinct.GetRange(offset, Math.Min(EntityQuery.DefaultMaxTake, distinct.Count - offset));
            found.AddRange(await repository.FindManyAsync(batch, cancellationToken).ConfigureAwait(false));
        }

        return found;
    }

    /// <summary>
    /// 把标识集合切成每 <see cref="EntityQuery.DefaultMaxTake"/> 个一批，供 <c>IN</c> 条件使用。
    /// </summary>
    public static IEnumerable<List<string>> Batches(IEnumerable<string> ids)
    {
        var distinct = ids.Distinct(StringComparer.Ordinal).ToList();
        for (var offset = 0; offset < distinct.Count; offset += EntityQuery.DefaultMaxTake)
        {
            yield return distinct.GetRange(offset, Math.Min(EntityQuery.DefaultMaxTake, distinct.Count - offset));
        }
    }

    /// <summary>
    /// 按查询的过滤与排序分页读完：每页 <see cref="EntityQuery.DefaultMaxTake"/> 条，直到不足一页或达到 <paramref name="limit"/>。查询的排序必须是全序，分页才不重不漏。
    /// </summary>
    public static async Task<List<TEntity>> ListAllAsync<TEntity>(IRepository<TEntity, string> repository, EntityQuery<TEntity> query, int? limit, CancellationToken cancellationToken)
        where TEntity : class, IEntity<string>
    {
        var rows = new List<TEntity>();
        var skip = 0;
        while (true)
        {
            var take = limit is { } max ? Math.Min(EntityQuery.DefaultMaxTake, max - rows.Count) : EntityQuery.DefaultMaxTake;
            if (take <= 0)
            {
                return rows;
            }

            var page = await repository.ListAsync(query.Page(skip, take), cancellationToken).ConfigureAwait(false);
            rows.AddRange(page);
            if (page.Count < take)
            {
                return rows;
            }

            skip += page.Count;
        }
    }

    /// <summary>
    /// 新增或整体替换：已有的行整行更新，没有的新增；同一批里同一标识后者覆盖前者。任一项失败即整体失败（由调用方据此回滚）。
    /// </summary>
    public static async ValueTask<Result> UpsertAsync<TEntity>(IRepository<TEntity, string> repository, IReadOnlyList<TEntity> rows, Action<TEntity, TEntity> copy, CancellationToken cancellationToken)
        where TEntity : class, IEntity<string>
    {
        if (rows.Count == 0)
        {
            return Result.Success();
        }

        // 后出现的项覆盖同一标识先出现的项，一行只写一次
        var latest = new Dictionary<string, TEntity>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            latest[row.Id] = row;
        }

        var existing = await FindManyAsync(repository, latest.Keys, cancellationToken).ConfigureAwait(false);
        var updates = new List<TEntity>(existing.Count);
        foreach (var current in existing)
        {
            // 把新内容复制到取回的行上再更新：并发戳等由协议维护的列保持在取回的实例里
            copy(latest[current.Id], current);
            updates.Add(current);
            latest.Remove(current.Id);
        }

        var additions = latest.Values.ToList();
        var updated = updates.Count == 0 ? [] : await repository.UpdateRangeAsync(updates, cancellationToken).ConfigureAwait(false);
        var added = additions.Count == 0 ? [] : await repository.AddRangeAsync(additions, cancellationToken).ConfigureAwait(false);
        return FirstFailure(updated) ?? FirstFailure(added) ?? Result.Success();
    }

    /// <summary>
    /// 批量删除这些行；任一项失败即整体失败。
    /// </summary>
    public static async ValueTask<Result> RemoveManyAsync<TEntity>(IRepository<TEntity, string> repository, IReadOnlyList<TEntity> rows, CancellationToken cancellationToken)
        where TEntity : class, IEntity<string>
    {
        if (rows.Count == 0)
        {
            return Result.Success();
        }

        var removed = await repository.RemoveRangeAsync(rows, cancellationToken).ConfigureAwait(false);
        return FirstFailure(removed) ?? Result.Success();
    }

    /// <summary>
    /// 逐项结果里的第一个失败；没有为 <see langword="null"/>。
    /// </summary>
    private static Result? FirstFailure(IReadOnlyList<Result> results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                return result;
            }
        }

        return null;
    }
}
