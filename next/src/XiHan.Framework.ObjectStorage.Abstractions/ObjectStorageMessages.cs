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

    /// <summary>
    /// 对象不存在。参数：<c>container</c>、<c>key</c>。
    /// </summary>
    public const string BlobNotFound = "objectstorage.blob.not_found";

    /// <summary>
    /// 对象已存在。参数：<c>container</c>、<c>key</c>。
    /// </summary>
    public const string BlobExists = "objectstorage.blob.exists";

    /// <summary>
    /// 存储不可达。参数：<c>reason</c>。
    /// </summary>
    public const string StoreUnavailable = "objectstorage.store.unavailable";

    /// <summary>
    /// 配额超限。参数：<c>limit</c>、<c>requested</c>。
    /// </summary>
    public const string QuotaExceeded = "objectstorage.quota.exceeded";

    /// <summary>
    /// 配额已用尽（健康项）。参数：<c>limit</c>、<c>used</c>。
    /// </summary>
    public const string QuotaReached = "objectstorage.quota.reached";

    /// <summary>
    /// 读取范围不可满足。参数：<c>offset</c>、<c>length</c>（对象总长）。
    /// </summary>
    public const string RangeNotSatisfiable = "objectstorage.range.not_satisfiable";

    /// <summary>
    /// 分片上传不存在或已结束。参数：<c>container</c>、<c>key</c>、<c>upload</c>。
    /// </summary>
    public const string UploadNotFound = "objectstorage.multipart.upload_not_found";

    /// <summary>
    /// 分片清单为空。
    /// </summary>
    public const string PartsEmpty = "objectstorage.multipart.parts_empty";

    /// <summary>
    /// 分片编号没有严格升序。参数：<c>part</c>。
    /// </summary>
    public const string PartsUnordered = "objectstorage.multipart.parts_unordered";

    /// <summary>
    /// 分片未上传或 ETag 不符。参数：<c>part</c>。
    /// </summary>
    public const string PartUnknown = "objectstorage.multipart.part_unknown";

    /// <summary>
    /// 非末片小于最小分片。参数：<c>part</c>、<c>length</c>、<c>minimum</c>。
    /// </summary>
    public const string PartTooSmall = "objectstorage.multipart.part_too_small";

    /// <summary>
    /// 存储拒绝了分片清单。参数：<c>reason</c>。
    /// </summary>
    public const string PartsRejected = "objectstorage.multipart.parts_rejected";

    /// <summary>
    /// 预签名地址的签名不符或参数不全。
    /// </summary>
    public const string SignedUrlInvalid = "objectstorage.signed_url.invalid";

    /// <summary>
    /// 预签名地址已过期。参数：<c>expiresAt</c>。
    /// </summary>
    public const string SignedUrlExpired = "objectstorage.signed_url.expired";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(ObjectStorageMessages));
}
