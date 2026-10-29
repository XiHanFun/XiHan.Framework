// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 缓存子系统的消息键。
/// </summary>
/// <remarks>
/// 各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class CachingMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "caching";

    /// <summary>
    /// 幂等键不合法。
    /// </summary>
    public const string InvalidIdempotencyKey = "caching.idempotency.invalid_key";

    /// <summary>
    /// 同一幂等键的调用刚完成或仍在处理中。
    /// </summary>
    public const string IdempotentRecentlyCompleted = "caching.idempotency.recently_completed";

    /// <summary>
    /// 同一幂等键的调用仍在处理中。
    /// </summary>
    public const string IdempotentInProgress = "caching.idempotency.in_progress";

    /// <summary>
    /// 内存缓存已达容量上限。
    /// </summary>
    /// <remarks>
    /// 参数：<c>capacity</c>。
    /// </remarks>
    public const string CapacityReached = "caching.store.capacity_reached";

    /// <summary>
    /// 延迟装载的目录。
    /// </summary>
    /// <remarks>
    /// 首次访问时读嵌入的语言文件，之后复用同一实例。
    /// </remarks>
    private static readonly Lazy<MessageCatalog> _catalog = new(static () => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(CachingMessages)));

    /// <summary>
    /// 目录实例。
    /// </summary>
    /// <remarks>
    /// 首次访问时从嵌入资源装载。
    /// </remarks>
    public static MessageCatalog Catalog => _catalog.Value;
}
