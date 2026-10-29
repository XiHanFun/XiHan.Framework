// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Idempotency;

/// <summary>
/// Idempotency 阶段的中间件。
/// </summary>
/// <remarks>
/// Baggage 里有 <see cref="BaggageKeys.IdempotencyKey"/> 时，按（租户、主体、键）在 <see cref="ISharedCache"/> 的存储里去重。首次调用写入"处理中"标记并执行，完成后用调用结果替换标记；处理中的重复调用返回 <see cref="CachingErrors.IdempotentInProgress"/>；已完成的重复调用直接回放结果，不执行处理器。处理器抛出异常时清除标记，允许重试。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class IdempotencyMiddleware(IdempotencyOptions options) : IPipelineMiddleware
{
    /// <summary>
    /// 上下文项键：标记本次调用的结果来自回放。
    /// </summary>
    private static readonly ContextItemKey<bool> _replayedKey = new("xihan.idempotency.replayed");

    /// <summary>
    /// 幂等选项（结果与"处理中"标记的保留时间）。
    /// </summary>
    private readonly IdempotencyOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 本次调用的结果是否来自回放。
    /// </summary>
    public static bool WasReplayed(InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Get(_replayedKey).ValueOr(false);
    }

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);
        if (!context.TryGetBaggage(BaggageKeys.IdempotencyKey, out var idempotencyKey))
        {
            await nextStep(context).ConfigureAwait(false);
            return;
        }

        // 作用域键 = 租户 + 主体 + 幂等键：不同租户或用户即使提交相同的键也互不回放对方的结果
        var security = context.GetSecurity();
        var principalId = security.Principal.Id.Length == 0 ? "-" : security.Principal.Id;
        var scoped = $"idem:{security.Tenant?.Id ?? "-"}:{principalId}:{idempotencyKey}";
        if (!CacheKey.IsValid(scoped))
        {
            context.SetOutcome(Error.Validation(CachingErrors.InvalidKey, CachingMessages.InvalidIdempotencyKey));
            return;
        }

        var key = new CacheKey(scoped);
        var store = context.Services.GetRequiredService<ISharedCache>().Store;
        var marker = Serialize(IdempotencyRecord.InProgress);

        // 原子的"不存在才写"决定谁是首次调用：只有写入成功的调用者执行处理器，其余走下面的读取分支
        if (await store.TryAddAsync(key, marker, CacheEntryOptions.Expires(_options.InProgressTimeToLive), context.CancellationToken).ConfigureAwait(false))
        {
            try
            {
                await nextStep(context).ConfigureAwait(false);
            }
            catch
            {
                // 处理器抛出异常时清除自己的标记以允许重试：按值比较删，避免误删标记过期后他人写入的新记录；
                // 用 None 保证调用已取消时也能清理
                await store.TryRemoveAsync(key, marker, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            // 处理完成后用结果替换"处理中"标记；没有显式结果视为成功
            var outcome = context.GetOutcome().ValueOr(Result.Success());
            await store.SetAsync(key, Serialize(IdempotencyRecord.From(outcome)), CacheEntryOptions.Expires(_options.TimeToLive), context.CancellationToken).ConfigureAwait(false);
            return;
        }

        // 标记已存在：读出记录判断是仍在处理还是已完成
        var existing = await store.GetAsync(key, context.CancellationToken).ConfigureAwait(false);
        if (!existing.TryGetValue(out var data))
        {
            // 标记在两次操作之间过期：当作处理中，让调用方稍后重试
            context.SetOutcome(Error.Conflict(CachingErrors.IdempotentInProgress, CachingMessages.IdempotentRecentlyCompleted));
            return;
        }

        var record = JsonSerializer.Deserialize(data.Span, CachingJsonContext.Default.IdempotencyRecord)
            ?? throw new InvalidOperationException("The idempotency record is corrupted.");
        if (record.IsInProgress)
        {
            context.SetOutcome(Error.Conflict(CachingErrors.IdempotentInProgress, CachingMessages.IdempotentInProgress));
            return;
        }

        // 已完成：直接回放记录的结果，不再执行处理器
        context.Set(_replayedKey, true);
        context.SetOutcome(record.ToResult());
    }

    /// <summary>
    /// 用源生成上下文把记录序列化为 UTF-8 JSON 字节。
    /// </summary>
    private static ReadOnlyMemory<byte> Serialize(IdempotencyRecord record)
        => JsonSerializer.SerializeToUtf8Bytes(record, CachingJsonContext.Default.IdempotencyRecord);
}
