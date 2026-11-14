// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database.Translations;

/// <summary>
/// 翻译的批量读写（作用域服务，须在调用的 Transaction 阶段内使用）
/// </summary>
/// <remarks>
/// 一次查询取回已有行，再用仓储的批量方法一次写入。写入在调用方的事务提交后才可见，所以作用域释放时（调用已提交）才经 <see cref="MessageTranslationLoader.PublishChangeAsync"/> 换翻译版本戳：本实例立即重新加载，其他实例在 <see cref="DatabaseLocalizationOptions.ChangeCheckInterval"/> 内重新加载（ADR-0229）。
/// 换版本戳遇到缓存后端故障只记错误日志、不抛出：写入已经提交，本实例照常重新加载，其他实例靠 <see cref="DatabaseLocalizationOptions.RefreshInterval"/> 的周期刷新收敛
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class MessageTranslationStore : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 本作用域里是否写过翻译
    /// </summary>
    /// <remarks>
    /// 作用域释放时据此换版本戳
    /// </remarks>
    private bool _changed;

    /// <summary>
    /// 仓储
    /// </summary>
    private readonly IRepository<MessageTranslation, Guid> _repository;

    /// <summary>
    /// ID 生成器
    /// </summary>
    private readonly IIdGenerator<Guid> _ids;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 加载器
    /// </summary>
    /// <remarks>
    /// 写入提交后经它换版本戳并让本实例重新加载
    /// </remarks>
    private readonly MessageTranslationLoader _loader;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    public MessageTranslationStore(IRepository<MessageTranslation, Guid> repository, IIdGenerator<Guid> ids, TimeProvider clock, MessageTranslationLoader loader, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _logger = loggerFactory.CreateLogger<MessageTranslationStore>();
    }

    /// <summary>
    /// 新增或覆盖，按顺序逐项返回
    /// </summary>
    /// <remarks>
    /// 已有的 (键, 区域) 更新那一行，没有的新增；重复的键在同一批里后者覆盖前者（同一行只写一次，结果相同）。表上 <c>(Key, Locale)</c> 的唯一索引兜住并发：两个调用同时新增同一个键与区域，后插入的撞上索引，那一批新增的项返回 <c>PERSISTENCE_CONFLICT_003</c>（唯一键重复），调用随后只能回滚；重试时读到先提交的那一行，按更新处理，不会写出第二行。内存持久化不建索引、不校验唯一性
    /// </remarks>
    public async ValueTask<IReadOnlyList<Result>> UpsertAsync(IReadOnlyList<MessageTranslationEntry> entries, CancellationToken cancellationToken)
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

        // 先记下再写：写到一半抛出时调用失败回滚，释放时换版本戳只多一次无害的重新加载
        _changed = true;
        var updated = updates.Count == 0 ? [] : await _repository.UpdateRangeAsync(updates, cancellationToken).ConfigureAwait(false);
        var added = additions.Count == 0 ? [] : await _repository.AddRangeAsync(additions, cancellationToken).ConfigureAwait(false);
        var results = new Result[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            var slot = slots[index];
            results[index] = slot >= 0 ? updated[slot] : added[~slot];
        }

        return results;
    }

    /// <summary>
    /// 删除，按顺序逐项返回
    /// </summary>
    /// <remarks>
    /// 不存在 → <see cref="LocalizationErrors.TranslationNotFound"/>，同一批里同一键第二次同样不存在
    /// </remarks>
    public async ValueTask<IReadOnlyList<Result>> RemoveAsync(IReadOnlyList<MessageTranslationKey> keys, CancellationToken cancellationToken)
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

        _changed |= removals.Count > 0;
        var removed = removals.Count == 0 ? [] : await _repository.RemoveRangeAsync(removals, cancellationToken).ConfigureAwait(false);
        var results = new Result[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            Result outcome = slots[index] >= 0
                ? removed[slots[index]]
                : Error.NotFound(LocalizationErrors.TranslationNotFound, LocalizationMessages.TranslationNotFound, MessageArguments.Of(("key", keys[index].Key), ("locale", keys[index].Locale)));
            results[index] = outcome;
        }

        return results;
    }

    /// <summary>
    /// 列出一个区域的全部翻译（为空时列全部），按键排序，最多 <see cref="EntityQuery.DefaultMaxTake"/> 条一页
    /// </summary>
    /// <remarks>
    /// <paramref name="take"/> 超过上限时构造查询抛出携带 <c>PERSISTENCE_VALIDATION_005</c> 的异常，调用里由 Transaction 阶段转为同码的校验结果
    /// </remarks>
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
    /// 按自然键集合查询已有的行
    /// </summary>
    /// <remarks>
    /// 键与区域各取集合做 <c>IN</c> 过滤，再在内存里按 (键, 区域) 精确匹配，不逐键查询。键每 <see cref="EntityQuery.DefaultMaxTake"/> 个一批，每批的结果按标识分页读完
    /// </remarks>
    private async Task<Dictionary<MessageTranslationKey, MessageTranslation>> FindManyAsync(IReadOnlyList<MessageTranslationKey> keys, CancellationToken cancellationToken)
    {
        var locales = keys.Select(static k => k.Locale).Distinct(StringComparer.Ordinal).ToList();
        var wanted = new HashSet<MessageTranslationKey>(keys);
        var found = new Dictionary<MessageTranslationKey, MessageTranslation>();
        foreach (var chunk in keys.Select(static k => k.Key).Distinct(StringComparer.Ordinal).Chunk(EntityQuery.DefaultMaxTake))
        {
            var names = chunk.ToList();
            // 两个 IN 的笛卡尔积可能多取回一些行（键 A 的区域 x 与键 B 的区域 y 都在集合里），精确匹配在内存里完成
            // 结果必须分页读完：只读第一页时，超出一页的已有行被当成新行再插一遍——表上没有唯一索引时就是同一键与区域的第二行
            var query = EntityQuery.All<MessageTranslation>()
                .Where(new Specification<MessageTranslation>(t => names.Contains(t.Key) && locales.Contains(t.Locale)))
                .OrderBy(static t => t.Id);
            for (var skip = 0; ; skip += EntityQuery.DefaultMaxTake)
            {
                var page = await _repository.ListAsync(query.Page(skip, EntityQuery.DefaultMaxTake), cancellationToken).ConfigureAwait(false);
                foreach (var row in page)
                {
                    var key = new MessageTranslationKey(row.Key, row.Locale);
                    if (wanted.Contains(key))
                    {
                        found[key] = row;
                    }
                }

                if (page.Count < EntityQuery.DefaultMaxTake)
                {
                    break;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// 作用域释放：写过翻译就换版本戳
    /// </summary>
    /// <remarks>
    /// 管道里的调用先提交、后释放作用域，此后重新加载读到的是提交之后的行；回滚之后换版本戳只多一次无害的重新加载。可以重复调用
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (!_changed)
        {
            return;
        }

        _changed = false;
        try
        {
            // 释放时没有调用方的取消令牌，也不应被取消：这一次决定已提交的翻译多快对别的实例生效
            await _loader.PublishChangeAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (FrameworkException exception) when (exception.Code == CachingErrors.StoreUnavailable)
        {
            LogPublishFailed(_logger, exception);
        }
    }

    /// <summary>
    /// 同步释放：等待 <see cref="DisposeAsync"/> 完成
    /// </summary>
    /// <remarks>
    /// 容器同步释放作用域时走这里；内存缓存同步完成，跨进程缓存会阻塞到版本戳写完，应优先异步释放作用域
    /// </remarks>
    public void Dispose()
    {
        var pending = DisposeAsync();
        if (!pending.IsCompletedSuccessfully)
        {
            pending.AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// 换版本戳失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Publishing the message translation version after the write failed because the cache is unavailable; other instances pick up the change at their next periodic refresh.")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception);
}
