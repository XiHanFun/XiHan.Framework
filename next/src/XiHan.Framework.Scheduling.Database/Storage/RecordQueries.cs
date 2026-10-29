// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Database.Storage;

/// <summary>
/// 存储共用的仓储读写。
/// </summary>
/// <remarks>
/// 按标识批量查找与查询都以 <see cref="EntityQuery.DefaultMaxTake"/> 为一批，超过的分批或分页循环；批量写入把逐项结果汇总成一个结果，与别的节点撞上的项按调用方要求重试或跳过。
/// </remarks>
internal static class RecordQueries
{
    /// <summary>
    /// 与别的写入者撞上时的重试次数上限。
    /// </summary>
    private const int MaxAttempts = 3;

    /// <summary>
    /// 是否是与别的节点的竞争：并发戳冲突（行被改了）、行已被删、行已被别人插入。
    /// </summary>
    public static bool IsRace(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Code is PersistenceErrors.Concurrency or PersistenceErrors.NotFound or PersistenceErrors.Duplicate;
    }

    /// <summary>
    /// 按标识批量查找：去重后每 <see cref="EntityQuery.DefaultMaxTake"/> 个一批，不存在的不在结果里。
    /// </summary>
    public static async Task<List<TEntity>> FindManyAsync<TEntity>(IRepository<TEntity, string> repository, IEnumerable<string> ids, CancellationToken cancellationToken)
        where TEntity : class, IEntity<string>
    {
        var found = new List<TEntity>();
        foreach (var batch in Batches(ids))
        {
            found.AddRange(await repository.FindManyAsync(batch, cancellationToken).ConfigureAwait(false));
        }

        return found;
    }

    /// <summary>
    /// 把标识集合去重后切成每 <see cref="EntityQuery.DefaultMaxTake"/> 个一批，供 <c>IN</c> 条件使用。
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
    /// 按查询的过滤与排序分页读完。
    /// </summary>
    /// <remarks>
    /// 每页 <see cref="EntityQuery.DefaultMaxTake"/> 条，直到不足一页。查询的排序必须是全序，分页才不重不漏。
    /// </remarks>
    public static async Task<List<TEntity>> ListAllAsync<TEntity>(IRepository<TEntity, string> repository, EntityQuery<TEntity> query, CancellationToken cancellationToken)
        where TEntity : class, IEntity<string>
    {
        var rows = new List<TEntity>();
        var skip = 0;
        while (true)
        {
            var page = await repository.ListAsync(query.Page(skip, EntityQuery.DefaultMaxTake), cancellationToken).ConfigureAwait(false);
            rows.AddRange(page);
            if (page.Count < EntityQuery.DefaultMaxTake)
            {
                return rows;
            }

            skip += page.Count;
        }
    }

    /// <summary>
    /// 新增或整体替换。
    /// </summary>
    /// <remarks>
    /// 已有的行整行更新，没有的新增；同一批里同一标识后者覆盖前者。与别的节点撞上的项（戳冲突、刚被删、刚被插入）重读后再写，最多 <see cref="MaxAttempts"/> 轮；其余失败立即整体失败（由调用方据此回滚）。
    /// </remarks>
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

        var pending = latest.Keys.ToList();
        for (var attempt = 0; ; attempt++)
        {
            var existing = await FindManyAsync(repository, pending, cancellationToken).ConfigureAwait(false);
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var current in existing)
            {
                // 把新内容复制到取回的行上再更新：并发戳留在取回的实例里，整体替换不与上一次读到的版本比对
                copy(latest[current.Id], current);
                found.Add(current.Id);
            }

            var additions = pending.Where(id => !found.Contains(id)).Select(id => latest[id]).ToList();
            var retry = new List<string>();
            var races = new List<Error>();
            var fatal = await WriteAsync(repository.UpdateRangeAsync, existing, retry, races, cancellationToken).ConfigureAwait(false)
                ?? await WriteAsync(repository.AddRangeAsync, additions, retry, races, cancellationToken).ConfigureAwait(false);
            if (fatal is not null)
            {
                return fatal;
            }

            if (retry.Count == 0)
            {
                return Result.Success();
            }

            // 连续几轮都撞上别的写入者：把最后一次冲突原样报告
            if (attempt + 1 >= MaxAttempts)
            {
                return races[^1];
            }

            pending = retry;
        }
    }

    /// <summary>
    /// 批量更新这些行。
    /// </summary>
    /// <remarks>
    /// 与别的节点撞上的行（已被改走或删掉）跳过——它的新状态由改它的那个节点负责；其余失败整体失败。返回更新成功的行。
    /// </remarks>
    public static async ValueTask<Result<List<TEntity>>> UpdateManyAsync<TEntity>(IRepository<TEntity, string> repository, IReadOnlyList<TEntity> rows, CancellationToken cancellationToken)
        where TEntity : class, IEntity<string>
    {
        var updated = new List<TEntity>(rows.Count);
        foreach (var chunk in rows.Chunk(EntityQuery.DefaultMaxTake))
        {
            var results = await repository.UpdateRangeAsync(chunk, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < results.Count; index++)
            {
                if (results[index].IsSuccess)
                {
                    updated.Add(chunk[index]);
                }
                else if (!IsRace(results[index].Error!))
                {
                    return results[index].Error!;
                }
            }
        }

        return updated;
    }

    /// <summary>
    /// 批量删除这些行。
    /// </summary>
    /// <remarks>
    /// 已经不存在的忽略，其余失败整体失败。返回实际删除的条数。
    /// </remarks>
    public static async ValueTask<Result<int>> RemoveManyAsync<TEntity>(IRepository<TEntity, string> repository, IReadOnlyList<TEntity> rows, CancellationToken cancellationToken)
        where TEntity : class, IEntity<string>
    {
        var removed = 0;
        foreach (var chunk in rows.Chunk(EntityQuery.DefaultMaxTake))
        {
            foreach (var result in await repository.RemoveRangeAsync(chunk, cancellationToken).ConfigureAwait(false))
            {
                if (result.IsSuccess)
                {
                    removed++;
                }
                else if (!string.Equals(result.Error!.Code, PersistenceErrors.NotFound, StringComparison.Ordinal))
                {
                    return result.Error;
                }
            }
        }

        return removed;
    }

    /// <summary>
    /// 分批写入并收集逐项结果。
    /// </summary>
    /// <remarks>
    /// 与别的写入者撞上的项把标识放进 <paramref name="retry"/>、错误放进 <paramref name="races"/>；返回第一个非竞争的失败，没有为 <see langword="null"/>。
    /// </remarks>
    private static async ValueTask<Error?> WriteAsync<TEntity>(Func<IReadOnlyList<TEntity>, CancellationToken, Task<IReadOnlyList<Result>>> write, List<TEntity> rows, List<string> retry, List<Error> races, CancellationToken cancellationToken)
        where TEntity : class, IEntity<string>
    {
        foreach (var chunk in rows.Chunk(EntityQuery.DefaultMaxTake))
        {
            var results = await write(chunk, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < results.Count; index++)
            {
                if (results[index].IsSuccess)
                {
                    continue;
                }

                if (!IsRace(results[index].Error!))
                {
                    return results[index].Error;
                }

                retry.Add(chunk[index].Id);
                races.Add(results[index].Error!);
            }
        }

        return null;
    }
}
