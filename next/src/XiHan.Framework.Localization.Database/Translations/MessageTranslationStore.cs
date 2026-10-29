// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database.Translations;

/// <summary>
/// 翻译的批量读写（作用域服务，须在调用的 Transaction 阶段内使用）。
/// </summary>
/// <remarks>
/// 一次查询取回已有行，再用仓储的批量方法一次写入。写入后调用 <see cref="MessageTranslationLoader.ReloadAsync"/> 或等待周期刷新才会生效。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class MessageTranslationStore
{
    /// <summary>
    /// 仓储。
    /// </summary>
    private readonly IRepository<MessageTranslation, Guid> _repository;

    /// <summary>
    /// ID 生成器。
    /// </summary>
    private readonly IIdGenerator<Guid> _ids;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 创建。
    /// </summary>
    public MessageTranslationStore(IRepository<MessageTranslation, Guid> repository, IIdGenerator<Guid> ids, TimeProvider clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 新增或覆盖，按顺序逐项返回。
    /// </summary>
    /// <remarks>
    /// 重复的键在同一批里后者覆盖前者（同一行只写一次，结果相同）。
    /// </remarks>
    public async ValueTask<IReadOnlyList<MessageTranslationResult>> UpsertAsync(IReadOnlyList<MessageTranslationEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return [];
        }

        var now = _clock.GetUtcNow();
        var existing = await FindManyAsync(entries.Select(static e => new MessageTranslationKey(e.Key, e.Locale)).ToList(), cancellationToken).ConfigureAwait(false);
        // 同一自然键在一批里只对应一行：已有的更新、没有的新增；后出现的项覆盖同一行的模板，各项结果指向同一行的结果
        var rows = new Dictionary<MessageTranslationKey, int>();
        var updates = new List<MessageTranslation>();
        var additions = new List<MessageTranslation>();
        var slots = new int[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var key = new MessageTranslationKey(entry.Key, entry.Locale);
            if (rows.TryGetValue(key, out var slot))
            {
                var row = slot >= 0 ? updates[slot] : additions[~slot];
                row.Template = entry.Template;
                row.ModifiedAt = now;
                slots[index] = slot;
                continue;
            }

            if (existing.TryGetValue(key, out var found))
            {
                found.Template = entry.Template;
                found.ModifiedAt = now;
                updates.Add(found);
                slot = updates.Count - 1;
            }
            else
            {
                additions.Add(new MessageTranslation { Id = _ids.NewId(), Key = entry.Key, Locale = entry.Locale, Template = entry.Template, ModifiedAt = now });
                slot = ~(additions.Count - 1);
            }

            rows[key] = slot;
            slots[index] = slot;
        }

        var updated = updates.Count == 0 ? [] : await _repository.UpdateRangeAsync(updates, cancellationToken).ConfigureAwait(false);
        var added = additions.Count == 0 ? [] : await _repository.AddRangeAsync(additions, cancellationToken).ConfigureAwait(false);
        var results = new MessageTranslationResult[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            var slot = slots[index];
            results[index] = new MessageTranslationResult(new MessageTranslationKey(entries[index].Key, entries[index].Locale), slot >= 0 ? updated[slot] : added[~slot]);
        }

        return results;
    }

    /// <summary>
    /// 删除，按顺序逐项返回。
    /// </summary>
    /// <remarks>
    /// 不存在 → <see cref="LocalizationErrors.TranslationNotFound"/>，同一批里同一键第二次同样不存在。
    /// </remarks>
    public async ValueTask<IReadOnlyList<MessageTranslationResult>> RemoveAsync(IReadOnlyList<MessageTranslationKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return [];
        }

        var existing = await FindManyAsync(keys, cancellationToken).ConfigureAwait(false);
        var removals = new List<MessageTranslation>();
        var slots = new int[keys.Count];
        var seen = new HashSet<MessageTranslationKey>();
        for (var index = 0; index < keys.Count; index++)
        {
            // 第一次出现且存在的键进入批量删除；重复出现或不存在的直接记为 NotFound
            if (seen.Add(keys[index]) && existing.TryGetValue(keys[index], out var row))
            {
                removals.Add(row);
                slots[index] = removals.Count - 1;
            }
            else
            {
                slots[index] = -1;
            }
        }

        var removed = removals.Count == 0 ? [] : await _repository.RemoveRangeAsync(removals, cancellationToken).ConfigureAwait(false);
        var results = new MessageTranslationResult[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            Result outcome = slots[index] >= 0
                ? removed[slots[index]]
                : Error.NotFound(LocalizationErrors.TranslationNotFound, LocalizationMessages.TranslationNotFound, MessageArguments.Of(("key", keys[index].Key), ("locale", keys[index].Locale)));
            results[index] = new MessageTranslationResult(keys[index], outcome);
        }

        return results;
    }

    /// <summary>
    /// 列出一个区域的全部翻译（为空时列全部），按键排序，最多 <see cref="EntityQuery.DefaultMaxTake"/> 条一页。
    /// </summary>
    public Task<IReadOnlyList<MessageTranslation>> ListAsync(string? locale, int skip, int take, CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<MessageTranslation>().OrderBy(static t => t.Key).OrderBy(static t => t.Locale).Page(skip, take);
        if (locale is not null)
        {
            var normalized = LocaleName.Normalize(LocaleName.Validate(locale));
            query = query.Where(new Specification<MessageTranslation>(t => t.Locale == normalized));
        }

        return _repository.ListAsync(query, cancellationToken);
    }

    /// <summary>
    /// 按自然键集合一次查询。
    /// </summary>
    /// <remarks>
    /// 键与区域各取集合做 <c>IN</c> 过滤，再在内存里按 (键, 区域) 精确匹配，不逐键查询。
    /// </remarks>
    private async Task<Dictionary<MessageTranslationKey, MessageTranslation>> FindManyAsync(IReadOnlyList<MessageTranslationKey> keys, CancellationToken cancellationToken)
    {
        var names = keys.Select(static k => k.Key).Distinct(StringComparer.Ordinal).ToList();
        var locales = keys.Select(static k => k.Locale).Distinct(StringComparer.Ordinal).ToList();
        // 两个 IN 的笛卡尔积可能多取回一些行（键 A 的区域 x 与键 B 的区域 y 都在集合里），精确匹配在内存里完成；条数上限取查询允许的最大页
        var query = EntityQuery.All<MessageTranslation>()
            .Where(new Specification<MessageTranslation>(t => names.Contains(t.Key) && locales.Contains(t.Locale)))
            .Page(0, EntityQuery.DefaultMaxTake);
        var rows = await _repository.ListAsync(query, cancellationToken).ConfigureAwait(false);
        var wanted = new HashSet<MessageTranslationKey>(keys);
        var found = new Dictionary<MessageTranslationKey, MessageTranslation>();
        foreach (var row in rows)
        {
            var key = new MessageTranslationKey(row.Key, row.Locale);
            if (wanted.Contains(key))
            {
                found[key] = row;
            }
        }

        return found;
    }
}
