// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Remote;

/// <summary>
/// 远程缓存插件侧错误的消息键（<c>framework-sdk-go</c> 的 <c>caching</c> 包等实现返回这些键）。
/// </summary>
/// <remarks>
/// 文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，宿主据此翻译插件的失败，不依赖插件自带的目录。
/// </remarks>
[ApiLevel(Stability.Experimental, "5.0")]
public static class RemoteCachingMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "caching.remote";

    /// <summary>
    /// 缓存键不合法。
    /// </summary>
    public const string InvalidKey = "caching.remote.key_invalid";

    /// <summary>
    /// 过期选项不合法。
    /// </summary>
    public const string InvalidOptions = "caching.remote.options_invalid";

    /// <summary>
    /// 后端不可用。
    /// </summary>
    public const string StoreUnavailable = "caching.remote.store_unavailable";

    /// <summary>
    /// 存储已满。
    /// </summary>
    public const string CapacityReached = "caching.remote.capacity_reached";

    /// <summary>
    /// 延迟装载的目录。
    /// </summary>
    /// <remarks>
    /// 首次访问时读嵌入的语言文件，之后复用同一实例。
    /// </remarks>
    private static readonly Lazy<MessageCatalog> _catalog = new(static () => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(RemoteCachingMessages)));

    /// <summary>
    /// 目录实例。
    /// </summary>
    /// <remarks>
    /// 首次访问时从嵌入资源装载。
    /// </remarks>
    public static MessageCatalog Catalog => _catalog.Value;
}
