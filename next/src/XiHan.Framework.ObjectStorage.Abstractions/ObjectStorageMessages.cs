// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions;

/// <summary>
/// 对象存储的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ObjectStorageMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "objectstorage";

    /// <summary>对象不存在。参数：<c>container</c>、<c>key</c>。</summary>
    public const string BlobNotFound = "objectstorage.blob.not_found";

    /// <summary>对象已存在。参数：<c>container</c>、<c>key</c>。</summary>
    public const string BlobExists = "objectstorage.blob.exists";

    /// <summary>存储不可达。参数：<c>reason</c>。</summary>
    public const string StoreUnavailable = "objectstorage.store.unavailable";

    /// <summary>配额超限。参数：<c>limit</c>、<c>requested</c>。</summary>
    public const string QuotaExceeded = "objectstorage.quota.exceeded";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(ObjectStorageMessages));
}
