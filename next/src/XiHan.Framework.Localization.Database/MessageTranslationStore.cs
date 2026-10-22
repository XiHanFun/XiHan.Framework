// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database;

/// <summary>
/// 翻译的批量读写（作用域服务，须在调用的 Transaction 阶段内使用）。写入后调用 <see cref="MessageTranslationLoader.ReloadAsync"/> 或等待周期刷新才会生效。
/// </summary>
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
    /// 新增或覆盖，按顺序逐项返回；重复的键在同一批里后者覆盖前者。
    /// </summary>
    public async ValueTask<IReadOnlyList<MessageTranslationResult>> UpsertAsync(IReadOnlyList<MessageTranslationEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var results = new List<MessageTranslationResult>(entries.Count);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = _clock.GetUtcNow();
            Result outcome;
            if ((await FindAsync(entry.Key, entry.Locale, cancellationToken).ConfigureAwait(false)).TryGetValue(out var existing))
            {
                existing.Template = entry.Template;
                existing.ModifiedAt = now;
                outcome = await _repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                outcome = await _repository.AddAsync(new MessageTranslation { Id = _ids.NewId(), Key = entry.Key, Locale = entry.Locale, Template = entry.Template, ModifiedAt = now }, cancellationToken).ConfigureAwait(false);
            }

            results.Add(new MessageTranslationResult(new MessageTranslationKey(entry.Key, entry.Locale), outcome));
        }

        return results;
    }

    /// <summary>
    /// 删除，按顺序逐项返回；不存在 → <see cref="LocalizationErrors.TranslationNotFound"/>。
    /// </summary>
    public async ValueTask<IReadOnlyList<MessageTranslationResult>> RemoveAsync(IReadOnlyList<MessageTranslationKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var results = new List<MessageTranslationResult>(keys.Count);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result outcome = (await FindAsync(key.Key, key.Locale, cancellationToken).ConfigureAwait(false)).TryGetValue(out var existing)
                ? await _repository.RemoveAsync(existing, cancellationToken).ConfigureAwait(false)
                : Error.NotFound(LocalizationErrors.TranslationNotFound, LocalizationMessages.TranslationNotFound, MessageArguments.Of(("key", key.Key), ("locale", key.Locale)));
            results.Add(new MessageTranslationResult(key, outcome));
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
    /// 按自然键查找。
    /// </summary>
    private async Task<Maybe<MessageTranslation>> FindAsync(string key, string locale, CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<MessageTranslation>().Where(new Specification<MessageTranslation>(t => t.Key == key && t.Locale == locale)).Page(0, 1);
        var found = await _repository.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return found.Count == 0 ? default : Maybe.Some(found[0]);
    }
}
